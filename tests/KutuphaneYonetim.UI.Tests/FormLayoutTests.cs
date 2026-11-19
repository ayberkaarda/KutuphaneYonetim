namespace KutuphaneYonetim.UI.Tests;

/// <summary>
/// Builds every screen without showing it (no window handle, nothing on screen) to check that the designer
/// code and resources load and that every control lies inside the client area without overlapping another.
/// </summary>
public sealed class FormLayoutTests
{
    public static TheoryData<string> FormNames => new(FormFactories.Keys);

    private static readonly Dictionary<string, Func<Form>> FormFactories = new()
    {
        [nameof(Form1)] = () => new Form1(null!),
        [nameof(KitapForm)] = () => new KitapForm(null!),
        [nameof(UyeForm)] = () => new UyeForm(null!),
        [nameof(OduncForm)] = () => new OduncForm(null!, null!, null!),
        [nameof(RezervasyonForm)] = () => new RezervasyonForm(null!, null!, null!),
    };

    [Theory]
    [MemberData(nameof(FormNames))]
    public void Form_ControlsFitInsideClientAreaWithoutOverlapping(string formName)
    {
        using var form = FormFactories[formName]();
        var client = new Rectangle(Point.Empty, form.ClientSize);
        var controls = form.Controls.Cast<Control>().Where(c => c.Visible || !c.Created).ToList();

        Assert.NotEmpty(controls);
        Assert.NotNull(form.Icon);
        foreach (var control in controls)
        {
            Assert.True(client.Contains(control.Bounds), $"{formName}.{control.Name} {control.Bounds} lies outside {client}.");
        }

        for (var i = 0; i < controls.Count; i++)
        {
            for (var j = i + 1; j < controls.Count; j++)
            {
                Assert.False(
                    controls[i].Bounds.IntersectsWith(controls[j].Bounds),
                    $"{formName}: {controls[i].Name} {controls[i].Bounds} overlaps {controls[j].Name} {controls[j].Bounds}.");
            }
        }
    }
}
