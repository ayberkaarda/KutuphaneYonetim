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

    private readonly InfoBar _status = new();
    private readonly FlowLayoutPanel _actions;
    private readonly Label _footerNote;

    private protected AppDialog(string title, string subtitle, string glyph, Size clientSize)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = Theme.Fonts.Body;
        Text = title + " - " + AppName;
        Icon = Theme.AppIcon;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = clientSize;
        MinimumSize = SizeFromClientSize(new Size(clientSize.Width * 4 / 5, clientSize.Height * 4 / 5));
        Prompts = UserPrompts.For(this);

        Content = new Panel { Name = "content", Dock = DockStyle.Fill, Margin = Padding.Empty };
        var body = new Panel
        {
            Name = "body",
            Dock = DockStyle.Fill,
            Padding = new Padding(Spacing.XL, Spacing.L, Spacing.XL, 0),
        };
        body.Controls.Add(Content);
        body.Controls.Add(_status);

        (_actions, _footerNote, var footer) = BuildFooter();
        Controls.Add(body);
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
