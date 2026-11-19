using KutuphaneYonetim.UI.Theming;

namespace KutuphaneYonetim.UI.Infrastructure;

/// <summary>
/// Factories for the building blocks of the screens: header, cards, labelled fields, inputs, toolbars
/// and buttons. Everything uses the theme tokens and logical pixels, and lays out with table and flow
/// panels, so no screen positions or colors controls by hand.
/// </summary>
internal static class FormLayout
{
    /// <summary>Half the difference between the shared control height and a native date or number box.</summary>
    private const int NativeInputInset = 4;

    /// <summary>Header of a screen: glyph badge (or the logo), title and one line of description.</summary>
    public static TableLayoutPanel Header(string title, string subtitle, string glyph, Image? logo = null)
    {
        var p = Theme.Current;
        var header = new TableLayoutPanel
        {
            Name = "header",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(Spacing.XL, Spacing.L, Spacing.XL, Spacing.L),
            BackColor = p.Surface,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Paint += (_, e) => DrawDivider(header, e.Graphics, top: false);

        var badge = new GlyphBadge(glyph, Sizes.HeaderBadge, 15F) { Name = "headerBadge", Image = logo, Anchor = AnchorStyles.Left };
        var titleLabel = new Label
        {
            Name = "headerTitle",
            Text = title,
            AutoSize = true,
            Font = Theme.Fonts.Title,
            ForeColor = p.Text,
            UseMnemonic = false,
            Margin = Padding.Empty,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        var subtitleLabel = new Label
        {
            Name = "headerSubtitle",
            Text = subtitle,
            AutoSize = true,
            Font = Theme.Fonts.Body,
            ForeColor = p.TextSecondary,
            UseMnemonic = false,
            Margin = new Padding(0, Spacing.XS / 2, 0, 0),
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
        };
        header.Controls.Add(badge, 0, 0);
        header.SetRowSpan(badge, 2);
        header.Controls.Add(titleLabel, 1, 0);
        header.Controls.Add(subtitleLabel, 1, 1);
        return header;
    }

    /// <summary>A card with an optional section title above <paramref name="content"/>.</summary>
    public static CardPanel Card(string name, string? title, Control content, bool fill = true)
    {
        ArgumentNullException.ThrowIfNull(content);

        var card = new CardPanel { Name = name, Dock = fill ? DockStyle.Fill : DockStyle.Top };
        content.Dock = fill ? DockStyle.Fill : DockStyle.Top;
        card.Controls.Add(content);
        if (title is not null)
        {
            var heading = SectionTitle(title);
            heading.Name = name + "Title";
            heading.Dock = DockStyle.Top;
            heading.Padding = new Padding(0, 0, 0, Spacing.M);
            card.Controls.Add(heading);
        }

        if (!fill)
        {
            card.AutoSize = true;
            card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }

        return card;
    }

    public static Label SectionTitle(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = Theme.Fonts.Subtitle,
        ForeColor = Theme.Current.Text,
        UseMnemonic = false,
        Margin = new Padding(0, 0, 0, Spacing.S),
    };

    /// <summary>Label of an input; an ampersand marks the Alt access key that moves to the input.</summary>
    public static Label Caption(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = Theme.Fonts.BodyStrong,
        ForeColor = Theme.Current.Text,
        Margin = new Padding(0, 0, 0, Spacing.XS / 2),
    };

    /// <summary>Secondary explanatory text.</summary>
    public static Label Hint(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = Theme.Fonts.Caption,
        ForeColor = Theme.Current.TextMuted,
        UseMnemonic = false,
        Margin = new Padding(0, Spacing.XS, 0, 0),
    };

    /// <summary>A read-only value, for example the selected member's unpaid fine.</summary>
    public static Label Value(string name) => new()
    {
        Name = name,
        AutoSize = true,
        Font = Theme.Fonts.Value,
        ForeColor = Theme.Current.Text,
        UseMnemonic = false,
        Margin = new Padding(0, 0, Spacing.M, 0),
    };

    /// <summary>Body text that wraps inside its column.</summary>
    public static Label Text(string name) => new()
    {
        Name = name,
        AutoSize = true,
        Font = Theme.Fonts.Body,
        ForeColor = Theme.Current.Text,
        UseMnemonic = false,
        Margin = Padding.Empty,
    };

    public static ThemedButton Button(string name, string text, ButtonKind kind = ButtonKind.Secondary) =>
        new(kind) { Name = name, Text = text };

    /// <summary>A text box framed at the shared control height.</summary>
    public static TextField Input(TextBox box) => new(box);

    public static ThemedComboBox Picker(string name, int width, string placeholder = "") =>
        new() { Name = name, Width = width, Placeholder = placeholder };

    public static DateTimePicker DatePicker(string name) =>
        new() { Name = name, Format = DateTimePickerFormat.Short, Width = 140, Font = Theme.Fonts.Body };

    /// <summary>
    /// A labelled input: caption above, input below and an optional hint. The caption's access key
    /// moves the focus to the input, and the input gets the caption as its accessible name.
    /// </summary>
    public static TableLayoutPanel Field(string caption, Control input, string? hint = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        var field = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, Spacing.L, Spacing.M),
        };
        field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        field.Controls.Add(Caption(caption));
        if (input.Dock == DockStyle.None)
        {
            input.Anchor = input is ThemedComboBox or TextField or TextBox
                ? AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
                : AnchorStyles.Left | AnchorStyles.Top;
        }

        // Date pickers and number boxes keep their native height (shorter than the shared control
        // height); a little extra top margin centers them on the row of drop-down lists and buttons.
        input.Margin = input is DateTimePicker or NumericUpDown
            ? new Padding(0, Spacing.XS + NativeInputInset, 0, NativeInputInset)
            : new Padding(0, Spacing.XS, 0, 0);
        var accessible = input is TextField framed ? framed.Input : input;
        accessible.AccessibleName ??= caption.Replace("&", string.Empty, StringComparison.Ordinal);
        field.Controls.Add(input);
        if (hint is not null)
        {
            var hintLabel = Hint(hint);
            hintLabel.Name = (accessible.Name ?? "field") + "Hint";
            field.Controls.Add(hintLabel);
        }

        return field;
    }

