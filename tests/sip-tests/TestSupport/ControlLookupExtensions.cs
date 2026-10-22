using Clc.Sip.Helpers;

namespace Clc.Sip.Tests.TestSupport;

internal static class ControlLookupExtensions
{
    public static TextBox GetTextBox(this Control parent, string name) => GetRequiredControl<TextBox>(parent, name);
    public static CheckBox GetCheckBox(this Control parent, string name) => GetRequiredControl<CheckBox>(parent, name);
    public static ComboBox GetComboBox(this Control parent, string name) => GetRequiredControl<ComboBox>(parent, name);
    public static Button GetButton(this Control parent, string name) => GetRequiredControl<Button>(parent, name);
    public static RichTextBox GetRichTextBox(this Control parent, string name) => GetRequiredControl<RichTextBox>(parent, name);
    public static DataGridView GetDataGridView(this Control parent, string name) => GetRequiredControl<DataGridView>(parent, name);
    public static MaskedTextBox GetMaskedTextBox(this Control parent, string name) => GetRequiredControl<MaskedTextBox>(parent, name);
    public static ThemedDateTimePicker GetDatePicker(this Control parent, string name) => GetRequiredControl<ThemedDateTimePicker>(parent, name);
    public static TableLayoutPanel GetTableLayoutPanel(this Control parent, string name) => GetRequiredControl<TableLayoutPanel>(parent, name);
    public static SplitContainer GetSplitContainer(this Control parent, string name) => GetRequiredControl<SplitContainer>(parent, name);
    public static Panel GetPanel(this Control parent, string name) => GetRequiredControl<Panel>(parent, name);
    public static StatusStrip GetStatusStrip(this Control parent, string name) => GetRequiredControl<StatusStrip>(parent, name);
    public static Control GetControl(this Control parent, string name) => GetRequiredControl<Control>(parent, name);

    private static T GetRequiredControl<T>(Control parent, string name) where T : Control
    {
        var matches = parent.Controls.Find(name, searchAllChildren: true);
        Assert.AreEqual(1, matches.Length, $"Expected one control named '{name}' within '{parent.Name}'.");
        Assert.IsInstanceOfType<T>(matches[0], $"Control '{name}' must be a {typeof(T).Name}.");
        return (T)matches[0];
    }
}
