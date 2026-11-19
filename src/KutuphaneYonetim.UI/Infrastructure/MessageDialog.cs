using System.Media;
using KutuphaneYonetim.UI.Theming;

namespace KutuphaneYonetim.UI.Infrastructure;

/// <summary>
/// A message or question window in the application's visual language: a severity badge, a heading,
/// the message and the answers in a footer (primary first). Enter answers with the primary button and
/// Esc with the safe one; the text can be copied with Ctrl+C as in a system message box.
/// </summary>
internal sealed class MessageDialog : Form
{
    private const int MessageWidth = 380;

    private readonly string _message;
    private readonly MessageBoxIcon _icon;

    public MessageDialog(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        _message = message;
        _icon = icon;
        var p = Theme.Current;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Name = nameof(MessageDialog);
        Text = Dialogs.AppName;
        Font = Theme.Fonts.Body;
        Icon = Theme.AppIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;
        BackColor = p.Surface;

        var (glyph, glyphColor, fill) = Severity(p, icon);
        var badge = new GlyphBadge(glyph, Sizes.HeaderBadge, 15F)
        {
            Name = "badge",
            GlyphColor = glyphColor,
            Fill = fill,
            Margin = new Padding(0, 0, Spacing.L, 0),
        };
        var heading = new Label
        {
            Name = "heading",
            Text = caption,
            AutoSize = true,
            Font = Theme.Fonts.Subtitle,
            ForeColor = p.Text,
            UseMnemonic = false,
            Margin = new Padding(0, Spacing.XS, 0, Spacing.S),
        };
        var text = new Label
        {
            Name = "message",
            Text = message,
            AutoSize = true,
            MaximumSize = new Size(MessageWidth, 0),
            Font = Theme.Fonts.Body,
            ForeColor = p.TextSecondary,
            UseMnemonic = false,
            Margin = Padding.Empty,
        };

        var body = new TableLayoutPanel
        {
            Name = "body",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 2,
            Dock = DockStyle.Top,
            Padding = new Padding(Spacing.XL, Spacing.XL, Spacing.XL, Spacing.XL),
            BackColor = p.Surface,
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        body.Controls.Add(badge, 0, 0);
        body.SetRowSpan(badge, 2);
        body.Controls.Add(heading, 1, 0);
        body.Controls.Add(text, 1, 1);

        var actions = new FlowLayoutPanel
        {
            Name = "actions",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Dock = DockStyle.Top,
            Padding = new Padding(Spacing.L, Spacing.M, Spacing.L, Spacing.M),
            BackColor = p.SurfaceAlt,
        };
        actions.Paint += (_, e) => FormLayout.DrawDivider(actions, e.Graphics, top: true);

        // The flow runs right to left, so the safe answer is added first and ends up on the right.
        if (buttons == MessageBoxButtons.YesNo)
        {
            var no = Answer("btnHayir", "Hayır", ButtonKind.Secondary, DialogResult.No);
            var yes = Answer("btnEvet", "Evet", ButtonKind.Primary, DialogResult.Yes);
            actions.Controls.Add(no);
            actions.Controls.Add(yes);
            yes.TabIndex = 0;
            no.TabIndex = 1;
            AcceptButton = yes;
            CancelButton = no;
        }
        else
        {
            var ok = Answer("btnTamam", "Tamam", ButtonKind.Primary, DialogResult.OK);
            actions.Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = ok;
        }

        Controls.Add(actions);
        Controls.Add(body);
        AccessibleName = caption;
        AccessibleDescription = message;
        ResumeLayout(false);
        PerformLayout();

        // Sized from the content after DPI scaling; a form that sizes itself only does so once shown.
        var bodySize = body.GetPreferredSize(Size.Empty);
        var actionsSize = actions.GetPreferredSize(Size.Empty);
        ClientSize = new Size(Math.Max(bodySize.Width, actionsSize.Width), bodySize.Height + actionsSize.Height);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        (_icon switch
        {
            MessageBoxIcon.Error => SystemSounds.Hand,
            MessageBoxIcon.Warning => SystemSounds.Exclamation,
            MessageBoxIcon.Question => SystemSounds.Question,
            _ => SystemSounds.Asterisk,
        }).Play();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Control && e.KeyCode == Keys.C)
        {
            Clipboard.SetText(Text + Environment.NewLine + _message);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    private static ThemedButton Answer(string name, string text, ButtonKind kind, DialogResult result)
    {
        var button = FormLayout.Button(name, text, kind);
        button.DialogResult = result;
        return button;
    }

    private static (string Glyph, Color Color, Color Fill) Severity(ThemePalette p, MessageBoxIcon icon) => icon switch
    {
        MessageBoxIcon.Error => (Glyphs.Error, p.Danger, p.DangerSubtle),
        MessageBoxIcon.Warning => (Glyphs.Warning, p.Warning, p.WarningSubtle),
        MessageBoxIcon.Question => (Glyphs.Question, p.AccentText, p.AccentSubtle),
        _ => (Glyphs.Info, p.Info, p.InfoSubtle),
    };
}
