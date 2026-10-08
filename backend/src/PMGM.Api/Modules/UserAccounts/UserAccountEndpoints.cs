using System.Data;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Membership;
using MemberMembership = PMGM.Api.Modules.Membership.Entities.Membership;

namespace PMGM.Api.Modules.UserAccounts;

public static class UserAccountEndpoints
{
    private static readonly string[] Statuses = ["active","reinstated","past_active","inactive","voluntary_withdrawal","forced_withdrawal","deceased"];
    private static IQueryable<MemberMembership> Eligible(PmgmDbContext db, DateOnly today)
        => db.Memberships.AsNoTracking().Where(m=>m.Status==MembershipCodes.MembershipStatus.Active &&
            (m.StartDate==null || m.StartDate<=today) && (m.EndDate==null || m.EndDate>=today) && m.Organization.Type=="workshop" &&
            (!db.InstitutionalStatusEvents.Any(e=>e.MemberId==m.MemberId && e.EffectiveDate<=today && Statuses.Contains(e.EventType)) ||
             new[]{"active","reinstated"}.Contains(db.InstitutionalStatusEvents.Where(e=>e.MemberId==m.MemberId && e.EffectiveDate<=today && Statuses.Contains(e.EventType))
                .OrderByDescending(e=>e.EffectiveDate).ThenByDescending(e=>e.RecordedAtUtc).Select(e=>e.EventType).FirstOrDefault()!)));

