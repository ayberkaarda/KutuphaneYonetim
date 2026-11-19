using System.ComponentModel;

namespace KutuphaneYonetim.UI.Theming;

/// <summary>
/// Read-only list grid with the theme's table style: quiet header, horizontal row dividers, row
/// selection, and a message in place of the rows while the list loads or when it is empty.
/// </summary>
internal sealed class ThemedGrid : DataGridView
{
    private bool _loading;
    private object? _selectedItem;

    public ThemedGrid()
    {
        ReadOnly = true;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        AllowUserToResizeRows = false;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        MultiSelect = false;
        RowHeadersVisible = false;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        StandardTab = true; // Tab leaves the grid instead of walking its cells.
        DoubleBuffered = true;
        ThemeStyler.StyleGrid(this);
    }

    /// <summary>Shown in the middle of the grid while it has no rows.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string EmptyText { get; set; } = string.Empty;

    /// <summary>Shown instead of <see cref="EmptyText"/> while <see cref="IsLoading"/> is set.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string LoadingText { get; set; } = "Yükleniyor…";

    /// <summary>A list request is on its way; an empty grid says so instead of "nothing found".</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsLoading
    {
        get => _loading;
        set
        {
            if (_loading != value)
            {
                _loading = value;
                Invalidate();
            }
        }
    }

    /// <summary>
    /// The item the screen acts on. Its row is the only highlighted one, also after the rows are bound
    /// again; while it is <see langword="null"/> no row looks selected, so the highlight never suggests
    /// a target the screen does not have.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;
            ApplySelection();
        }
    }

    /// <summary>A new window selects its first row by itself; the screen's own selection is put back.</summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplySelection();
    }

    protected override void OnDataBindingComplete(DataGridViewBindingCompleteEventArgs e)
    {
        base.OnDataBindingComplete(e);
        ApplySelection();
        Invalidate();
    }

    private void ApplySelection()
    {
        DataGridViewRow? match = null;
        if (_selectedItem is not null)
        {
            foreach (DataGridViewRow row in Rows)
            {
                if (ReferenceEquals(row.DataBoundItem, _selectedItem))
                {
                    match = row;
                    break;
                }
            }
        }

        ClearSelection();
        if (match is not null)
        {
            match.Selected = true;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPaint(e);
        var message = _loading ? LoadingText : EmptyText;
        if (Rows.Count > 0 || string.IsNullOrEmpty(message))
        {
            return;
        }

        var top = ColumnHeadersVisible && ColumnCount > 0 ? ColumnHeadersHeight : 0;
        var area = new Rectangle(Theme.Px(this, Spacing.L), top, Math.Max(0, Width - Theme.Px(this, 2 * Spacing.L)), Math.Max(0, Height - top));
        TextRenderer.DrawText(
            e.Graphics,
            message,
            Theme.Fonts.Body,
            area,
            Theme.Current.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }
}
