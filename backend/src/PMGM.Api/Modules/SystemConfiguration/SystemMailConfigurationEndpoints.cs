using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.UserAccounts;

namespace PMGM.Api.Modules.SystemConfiguration;

/// <summary>
/// Clave simétrica de 256 bits suministrada en el servidor mediante
/// SystemMail__EncryptionKey (base64), nunca desde API, git, Drive ni frontend.
/// El ciphertext en BD es irreconocible sin el secreto de despliegue.
/// </summary>
public sealed class SystemMailCredentialVault(IConfiguration configuration)
{
    public bool Available => Key() is not null;

    private byte[]? Key()
    {
        try
        {
            var encoded = configuration["SystemMail:EncryptionKey"];
            if (string.IsNullOrWhiteSpace(encoded)) return null;
            var key = Convert.FromBase64String(encoded);
            return key.Length == 32 ? key : null;
        }
        catch (FormatException) { return null; }
    }

    public string Protect(string plain)
    {
        var key = Key() ?? throw new InvalidOperationException("SMTP_KEY_NOT_CONFIGURED");
        if (string.IsNullOrWhiteSpace(plain) || plain.Length > 256)
            throw new ArgumentException("INVALID_SMTP_SECRET");
        var bytes = System.Text.Encoding.UTF8.GetBytes(plain);
        var iv = RandomNumberGenerator.GetBytes(12);
        var encrypted = new byte[bytes.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(key, 16))
            aes.Encrypt(iv, bytes, encrypted, tag,
                System.Text.Encoding.UTF8.GetBytes("PMGM-SMTP-GOOGLE-V1"));
        return "g1:" + Convert.ToBase64String([.. iv, .. tag, .. encrypted]);
    }

    public string Unprotect(string protectedValue)
    {
        var key = Key() ?? throw new InvalidOperationException("SMTP_KEY_NOT_CONFIGURED");
        if (!protectedValue.StartsWith("g1:", StringComparison.Ordinal))
            throw new InvalidOperationException("SMTP_SECRET_UNREADABLE");
        try
        {
            var payload = Convert.FromBase64String(protectedValue[3..]);
            if (payload.Length < 29) throw new CryptographicException();
            var result = new byte[payload.Length - 28];
            using (var aes = new AesGcm(key, 16))
                aes.Decrypt(payload.AsSpan(0, 12), payload.AsSpan(28), payload.AsSpan(12, 16),
                    result, System.Text.Encoding.UTF8.GetBytes("PMGM-SMTP-GOOGLE-V1"));
            return System.Text.Encoding.UTF8.GetString(result);
        }
        catch (Exception e) when (e is FormatException or CryptographicException)
        {
            throw new InvalidOperationException("SMTP_SECRET_UNREADABLE");
        }
    }
}

public sealed record GoogleSmtpConfiguration(
    string Host, int Port, string Username, string Password,
    string Sender, string SenderName);

public static class GoogleSmtpPolicy
{
    public const string SecretCode = "system.mail.smtp_secret.protected";
    public const string HostCode = "system.mail.smtp_host";
    public const string PortCode = "system.mail.smtp_port";
    public const string AddressCode = "system.mail.sender_address";
    public const string NameCode = "system.mail.sender_name";
    public const string SecurityCode = "system.mail.security";

    public static bool IsGoogleSmtp(string? host, int port, string? transport,
        string? username, string? sender)
        => string.Equals(host, "smtp.gmail.com", StringComparison.OrdinalIgnoreCase) &&
           port == 587 &&
           string.Equals(transport, "STARTTLS", StringComparison.OrdinalIgnoreCase) &&
           AccountPassword.ValidEmail(username) &&
           AccountPassword.ValidEmail(sender) &&
           string.Equals(username, sender, StringComparison.OrdinalIgnoreCase);

