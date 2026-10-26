using System.Text;
using PMGM.Api.Modules.RegimenInterior;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyOfficialRosterPdfTests
{
    private static readonly string[] Cols = ["N°", "Apellidos y nombres", "RUT", "Taller", "Firma"];
    private static readonly double[] Widths = [34, 250, 90, 200, 187];

    [Fact]
    public void Genera_un_PDF_valido_paginado_con_acentos_y_la_Ñ()
    {
        var rows = Enumerable.Range(1, 130).Select(i => (IReadOnlyList<string>)
            [i.ToString(), $"Muñoz Ibáñez, María José {i}", "12.345.678-5", "Taller Demostrativo Nº " + i, ""]).ToArray();
        var pdf = AssemblyOfficialRosterPdf.Render("Padrón de sufragantes habilitados — padrón oficial",
            ["Gran Logia Mixta de Chile · Asamblea"], Cols, Widths, rows, "Padron oficial - Resumen SHA-256 abcdef");
        var text = Encoding.Latin1.GetString(pdf);
        Assert.StartsWith("%PDF-1.4", text);
        Assert.EndsWith("%%EOF\n", text);
        Assert.Matches("/Count ([2-9]|\\d\\d)", text); // 130 filas requieren varias páginas
        Assert.DoesNotContain("/JS", text);
        Assert.DoesNotContain("/URI", text);
    }

    [Fact]
    public void Un_listado_vacio_igual_produce_un_PDF_con_aviso()
    {
        var pdf = AssemblyOfficialRosterPdf.Render("Título", ["Info"], Cols, Widths, [], "Pie");
        Assert.StartsWith("%PDF-1.4", Encoding.Latin1.GetString(pdf));
    }

    [Fact]
    public void Rechaza_columnas_inconsistentes()
    {
        Assert.Throws<ArgumentException>(() => AssemblyOfficialRosterPdf.Render("T", [], Cols, [10], [], "P"));
    }
}
