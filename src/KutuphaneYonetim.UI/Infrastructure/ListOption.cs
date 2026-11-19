namespace KutuphaneYonetim.UI.Infrastructure;

/// <summary>A combo box entry: Turkish text shown to the user and the value behind it.</summary>
internal sealed record ListOption<T>(string Text, T Value)
{
    public override string ToString() => Text;
}

internal static class ListOptionExtensions
{
    /// <summary>Fills a drop-down list with the options and selects the first one.</summary>
    public static void BindOptions<T>(this ComboBox comboBox, IReadOnlyList<ListOption<T>> options)
    {
        ArgumentNullException.ThrowIfNull(comboBox);

        comboBox.DisplayMember = nameof(ListOption<T>.Text);
        comboBox.ValueMember = nameof(ListOption<T>.Value);
        comboBox.DataSource = options;
    }

    /// <summary>The value of the selected option, or <paramref name="fallback"/> when nothing is selected.</summary>
    public static T SelectedOption<T>(this ComboBox comboBox, T fallback)
    {
        ArgumentNullException.ThrowIfNull(comboBox);

        return comboBox.SelectedItem is ListOption<T> option ? option.Value : fallback;
    }
}
