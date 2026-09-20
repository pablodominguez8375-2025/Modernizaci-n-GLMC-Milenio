using System.Net;
using System.Text;
using PMGM.Api.Modules.GrandSecretariat.Entities;

namespace PMGM.Api.Modules.GrandSecretariat;

public static class SecretariatAccessibleDocumentRenderer
{
    public static byte[] Render(SecretariatDocument document)
    {
        var title = WebUtility.HtmlEncode(document.Title);
        var code = WebUtility.HtmlEncode(document.DocumentCode);
        var kind = WebUtility.HtmlEncode(DocumentKind(document));
        var issued = document.IssuedAtUtc.ToString("dd-MM-yyyy");
        var paragraphs = document.Content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => $"<p>{WebUtility.HtmlEncode(value)}</p>");

        var html = """
            <!doctype html>
            <html lang="es">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>__CODE__ — __TITLE__</title>
              <style>
                :root{color-scheme:light;--azul:#243b67;--dorado:#8a6b1f;--texto:#172033;--papel:#fff}
                *{box-sizing:border-box}body{margin:0;background:#eef1f5;color:var(--texto);font-family:Cambria,Georgia,serif;font-size:12pt;line-height:1.15}
                main{max-width:21cm;min-height:29.7cm;margin:1rem auto;padding:2.5cm;background:var(--papel);box-shadow:0 2px 12px #0002}
                header{border-bottom:3px solid var(--dorado);padding-bottom:1rem;margin-bottom:2rem;text-align:center}
                .institution{color:var(--azul);font-size:15pt;font-weight:700;letter-spacing:.03em}.kind{margin:.5rem 0 0;color:var(--texto);font-weight:700;text-transform:uppercase}
                h1{color:var(--azul);font-size:18pt;line-height:1.25;margin:0 0 1.5rem}dl{display:grid;grid-template-columns:max-content 1fr;gap:.35rem 1rem;margin:0 0 2rem}dt{font-weight:700}dd{margin:0}
                p{margin:0 0:6pt;text-align:left}.format-note{border-top:1px solid #697386;margin-top:2rem;padding-top:.75rem;font-size:10pt}
                @media print{body{background:#fff}main{margin:0;box-shadow:none;max-width:none}button{display:none}}@media(max-width:700px){main{margin:0;padding:1.25rem;min-height:100vh}dl{grid-template-columns:1fr}}
              </style>
            </head>
            <body><main>
              <header><div class="institution">GRAN LOGIA MIXTA DE CHILE</div><p class="kind">__KIND__</p></header>
              <h1>__TITLE__</h1>
              <dl><dt>Código</dt><dd>__CODE__</dd><dt>Fecha de emisión</dt><dd>__ISSUED__</dd><dt>Estado</dt><dd>Emitido</dd></dl>
              <section aria-label="Contenido del documento">__CONTENT__</section>
              <p class="format-note">Versión digital accesible para lectura en pantalla e impresión. Documento generado desde el registro oficial de Gran Secretaría.</p>
            </main></body>
            </html>
            """
            .Replace("__CODE__", code, StringComparison.Ordinal)
            .Replace("__TITLE__", title, StringComparison.Ordinal)
            .Replace("__KIND__", kind, StringComparison.Ordinal)
            .Replace("__ISSUED__", issued, StringComparison.Ordinal)
            .Replace("__CONTENT__", string.Join(Environment.NewLine, paragraphs), StringComparison.Ordinal);

        return Encoding.UTF8.GetBytes(html);
    }

    public static string FileName(SecretariatDocument document)
    {
        var safeCode = string.Concat(document.DocumentCode.Select(value => char.IsLetterOrDigit(value) || value == '-' ? value : '-'));
        return $"{safeCode}-version-accesible.html";
    }

    private static string DocumentKind(SecretariatDocument document)
        => document.DocumentType == GrandSecretariatCodes.DocumentType.Decree
            ? "Decreto"
            : document.PlanchaKind == GrandSecretariatCodes.PlanchaKind.CeremonyAuthorization
                ? "Plancha de Autorización de Ceremonia"
                : "Plancha formal";
}
