namespace KutuphaneYonetim.UI.Theming;

/// <summary>
/// Named colors of one theme. Controls never use literal colors; they read a role from the current
/// palette, so the light, dark and high contrast themes stay consistent.
/// </summary>
internal sealed record ThemePalette
{
    public required string Name { get; init; }

    public bool IsDark { get; init; }

    /// <summary>Built from the Windows high contrast colors; custom painting steps back for native drawing.</summary>
    public bool IsHighContrast { get; init; }

    /// <summary>Window background behind cards: a warm paper tone.</summary>
    public required Color Page { get; init; }

    /// <summary>Cards, inputs, header and footer.</summary>
    public required Color Surface { get; init; }

    /// <summary>Grid headers, alternating rows and quiet areas.</summary>
    public required Color SurfaceAlt { get; init; }

    public required Color SurfaceHover { get; init; }

    /// <summary>Decorative hairlines between areas.</summary>
    public required Color Border { get; init; }

    /// <summary>Borders of inputs; at least 3:1 against the surface.</summary>
    public required Color BorderStrong { get; init; }

    public required Color Text { get; init; }

    public required Color TextSecondary { get; init; }

    public required Color TextMuted { get; init; }

    public required Color TextDisabled { get; init; }

    public required Color Accent { get; init; }

    public required Color AccentHover { get; init; }

    public required Color AccentPressed { get; init; }

    /// <summary>Text and glyphs on <see cref="Accent"/>.</summary>
    public required Color OnAccent { get; init; }

    /// <summary>Tinted background for selected rows and glyph badges.</summary>
    public required Color AccentSubtle { get; init; }

    /// <summary>Accent used as text or a glyph on <see cref="Surface"/>.</summary>
    public required Color AccentText { get; init; }

    /// <summary>The bookmark gold of the logo; a small decorative mark, never text.</summary>
    public required Color Ribbon { get; init; }

    public required Color Success { get; init; }

    public required Color SuccessSubtle { get; init; }

    public required Color Warning { get; init; }

    public required Color WarningSubtle { get; init; }

    public required Color Danger { get; init; }

    public required Color DangerSubtle { get; init; }

    public required Color Info { get; init; }

    public required Color InfoSubtle { get; init; }

    /// <summary>
    /// Light theme: warm paper neutrals and a deep binding-leather wine accent chosen for this
    /// application. The accent sits apart from the orange-red danger role, so destructive actions
    /// never look like the primary one.
    /// </summary>
    public static ThemePalette Light { get; } = new()
    {
        Name = "Light",
        Page = Hex(0xF5F1EA),
        Surface = Hex(0xFFFEFC),
        SurfaceAlt = Hex(0xF8F4EE),
        SurfaceHover = Hex(0xF1EBE2),
        Border = Hex(0xE4DCD0),
        BorderStrong = Hex(0x8C8176),
        Text = Hex(0x1F1A16),
        TextSecondary = Hex(0x4D443D),
        TextMuted = Hex(0x675D55),
        TextDisabled = Hex(0xA49A90),
        Accent = Hex(0x7A2B3F),
        AccentHover = Hex(0x692336),
        AccentPressed = Hex(0x581C2D),
        OnAccent = Hex(0xFFFFFF),
        AccentSubtle = Hex(0xF4E7E9),
        AccentText = Hex(0x7A2B3F),
        Ribbon = Hex(0xD9A23A),
        Success = Hex(0x1E6A45),
        SuccessSubtle = Hex(0xE7F2EA),
        Warning = Hex(0x7A4B00),
        WarningSubtle = Hex(0xFBF0D9),
        Danger = Hex(0xB02A17),
        DangerSubtle = Hex(0xFBEAE6),
        Info = Hex(0x23598C),
        InfoSubtle = Hex(0xE8F0F7),
    };

    /// <summary>Dark theme: the same roles stepped for a dark, warm surface, not an inverted light theme.</summary>
    public static ThemePalette Dark { get; } = new()
    {
        Name = "Dark",
        IsDark = true,
        Page = Hex(0x15120F),
        Surface = Hex(0x1F1B17),
        SurfaceAlt = Hex(0x26211C),
        SurfaceHover = Hex(0x2F2923),
        Border = Hex(0x3A332C),
        BorderStrong = Hex(0x80766B),
        Text = Hex(0xF2EDE6),
        TextSecondary = Hex(0xC7BEB4),
        TextMuted = Hex(0xA89E93),
        TextDisabled = Hex(0x655C53),
        Accent = Hex(0xE597A6),
        AccentHover = Hex(0xEDAAB7),
        AccentPressed = Hex(0xD98395),
        OnAccent = Hex(0x2B0B14),
        AccentSubtle = Hex(0x3D2129),
        AccentText = Hex(0xEDA6B4),
        Ribbon = Hex(0xE2B354),
        Success = Hex(0x7CCF9C),
        SuccessSubtle = Hex(0x173024),
        Warning = Hex(0xF0BF5C),
        WarningSubtle = Hex(0x33290F),
        Danger = Hex(0xFF9C8C),
        DangerSubtle = Hex(0x3C1B16),
        Info = Hex(0x92C0F0),
        InfoSubtle = Hex(0x172A3E),
    };

    /// <summary>
    /// High contrast theme built from the active Windows system colors, so the user's contrast theme is
    /// respected instead of being replaced by the application's colors.
    /// </summary>
    public static ThemePalette HighContrast() => new()
    {
        Name = "HighContrast",
        IsHighContrast = true,
        IsDark = SystemColors.Window.GetBrightness() < 0.5f,
        Page = SystemColors.Window,
        Surface = SystemColors.Window,
        SurfaceAlt = SystemColors.Window,
        SurfaceHover = SystemColors.Highlight,
        Border = SystemColors.WindowText,
        BorderStrong = SystemColors.WindowText,
        Text = SystemColors.WindowText,
        TextSecondary = SystemColors.WindowText,
        TextMuted = SystemColors.WindowText,
        TextDisabled = SystemColors.GrayText,
        Accent = SystemColors.Highlight,
        AccentHover = SystemColors.Highlight,
        AccentPressed = SystemColors.Highlight,
        OnAccent = SystemColors.HighlightText,
        AccentSubtle = SystemColors.Window,
        AccentText = SystemColors.HotTrack,
        Ribbon = SystemColors.WindowText,
        Success = SystemColors.WindowText,
        SuccessSubtle = SystemColors.Window,
        Warning = SystemColors.WindowText,
        WarningSubtle = SystemColors.Window,
        Danger = SystemColors.WindowText,
        DangerSubtle = SystemColors.Window,
        Info = SystemColors.WindowText,
        InfoSubtle = SystemColors.Window,
    };

    private static Color Hex(int rgb) => Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
}
