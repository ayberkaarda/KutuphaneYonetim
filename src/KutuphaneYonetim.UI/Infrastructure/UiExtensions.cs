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

    /// <summary>
    /// Like <see cref="RunAsync"/>, but through <paramref name="operation"/>: while a run is pending a
    /// new call is ignored and <paramref name="controls"/> stay disabled, so a write cannot be started twice.
    /// A run that starts clears the previous inline message of the screen.
    /// </summary>
    public static Task<bool> RunExclusiveAsync(
        this Form form,
        ExclusiveOperation operation,
        Func<Task> action,
        params Control[] controls)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(action);

        return operation.RunAsync(
            () =>
            {
                (form as IStatusSurface)?.ClearStatus();
                return form.RunAsync(action);
            },
            controls);
    }

    /// <summary>
    /// Column widths for a list: the <paramref name="fill"/> columns share the free width (at least
    /// <paramref name="fillMinimum"/> logical pixels), every other visible column fits its content and
    /// header. A list wider than the grid scrolls sideways instead of cutting values off.
    /// </summary>
    public static void FitColumns(this DataGridView grid, int fillMinimum, params string[] fill)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(fill);

        var scale = grid.DeviceDpi / 96F * Theming.Theme.RenderScale;
        foreach (DataGridViewColumn column in grid.Columns)
        {
            // The lists are plain collections that cannot be sorted by a header click; without a sort
            // mode the header does not keep room for a sort arrow either.
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            AlignNumbers(column);
            if (fill.Contains(column.DataPropertyName, StringComparer.Ordinal))
            {
                // The minimum goes first: changing it on a filling column of a grid without a window fails.
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                column.MinimumWidth = (int)Math.Round(fillMinimum * scale);
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
            else
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                column.MinimumWidth = HeaderWidth(grid, column.HeaderText, wholeText: true);
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }
        }
    }

    /// <summary>
    /// Width a column needs for its header: the whole text on one line, or (for headers that may wrap)
    /// the narrowest split into at most two lines, plus the cell padding. Used as a minimum so a header
    /// is never cut off and never takes more than two lines.
    /// </summary>
    public static int HeaderWidth(DataGridView grid, string header, bool wholeText)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(header);

        var font = grid.ColumnHeadersDefaultCellStyle.Font ?? grid.Font;
        int Measure(string text) => TextRenderer.MeasureText(text, font).Width;

        var words = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var widest = Measure(header);
        if (!wholeText && words.Length > 1)
        {
            for (var split = 1; split < words.Length; split++)
            {
                var first = string.Join(' ', words[..split]);
                var second = string.Join(' ', words[split..]);
                widest = Math.Min(widest, Math.Max(Measure(first), Measure(second)));
            }
        }

        return widest + grid.ColumnHeadersDefaultCellStyle.Padding.Horizontal + Theming.Theme.Px(grid, Theming.Spacing.M);
    }

    /// <summary>Counts, numbers and amounts are right aligned so their digits line up.</summary>
    private static void AlignNumbers(DataGridViewColumn column)
    {
        var type = Nullable.GetUnderlyingType(column.ValueType ?? typeof(object)) ?? column.ValueType;
        if (type == typeof(int) || type == typeof(decimal) || type == typeof(long) || type == typeof(double))
        {
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
    }

    /// <summary>
    /// Column widths for a wide list: like <see cref="FitColumns"/>, but the other columns fit their
    /// values only (at least <paramref name="valueMinimum"/> logical pixels) and long headers wrap onto
    /// a second line, so many short columns fit side by side.
    /// </summary>
    public static void FitWideColumns(this DataGridView grid, int fillMinimum, int valueMinimum, params string[] fill)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(fill);

        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.FitColumns(fillMinimum, fill);
        var scale = grid.DeviceDpi / 96F * Theming.Theme.RenderScale;
        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (column.AutoSizeMode != DataGridViewAutoSizeColumnMode.Fill)
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                column.MinimumWidth = Math.Max((int)Math.Round(valueMinimum * scale), HeaderWidth(grid, column.HeaderText, wholeText: false));
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCellsExceptHeader;
            }
        }
    }

    /// <summary>Shows the given columns as date and time without seconds.</summary>
    public static void FormatDateTimeColumns(this DataGridView grid, params string[] propertyNames) =>
        grid.FormatColumns("g", propertyNames);

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