    public static async Task<GoogleSmtpConfiguration?> ResolveAsync(
        PmgmDbContext db, SystemMailCredentialVault vault, CancellationToken ct)
    {
        if (!vault.Available) return null;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        var codes = new[] { SecretCode, HostCode, PortCode, AddressCode, NameCode, SecurityCode };
        var rows = await db.InstitutionalRuleSettings.AsNoTracking()
            .Where(x => codes.Contains(x.Code) && x.Status == "active" &&
                        x.EffectiveFrom <= today &&
                        (x.EffectiveTo == null || x.EffectiveTo >= today))
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
        var secret = rows.FirstOrDefault(x => x.Code == SecretCode)?.Value;
        if (string.IsNullOrWhiteSpace(secret)) return null;
        string V(string code, string fallback) =>
            rows.FirstOrDefault(x => x.Code == code)?.Value ?? fallback;
        var host = V(HostCode, "");
        var username = V(AddressCode, "");
        var security = V(SecurityCode, "STARTTLS");
        if (!int.TryParse(V(PortCode, "587"), out var port) ||
            !IsGoogleSmtp(host, port, security, username, username))
            return null;
        return new GoogleSmtpConfiguration(host, port, username,
            vault.Unprotect(secret), username, V(NameCode, "Gran Logia Mixta de Chile"));
    }
}

public interface ISystemMailSender
{
    Task SendAsync(GoogleSmtpConfiguration configuration, string recipient,
        string subject, string body, CancellationToken ct);
}

public sealed class SystemMailSender : ISystemMailSender
{
    public async Task SendAsync(GoogleSmtpConfiguration configuration, string recipient,
        string subject, string body, CancellationToken ct)
    {
        if (!GoogleSmtpPolicy.IsGoogleSmtp(configuration.Host, configuration.Port,
            "STARTTLS", configuration.Username, configuration.Sender))
            throw new InvalidOperationException("UNSUPPORTED_SMTP_CONFIGURATION");
        using var smtp = new SmtpClient(configuration.Host, configuration.Port)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(configuration.Username, configuration.Password),
            Timeout = 15000,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        using var mail = new MailMessage(
            new MailAddress(configuration.Sender, configuration.SenderName),
            new MailAddress(recipient))
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        await smtp.SendMailAsync(mail, ct);
    }
}

