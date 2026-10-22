namespace Clc.Sip.Helpers;

internal sealed class CommandOptionsPanel : TableLayoutPanel
{
    private readonly List<Action> resetActions = [];
    private readonly ToolTip toolTips;
    private readonly string controlNamePrefix;
    private readonly string commandCaption;
    private int nextTabIndex;

    public CommandOptionsPanel(string controlNamePrefix, string commandCaption, ToolTip toolTips)
    {
        this.controlNamePrefix = controlNamePrefix;
        this.commandCaption = commandCaption;
        this.toolTips = toolTips;
        Name = controlNamePrefix + "OptionsLayout";
        Dock = DockStyle.Top;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Margin = Padding.Empty;
        ColumnCount = 1;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
    }

    private void AddRow(Control control)
    {
        var row = RowCount++;
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(control, 0, row);
    }

    private void AddField(Control field, string label, string hint)
    {
        field.AccessibleName = label;
        field.TabIndex = nextTabIndex++;
        toolTips.SetToolTip(field, hint);
        AddRow(new OptionalFieldRow(new Label { Text = label }, field));
    }

    public TextBox AddTextField(string name, string label, string initial = "", string hint = "Blank omits this optional field.", int maxLength = 32767)
    {
        var field = new ThemedTextBox { Name = name, Text = initial, MaxLength = maxLength };
        AddField(field, label, hint);
        resetActions.Add(() => field.Text = initial);
        return field;
    }

    public CheckBox AddFlag(string name, string label, bool initial, string hint = "Checked sends Y; unchecked sends N.")
    {
        var field = new CheckBox
        {
            Name = name,
            Text = label,
            AccessibleName = label,
            Checked = initial,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10),
            TabIndex = nextTabIndex++
        };
        toolTips.SetToolTip(field, hint);
        AddRow(field);
        resetActions.Add(() => field.Checked = initial);
        return field;
    }

    public ThemedDateTimePicker AddDateField(string name, string label, bool initial, Func<DateTime> suggested, string hint)
    {
        var field = new ThemedDateTimePicker { Name = name, Value = suggested(), Checked = initial };
        AddField(field, label, hint);
        field.DescribeField(label);
        resetActions.Add(() =>
        {
            field.Value = suggested();
            field.Checked = initial;
        });
        return field;
    }

    public ComboBox AddChoice(string name, string label, string[] items)
    {
        var field = new ThemedComboBox { Name = name, DropDownStyle = ComboBoxStyle.DropDownList };
        field.Items.AddRange(items);
        field.SelectedIndex = 0;
        AddField(field, label, "");
        resetActions.Add(() => field.SelectedIndex = 0);
        return field;
    }

    public ComboBox AddOptionalFlag(string name, string label) => AddChoice(name, label, ["Omit field", "No (N)", "Yes (Y)"]);

    public ComboBox AddEnumField<T>(string name, string label) where T : struct, Enum
    {
        var field = new ThemedComboBox { Name = name, DropDownStyle = ComboBoxStyle.DropDownList, FormattingEnabled = true };
        field.Format += (_, e) => e.Value = System.Text.RegularExpressions.Regex.Replace(e.ListItem.ToString(), "([a-z])([A-Z])", "$1 $2");
        foreach (var value in Enum.GetValues<T>())
        {
            field.Items.Add(value);
        }

        field.SelectedIndex = 0;
        AddField(field, label, "");
        resetActions.Add(() => field.SelectedIndex = 0);
        return field;
    }

    public void AddHint(string text) => AddRow(new Label { Text = text, AutoSize = true, MaximumSize = new Size(245, 0), Margin = new Padding(0, 0, 0, 12) });

    public void AddResetButton(Action resetOtherFields = null)
    {
        var button = new ThemedButton
        {
            Name = "reset" + char.ToUpperInvariant(controlNamePrefix[0]) + controlNamePrefix[1..] + "OptionsButton",
            Text = "Reset " + commandCaption + " options",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 0, 0),
            TabIndex = nextTabIndex++
        };
        button.Click += (_, _) =>
        {
            SuspendLayout();
            try
            {
                foreach (var reset in resetActions)
                {
                    reset();
                }

                resetOtherFields?.Invoke();
            }
            finally { ResumeLayout(true); }
        };
        AddRow(button);
    }
}
