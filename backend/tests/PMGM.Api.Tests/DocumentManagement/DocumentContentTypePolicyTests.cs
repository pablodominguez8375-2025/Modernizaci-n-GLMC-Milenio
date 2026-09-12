using System.IO.Compression;
using System.Text;
using PMGM.Api.Modules.DocumentManagement;
using Xunit;

namespace PMGM.Api.Tests.DocumentManagement;

public sealed class DocumentContentTypePolicyTests
{
    [Theory]
    [InlineData("manual.pdf", "application/pdf")]
    [InlineData("acta.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("datos.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("presentacion.pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    [InlineData("nota.txt", "text/plain; charset=utf-8")]
    public void Metadata_accepts_supported_matching_extension(string fileName, string contentType)
    {
        var valid = DocumentContentTypePolicy.TryValidateMetadata(
            fileName,
            contentType,
            out var normalized,
            out var error);

        Assert.True(valid);
        Assert.False(string.IsNullOrWhiteSpace(normalized));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("manual.exe", "application/pdf")]
    [InlineData("manual.pdf", "application/octet-stream")]
    [InlineData("documento.docx", "application/pdf")]
    public void Metadata_rejects_extension_or_unsupported_mime(string fileName, string contentType)
    {
        var valid = DocumentContentTypePolicy.TryValidateMetadata(
            fileName,
            contentType,
            out _,
            out var error);

        Assert.False(valid);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public async Task Pdf_signature_must_match_declared_content()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var validPdf = new MemoryStream("%PDF-1.7\n% PMGM"u8.ToArray());
        await using var disguisedExecutable = new MemoryStream("MZ-not-a-pdf"u8.ToArray());

        Assert.True(await DocumentContentTypePolicy.MatchesContentAsync(
            validPdf,
            "application/pdf",
            cancellationToken));
        Assert.False(await DocumentContentTypePolicy.MatchesContentAsync(
            disguisedExecutable,
            "application/pdf",
            cancellationToken));
    }

    [Fact]
    public async Task Office_open_xml_requires_expected_internal_root()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var docx = await CreateOfficePackageAsync("word/document.xml", cancellationToken);
        await using var wrongPackage = await CreateOfficePackageAsync("xl/workbook.xml", cancellationToken);

        const string docxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        Assert.True(await DocumentContentTypePolicy.MatchesContentAsync(docx, docxMime, cancellationToken));
        Assert.False(await DocumentContentTypePolicy.MatchesContentAsync(wrongPackage, docxMime, cancellationToken));
    }

    private static async Task<MemoryStream> CreateOfficePackageAsync(
        string partName,
        CancellationToken cancellationToken)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var contentTypes = archive.CreateEntry("[Content_Types].xml");
            await using (var contentTypesStream = contentTypes.Open())
            {
                await contentTypesStream.WriteAsync(
                    Encoding.UTF8.GetBytes("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\" />"),
                    cancellationToken);
            }

            var part = archive.CreateEntry(partName);
            await using var partStream = part.Open();
            await partStream.WriteAsync(Encoding.UTF8.GetBytes("test"), cancellationToken);
        }

        stream.Position = 0;
        return stream;
    }
}