/// <summary>
/// Rutas de administración SMTP Google. Nunca devuelven secreto, RUT, recipient
/// de prueba ni detalles de fallos SMTP en logs HTTP/auditoría.
/// </summary>
public static class SystemMailConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapSystemMailConfigurationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var g = endpoints.MapGroup("/api/system/mail").RequireAuthorization()
            .WithTags("Sistema — Correo electrónico");
        g.MapGet("/credentials", StatusAsync);
        g.MapPost("/credentials", StoreCredentialAsync);
        g.MapPost("/test", TestAsync);
        return endpoints;
    }

    private static async Task<IResult> StatusAsync(
        HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access,
        SystemMailCredentialVault vault, CancellationToken ct)
    {
        if (!access.CanConfigureSystem(ctx.User)) return Results.Forbid();
        ctx.Response.Headers.CacheControl = "no-store";
        var found = await db.InstitutionalRuleSettings.AsNoTracking()
            .AnyAsync(x => x.Code == GoogleSmtpPolicy.SecretCode && x.Status == "active", ct);
        var encryptionReady = vault.Available;
        var transportReady = false;
        var reason = !encryptionReady ? "missing_encryption_key" :
            !found ? "missing_password" : "incomplete_transport";
        if (found && encryptionReady)
        {
            try
            {
                // Sólo lectura de configuración privada: NO abre red ni realiza SMTP.
                // Distinguir secreto almacenado de transporte realmente utilizable.
                transportReady = await GoogleSmtpPolicy.ResolveAsync(db, vault, ct) is not null;
                reason = transportReady ? "ready_to_test" : "incomplete_transport";
            }
            catch (InvalidOperationException)
            {
                // Una clave raíz reemplazada sin rotación no descifra el secreto
                // guardado. Nunca filtrar ciphertext ni detalles criptográficos.
                reason = "unreadable_password";
            }
        }
        return Results.Ok(new {
            configured = found && encryptionReady,
            encryptionReady,
            transportReady,
            reason
        });
    }

    private sealed record CredentialInput(string Password);

    private static async Task<IResult> StoreCredentialAsync(
        CredentialInput input, HttpContext ctx, PmgmDbContext db,
        IInstitutionalAccessService access, SystemMailCredentialVault vault,
        IAuditService audit, CancellationToken ct)
    {
        if (!access.CanConfigureSystem(ctx.User)) return Results.Forbid();
        ctx.Response.Headers.CacheControl = "no-store";
        if (!vault.Available) return Results.Conflict(new {
            code = "SMTP_ENCRYPTION_NOT_CONFIGURED",
            message = "El servidor requiere su clave de cifrado SMTP antes de guardar credenciales." });
        var password = input.Password?.Replace(" ", "", StringComparison.Ordinal).Trim();
        if (string.IsNullOrWhiteSpace(password) || password.Length is < 12 or > 256)
            return Results.BadRequest(new { code = "INVALID_APP_PASSWORD" });

        // Exigir una contraseña de aplicación, NO la clave principal de Google.
        var encrypted = vault.Protect(password);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serializa cambios concurrentes sin reutilizar la tabla pública de reglas.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({66920261023L})", ct);
        var entries = await db.InstitutionalRuleSettings
            .Where(x => x.Code == GoogleSmtpPolicy.SecretCode).ToListAsync(ct);
        if (entries.Count > 1) return Results.Conflict(new { code = "SMTP_SECRET_INCONSISTENT" });
        if (entries.Count == 1)
        {
            entries[0].Value = encrypted;
            entries[0].Status = "active";
            entries[0].EffectiveTo = null;
        }
        else
        {
            db.InstitutionalRuleSettings.Add(new InstitutionalRuleSetting {
                Code = GoogleSmtpPolicy.SecretCode, Value = encrypted,
                EffectiveFrom = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                    DateTimeOffset.UtcNow, "America/Santiago").DateTime),
                Status = "active", SourceReference = "SMTP cifrado — credencial gestionada" });
        }
        audit.Add(ctx, "system.mail.credential_updated", "MailTransport",
            "smtp.google", null, AuditResults.Success,
            new { CredentialConfigured = true });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Ok(new { configured = true });
    }

    private sealed record TestInput(string Recipient);

    private static async Task<IResult> TestAsync(
        TestInput input, HttpContext ctx, PmgmDbContext db,
        IInstitutionalAccessService access, SystemMailCredentialVault vault,
        ISystemMailSender sender, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanConfigureSystem(ctx.User)) return Results.Forbid();
        ctx.Response.Headers.CacheControl = "no-store";
        if (!AccountPassword.ValidEmail(input.Recipient))
            return Results.BadRequest(new { code = "INVALID_TEST_RECIPIENT" });
        GoogleSmtpConfiguration? settings;
        try { settings = await GoogleSmtpPolicy.ResolveAsync(db, vault, ct); }
        catch (InvalidOperationException) { return Results.Conflict(new { code = "SMTP_SECRET_UNREADABLE" }); }
        if (settings is null)
            return Results.Conflict(new { code = "SMTP_GOOGLE_CONFIG_INCOMPLETE",
                message = "Configure smtp.gmail.com, puerto 587, STARTTLS, remitente y clave de aplicación." });
        try
        {
            await sender.SendAsync(settings, input.Recipient,
                "Prueba de correo — Proyecto Centenario",
                "Se comprobó un envío real mediante SMTP de Google con STARTTLS.\n"+
                "Este mensaje no contiene credenciales.", ct);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or
            System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException)
        {
            audit.Add(ctx, "system.mail.delivery_tested", "MailTransport",
                "smtp.google", null, AuditResults.Rejected, new { Delivered = false });
            await db.SaveChangesAsync(ct);
            return Results.Problem(statusCode: 502,
                detail: "Google rechazó la autenticación, el envío o la conexión TLS. Compruebe la cuenta, contraseña de aplicación y conectividad.");
        }
        audit.Add(ctx, "system.mail.delivery_tested", "MailTransport",
            "smtp.google", null, AuditResults.Success, new { Delivered = true });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { sent = true, message = "Google aceptó el correo de prueba para entrega." });
    }
}
