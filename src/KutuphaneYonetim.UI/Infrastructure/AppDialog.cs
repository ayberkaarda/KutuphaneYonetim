using System.ComponentModel;
using KutuphaneYonetim.UI.Theming;

namespace KutuphaneYonetim.UI.Infrastructure;

/// <summary>
/// Common frame of every screen opened from the main menu: a header with glyph, title and a short
/// description; an inline message strip; the content area; and a footer with a hint on the left and
/// the actions on the right (Kapat last). Layout uses docking and table layouts in logical pixels and
/// is scaled per monitor by Windows Forms (<see cref="AutoScaleMode.Dpi"/>).
/// </summary>
public abstract class AppDialog : Form, IStatusSurface
{
    internal const string AppName = Dialogs.AppName;

    /// <summary>The usable desktop area of the screen a window opens on; tests replace it to stand in for small screens.</summary>
    internal static Func<Control, Rectangle> WorkingAreaOf { get; set; } = control => Screen.FromControl(control).WorkingArea;

    private readonly InfoBar _status = new();
    private readonly Panel _body;
    private Size _contentMinimum;
    private bool _sizeFitted;
    private readonly FlowLayoutPanel _actions;
    private readonly Label _footerNote;

    /// <param name="title">Screen name, also in the window title.</param>
    /// <param name="subtitle">One line under the title that says what the screen is for.</param>
    /// <param name="glyph">Icon font character of the header badge.</param>
    /// <param name="clientSize">Initial client size in logical pixels.</param>
    /// <param name="minimumClientSize">Smallest client size; four fifths of <paramref name="clientSize"/> when not given.</param>
    private protected AppDialog(string title, string subtitle, string glyph, Size clientSize, Size? minimumClientSize = null)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = Theme.Fonts.Body;
        Text = title + " - " + AppName;
        Icon = Theme.AppIcon;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = clientSize;
        MinimumSize = SizeFromClientSize(minimumClientSize ?? new Size(clientSize.Width * 4 / 5, clientSize.Height * 4 / 5));
        Prompts = UserPrompts.For(this);

        Content = new Panel { Name = "content", Dock = DockStyle.Fill, Margin = Padding.Empty };
        var body = new Panel
        {
            Name = "bodyContent",
            Dock = DockStyle.Fill,
            Padding = new Padding(Spacing.XL, Spacing.L, Spacing.XL, 0),
        };
        body.Controls.Add(Content);
        body.Controls.Add(_status);

        // The padding stays inside a scrolling frame of its own: padding on the scrolling panel itself is
        // not part of what the panel scrolls over, so the last row would stay out of reach.
        _body = new Panel { Name = "body", Dock = DockStyle.Fill, AutoScroll = true };
        _body.Controls.Add(body);
        _status.SizeChanged += (_, _) => KeepContentReachable();
        _status.VisibleChanged += (_, _) => KeepContentReachable();

