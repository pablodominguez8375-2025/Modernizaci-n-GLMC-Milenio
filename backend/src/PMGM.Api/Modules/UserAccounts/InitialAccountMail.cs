using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.SystemConfiguration;

namespace PMGM.Api.Modules.UserAccounts;

/// <summary>
/// El alta inicial utiliza la credencial cifrada administrada en Parámetros
/// cuando está configurada; mantiene compatibilidad con la configuración
/// SMTP de entorno previa (que también debe vivir fuera de GitHub).
/// </summary>
public sealed class InitialAccountMail(
    IOptions<InitialAccountMailOptions> options,
    PmgmDbContext db,
    SystemMailCredentialVault vault,
    ISystemMailSender sender) : IInitialAccountMail
{
    private readonly InitialAccountMailOptions settings = options.Value;
    private bool LegacyConfigured =>
        !string.IsNullOrWhiteSpace(settings.Host) &&
        settings.Port is > 0 and <= 65535 &&
        settings.EnableSsl &&
        AccountPassword.ValidEmail(settings.From);

    public bool Configured =>
        (LegacyConfigured || vault.Available) &&
        Uri.TryCreate(settings.LoginUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == "https";

    public async Task SendAsync(AccountIdentity account, string temporaryPassword, CancellationToken ct)
    {
        if (!Configured) throw new AccountDeliveryException();
        var body =
            $"Su cuenta de Proyecto Centenario está preparada.\n\nUsuario: {account.Email}\n" +
            $"Clave inicial temporal: {temporaryPassword}\nIngreso: {settings.LoginUrl}\n\n" +
            "Al ingresar debe cambiar esta clave por una personal. Si olvida su clave, utilice la recuperación en la pantalla de acceso.\n";

        try
        {
            // El sistema NO interpreta una clave de Parámetros como permiso
            // de usar TLS no verificado, servidor externo o puerto 465.
            var google = await GoogleSmtpPolicy.ResolveAsync(db, vault, ct);
            if (google is not null)
            {
                await sender.SendAsync(google, account.Email,
                    "Acceso inicial a Proyecto Centenario", body, ct);
                return;
            }
            if (!LegacyConfigured) throw new AccountDeliveryException();

            using var smtp = new SmtpClient(settings.Host, settings.Port)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Timeout = 30000
            };
            if (!string.IsNullOrWhiteSpace(settings.Username))
                smtp.Credentials = new NetworkCredential(settings.Username, settings.Password);
            using var message = new MailMessage(settings.From, account.Email)
            {
                Subject = "Acceso inicial a Proyecto Centenario",
                Body = body,
                IsBodyHtml = false
            };
            await smtp.SendMailAsync(message, ct);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException
            or System.Net.Sockets.SocketException or
            System.Security.Authentication.AuthenticationException)
        {
            throw new AccountDeliveryException();
        }
    }
}
