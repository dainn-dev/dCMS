using System.Text;
using ClosedXML.Excel;
using dCMS.Catalog.Worker.Imports;
using FluentAssertions;

namespace dCMS.Tests.Unit.Catalog.Imports;

public class XlsxStreamReaderTests
{
    [Fact]
    public async Task Csv_with_headers_emits_one_row_per_record_keyed_by_sku()
    {
        var csv = "sku,name,price\nABC-1,Widget,9.99\nABC-2,Gadget,19.99\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var rows = new List<ImportRow>();
        await foreach (var r in XlsxStreamReader.EnumerateRowsAsync(stream, CancellationToken.None))
            rows.Add(r);

        rows.Should().HaveCount(2);
        rows[0].Index.Should().Be(1);
        rows[0].Key.Should().Be("ABC-1");
        rows[0].Cells["sku"].Should().Be("ABC-1");
        rows[0].Cells["name"].Should().Be("Widget");
        rows[0].Cells["price"].Should().Be("9.99");
        rows[1].Key.Should().Be("ABC-2");
    }

    [Fact]
    public async Task Csv_skips_empty_lines_and_trims_headers()
    {
        var csv = "  sku , name\n\nA-1,One\n\nA-2,Two\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var rows = new List<ImportRow>();
        await foreach (var r in XlsxStreamReader.EnumerateRowsAsync(stream, CancellationToken.None))
            rows.Add(r);

        rows.Should().HaveCount(2);
        rows.Select(r => r.Key).Should().Equal("A-1", "A-2");
    }

    [Fact]
    public async Task Csv_handles_quoted_fields_with_commas()
    {
        var csv = "sku,name\n\"Q,1\",\"Hello, world\"\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var rows = new List<ImportRow>();
        await foreach (var r in XlsxStreamReader.EnumerateRowsAsync(stream, CancellationToken.None))
            rows.Add(r);

        rows.Should().HaveCount(1);
        rows[0].Cells["sku"].Should().Be("Q,1");
        rows[0].Cells["name"].Should().Be("Hello, world");
    }

    [Fact]
    public async Task Csv_falls_back_to_code_when_sku_absent()
    {
        var csv = "code,discount_value\nPROMO10,10\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var rows = new List<ImportRow>();
        await foreach (var r in XlsxStreamReader.EnumerateRowsAsync(stream, CancellationToken.None))
            rows.Add(r);

        rows.Should().HaveCount(1);
        rows[0].Key.Should().Be("PROMO10");
    }

    [Fact]
    public async Task Csv_falls_back_to_row_index_when_no_key_columns()
    {
        var csv = "foo,bar\nx,y\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var rows = new List<ImportRow>();
        await foreach (var r in XlsxStreamReader.EnumerateRowsAsync(stream, CancellationToken.None))
            rows.Add(r);

        rows.Should().HaveCount(1);
        rows[0].Key.Should().Be("row:1");
    }

    [Fact]
    public async Task Xlsx_emits_rows_keyed_by_sku()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("import");
        ws.Cell(1, 1).Value = "sku";
        ws.Cell(1, 2).Value = "name";
        ws.Cell(1, 3).Value = "price";
        ws.Cell(2, 1).Value = "X-1";
        ws.Cell(2, 2).Value = "Alpha";
        ws.Cell(2, 3).Value = 1.50;
        ws.Cell(3, 1).Value = "X-2";
        ws.Cell(3, 2).Value = "Beta";
        ws.Cell(3, 3).Value = 2.50;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var rows = new List<ImportRow>();
        await foreach (var r in XlsxStreamReader.EnumerateRowsAsync(ms, CancellationToken.None))
            rows.Add(r);

        rows.Should().HaveCount(2);
        rows[0].Key.Should().Be("X-1");
        rows[0].Cells["sku"].Should().Be("X-1");
        rows[0].Cells["name"].Should().Be("Alpha");
        rows[1].Key.Should().Be("X-2");
    }

    [Fact]
    public async Task Xlsx_skips_empty_rows()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("import");
        ws.Cell(1, 1).Value = "sku";
        ws.Cell(1, 2).Value = "name";
        ws.Cell(2, 1).Value = "Y-1";
        ws.Cell(2, 2).Value = "First";
        // Row 3 intentionally blank
        ws.Cell(4, 1).Value = "Y-2";
        ws.Cell(4, 2).Value = "Second";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var rows = new List<ImportRow>();
        await foreach (var r in XlsxStreamReader.EnumerateRowsAsync(ms, CancellationToken.None))
            rows.Add(r);

        rows.Should().HaveCount(2);
        rows.Select(r => r.Key).Should().Equal("Y-1", "Y-2");
    }
}