    /// <summary>
    /// A button placed like a field without a caption, so it lines up with the inputs of a row of
    /// labelled fields (the empty caption takes the same height as theirs).
    /// </summary>
    public static TableLayoutPanel FieldAction(Control button)
    {
        ArgumentNullException.ThrowIfNull(button);

        var field = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, Spacing.M),
        };
        var caption = Caption(" ");
        caption.UseMnemonic = false;
        caption.AccessibleRole = AccessibleRole.None;
        field.Controls.Add(caption);
        button.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        button.Margin = new Padding(0, Spacing.XS, 0, 0);
        field.Controls.Add(button);
        return field;
    }

    /// <summary>
    /// Fields in one row with explicit column widths: a percentage shares the free width, zero sizes
    /// the column to its field.
    /// </summary>
    public static TableLayoutPanel FieldRow(string name, params (Control Field, float Percent)[] columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        var row = new TableLayoutPanel
        {
            Name = name,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = columns.Length,
            RowCount = 1,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
        };
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        for (var i = 0; i < columns.Length; i++)
        {
            var (field, percent) = columns[i];
            field.Margin = new Padding(field.Margin.Left, field.Margin.Top, field.Margin.Right, 0); // The card's padding closes the row.
            row.ColumnStyles.Add(percent > 0 ? new ColumnStyle(SizeType.Percent, percent) : new ColumnStyle(SizeType.AutoSize));
            row.Controls.Add(field, i, 0);
        }

        return row;
    }

    /// <summary>
    /// Fields in rows; every row is split evenly across the widest row, and a single field in a row
    /// spans the full width.
    /// </summary>
    public static TableLayoutPanel Fields(string name, params Control[][] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var columns = Math.Max(1, rows.Max(r => r.Length));
        var grid = new TableLayoutPanel
        {
            Name = name,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = columns,
            RowCount = rows.Length,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
        };
        for (var c = 0; c < columns; c++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
        }

        for (var r = 0; r < rows.Length; r++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var row = rows[r];
            for (var c = 0; c < row.Length; c++)
            {
                grid.Controls.Add(row[c], c, r);
                if (row.Length == 1)
                {
                    grid.SetColumnSpan(row[c], columns);
                }
            }
        }

        return grid;
    }

    /// <summary>
    /// Controls stacked top to bottom in a single auto-sized column; unlike docked panels it grows
    /// when a child grows (for example when a label wraps on a narrow window or at a high DPI).
    /// </summary>
    public static TableLayoutPanel Stack(string name, params Control[] controls)
    {
        ArgumentNullException.ThrowIfNull(controls);

        var stack = new TableLayoutPanel
        {
            Name = name,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = controls.Length,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var control in controls)
        {
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Dock = DockStyle.Fill;
            stack.Controls.Add(control);
        }

        return stack;
    }

    /// <summary>
    /// The page layout of a screen: rows top to bottom that take their content's height, except the
    /// rows marked to fill, which share the remaining height.
    /// </summary>
    public static TableLayoutPanel Page(params (Control Control, bool Fill)[] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var page = new TableLayoutPanel
        {
            Name = "page",
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = rows.Length,
            Margin = Padding.Empty,
        };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var (control, fill) in rows)
        {
            page.RowStyles.Add(fill ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
            control.Dock = DockStyle.Fill;
            page.Controls.Add(control);
        }

        // Without a filling row the table would stretch the last row; an empty row takes the rest instead.
        if (!rows.Any(row => row.Fill))
        {
            page.RowCount++;
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        }

        return page;
    }

    /// <summary>Two columns: a fixed-width editor card on the left and a list card that takes the rest.</summary>
    public static TableLayoutPanel Split(Control editor, Control list, int editorWidth = Sizes.EditorWidth)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(list);

        var layout = new TableLayoutPanel { Name = "layout", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, editorWidth));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        editor.Dock = DockStyle.Fill;
        editor.Margin = new Padding(0, 0, Spacing.S, Spacing.L);
        list.Dock = DockStyle.Fill;
        list.Margin = new Padding(Spacing.S, 0, 0, Spacing.L);
        layout.Controls.Add(editor, 0, 0);
        layout.Controls.Add(list, 1, 0);
        return layout;
    }

    /// <summary>
    /// A single-row bar: the controls marked to fill share the free width, the others keep their size.
    /// Used for search bars, action rows and pagers, so the controls line up on one center line.
    /// </summary>
    public static TableLayoutPanel Bar(string name, params (Control Control, bool Fill)[] items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var bar = new TableLayoutPanel
        {
            Name = name,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = items.Length,
            RowCount = 1,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
        };
        bar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        for (var i = 0; i < items.Length; i++)
        {
            var (control, fill) = items[i];
            bar.ColumnStyles.Add(fill ? new ColumnStyle(SizeType.Percent, 100) : new ColumnStyle(SizeType.AutoSize));

            // Inputs and buttons share one height and line up at the top (a drop-down list only takes
            // its final height once its window exists); text is centered on the row.
            var vertical = control is Label ? AnchorStyles.None : AnchorStyles.Top;
            control.Anchor = (fill ? AnchorStyles.Left | AnchorStyles.Right : AnchorStyles.Left) | vertical;
            bar.Controls.Add(control, i, 0);
        }

        return bar;
    }

    /// <summary>An empty column for <see cref="Bar"/> that pushes the following items to the right.</summary>
    public static Control Spacer() => new Panel { Name = "spacer", Height = 1, Margin = Padding.Empty, TabStop = false };

    /// <summary>A left-to-right row that wraps on narrow windows.</summary>
    public static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        row.Controls.AddRange(controls);
        return row;
    }

    /// <summary>A hairline at the top or bottom edge of <paramref name="control"/>.</summary>
    public static void DrawDivider(Control control, Graphics graphics, bool top)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(graphics);

        using var pen = new Pen(Theme.Current.Border, Math.Max(1, Theme.Px(control, 1)));
        var y = top ? 0 : control.Height - 1;
        graphics.DrawLine(pen, 0, y, control.Width, y);
    }
}
