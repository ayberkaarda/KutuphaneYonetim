using KutuphaneYonetim.UI.Theming;

namespace KutuphaneYonetim.UI.Infrastructure;

/// <summary>Something that can show an inline result, validation or error message.</summary>
internal interface IStatusSurface
{
    void ShowStatus(StatusKind kind, string message);

    void ClearStatus();
}

/// <summary>
/// The single place that opens message windows. Questions that need an answer and errors outside a
/// screen use the themed <see cref="MessageDialog"/>; results and validation messages of a screen are
/// shown inline instead (see <see cref="IStatusSurface"/>).
/// </summary>
internal static class Dialogs
{
    internal const string AppName = "Kütüphane Yönetim";

    /// <summary>
    /// Replaces the dialog, for example in tests and in the offscreen screenshot tool, which must never
    /// open a window. <see langword="null"/> in the application.
    /// </summary>
    internal static Func<string, string, MessageBoxButtons, MessageBoxIcon, DialogResult>? Override { get; set; }

    public static DialogResult Show(IWin32Window? owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        if (Override is { } replacement)
        {
            return replacement(text, caption, buttons, icon);
        }

        // A window needs an STA thread; errors reported from a worker thread fall back to the system box.
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            return MessageBox.Show(owner, text, caption, buttons, icon);
        }

        using var dialog = new MessageDialog(text, caption, buttons, icon);
        return dialog.ShowDialog(owner);
    }

    /// <summary>A yes/no question; <see langword="true"/> when the user answers yes.</summary>
    public static bool Confirm(IWin32Window? owner, string text) =>
        Show(owner, text, "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
}
