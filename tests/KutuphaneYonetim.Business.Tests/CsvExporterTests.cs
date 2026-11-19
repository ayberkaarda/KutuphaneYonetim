using System.Text;
using KutuphaneYonetim.Business.Reports;

namespace KutuphaneYonetim.Business.Tests;

public sealed class CsvExporterTests : IDisposable
{
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "kutuphane-csv-test-" + Guid.NewGuid().ToString("N"));
    private readonly CsvExporter _exporter = new();

    public CsvExporterTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ReportTable Table(params string[][] rows) => new(["Ad", "Not"], rows);

    [Theory]
    [InlineData("=SUM(A1:A2)", "'=SUM(A1:A2)")]
    [InlineData("+90 555", "'+90 555")]
    [InlineData("-5", "'-5")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("\tx", "'\tx")]
    [InlineData("a=b", "a=b")] // Only a leading character is dangerous.
    [InlineData("Suç ve Ceza", "Suç ve Ceza")]
    [InlineData("", "")]
    public void Escape_NeutralisesFormulas(string cell, string expected)
    {
        Assert.Equal(expected, CsvExporter.Escape(cell));
    }

    [Theory]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData("=1;2", "\"'=1;2\"")] // Apostrophe first, then quoting.
    [InlineData("\r=x", "\"'\r=x\"")]
    public void Escape_QuotesSeparatorsQuotesAndLineBreaks(string cell, string expected)
    {
        Assert.Equal(expected, CsvExporter.Escape(cell));
    }

    [Fact]
    public void Format_UsesSemicolonAndCrLf_HeaderFirst()
    {
        var text = CsvExporter.Format(Table(["Kürk Mantolu Madonna", "12,50"], ["=HYPERLINK(\"x\")", "-"]));

        Assert.Equal(
            "Ad;Not\r\nKürk Mantolu Madonna;12,50\r\n\"'=HYPERLINK(\"\"x\"\")\";'-\r\n",
            text);
    }

    [Fact]
    public async Task ExportAsync_WritesUtf8WithBom()
    {
        var path = Path.Combine(_directory, "rapor.csv");

        await _exporter.ExportAsync(Table(["İnce Memed", "ş ğ ü ö ç ı"]), path, Ct);

        var bytes = await File.ReadAllBytesAsync(path, Ct);
        Assert.Equal(Bom, bytes[..3]);
        Assert.Equal("Ad;Not\r\nİnce Memed;ş ğ ü ö ç ı\r\n", Encoding.UTF8.GetString(bytes[3..]));
    }

    [Fact]
    public async Task ExportAsync_HeaderOnlyTable_StillWritesHeader()
    {
        var path = Path.Combine(_directory, "bos.csv");

        await _exporter.ExportAsync(Table(), path, Ct);

        Assert.Equal("Ad;Not\r\n", await File.ReadAllTextAsync(path, Ct));
    }

    [Fact]
    public async Task ExportAsync_ReplacesExistingFile_AndLeavesNoTemporaryFile()
    {
        var path = Path.Combine(_directory, "rapor.csv");
        await File.WriteAllTextAsync(path, "old content that is much longer than the new one", Ct);

        await _exporter.ExportAsync(Table(["x", "y"]), path, Ct);

        Assert.Equal("Ad;Not\r\nx;y\r\n", await File.ReadAllTextAsync(path, Ct));
        Assert.Equal([path], Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task ExportAsync_WhenTargetCannotBeReplaced_KeepsOriginalAndCleansUp()
    {
        var path = Path.Combine(_directory, "kilitli.csv");
        await File.WriteAllTextAsync(path, "original", Ct);

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(() => _exporter.ExportAsync(Table(["x", "y"]), path, Ct));
            Assert.True(error is IOException or UnauthorizedAccessException, error.GetType().FullName);
        }

        Assert.Equal("original", await File.ReadAllTextAsync(path, Ct));
        Assert.Equal([path], Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task ExportAsync_MissingFolder_Throws_WithoutCreatingFiles()
    {
        var path = Path.Combine(_directory, "yok", "rapor.csv");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => _exporter.ExportAsync(Table(), path, Ct));

        Assert.Empty(Directory.GetFileSystemEntries(_directory));
    }
}
