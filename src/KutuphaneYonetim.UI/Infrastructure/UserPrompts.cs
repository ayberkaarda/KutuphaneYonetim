using KutuphaneYonetim.UI.Theming;

namespace KutuphaneYonetim.UI.Infrastructure;

/// <summary>
/// The questions and messages a screen shows around its operations. Screens use <see cref="For"/>:
/// results and warnings appear inline in the screen and questions in the themed message dialog. Tests
/// replace it so the operations can run without anything appearing.
/// </summary>
internal sealed class UserPrompts
{
    /// <summary>Prompts for code without a screen: everything goes through <see cref="Dialogs"/>.</summary>
    public static UserPrompts Default { get; } = new();

    /// <summary>Asks a yes/no question; true when the user answers yes.</summary>
    public Func<string, bool> Confirm { get; init; } = text => Dialogs.Confirm(null, text);

    public Action<string> Inform { get; init; } =
        text => Dialogs.Show(null, text, "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);

    public Action<string> Warn { get; init; } =
        text => Dialogs.Show(null, text, "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    /// <summary>Results and warnings in the screen's message strip, questions in a dialog owned by the screen.</summary>
    public static UserPrompts For<TScreen>(TScreen screen)
        where TScreen : IWin32Window, IStatusSurface
    {
        ArgumentNullException.ThrowIfNull(screen);

        return new UserPrompts
        {
            Confirm = text => Dialogs.Confirm(screen, text),
            Inform = text => screen.ShowStatus(StatusKind.Success, text),
            Warn = text => screen.ShowStatus(StatusKind.Warning, text),
        };
    }
}
