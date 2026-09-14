using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.LodgeManagement;

namespace PMGM.Api.Modules.Membership;

public static class MemberSelfEndpoints
{
    public static IEndpointRouteBuilder MapMemberSelfEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/member-self")
            .WithTags("Member Self Service")
            .RequireAuthorization();

        group.MapGet("/profile", GetSelfProfileAsync);
        group.MapPut("/contact", UpdateSelfContactAsync);
        return endpoints;
    }

    private static async Task<IResult> GetSelfProfileAsync(
        HttpContext httpContext,
        PmgmDbContext db,
        LodgeManagementDbContext lodgeDb,
        IInstitutionalMemberContextResolver resolver,
        CancellationToken cancellationToken)
    {
        var context = await resolver.ResolveAsync(httpContext.User, cancellationToken);
        if (context is null)
        {
            return Results.NotFound(new { message = "La identidad autenticada no está vinculada a un Hermano institucional activo." });
        }

        var member = await db.Members
            .AsNoTracking()
            .Where(x => x.Id == context.MemberId)
            .Select(x => new
            {
                x.Id,
                x.PersonId,
                x.InstitutionalNumber,
                x.Person.FirstNames,
                x.Person.LastNames,
                x.Person.Email,
                x.Person.Phone,
                x.Person.Address
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (member is null)
        {
            return Results.NotFound();
        }

        var currentMembership = await db.Memberships
            .AsNoTracking()
            .Where(x => x.MemberId == context.MemberId &&
                        x.Status == MembershipCodes.MembershipStatus.Active &&
                        x.EndDate == null)
            .OrderByDescending(x => x.StartDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.OrganizationId,
                Organization = x.Organization.Name,
                OrganizationNumber = x.Organization.Number,
                OrganizationType = x.Organization.Type,
                x.MembershipType,
                x.StartDate,
                x.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        var degreeEvents = await db.DegreeEvents
            .AsNoTracking()
            .Where(x => x.MemberId == context.MemberId)
            .OrderBy(x => x.EffectiveDate)
            .ThenBy(x => x.RecordedAtUtc)
            .Select(x => new
            {
                x.Degree,
                x.EventType,
                x.EffectiveDate,
                x.OrganizationId,
                Organization = x.Organization.Name
            })
            .ToListAsync(cancellationToken);

        var latestStatus = await db.InstitutionalStatusEvents
            .AsNoTracking()
            .Where(x => x.MemberId == context.MemberId)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.EventType, x.EffectiveDate })
            .FirstOrDefaultAsync(cancellationToken);

        var initiation = degreeEvents.FirstOrDefault(x => x.EventType == MembershipCodes.DegreeEvent.Initiation)?.EffectiveDate;
        var wageIncrease = degreeEvents.FirstOrDefault(x => x.EventType == MembershipCodes.DegreeEvent.WageIncrease)?.EffectiveDate;
        var exaltation = degreeEvents.FirstOrDefault(x => x.EventType == MembershipCodes.DegreeEvent.Exaltation)?.EffectiveDate;
        var latestDegree = degreeEvents.LastOrDefault();

        var financial = await db.FinancialRegularitySnapshots
            .AsNoTracking()
            .Where(x => x.MemberId == context.MemberId)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.Status, x.AsOfDate, x.Scope })
            .FirstOrDefaultAsync(cancellationToken);

        object? hospitalaria = null;
        if (currentMembership is not null)
        {
            hospitalaria = await db.HospitalariaRegularitySnapshots
                .AsNoTracking()
                .Where(x => x.OrganizationId == currentMembership.OrganizationId)
                .OrderByDescending(x => x.AsOfDate)
                .ThenByDescending(x => x.RecordedAtUtc)
                .Select(x => new { x.Status, x.AsOfDate })
                .FirstOrDefaultAsync(cancellationToken);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var attendanceFrom = today.AddMonths(-12);
        var attendanceRows = await lodgeDb.LodgeAttendanceRecords
            .AsNoTracking()
            .Where(x => x.MemberId == context.MemberId &&
                        x.Meeting.MeetingDate >= attendanceFrom &&
                        x.Meeting.MeetingDate <= today &&
                        x.Meeting.Status != LodgeManagementCodes.MeetingStatus.Cancelled)
            .Select(x => new
            {
                x.MeetingId,
                x.Status,
                x.RecordedAtUtc,
                x.Meeting.MeetingDate,
                x.Meeting.MeetingType,
                x.Meeting.Grade,
                x.Meeting.Title
            })
            .ToListAsync(cancellationToken);

        var attendanceHistory = attendanceRows
            .GroupBy(x => x.MeetingId)
            .Select(group => group.OrderByDescending(x => x.RecordedAtUtc).First())
            .OrderByDescending(x => x.MeetingDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .ToList();

        var presentCount = attendanceHistory.Count(x => x.Status == LodgeManagementCodes.AttendanceStatus.Present);
        var absentCount = attendanceHistory.Count(x => x.Status == LodgeManagementCodes.AttendanceStatus.Absent);
        var excusedCount = attendanceHistory.Count(x => x.Status == LodgeManagementCodes.AttendanceStatus.Excused);
        var attendanceTotal = attendanceHistory.Count;
        var attendancePercentage = attendanceTotal == 0
            ? (int?)null
            : (int)Math.Round((double)presentCount * 100d / attendanceTotal, MidpointRounding.AwayFromZero);

        var instructionRows = await lodgeDb.LodgeInstructionAttendanceRecords
            .AsNoTracking()
            .Where(x => x.MemberId == context.MemberId &&
                        x.InstructionSession.Status == LodgeManagementCodes.InstructionStatus.Held)
            .Select(x => new
            {
                x.InstructionSessionId,
                x.Status,
                x.RecordedAtUtc,
                x.InstructionSession.InstructionDate,
                x.InstructionSession.Grade,
                x.InstructionSession.Topic,
                x.InstructionSession.ResponsibleOffice
            })
            .ToListAsync(cancellationToken);

        var instructionHistory = instructionRows
            .GroupBy(x => x.InstructionSessionId)
            .Select(group => group.OrderByDescending(x => x.RecordedAtUtc).First())
            .OrderByDescending(x => x.InstructionDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .ToList();

        var upcomingMeetings = Array.Empty<object>();
        if (currentMembership is not null)
        {
            var meetingRows = await lodgeDb.LodgeMeetings
                .AsNoTracking()
                .Where(x => x.OrganizationId == currentMembership.OrganizationId &&
                            x.MeetingDate >= today &&
                            x.Status != LodgeManagementCodes.MeetingStatus.Cancelled &&
                            (x.Status == LodgeManagementCodes.MeetingStatus.Scheduled ||
                             x.Status == LodgeManagementCodes.MeetingStatus.Open))
                .OrderBy(x => x.MeetingDate)
                .ThenBy(x => x.CreatedAtUtc)
                .Select(x => new
                {
                    x.Id,
                    x.MeetingDate,
                    x.MeetingType,
                    x.Grade,
                    x.Title,
                    x.Status
                })
                .Take(24)
                .ToListAsync(cancellationToken);

            upcomingMeetings = meetingRows
                .Where(x => IsMeetingVisibleToDegree(x.Grade, context.EffectiveDegree))
                .Take(8)
                .Select(x => (object)new
                {
                    x.Id,
                    x.MeetingDate,
                    x.MeetingType,
                    x.Grade,
                    x.Title,
                    x.Status
                })
                .ToArray();
        }

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new
        {
            member = new
            {
                member.Id,
                member.InstitutionalNumber,
                member.FirstNames,
                member.LastNames
            },
            contact = new
            {
                member.Email,
                member.Phone,
                member.Address
            },
            current = new
            {
                membership = currentMembership,
                degree = latestDegree,
                effectiveDegree = context.EffectiveDegree,
                institutionalStatus = latestStatus
            },
            milestones = new
            {
                initiation,
                wageIncrease,
                exaltation
            },
            regularity = new
            {
                financial,
                hospitalaria
            },
            activity = new
            {
                attendance = new
                {
                    fromDate = attendanceFrom,
                    throughDate = today,
                    total = attendanceTotal,
                    present = presentCount,
                    absent = absentCount,
                    excused = excusedCount,
                    percentage = attendancePercentage,
                    history = attendanceHistory.Take(12).Select(x => new
                    {
                        x.MeetingId,
                        x.MeetingDate,
                        x.MeetingType,
                        x.Grade,
                        x.Title,
                        attendanceStatus = x.Status
                    })
                },
                instruction = new
                {
                    total = instructionHistory.Count,
                    history = instructionHistory.Take(20).Select(x => new
                    {
                        x.InstructionSessionId,
                        x.InstructionDate,
                        x.Grade,
                        x.Topic,
                        attendanceStatus = x.Status,
                        x.ResponsibleOffice
                    })
                },
                upcomingMeetings
            }
        });
    }

    private static async Task<IResult> UpdateSelfContactAsync(
        UpdateSelfContactRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalMemberContextResolver resolver,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var context = await resolver.ResolveAsync(httpContext.User, cancellationToken);
        if (context is null)
        {
            return Results.NotFound(new { message = "La identidad autenticada no está vinculada a un Hermano institucional activo." });
        }

        var member = await db.Members
            .Include(x => x.Person)
            .SingleOrDefaultAsync(x => x.Id == context.MemberId, cancellationToken);
        if (member is null)
        {
            return Results.NotFound();
        }

        var email = Normalize(request.Email);
        var phone = Normalize(request.Phone);
        var address = Normalize(request.Address);

        if (email is { Length: > 320 } || (email is not null && !LooksLikeEmail(email)))
        {
            return Results.BadRequest(new { message = "El correo electrónico no tiene un formato válido." });
        }
        if (phone is { Length: > 80 })
        {
            return Results.BadRequest(new { message = "El teléfono supera el máximo permitido." });
        }
        if (address is { Length: > 500 })
        {
            return Results.BadRequest(new { message = "El domicilio supera el máximo permitido." });
        }

        var changedFields = new List<string>(3);
        if (!string.Equals(member.Person.Email, email, StringComparison.Ordinal))
        {
            member.Person.Email = email;
            changedFields.Add("email");
        }
        if (!string.Equals(member.Person.Phone, phone, StringComparison.Ordinal))
        {
            member.Person.Phone = phone;
            changedFields.Add("phone");
        }
        if (!string.Equals(member.Person.Address, address, StringComparison.Ordinal))
        {
            member.Person.Address = address;
            changedFields.Add("address");
        }

        if (changedFields.Count == 0)
        {
            httpContext.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(new { status = "unchanged", changedFields });
        }

        audit.Add(
            httpContext,
            action: "member.self.contact.updated",
            entityType: "member",
            entityId: member.Id.ToString(),
            organizationId: null,
            result: AuditResults.Success,
            metadata: new { ChangedFields = changedFields });

        await db.SaveChangesAsync(cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { status = "updated", changedFields });
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static bool LooksLikeEmail(string value)
    {
        var at = value.IndexOf('@');
        return at > 0 && at < value.Length - 1 && value.IndexOf('.', at) > at + 1;
    }

    private static bool IsMeetingVisibleToDegree(string grade, int effectiveDegree)
    {
        if (grade == LodgeManagementCodes.Grade.All) return true;

        var requiredDegree = LodgeManagementCodes.Grade.ToNumeric(grade)
            ?? (int.TryParse(grade, out var numericGrade) ? numericGrade : int.MaxValue);
        return requiredDegree <= effectiveDegree;
    }
}

public sealed record UpdateSelfContactRequest(string? Email, string? Phone, string? Address);