        (_actions, _footerNote, var footer) = BuildFooter();
        Controls.Add(_body);
        Controls.Add(footer);
        Controls.Add(FormLayout.Header(title, subtitle, glyph));
    }

    /// <summary>How the screen asks questions and reports results; tests replace it so nothing is shown.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal UserPrompts Prompts { get; set; }

    /// <summary>The area between the message strip and the footer; screens add their layout here.</summary>
    private protected Panel Content { get; }

    /// <summary>The inline message strip.</summary>
    internal InfoBar StatusBar => _status;

    /// <summary>Shows a result, validation or error message inside the window; it never takes the focus.</summary>
    internal void ShowStatus(StatusKind kind, string message) => _status.Show(kind, message);

    internal void ClearStatus() => _status.Clear();

    void IStatusSurface.ShowStatus(StatusKind kind, string message) => ShowStatus(kind, message);

    void IStatusSurface.ClearStatus() => ClearStatus();

    /// <summary>A short hint on the left of the footer.</summary>
    private protected void SetFooterNote(string text) => _footerNote.Text = text;

    /// <summary>Places the footer buttons left to right; Tab follows the same order.</summary>
    private protected void SetFooterButtons(params Button[] buttons)
    {
        ArgumentNullException.ThrowIfNull(buttons);

        // The panel flows right to left, so the last button is added first.
        for (var i = buttons.Length - 1; i >= 0; i--)
        {
            _actions.Controls.Add(buttons[i]);
        }

        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].TabIndex = i;
        }
    }

    /// <summary>A "Kapat" button that closes the window; also bound to Esc.</summary>
    private protected Button CreateCloseButton(string name = "btnKapat")
    {
        var close = FormLayout.Button(name, "Kapat", ButtonKind.Secondary);
        close.Click += (_, _) => Close();
        CancelButton = close;
        return close;
    }

    /// <summary>Applies the theme to everything that was added and resumes layout; call at the end of the constructor.</summary>
    private protected void FinishLayout()
    {
        ThemeStyler.Apply(this);
        ResumeLayout(false);
        PerformLayout();
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Measured once the window exists, so the real fonts and metrics decide how the content lays out.
        if (!_sizeFitted)
        {
            _sizeFitted = true;
            FitSizeToContent();
        }
    }

    /// <summary>
    /// Sizes the window for its content and for the screen. The content is laid out at the smallest size it
    /// needs (the design minimum, grown until nothing overflows, also while a two-line message strip is shown);
    /// the window opens and shrinks no larger than the working area of the screen. Where the screen is smaller
    /// than the content, the body scrolls, while the header and the footer actions stay in view.
    /// </summary>
    private void FitSizeToContent()
    {
        // Child handles change the measured heights of combo boxes and numeric fields, so create them first
        // (the window stays hidden).
        CreateChildHandles(this);
        var design = ClientSize;
        var natural = Pickers(this).ToDictionary(picker => picker, picker => picker.Width);
        var trial = ClientSizeFromWindow(MinimumSize);
        ShowStatus(StatusKind.Info, "Örnek ileti" + Environment.NewLine + "ikinci satır");
        try
        {
            for (var pass = 0; pass < 24; pass++)
            {
                ClientSize = trial;
                PerformLayout();
                var overflow = Overflow(this) + Squeeze(natural);
                if (overflow.IsEmpty)
                {
                    break;
                }

                trial += overflow;
            }

            _contentMinimum = new Size(_body.ClientSize.Width, _body.ClientSize.Height - _status.Height);
        }
        finally
        {
            ClearStatus();
        }

        var area = WorkingAreaOf(this).Size;
        MinimumSize = ForScreen(SizeFromClientSize(trial), area);
        var opening = new Size(Math.Max(design.Width, trial.Width), Math.Max(design.Height, trial.Height));
        Size = ForScreen(SizeFromClientSize(opening), area);
        KeepContentReachable();
        PerformLayout();
    }

    /// <summary>The scrollable area always holds the content at its minimum size plus the message strip.</summary>
    private void KeepContentReachable()
    {
        if (!_contentMinimum.IsEmpty)
        {
            _body.AutoScrollMinSize = new Size(_contentMinimum.Width, _contentMinimum.Height + (_status.IsShown ? _status.Height : 0));
        }
    }

    /// <summary>
    /// Boxes with a fixed width are squeezed, not cut off, when their column gets too narrow; this is how far
    /// the window has to grow for them to have the width they have at the design size.
    /// </summary>
    private static Size Squeeze(Dictionary<Control, int> natural)
    {
        var width = 0;
        foreach (var (picker, wanted) in natural)
        {
            if (picker.Width > 0)
            {
                width = Math.Max(width, wanted - picker.Width - 1);
            }
        }

        return new Size(width, 0);
    }

    private static IEnumerable<Control> Pickers(Control parent) =>
        parent.Controls.Cast<Control>().SelectMany(child =>
            child is ThemedComboBox or DateTimePicker or NumericUpDown ? [child] : Pickers(child));

    private Size ClientSizeFromWindow(Size window) =>
        new(window.Width - (Width - ClientSize.Width), window.Height - (Height - ClientSize.Height));

    /// <summary>
    /// The window size limited to the screen. A window that is cut in height gets a vertical scroll bar, which
    /// takes width from the content, so that width is added (as far as the screen allows).
    /// </summary>
    private static Size ForScreen(Size window, Size area)
    {
        var scrollBar = window.Height > area.Height ? SystemInformation.VerticalScrollBarWidth : 0;
        return new Size(Math.Min(window.Width + scrollBar, area.Width), Math.Min(window.Height, area.Height));
    }

    /// <summary>
    /// A drop-down box takes its final height (which follows the font) only after its window exists, but the
    /// container that holds it has measured the earlier, smaller height and would cut it off. The container is
    /// made at least as tall as the box needs.
    /// </summary>
    private static void MakeRoomFor(ThemedComboBox combo)
    {
        if (combo.Parent is not { } parent)
        {
            return;
        }

        var needed = combo.Bottom + combo.Margin.Bottom + parent.Padding.Bottom;
        if (parent.Height < needed)
        {
            parent.MinimumSize = new Size(parent.MinimumSize.Width, needed);
        }
    }

    private static void CreateChildHandles(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            _ = child.Handle;
            CreateChildHandles(child);
            if (child is ThemedComboBox combo)
            {
                MakeRoomFor(combo);
            }
        }
    }

    /// <summary>How far the controls reach beyond the visible area of their containers (right, bottom).</summary>
    private static Size Overflow(Control root)
    {
        const int Tolerance = 1; // Field wrappers settle the height of combo boxes one pixel late.
        var width = 0;
        var height = 0;
        foreach (Control child in root.Controls)
        {
            if (child is InfoBar { Visible: false })
            {
                continue;
            }

            if (child is not (Label or Panel or TableLayoutPanel or FlowLayoutPanel or GroupBox) && child.Parent is not NumericUpDown
                && child.Width > 0 && child.Height > 0)
            {
                // Position of the control in the client area of each ancestor; a container of zero size
                // cuts off everything in it.
                var offset = child.Location;
                for (var ancestor = child.Parent; ancestor is not null; ancestor = ancestor.Parent)
                {
                    width = Math.Max(width, Math.Max(offset.X + child.Width - ancestor.ClientSize.Width, -offset.X) - Tolerance);
                    height = Math.Max(height, offset.Y + child.Height - ancestor.ClientSize.Height - Tolerance);
                    offset = new Point(offset.X + ancestor.Left, offset.Y + ancestor.Top);
                }
            }

            var nested = Overflow(child);
            width = Math.Max(width, nested.Width);
            height = Math.Max(height, nested.Height);
        }

        return new Size(width, height);
    }

    private static (FlowLayoutPanel Actions, Label Note, Control Footer) BuildFooter()
    {
        var footer = new TableLayoutPanel
        {
            Name = "footer",
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(Spacing.XL - Spacing.XS, Spacing.M, Spacing.XL - Spacing.XS, Spacing.M),
            BackColor = Theme.Current.Surface,
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Paint += (_, e) => FormLayout.DrawDivider(footer, e.Graphics, top: true);

        var note = FormLayout.Hint(string.Empty);
        note.Name = "footerNote";
        note.AutoSize = false;
        note.AutoEllipsis = true;
        note.Dock = DockStyle.Fill;
        note.TextAlign = ContentAlignment.MiddleLeft;
        note.Margin = new Padding(Spacing.XS, 0, Spacing.M, 0);
        var actions = new FlowLayoutPanel
        {
            Name = "actions",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = Padding.Empty,
        };
        footer.Controls.Add(note, 0, 0);
        footer.Controls.Add(actions, 1, 0);
        return (actions, note, footer);
    }
}
