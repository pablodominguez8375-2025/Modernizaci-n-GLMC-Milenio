using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using PMGM.Api.Modules.SystemConfiguration;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class SystemMailGooglePolicyTests
{
    private static SystemMailCredentialVault Vault(string base64) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["SystemMail:EncryptionKey"] = base64 }).Build());

    [Fact]
    public void Encrypted_secret_roundtrip_never_contains_password_and_randomizes_iv()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var vault = Vault(key);
        const string password = "abcdefghijklmnop";
        var first = vault.Protect(password);
        var second = vault.Protect(password);

        Assert.True(vault.Available);
        Assert.StartsWith("g1:", first);
        Assert.NotEqual(password, first);
        Assert.DoesNotContain(password, first);
        Assert.NotEqual(first, second);
        Assert.Equal(password, vault.Unprotect(first));
        Assert.Equal(password, vault.Unprotect(second));
    }

    [Fact]
    public void Incorrect_key_and_tampered_ciphertext_are_rejected_without_secret()
    {
        var secret = Vault(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)))
            .Protect("abcdefghijklmnop");
        var another = Vault(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var error = Assert.Throws<InvalidOperationException>(() => another.Unprotect(secret));
        Assert.Equal("SMTP_SECRET_UNREADABLE", error.Message);

        var keyUnavailable = Vault("invalid");
        Assert.False(keyUnavailable.Available);
        Assert.Throws<InvalidOperationException>(() => keyUnavailable.Protect("abcdefghijklmnop"));
    }

    [Theory]
    [InlineData("smtp.gmail.com", 587, "STARTTLS", true)]
    [InlineData("smtp.gmail.com", 465, "STARTTLS", false)]
    [InlineData("smtp.gmail.com", 587, "none", false)]
    [InlineData("127.0.0.1", 587, "STARTTLS", false)]
    [InlineData("smtp-relay.gmail.com", 587, "STARTTLS", false)]
    [InlineData("mail.example.org", 587, "STARTTLS", false)]
    public void Google_transport_is_fixed_to_tls_587_and_mailbox_identity(
        string host, int port, string mode, bool expected)
    {
        Assert.Equal(expected, GoogleSmtpPolicy.IsGoogleSmtp(host, port, mode,
            "notificaciones@example.org", "notificaciones@example.org"));
        Assert.False(GoogleSmtpPolicy.IsGoogleSmtp(host, port, mode,
            "notificaciones@example.org", "otra@example.org"));
    }
}
