using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace PMGM.Api.Modules.UserAccounts;

public sealed class InitialAccountMail(IOptions<InitialAccountMailOptions> options) : IInitialAccountMail
{
    private readonly InitialAccountMailOptions settings = options.Value;
    public bool Configured => !string.IsNullOrWhiteSpace(settings.Host) && settings.Port is > 0 and <= 65535 &&
        settings.EnableSsl && AccountPassword.ValidEmail(settings.From) &&
        Uri.TryCreate(settings.LoginUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https";
    public async Task SendAsync(AccountIdentity account, string temporaryPassword, CancellationToken ct)
    {
        if (!Configured) throw new AccountDeliveryException();
        using var smtp = new SmtpClient(settings.Host, settings.Port) { EnableSsl = true, Timeout = 30000 };
        if (!string.IsNullOrWhiteSpace(settings.Username)) smtp.Credentials = new NetworkCredential(settings.Username, settings.Password);
        using var message = new MailMessage(settings.From, account.Email)
        {
            Subject = "Acceso inicial a Proyecto Centenario",
            Body = $"Su cuenta de Proyecto Centenario está preparada.\n\nUsuario: {account.Email}\nClave inicial temporal: {temporaryPassword}\nIngreso: {settings.LoginUrl}\n\nAl ingresar debe cambiar esta clave por una personal. Si olvida su clave, utilice la recuperación en la pantalla de acceso.\n",
            IsBodyHtml = false
        };
        try { await smtp.SendMailAsync(message, ct); }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException) { throw new AccountDeliveryException(); }
    }
}
