namespace KutuphaneYonetim.UI.Infrastructure;

/// <summary>
/// The message boxes a screen shows around its operations. Screens use <see cref="Default"/>; tests
/// replace it so the operations can run without any dialog appearing.
/// </summary>
internal sealed class UserPrompts
{
    public static UserPrompts Default { get; } = new();

    /// <summary>Asks a yes/no question; true when the user answers yes.</summary>
    public Func<string, bool> Confirm { get; init; } =
        text => MessageBox.Show(text, "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    public Action<string> Inform { get; init; } =
        text => MessageBox.Show(text, "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);

    public Action<string> Warn { get; init; } =
        text => MessageBox.Show(text, "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
