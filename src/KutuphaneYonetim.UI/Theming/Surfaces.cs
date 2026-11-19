using System.ComponentModel;

namespace KutuphaneYonetim.UI.Theming;

/// <summary>A rounded surface with a hairline border that groups related content.</summary>
internal sealed class CardPanel : Panel
{
    public CardPanel()
    {
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Theme.Current.Surface;
        Padding = new Padding(Spacing.L);
        Margin = new Padding(0, 0, 0, Spacing.L);
    }

    /// <summary>
    /// An auto-sized card measures its stacked children by their preferred height at the card's
    /// width, so it grows when their content wraps (a docked panel would use their current height).
    /// </summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        if (!AutoSize)
        {
            return base.GetPreferredSize(proposedSize);
        }

        var width = proposedSize.Width is > 0 and < int.MaxValue ? proposedSize.Width : Width;
        var inner = Math.Max(0, width - Padding.Horizontal);
        var height = Padding.Vertical;
        foreach (Control child in Controls)
        {
            if (child.Visible || !child.IsHandleCreated)
            {
                height += child.GetPreferredSize(new Size(inner, 0)).Height + child.Margin.Vertical;
            }
        }

        return new Size(width, height);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var p = Theme.Current;
        e.Graphics.Clear(Painting.ParentBackground(this));
        Painting.FillRounded(
            e.Graphics,
            new RectangleF(0, 0, Width, Height),
            Theme.PxF(this, Radius.Card),
            BackColor,
            p.Border,
            Math.Max(1F, Theme.PxF(this, 1)));
    }
}

/// <summary>A square badge with a glyph (or the application logo) on a tinted background.</summary>
internal sealed class GlyphBadge : Control
{
    public GlyphBadge(string glyph, int size, float glyphPoints = 14F)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Glyph = glyph;
        GlyphPoints = glyphPoints;
        Size = new Size(size, size);
        Margin = new Padding(0, 0, Spacing.M, 0);
        TabStop = false;
        AccessibleRole = AccessibleRole.Graphic;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Glyph { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float GlyphPoints { get; set; }

    /// <summary>Glyph color; the accent text color when not set.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color? GlyphColor { get; set; }

    /// <summary>Badge fill; the subtle accent when not set.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color? Fill { get; set; }

    /// <summary>When set, the image is drawn instead of the glyph.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image? Image { get; set; }

    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(Painting.ParentBackground(this));
        if (Image is { } image)
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.DrawImage(image, ClientRectangle);
            return;
        }

        var fill = p.IsHighContrast ? p.Surface : Fill ?? p.AccentSubtle;
        Painting.FillRounded(g, new RectangleF(0, 0, Width, Height), Theme.PxF(this, Radius.Card), fill, p.IsHighContrast ? p.Border : null);
        Painting.DrawGlyph(g, Glyph, Theme.Fonts.Glyph(GlyphPoints), ClientRectangle, p.IsHighContrast ? p.Text : GlyphColor ?? p.AccentText);
    }
}

/// <summary>Severity of an inline message.</summary>
internal enum StatusKind
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>
/// An inline message strip (the "info bar" pattern). Results, validation messages and errors appear
/// inside the window instead of in a modal box, so nothing steals the focus; screen readers are told
/// about the change through the accessibility name.
/// </summary>
internal sealed class InfoBar : Control
{
    private const TextFormatFlags TextFlags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;

    private StatusKind _kind;
    private bool _shown;

    public InfoBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Name = "statusBar";
        Dock = DockStyle.Top;
        Visible = false;
        TabStop = false;
        Font = Theme.Fonts.Body;
        AccessibleRole = AccessibleRole.Alert;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public StatusKind Kind => _kind;

    /// <summary>Whether a message is currently shown.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsShown => _shown;

    /// <summary>Space kept below the strip, inside its own height, so content does not touch it.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Gap { get; set; } = Spacing.M;

    public void Show(StatusKind kind, string message)
    {
        _kind = kind;
        Text = message;
        AccessibleName = message;
        _shown = true;
        Visible = true;
        UpdateHeight();
        Invalidate();
        if (IsHandleCreated)
        {
            AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
        }
    }

    public void Clear()
    {
        _shown = false;
        Visible = false;
        Text = string.Empty;
        AccessibleName = null;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateHeight();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(Painting.ParentBackground(this));
        var (fill, accent, glyph) = Style(p);
        var box = new RectangleF(0, 0, Width, Height - Theme.Px(this, Gap));
        Painting.FillRounded(g, box, Theme.PxF(this, Radius.Card), fill, p.IsHighContrast ? p.Border : Blend(accent, fill), Math.Max(1F, Theme.PxF(this, 1)));

        // A narrow severity edge on the left, so the kind reads at a glance without relying on the glyph.
        if (!p.IsHighContrast)
        {
            var edge = Theme.PxF(this, 3);
            Painting.FillRounded(g, new RectangleF(0, 0, edge * 2, box.Height), edge, accent);
            using var cover = new SolidBrush(fill);
            g.FillRectangle(cover, edge, Theme.PxF(this, 1), edge, Math.Max(0, box.Height - Theme.PxF(this, 2)));
        }

        var pad = Theme.Px(this, Spacing.M);
        var glyphSize = Theme.Px(this, 20);
        Painting.DrawGlyph(g, glyph, Theme.Fonts.Glyph(11F), new Rectangle(pad + Theme.Px(this, 2), pad, glyphSize, Math.Max(glyphSize, Font.Height)), accent);
        var textLeft = TextLeft();
        TextRenderer.DrawText(g, Text, Font, new Rectangle(textLeft, pad, Math.Max(0, Width - textLeft - pad), (int)box.Height - (2 * pad)), p.Text, TextFlags);
    }

    private int TextLeft() => Theme.Px(this, Spacing.M + 2) + Theme.Px(this, 20) + Theme.Px(this, Spacing.S);

    private void UpdateHeight()
    {
        if (!_shown || Width <= 0)
        {
            return;
        }

        var pad = Theme.Px(this, Spacing.M);
        var textLeft = TextLeft();
        var measured = TextRenderer.MeasureText(Text, Font, new Size(Math.Max(1, Width - textLeft - pad), int.MaxValue), TextFlags);
        var height = Math.Max(measured.Height, Font.Height) + (2 * pad) + Theme.Px(this, Gap);
        if (Height != height)
        {
            Height = height;
        }
    }

    private (Color Fill, Color Accent, string Glyph) Style(ThemePalette p) => _kind switch
    {
        StatusKind.Success => (p.SuccessSubtle, p.Success, Glyphs.Success),
        StatusKind.Warning => (p.WarningSubtle, p.Warning, Glyphs.Warning),
        StatusKind.Error => (p.DangerSubtle, p.Danger, Glyphs.Error),
        _ => (p.InfoSubtle, p.Info, Glyphs.Info),
    };

    private static Color Blend(Color color, Color background) => Theme.Flatten(Color.FromArgb(70, color), background);
}
