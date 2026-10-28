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
        group.MapGet("/inventory",GetInventory);
        group.MapGet("/lifecycle-policy",GetLifecyclePolicy);
        group.MapGet("/{subject}/lifecycle-verification",GetLifecycleVerification);
        group.MapGet("/{subject}/lifecycle-reconciliation",GetLifecycleReconciliation);
        group.MapPost("/{subject}/activate", (string subject,AccountLifecycleMutationRequest request,
            HttpContext ctx,PmgmDbContext db,IInstitutionalAccessService access,
            IAccountIdentityProvider identity,IAuditService audit,
            Microsoft.Extensions.Options.IOptions<AccountIdentityOptions> opts,CancellationToken ct) =>
            MutateLifecycle(subject,request,AccountLifecycleAction.Activate,ctx,db,access,identity,audit,opts,ct));
        group.MapPost("/{subject}/deactivate", (string subject,AccountLifecycleMutationRequest request,
            HttpContext ctx,PmgmDbContext db,IInstitutionalAccessService access,
            IAccountIdentityProvider identity,IAuditService audit,
            Microsoft.Extensions.Options.IOptions<AccountIdentityOptions> opts,CancellationToken ct) =>
            MutateLifecycle(subject,request,AccountLifecycleAction.Deactivate,ctx,db,access,identity,audit,opts,ct));
        group.MapPost("/{subject}/delete-permanently", (string subject,AccountLifecycleMutationRequest request,
            HttpContext ctx,PmgmDbContext db,IInstitutionalAccessService access,
            IAccountIdentityProvider identity,IAuditService audit,
            Microsoft.Extensions.Options.IOptions<AccountIdentityOptions> opts,CancellationToken ct) =>
            MutateLifecycle(subject,request,AccountLifecycleAction.DeletePermanently,ctx,db,access,identity,audit,opts,ct));
        group.MapPost("",CreateUserAccount);
        group.MapPost("/{subject}/password-recovery",ResetAccountAccess);
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
            canCreatePlatformAdministrator=false,canCreateUser=access.IsPlatformOperator(ctx.User),canCreateSuperuser=access.IsPlatformSuperAdmin(ctx.User),
            identityConfigured=identity.Configured,mailConfigured=mail.Configured,
            deliveryConfigured=identity.Configured&&mail.Configured });
    }
    private static async Task<IResult> GetUsers(HttpContext ctx,IInstitutionalAccessService access,IAccountIdentityProvider identity,CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl="private, no-store";
        if(!access.CanConfigureSystem(ctx.User)) return Results.Forbid();
        try { return Results.Ok(new { items=await identity.ListAsync(ct),configured=identity.Configured }); }
        catch(Exception e) when(e is AccountDeliveryException or HttpRequestException or TaskCanceledException)
        { return Failure(); }
    }
    // Inventario de origen Keycloak exclusivo del Máster. No equivale a permiso de
    // mutación: las cuentas heredadas/externas se clasifican como protegidas.
    private static async Task<IResult> GetInventory(HttpContext ctx,IInstitutionalAccessService access,
        IAccountIdentityProvider identity,CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl="private, no-store";
        // Un rol heredado platform_superadmin no convierte a qa.admin ni a otra
        // cuenta bootstrap en el único Máster admin del contrato #604.
        if(!AccountLifecyclePolicy.IsUniqueMaster(ctx.User,access)) return Results.Forbid();
        if(identity is not IAccountIdentityInventory inventory || !identity.Configured)
            return Results.Json(new { message="No está disponible el inventario de identidades." },
                statusCode:StatusCodes.Status503ServiceUnavailable);
        try { return Results.Ok(new { items=await inventory.ListInventoryAsync(ct),configured=true }); }
        catch(Exception e) when(e is AccountDeliveryException or HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        { return Results.Json(new { message="No se pudo consultar el inventario de identidades." },
                statusCode:StatusCodes.Status503ServiceUnavailable); }
    }
    // Contrato informativo: la interfaz nunca debe habilitar acciones destructivas
    // mientras la API de ejecución no supere los controles de identidad y auditoría.
    private static IResult GetLifecyclePolicy(HttpContext ctx,IInstitutionalAccessService access,
        Microsoft.Extensions.Options.IOptions<AccountIdentityOptions> options,
        IAccountIdentityProvider identity)
    {
        ctx.Response.Headers.CacheControl="private, no-store";
        if(!AccountLifecyclePolicy.IsUniqueMaster(ctx.User,access)) return Results.Forbid();
        return Results.Ok(new {
            operationsAvailable=options.Value.RequireLiveTokenIntrospection &&
                options.Value.EnableLifecycleMutations && identity.Configured &&
                identity is IAccountIdentityInventory &&
                identity is IAccountIdentitySecurityReader &&
                identity is IUniqueMasterIdentityVerifier &&
                identity is IAccountIdentityLifecycleCommands,
            actionNames=new[]{"activate","deactivate","delete_permanently"},
            reasonRequired=true,
            reasonMinLength=10,
            reasonMaxLength=200,
            deletionNeedsExactUsername=true,
            requiresLiveIdentityVerification=true,
            requiresEffectiveSessionRevocation=true
        });
    }

    // Verificación previa solo lectura. Los estados informativos NO otorgan
    // permisos; la mutación requerirá otra comprobación atómica del proveedor.
    private static async Task<IResult> GetLifecycleVerification(string subject,HttpContext ctx,
        IInstitutionalAccessService access,IAccountIdentityProvider identity,CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl="private, no-store";
        if(!AccountLifecyclePolicy.IsUniqueMaster(ctx.User,access)) return Results.Forbid();
        if(!Guid.TryParse(subject,out var parsed)) return Results.NotFound();
        if(identity is not IAccountIdentitySecurityReader securityReader || !identity.Configured)
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        try
        {
            var snapshot=await securityReader.ReadSecuritySnapshotAsync(parsed.ToString(),ct);
            if(snapshot is null) return Results.NotFound();
            return Results.Ok(new {
                snapshot.Subject,snapshot.Username,snapshot.Enabled,snapshot.Managed,
                snapshot.Kind,snapshot.PrivilegedRoleDetected,snapshot.EffectiveRoleEvidenceComplete,
                operationsAvailable=false,
                requiresFurtherVerification=!snapshot.EffectiveRoleEvidenceComplete
            });
        }
        catch(Exception e) when(e is AccountDeliveryException or HttpRequestException or TaskCanceledException
            or System.Text.Json.JsonException)
        { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
    }

    // Seguimiento read-only de intentos sin cierre confirmado. No reintenta
    // comandos ni infiere que Keycloak y PostgreSQL estén conciliados.
    private static async Task<IResult> GetLifecycleReconciliation(string subject,
        HttpContext ctx,PmgmDbContext db,IInstitutionalAccessService access,
        IAccountIdentityProvider identity,CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl="private, no-store";
        if(!AccountLifecyclePolicy.IsUniqueMaster(ctx.User,access)) return Results.Forbid();
        if(!Guid.TryParse(subject,out var target)) return Results.NotFound();
        var actor=DynamicTreasuryAccess.Subject(ctx.User);
        if(identity is not IUniqueMasterIdentityVerifier verifier ||
            !identity.Configured || string.IsNullOrWhiteSpace(actor))
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        try
        {
            if(!await verifier.VerifyUniqueMasterDistinctFromAsync(actor,target.ToString(),ct))
                return Results.Forbid();
            var canonical=target.ToString();
            const string requested="system.users.lifecycle_requested";
            const string completed="system.users.lifecycle_completed";
            const string failed="system.users.lifecycle_failed";
            const string preflightRejected="system.users.lifecycle_preflight_rejected";
            // Nunca retornar metadatos, motivos, IP ni nombres de personas.
            var events=await db.AuditEvents.AsNoTracking()
                .Where(e=>e.EntityType=="UserAccount" && e.EntityId==canonical &&
                    (e.Action==requested || e.Action==completed || e.Action==failed ||
                     e.Action==preflightRejected))
                .OrderByDescending(e=>e.OccurredAtUtc).ThenByDescending(e=>e.Id)
                .Select(e=>new {e.Action,e.MetadataJson,e.OccurredAtUtc})
                .Take(201).ToListAsync(ct);
            var truncated=events.Count>200;
            // Sólo correlacionar por ID generado por el servidor. La cabecera
            // X-Correlation-ID es aportada por el cliente y no es evidencia.
            var linked=events.Take(200).Select(e=>new {
                e.Action,e.OccurredAtUtc,
                OperationId=ReadLifecycleOperationId(e.MetadataJson)
            }).ToArray();
            var items=linked.Where(e=>e.OperationId is not null)
                .GroupBy(e=>e.OperationId!,StringComparer.Ordinal)
                .Where(g=>g.Any(e=>e.Action==requested))
                .Select(g=>
                {
                    var hasCompleted=g.Any(e=>e.Action==completed);
                    var hasFailed=g.Any(e=>e.Action==failed);
                    var preflightDenied=g.Any(e=>e.Action==preflightRejected);
                    return new {
                        operationId=g.Key,
                        requestedAtUtc=g.Where(e=>e.Action==requested)
                            .Min(e=>e.OccurredAtUtc),
                        state=hasCompleted?"completed":hasFailed?
                            "failed_needs_review":preflightDenied?
                            "denied_no_external_mutation":"unresolved_needs_review",
                        requiresReview=!hasCompleted && !preflightDenied
                    };
                })
                .OrderByDescending(x=>x.requestedAtUtc)
                .Take(50).ToArray();
            return Results.Ok(new {subject=canonical,items,historyTruncated=truncated,
                unlinkedLegacyEvents=linked.Count(e=>e.OperationId is null),
                automaticRetryAllowed=false});
        }
        catch(Exception e) when(e is AccountDeliveryException or HttpRequestException
            or TaskCanceledException or System.Text.Json.JsonException
            or System.Data.Common.DbException)
        { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
    }

    private static string? ReadLifecycleOperationId(string? metadata)
    {
        if(string.IsNullOrWhiteSpace(metadata)) return null;
        try
        {
            using var json=System.Text.Json.JsonDocument.Parse(metadata);
            if(json.RootElement.ValueKind==System.Text.Json.JsonValueKind.Object &&
                json.RootElement.TryGetProperty("operationId",out var id) &&
                id.ValueKind==System.Text.Json.JsonValueKind.String &&
                Guid.TryParseExact(id.GetString(),"N",out var parsed))
                return parsed.ToString("N");
        }
        catch(System.Text.Json.JsonException) { }
        return null;
    }

    // Acciones opt-in: el despliegue existente permanece solo lectura. No
    // equivale a aprobación QA/UAT: la bandera se habilita tras probar
    // introspección real, auditoría y respaldo del entorno autorizado.
    private static async Task<IResult> MutateLifecycle(string subject,
        AccountLifecycleMutationRequest request,AccountLifecycleAction action,
        HttpContext ctx,PmgmDbContext db,IInstitutionalAccessService access,
        IAccountIdentityProvider identity,IAuditService audit,
        Microsoft.Extensions.Options.IOptions<AccountIdentityOptions> options,
        CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl="private, no-store";
        if(!AccountLifecyclePolicy.IsUniqueMaster(ctx.User,access)) return Results.Forbid();
        var cfg=options.Value;
        if(!cfg.EnableLifecycleMutations || !cfg.RequireLiveTokenIntrospection ||
            identity is not IAccountIdentityInventory inventory ||
            identity is not IAccountIdentitySecurityReader reader ||
            identity is not IAccountIdentityLifecycleCommands commands ||
            identity is not IUniqueMasterIdentityVerifier masterVerifier ||
            !identity.Configured)
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

        var validReason=AccountLifecyclePolicy.ValidateReason(request.Reason);
        if(!validReason.Allowed)
            return Results.BadRequest(new {code=validReason.DenialCode,
                message="El motivo debe contener de 10 a 200 caracteres sin caracteres de control."});
        if(!Guid.TryParse(subject,out var targetId)) return Results.NotFound();
        var targetSubject=targetId.ToString();
        var actor=DynamicTreasuryAccess.Subject(ctx.User)??"";
        if(string.Equals(actor,targetSubject,StringComparison.OrdinalIgnoreCase))
            return Results.Forbid();

        var actionCode=action switch {
            AccountLifecycleAction.Activate=>"activate",
            AccountLifecycleAction.Deactivate=>"deactivate",
            _=>"delete_permanently"
        };
        // ID generado en el servidor, distinto de X-Correlation-ID del cliente.
        var operationId=Guid.NewGuid().ToString("N");
        // Registro durable de la intención ANTES de llamar Keycloak o abrir la
        // transacción de conciliación. Si el proceso cae durante una mutación
        // externa, este evento sobrevive y permite localizar el caso pendiente.
        // No representa aprobación ni una operación completada.
        audit.Add(ctx,"system.users.lifecycle_requested","UserAccount",targetSubject,
            null,AuditResults.Observed,new {operationId,action=actionCode,reason=request.Reason?.Trim()});
        await db.SaveChangesAsync(ct);

        // Serializar por identidad en PostgreSQL: la verificación no puede
        // hacerse fuera del bloqueo ni confiar en datos de la pantalla.
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        // Las denegaciones de preflight nunca alcanzan comandos externos.
        // Cerrar su auditoría fuera de la transacción evita falsos pendientes.
        async Task<IResult> RejectPreflight(IResult response,string code)
        {
            await tx.RollbackAsync(CancellationToken.None);
            await tx.DisposeAsync();
            db.ChangeTracker.Clear();
            audit.Add(ctx,"system.users.lifecycle_preflight_rejected",
                "UserAccount",targetSubject,null,AuditResults.Rejected,
                new {operationId,action=actionCode,code});
            await db.SaveChangesAsync(CancellationToken.None);
            return response;
        }
        var externalMutationStarted=false;
        try
        {
            var lockKey="account-lifecycle:"+targetSubject;
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey},0))",ct);
            // Operación previa sin cierre seguro: bloquear antes de TODA
            // mutación externa. Una nueva solicitud no puede "arreglar" ni
            // repetir un fallo parcial mediante una acción de la pantalla.
            // La comprobación se hace bajo el advisory lock por cuenta.
            var recent=await db.AuditEvents.AsNoTracking()
                .Where(e=>e.EntityType=="UserAccount" && e.EntityId==targetSubject &&
                    (e.Action=="system.users.lifecycle_requested" ||
                     e.Action=="system.users.lifecycle_completed" ||
                     e.Action=="system.users.lifecycle_failed" ||
                     e.Action=="system.users.lifecycle_preflight_rejected"))
                .OrderByDescending(e=>e.OccurredAtUtc).ThenByDescending(e=>e.Id)
                .Select(e=>new {e.Action,e.MetadataJson})
                .Take(501).ToListAsync(ct);
            // Si no cabe toda la historia de la cuenta en el límite de
            // inspección, no asumir que los intentos antiguos quedaron cerrados.
            if(recent.Count>500)
                return await RejectPreflight(Results.Conflict(
                    new {code="lifecycle_history_requires_review"}),
                    "lifecycle_history_requires_review");
            var priorOperations=recent.Select(e=>new {
                e.Action,Id=ReadLifecycleOperationId(e.MetadataJson)
            }).Where(e=>e.Id is not null && e.Id!=operationId)
                .GroupBy(e=>e.Id!,StringComparer.Ordinal);
            if(priorOperations.Any(g=>
                g.Any(e=>e.Action=="system.users.lifecycle_failed") ||
                (g.Any(e=>e.Action=="system.users.lifecycle_requested") &&
                 !g.Any(e=>e.Action=="system.users.lifecycle_completed") &&
                 !g.Any(e=>e.Action=="system.users.lifecycle_preflight_rejected"))))
                return await RejectPreflight(Results.Conflict(
                    new {code="prior_lifecycle_requires_review"}),
                    "prior_lifecycle_requires_review");
            // Eventos antiguos sin operationId tampoco acreditan cierre seguro.
            if(recent.Any(e=>ReadLifecycleOperationId(e.MetadataJson) is null))
                return await RejectPreflight(Results.Conflict(
                    new {code="legacy_lifecycle_audit_requires_review"}),
                    "legacy_lifecycle_audit_requires_review");
            var matches=(await inventory.ListInventoryAsync(ct))
                .Where(x=>string.Equals(x.Subject,targetSubject,StringComparison.OrdinalIgnoreCase))
                .Take(2).ToArray();
            if(matches.Length!=1)
                return await RejectPreflight(Results.NotFound(),"target_not_unique_or_missing");
            var target=matches[0];
            var live=await reader.ReadSecuritySnapshotAsync(targetSubject,ct);
            if(live is null || live.Subject!=targetSubject ||
                !string.Equals(live.Username,target.Username,StringComparison.Ordinal) ||
                live.Enabled!=target.Enabled || live.Managed!=target.Managed)
                return await RejectPreflight(Results.Conflict(new {code="identity_state_changed"}),
                    "identity_state_changed");

            // Nunca confiar solo en el rol contenido en un JWT anterior:
            // revalidar en Keycloak la existencia y rol efectivo del único
            // Máster para TODAS las operaciones, no únicamente qa.admin.
            var masterVerified=
                await masterVerifier.VerifyUniqueMasterDistinctFromAsync(actor,targetSubject,ct);
            if(!masterVerified)
                return await RejectPreflight(Results.Forbid(),"master_not_verified");
            var qaException=action==AccountLifecycleAction.DeletePermanently &&
                string.Equals(target.Username,"qa.admin",StringComparison.OrdinalIgnoreCase);

            var entitled=false;
            if(action==AccountLifecycleAction.Activate && live.Kind=="member" && live.Managed)
            {
                var links=await db.Database.SqlQuery<Guid>(
                    $"SELECT \"MemberId\" AS \"Value\" FROM core.member_identity_links WHERE \"Issuer\"={identity.Issuer} AND \"Subject\"={targetSubject} AND \"RevokedAtUtc\" IS NULL")
                    .ToListAsync(ct);
                entitled=links.Count==1 && await Eligible(db,DynamicAccessEndpoints.Today())
                    .AnyAsync(m=>m.MemberId==links[0],ct);
            }
            // Soporte exige verificación de perfiles propia antes de reactivar;
            // se deniega hasta implementar esa comprobación de concesiones.
            var evidence=new AccountLifecycleEvidence(
                RolesReadFromKeycloak:true,
                HasPrivilegedEffectiveRole:live.PrivilegedRoleDetected,
                IdentityStateVerified:true,
                CurrentEntitlementVerified:entitled,
                RevocationGuaranteed:true, // validación OnTokenValidated con flag obligatorio
                HistoricalDataRetentionVerified:true, // no eliminar hechos ni auditoría
                LegacyQaAdminSubjectVerified:qaException &&
                    live.Username=="qa.admin" && target.Kind=="legacy_protected",
                UniqueMasterSubjectVerifiedDistinct:masterVerified,
                EffectiveRoleEvidenceComplete:live.EffectiveRoleEvidenceComplete);
            var decision=AccountLifecyclePolicy.Evaluate(
                action,target,actor,evidence,request.Confirmation);
            if(!decision.Allowed)
                return await RejectPreflight(Results.Conflict(new {code=decision.DenialCode}),
                    decision.DenialCode??"policy_denied");


            // Todo error a partir de aquí puede implicar una mutación parcial
            // en Keycloak y exige conciliación manual, incluso si la llamada falla.
            externalMutationStarted=true;
            if(action==AccountLifecycleAction.Activate)
                await commands.ReactivateAsync(targetSubject,ct);
            else
            {
                await commands.DisableAndLogoutAsync(targetSubject,ct);
                if(action==AccountLifecycleAction.DeletePermanently)
                {
                    await commands.DeleteDisabledAsync(targetSubject,ct);
                    var now=DateTimeOffset.UtcNow;
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE core.member_identity_links SET \"RevokedAtUtc\"={now} WHERE \"Issuer\"={identity.Issuer} AND \"Subject\"={targetSubject} AND \"RevokedAtUtc\" IS NULL",ct);
                }
            }
            var after=await reader.ReadSecuritySnapshotAsync(targetSubject,ct);
            if(action==AccountLifecycleAction.DeletePermanently
                ? after is not null
                : after is null || after.Enabled!=(action==AccountLifecycleAction.Activate))
                throw new AccountDeliveryException();

            audit.Add(ctx,"system.users.lifecycle_completed","UserAccount",targetSubject,
                null,AuditResults.Success,new {operationId,action=actionCode,reason=request.Reason?.Trim(),
                    identityVerified=true});
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(new {subject=targetSubject,action=actionCode,completed=true});
        }
        catch(Exception e) when(e is AccountDeliveryException or AccountConflictException
            or HttpRequestException or TaskCanceledException or System.Text.Json.JsonException
            or DbUpdateException or System.Data.Common.DbException)
        {
            if(!externalMutationStarted)
                return await RejectPreflight(
                    Results.StatusCode(StatusCodes.Status503ServiceUnavailable),
                    "preflight_provider_or_storage_unavailable");
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            // Si Keycloak cambió pero la transacción DB falló, no anunciar éxito.
            audit.Add(ctx,"system.users.lifecycle_failed","UserAccount",targetSubject,
                null,AuditResults.Rejected,new {operationId,action=action.ToString(),
                    reason="identity_or_storage_reconciliation_required"});
            await db.SaveChangesAsync(CancellationToken.None);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> CreateUserAccount(CreateUserAccountRequest request,HttpContext ctx,PmgmDbContext db,
        IInstitutionalAccessService access,IAccountIdentityProvider identity,IInitialAccountMail mail,IAuditService audit,
        ILoggerFactory logs,CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl="private, no-store";
        // Decisión Sponsor #604: el Máster único es la única autoridad para
        // altas de usuarios y superusuarios, aun si otro rol puede administrar
        // vistas del Sistema. Denegar antes de tocar identidad, SMTP o BD.
        if(!access.CanConfigureSystem(ctx.User) || !access.IsPlatformOperator(ctx.User))
            return Results.Forbid();
        // Solo el Máster crea Superusuarios; un Superusuario crea usuarios comunes.
        if(request.Superuser && !access.IsPlatformSuperAdmin(ctx.User))
        {
            audit.Add(ctx,"system.users.superuser_creation_denied","UserAccount","platform",null,AuditResults.Rejected,new { });
            await db.SaveChangesAsync(ct);
            return Results.Json(new { message="Solo el Máster puede crear Superusuarios." },statusCode:StatusCodes.Status403Forbidden);
        }
        // Solo existe UN Máster (cuenta «admin», provista fuera de esta API). Nadie, ni siquiera el Máster,
        // puede crear, duplicar ni promover otro: se rechaza antes de tocar identidad, correo o base.
        if(request.PlatformAdministrator)
        {
            audit.Add(ctx,"system.users.master_creation_denied","UserAccount","platform",null,AuditResults.Rejected,new { superuser=request.Superuser });
            await db.SaveChangesAsync(ct);
            return Results.Json(new { message="No se puede crear otro Máster: la plataforma tiene una sola cuenta Máster. Cree un Usuario o un Superusuario." },statusCode:StatusCodes.Status403Forbidden);
        }
        if(request.Superuser ? request.MemberId is not null || request.OrganizationId is not null ||
            string.IsNullOrWhiteSpace(request.PlatformName) || request.PlatformName.Length>200 || !AccountPassword.ValidEmail(request.PlatformEmail)
            : request.MemberId is null || request.OrganizationId is null || request.PlatformEmail is not null || request.PlatformName is not null)
            return Results.BadRequest(new { message="Seleccione un Hermano activo y su Taller. Sólo el Superusuario de soporte admite nombre y correo propios." });
        if(!identity.Configured || !mail.Configured) return Failure();
        PreparedIdentity? prepared=null;var committed=false;
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            var lockKey="accounts:"+(request.MemberId?.ToString()??request.PlatformEmail!.ToLowerInvariant());
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey},0))",ct);
            AccountIdentity account;
            List<string> links=[];
            if(request.Superuser) account=new(request.PlatformName!.Trim(),request.PlatformEmail!,null,null,false,true);
            else
            {
                var candidate=await Eligible(db,DynamicAccessEndpoints.Today()).Where(m=>m.MemberId==request.MemberId && m.OrganizationId==request.OrganizationId)
                    .Select(m=>new AccountCandidate(m.MemberId,m.OrganizationId,m.Member.Person.FirstNames+" "+m.Member.Person.LastNames,m.Member.Person.Email??"",m.Organization.Name))
                    .FirstOrDefaultAsync(ct);
                if(candidate is null || !AccountPassword.ValidEmail(candidate.Email))
                    return Results.BadRequest(new { message="El Hermano debe estar activo en ese Taller y tener un correo válido en su ficha." });
                account=new(candidate.Name,candidate.Email,candidate.MemberId,candidate.OrganizationId,false,false);
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
                new { account.MemberId,account.Superuser,temporaryCredential=true });
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);committed=true;await tx.DisposeAsync();
            // Recheck immediately before activation; the SMTP wait must not authorize an inactive member.
            if(account.MemberId is Guid id && !await Eligible(db,DynamicAccessEndpoints.Today()).AnyAsync(m=>m.MemberId==id&&m.OrganizationId==account.OrganizationId,ct))
                throw new AccountDeliveryException();
            await identity.EnableAsync(prepared.Subject,ct);
            audit.Add(ctx,"system.users.created","UserAccount",prepared.Subject,account.OrganizationId,AuditResults.Success,
                new { account.MemberId,account.Superuser,requiresPasswordChange=true });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/system/user-accounts/{prepared.Subject}",new {
                subject=prepared.Subject,email=account.Email,account.MemberId,account.OrganizationId,
                platformAdministrator=false,superuser=account.Superuser,requiresPasswordChange=true,initialPasswordEmailSent=true });
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
                new { request.Superuser,reason=e is AccountConflictException?"conflict":"delivery_or_identity" });
            await db.SaveChangesAsync(CancellationToken.None);
            return e is AccountConflictException ? Results.Conflict(new { message="Ya existe una cuenta o vínculo de identidad para este Hermano/correo." }) : Failure();
        }
    }
    // Restablecer acceso (#556). Decisión PO 19-10: A) enlace de un solo uso por correo; B) clave temporal mostrada UNA vez.
    // Solo el Máster; nunca a sí mismo ni a la cuenta Máster; la clave jamás se guarda, audita ni registra.
    private static readonly Dictionary<string,Queue<DateTimeOffset>> RecoveryAttempts=new();
    private static bool RateLimited(string key,int max,TimeSpan window)
    {
        lock(RecoveryAttempts)
        {
            var now=DateTimeOffset.UtcNow;
            if(!RecoveryAttempts.TryGetValue(key,out var q)) RecoveryAttempts[key]=q=new();
            while(q.Count>0 && now-q.Peek()>window) q.Dequeue();
            if(q.Count>=max) return true;
            q.Enqueue(now);return false;
        }
    }
    public static void ResetRateLimits(){lock(RecoveryAttempts)RecoveryAttempts.Clear();}
    private static async Task<IResult> ResetAccountAccess(string subject,PasswordRecoveryRequest request,HttpContext ctx,PmgmDbContext db,
        IInstitutionalAccessService access,IAccountIdentityProvider identity,IAuditService audit,ILoggerFactory logs,CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl="private, no-store";
        if(!access.CanConfigureSystem(ctx.User) || !access.IsPlatformOperator(ctx.User)) return Results.Forbid();
        var temporary=request.Modality=="temporary";
        var reason=request.Reason?.Trim()??"";
        if(request.Modality is not ("email" or "temporary") || reason.Length>200 || temporary && reason.Length<10)
            return Results.BadRequest(new { message=temporary?"Indique el motivo (10 a 200 caracteres) para entregar una clave temporal.":"Seleccione cómo restablecer el acceso." });
        var actor=DynamicTreasuryAccess.Subject(ctx.User)??"";
        var selfTarget=string.Equals(actor,subject,StringComparison.OrdinalIgnoreCase);
        if(!selfTarget)
        {
            if(!Guid.TryParse(subject,out var parsed)) return Results.NotFound(new { message="La cuenta no existe o no es administrada por la plataforma." });
            subject=parsed.ToString();
        }
        async Task<IResult> Reject(int status,string message,string why)
        {
            audit.Add(ctx,"system.users.access_reset_denied","UserAccount",subject,null,AuditResults.Rejected,new { modality=request.Modality,reason=why });
            await db.SaveChangesAsync(ct);
            return Results.Json(new { message },statusCode:status);
        }
        if(selfTarget)
            return await Reject(StatusCodes.Status403Forbidden,"No puede restablecer su propio acceso; solicítelo a otra autoridad o use «Olvidé mi clave».","self");
        if(!identity.Configured) return Failure();
        if(RateLimited("actor:"+actor,10,TimeSpan.FromMinutes(10)) || RateLimited("account:"+subject,3,TimeSpan.FromMinutes(10)))
            return await Reject(StatusCodes.Status429TooManyRequests,"Demasiados intentos. Espere unos minutos antes de reintentar.","rate_limited");
        try
        {
            var user=await identity.FindManagedAsync(subject,ct);
            if(user is null) return await Reject(StatusCodes.Status404NotFound,"La cuenta no existe o no es administrada por la plataforma.","unknown_account");
            if(user.Kind=="platform") return await Reject(StatusCodes.Status403Forbidden,"La cuenta Máster no se restablece desde esta pantalla.","master_account");
            if(user.Kind=="superuser" && !access.IsPlatformSuperAdmin(ctx.User))
                return await Reject(StatusCodes.Status403Forbidden,"Solo el Máster puede restablecer el acceso de otro Superusuario.","superuser_account");
            if(!user.Enabled) return await Reject(StatusCodes.Status409Conflict,"La cuenta tiene el alta pendiente; complete o repita el alta.","not_enabled");
            if(!temporary && !AccountPassword.ValidEmail(user.Email))
                return await Reject(StatusCodes.Status400BadRequest,"La cuenta no tiene un correo válido. Use la clave temporal con motivo.","no_email");
            string? password=null;
            if(temporary){password=AccountPassword.Generate();await identity.SetTemporaryPasswordAsync(subject,password,ct);}
            else await identity.SendPasswordRecoveryEmailAsync(subject,ct);
            audit.Add(ctx,"system.users.access_reset","UserAccount",subject,null,AuditResults.Success,
                new { modality=request.Modality,accountKind=user.Kind,reason=temporary?reason:null,requiresPasswordChange=true,sessionsClosed=true });
            await db.SaveChangesAsync(ct);
            return temporary
                ? Results.Ok(new { subject,modality="temporary",temporaryPassword=password,requiresPasswordChange=true,shownOnce=true,name=user.Name })
                : Results.Ok(new { subject,modality="email",emailSent=true,expiresInMinutes=30,name=user.Name,email=user.Email });
        }
        catch(Exception e) when(e is AccountDeliveryException or AccountConflictException or HttpRequestException or TaskCanceledException)
        {
            logs.CreateLogger("UserAccounts").LogError("No se completó el restablecimiento de acceso. Correlación {CorrelationId}.",ctx.TraceIdentifier);
            audit.Add(ctx,"system.users.access_reset_failed","UserAccount",subject,null,AuditResults.Rejected,new { modality=request.Modality,reason="identity_or_delivery" });
            await db.SaveChangesAsync(CancellationToken.None);
            return Results.Json(new { message="No se pudo restablecer el acceso. Revise la configuración de identidad/correo e inténtelo más tarde." },statusCode:503);
        }
    }
    private static IResult Failure()=>Results.Json(new { message="No se completó el alta o el envío. Revise la configuración de identidad/correo antes de reintentar." },statusCode:503);
}
