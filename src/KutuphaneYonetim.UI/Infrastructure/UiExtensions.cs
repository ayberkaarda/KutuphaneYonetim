namespace KutuphaneYonetim.UI.Infrastructure;

internal static class UiExtensions
{
    /// <summary>
    /// Runs an asynchronous UI action with a wait cursor. Business rule violations are shown as
    /// warnings; anything else is reported through <see cref="ErrorReporter"/>.
    /// </summary>
    public static async Task RunAsync(this Form form, Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(action);

        form.UseWaitCursor = true;
        try
        {
            await action();
        }
#pragma warning disable CA1031 // UI boundary: every failure is reported to the user.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            ErrorReporter.Show(ex, form);
        }
        finally
        {
            form.UseWaitCursor = false;
        }
    }

    /// <summary>Applies Turkish header texts and hides the columns that are not listed.</summary>
    public static void ConfigureColumns(this DataGridView grid, IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(headers);

        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (headers.TryGetValue(column.DataPropertyName, out var header))
            {
                column.HeaderText = header;
                column.Visible = true;
            }
            else
            {
                column.Visible = false;
            }
        }
    }

    /// <summary>Shows the given decimal columns with two decimals (for amounts in TL).</summary>
    public static void FormatMoneyColumns(this DataGridView grid, params string[] propertyNames) =>
        grid.FormatColumns("N2", propertyNames);

    /// <summary>Shows the given columns as dates without time.</summary>
    public static void FormatDateColumns(this DataGridView grid, params string[] propertyNames) =>
        grid.FormatColumns("d", propertyNames);

    private static void FormatColumns(this DataGridView grid, string format, string[] propertyNames)
    {
        ArgumentNullException.ThrowIfNull(grid);

        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (propertyNames.Contains(column.DataPropertyName, StringComparer.Ordinal))
            {
                column.DefaultCellStyle.Format = format;
            }
        }
    }
}
