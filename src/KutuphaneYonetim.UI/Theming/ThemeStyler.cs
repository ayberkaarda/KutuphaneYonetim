namespace KutuphaneYonetim.UI.Theming;

/// <summary>
/// Applies the current palette to standard Windows Forms controls, so forms do not set colors one by
/// one. Custom painted controls read the palette themselves. Under high contrast nothing is recolored
/// and the system colors stay in effect.
/// </summary>
internal static class ThemeStyler
{
    /// <summary>Styles <paramref name="root"/> and all of its children.</summary>
    public static void Apply(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var p = Theme.Current;
        if (p.IsHighContrast)
        {
            return;
        }

        if (root is Form form)
        {
            form.BackColor = p.Page;
            form.ForeColor = p.Text;
        }

        Walk(root, p);
    }

    /// <summary>Table style for a <see cref="DataGridView"/>; padding follows the control's DPI.</summary>
    public static void StyleGrid(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var p = Theme.Current;
        grid.Font = Theme.Fonts.Body;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        var cellPadding = new Padding(Theme.Px(grid, Spacing.S), Theme.Px(grid, 6), Theme.Px(grid, Spacing.S), Theme.Px(grid, 6));
        grid.ColumnHeadersDefaultCellStyle.Padding = cellPadding;
        grid.ColumnHeadersDefaultCellStyle.Font = Theme.Fonts.BodyStrong;
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        grid.DefaultCellStyle.Padding = cellPadding;
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

        if (p.IsHighContrast)
        {
            return;
        }

        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = p.Surface;
        grid.GridColor = p.Border;
        grid.ColumnHeadersDefaultCellStyle.BackColor = p.SurfaceAlt;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = p.TextSecondary;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = p.SurfaceAlt;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = p.TextSecondary;
        grid.DefaultCellStyle.BackColor = p.Surface;
        grid.DefaultCellStyle.ForeColor = p.Text;
        grid.DefaultCellStyle.SelectionBackColor = p.AccentSubtle;
        grid.DefaultCellStyle.SelectionForeColor = p.Text;
        grid.AlternatingRowsDefaultCellStyle.BackColor = p.SurfaceAlt;
    }

    /// <summary>
    /// Writes a grid cell in a status <paramref name="color"/> and a stronger weight. The selected cell keeps the
    /// color too, except under high contrast: there the selection pair belongs to the system (HighlightText on
    /// Highlight) and a status color on that background could be unreadable.
    /// </summary>
    internal static void EmphasizeCell(DataGridViewCellStyle style, Color color)
    {
        ArgumentNullException.ThrowIfNull(style);

        style.ForeColor = color;
        if (!Theme.Current.IsHighContrast)
        {
            style.SelectionForeColor = color;
        }

        style.Font = Theme.Fonts.BodyStrong;
    }

    private static void Walk(Control parent, ThemePalette p)
    {
        foreach (Control control in parent.Controls)
        {
            switch (control)
            {
                case TextBox box when box.Parent is TextField field:
                    field.SyncInputBackground();
                    break;
                case TextBox box:
                    if (box.BorderStyle == BorderStyle.Fixed3D)
                    {
                        box.BorderStyle = BorderStyle.FixedSingle;
                    }

                    box.BackColor = p.Surface;
                    box.ForeColor = p.Text;
                    break;
                case NumericUpDown number:
                    number.BorderStyle = BorderStyle.FixedSingle;
                    number.BackColor = p.Surface;
                    number.ForeColor = p.Text;
                    break;
                case DateTimePicker picker:
                    picker.CalendarMonthBackground = p.Surface;
                    picker.CalendarForeColor = p.Text;
                    picker.CalendarTitleBackColor = p.Accent;
                    picker.CalendarTitleForeColor = p.OnAccent;
                    break;
                case CheckBox check:
                    check.ForeColor = p.Text;
                    break;
            }

            if (control.HasChildren && control is not DataGridView and not NumericUpDown and not DateTimePicker)
            {
                Walk(control, p);
            }
        }
    }
}
