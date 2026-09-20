using System.Text;
using PMGM.Api.Modules.GrandSecretariat;
using PMGM.Api.Modules.GrandSecretariat.Entities;
using Xunit;

namespace PMGM.Api.Tests.GrandSecretariat;

public sealed class SecretariatAccessibleDocumentRendererTests
{
    [Fact]
    public void Render_produces_semantic_accessible_html_and_escapes_content()
    {
        var document = new SecretariatDocument
        {
            DocumentType = GrandSecretariatCodes.DocumentType.Plancha,
            PlanchaKind = GrandSecretariatCodes.PlanchaKind.CeremonyAuthorization,
            DocumentCode = "PLA-AUT-CER-2026-001",
            Title = "Autorización <solemne>",
            Content = "Primer párrafo.\nSegundo <párrafo> & cierre.",
            Status = GrandSecretariatCodes.DocumentStatus.Issued,
            IssuedAtUtc = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero)
        };

        var html = Encoding.UTF8.GetString(SecretariatAccessibleDocumentRenderer.Render(document));

        Assert.Contains("<html lang=\"es\">", html);
        Assert.Contains("<main>", html);
        Assert.Contains("aria-label=\"Contenido del documento\"", html);
        Assert.Contains("font-family:Cambria", html);
        Assert.Contains("#243b67", html);
        Assert.Contains("#8a6b1f", html);
        Assert.Contains("Autorización &lt;solemne&gt;", html);
        Assert.Contains("Segundo &lt;párrafo&gt; &amp; cierre.", html);
        Assert.DoesNotContain("Segundo <párrafo>", html);
    }

    [Fact]
    public void FileName_removes_unsafe_characters()
    {
        var document = new SecretariatDocument { DocumentCode = "DEC/2026: 01" };
        Assert.Equal("DEC-2026--01-version-accesible.html", SecretariatAccessibleDocumentRenderer.FileName(document));
    }
}
