namespace KutuphaneYonetim.UI.Theming;

/// <summary>
/// Frame around a borderless <see cref="TextBox"/>: the shared control height, a rounded border and
/// an accent focus ring, so text inputs line up with drop-down lists and buttons. The text box itself
/// stays a normal, focusable control (tab order, access keys and screen readers are unchanged).
/// Under high contrast the native border is kept.
/// </summary>
internal sealed class TextField : Control
{
    private bool _hover;

    public TextField(TextBox input)
    {
        ArgumentNullException.ThrowIfNull(input);

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Input = input;
        Name = input.Name + "Field";
        TabStop = false;
        Cursor = Cursors.IBeam;
        Margin = new Padding(0, Spacing.XS, 0, Spacing.XS);
        Font = Theme.Fonts.Body;
        Size = new Size(Math.Max(input.Width, 120), Sizes.ControlHeight);
        MinimumSize = new Size(0, Sizes.ControlHeight);

        input.Font = Theme.Fonts.Body;
        input.Margin = Padding.Empty;
        input.BorderStyle = Theme.Current.IsHighContrast ? BorderStyle.FixedSingle : BorderStyle.None;
        input.GotFocus += (_, _) => Invalidate();
        input.LostFocus += (_, _) => Invalidate();
        input.MouseEnter += (_, _) => SetHover(true);
        input.MouseLeave += (_, _) => SetHover(false);
        input.EnabledChanged += (_, _) =>
        {
            SyncInputBackground();
            Invalidate();
        };
        Controls.Add(input);
        SyncInputBackground();
    }

    public TextBox Input { get; }

    protected override void OnClick(EventArgs e)
    {
        Input.Focus();
        base.OnClick(e);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        SetHover(true);
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        SetHover(false);
        base.OnMouseLeave(e);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var inset = Theme.Px(this, Spacing.S + 2);
        var height = Input.PreferredHeight;
        Input.SetBounds(inset, Math.Max(0, (Height - height) / 2), Math.Max(0, Width - (2 * inset)), height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(Painting.ParentBackground(this));
        if (p.IsHighContrast)
        {
            return;
        }

        var enabled = Input.Enabled;
        var focused = Input.Focused;
        var border = !enabled ? p.Border : focused ? p.Accent : _hover ? p.TextSecondary : p.BorderStrong;
        var width = focused ? Theme.PxF(this, Sizes.FocusRing) : Math.Max(1F, Theme.PxF(this, 1));
        var fill = enabled ? p.Surface : p.SurfaceAlt;
        Painting.FillRounded(g, new RectangleF(0, 0, Width, Height), Theme.PxF(this, Radius.Control), fill, border, width);
    }

    /// <summary>The borderless text box takes the field's fill, so the frame reads as one input.</summary>
    internal void SyncInputBackground()
    {
        var p = Theme.Current;
        if (!p.IsHighContrast)
        {
            Input.BackColor = Input.Enabled ? p.Surface : p.SurfaceAlt;
            Input.ForeColor = p.Text;
        }
    }

    private void SetHover(bool hover)
    {
        if (_hover != hover)
        {
            _hover = hover;
            Invalidate();
        }
    }
}
