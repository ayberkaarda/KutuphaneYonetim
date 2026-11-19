using System.Text;

namespace KutuphaneYonetim.Business.Reports;

/// <summary>Writes report tables as CSV files that open directly in a Turkish Excel.</summary>
public interface ICsvExporter
{
    /// <summary>
    /// Writes <paramref name="table"/> to <paramref name="path"/>. The file is first written to a temporary
    /// file in the same folder and then moved over the target, so an existing file is either replaced
    /// completely or left untouched.
    /// </summary>
    Task ExportAsync(ReportTable table, string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// CSV in UTF-8 with a byte order mark, <c>;</c> as separator and CRLF line ends. Cells containing the
/// separator, quotes or line breaks are quoted. Cells that a spreadsheet would read as a formula (starting
/// with <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, tab or carriage return) get a leading apostrophe.
/// </summary>
internal sealed class CsvExporter : ICsvExporter
{
    public const char Separator = ';';

    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public async Task ExportAsync(ReportTable table, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var target = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(target)
            ?? throw new ArgumentException("The path has no directory.", nameof(path));
        var temporary = Path.Combine(directory, "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using (stream.ConfigureAwait(false))
            {
                var writer = new StreamWriter(stream, Utf8WithBom);
                await using (writer.ConfigureAwait(false))
                {
                    await writer.WriteAsync(Format(table).AsMemory(), cancellationToken).ConfigureAwait(false);
                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            File.Move(temporary, target, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    /// <summary>The CSV text of the table (without the byte order mark).</summary>
    public static string Format(ReportTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var builder = new StringBuilder();
        AppendLine(builder, table.Headers);
        foreach (var row in table.Rows)
        {
            AppendLine(builder, row);
        }

        return builder.ToString();
    }

    /// <summary>Neutralises spreadsheet formulas and quotes the cell when needed.</summary>
    public static string Escape(string? cell)
    {
        var value = cell ?? string.Empty;
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        if (value.IndexOfAny([Separator, '"', '\r', '\n']) >= 0)
        {
            value = "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return value;
    }

    private static void AppendLine(StringBuilder builder, IEnumerable<string> cells)
    {
        builder.AppendJoin(Separator, cells.Select(Escape));
        builder.Append("\r\n");
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best effort: a leftover temporary file is harmless.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }
    }
}