    public static IEndpointRouteBuilder MapUserAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group=endpoints.MapGroup("/api/system/user-accounts").RequireAuthorization().WithTags("Cuentas de usuarios");
        group.MapGet("/eligible-members",GetEligibleMembers);
        group.MapGet("",GetUsers);
        group.MapPost("",CreateUserAccount);
        return endpoints;
    }
    private static async Task<IResult> GetEligibleMembers(HttpContext ctx,PmgmDbContext db,IInstitutionalAccessService access,
        IAccountIdentityProvider identity,IInitialAccountMail mail,CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl="private, no-store";
        if(!access.CanConfigureSystem(ctx.User)) return Results.Forbid();
        var rows=await Eligible(db,DynamicAccessEndpoints.Today()).Select(m=>new AccountCandidate(m.MemberId,m.OrganizationId,
            m.Member.Person.FirstNames+" "+m.Member.Person.LastNames,m.Member.Person.Email??"",m.Organization.Name)).Distinct().ToListAsync(ct);
        return Results.Ok(new { items=rows.Where(x=>AccountPassword.ValidEmail(x.Email)).ToArray(),
            canCreatePlatformAdministrator=access.IsPlatformSuperAdmin(ctx.User),deliveryConfigured=identity.Configured&&mail.Configured });
    }
    private static async Task<IResult> GetUsers(HttpContext ctx,IInstitutionalAccessService access,IAccountIdentityProvider identity,CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl="private, no-store";
        if(!access.CanConfigureSystem(ctx.User)) return Results.Forbid();
        try { return Results.Ok(new { items=await identity.ListAsync(ct),configured=identity.Configured }); }
        catch(Exception e) when(e is AccountDeliveryException or HttpRequestException or TaskCanceledException)
        { return Failure(); }
    }
    private static async Task<IResult> CreateUserAccount(CreateUserAccountRequest request,HttpContext ctx,PmgmDbContext db,
        IInstitutionalAccessService access,IAccountIdentityProvider identity,IInitialAccountMail mail,IAuditService audit,
        ILoggerFactory logs,CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl="private, no-store";
        if(!access.CanConfigureSystem(ctx.User)) return Results.Forbid();
        if(request.PlatformAdministrator && !access.IsPlatformSuperAdmin(ctx.User)) return Results.Forbid();
        if(request.PlatformAdministrator ? request.MemberId is not null || request.OrganizationId is not null ||
            string.IsNullOrWhiteSpace(request.PlatformName) || request.PlatformName.Length>200 || !AccountPassword.ValidEmail(request.PlatformEmail)
            : request.MemberId is null || request.OrganizationId is null || request.PlatformEmail is not null || request.PlatformName is not null)
            return Results.BadRequest(new { message="Seleccione un Hermano activo y su Taller. Sólo la excepción de plataforma admite nombre y correo propios." });
        if(!identity.Configured || !mail.Configured) return Failure();
        PreparedIdentity? prepared=null;var committed=false;
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            var lockKey="accounts:"+(request.MemberId?.ToString()??request.PlatformEmail!.ToLowerInvariant());
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey},0))",ct);
            AccountIdentity account;
            List<string> links=[];
            if(request.PlatformAdministrator) account=new(request.PlatformName!.Trim(),request.PlatformEmail!,null,null,true);
            else
            {
                var candidate=await Eligible(db,DynamicAccessEndpoints.Today()).Where(m=>m.MemberId==request.MemberId && m.OrganizationId==request.OrganizationId)
                    .Select(m=>new AccountCandidate(m.MemberId,m.OrganizationId,m.Member.Person.FirstNames+" "+m.Member.Person.LastNames,m.Member.Person.Email??"",m.Organization.Name))
                    .FirstOrDefaultAsync(ct);
                if(candidate is null || !AccountPassword.ValidEmail(candidate.Email))
                    return Results.BadRequest(new { message="El Hermano debe estar activo en ese Taller y tener un correo válido en su ficha." });
                account=new(candidate.Name,candidate.Email,candidate.MemberId,candidate.OrganizationId,false);
                links=await db.Database.SqlQuery<string>($"SELECT CASE WHEN \"Issuer\"={identity.Issuer} THEN \"Subject\" ELSE 'other-issuer' END AS \"Value\" FROM core.member_identity_links WHERE \"MemberId\"={candidate.MemberId} AND \"RevokedAtUtc\" IS NULL").ToListAsync(ct);
            }
            // Email from the record is the only delivery destination; lock it across different members too.
            var emailLock="account-email:"+account.Email.ToLowerInvariant();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({emailLock},0))",ct);
            var password=AccountPassword.Generate();
            prepared=await identity.PrepareDisabledAsync(account,password,ct);
            if(links.Count>1 || links.Count==1 && links[0]!=prepared.Subject) throw new AccountConflictException();
            if(account.MemberId is Guid member && links.Count==0)
            {
                var linkId=Guid.NewGuid();var now=DateTimeOffset.UtcNow;var actor=DynamicTreasuryAccess.Subject(ctx.User)??"";
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO core.member_identity_links (\"Id\",\"MemberId\",\"Issuer\",\"Subject\",\"CreatedAtUtc\",\"CreatedBySubject\") VALUES ({linkId},{member},{identity.Issuer},{prepared.Subject},{now},{actor})",ct);
            }
            await mail.SendAsync(account,password,ct);
            audit.Add(ctx,"system.users.prepared","UserAccount",prepared.Subject,account.OrganizationId,AuditResults.Success,
                new { account.MemberId,account.PlatformAdministrator,temporaryCredential=true });
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);committed=true;await tx.DisposeAsync();
            // Recheck immediately before activation; the SMTP wait must not authorize an inactive member.
            if(account.MemberId is Guid id && !await Eligible(db,DynamicAccessEndpoints.Today()).AnyAsync(m=>m.MemberId==id&&m.OrganizationId==account.OrganizationId,ct))
                throw new AccountDeliveryException();
            await identity.EnableAsync(prepared.Subject,ct);
            audit.Add(ctx,"system.users.created","UserAccount",prepared.Subject,account.OrganizationId,AuditResults.Success,
                new { account.MemberId,account.PlatformAdministrator,requiresPasswordChange=true });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/system/user-accounts/{prepared.Subject}",new {
                subject=prepared.Subject,email=account.Email,account.MemberId,account.OrganizationId,
                platformAdministrator=account.PlatformAdministrator,requiresPasswordChange=true,initialPasswordEmailSent=true });
        }
        catch(Exception e) when(e is AccountConflictException or AccountDeliveryException or HttpRequestException or TaskCanceledException or DbUpdateException or System.Data.Common.DbException)
        {
            if(!committed) { await tx.RollbackAsync(CancellationToken.None);await tx.DisposeAsync(); }
            if(prepared is not null)
            {
                try { await identity.RemoveAsync(prepared.Subject,CancellationToken.None); }
                catch { logs.CreateLogger("UserAccounts").LogError("No se completó compensación de cuenta; revisar estado en identidad. Correlación {CorrelationId}.",ctx.TraceIdentifier); }
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE core.member_identity_links SET \"RevokedAtUtc\"={DateTimeOffset.UtcNow} WHERE \"Issuer\"={identity.Issuer} AND \"Subject\"={prepared.Subject} AND \"RevokedAtUtc\" IS NULL",CancellationToken.None);
            }
            // Rejection audit contains identifiers only, never contact, credential, provider body or exception text.
            db.ChangeTracker.Clear();audit.Add(ctx,"system.users.creation_failed","UserAccount",request.MemberId?.ToString()??"platform",request.OrganizationId,AuditResults.Rejected,
                new { request.PlatformAdministrator,reason=e is AccountConflictException?"conflict":"delivery_or_identity" });
            await db.SaveChangesAsync(CancellationToken.None);
            return e is AccountConflictException ? Results.Conflict(new { message="Ya existe una cuenta o vínculo de identidad para este Hermano/correo." }) : Failure();
        }
    }
    private static IResult Failure()=>Results.Json(new { message="No se completó el alta o el envío. Revise la configuración de identidad/correo antes de reintentar." },statusCode:503);
}
