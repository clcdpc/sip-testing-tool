namespace Clc.Sip.Tests.TestSupport;

internal static class CheckoutTestData
{
    public static void ApplySelectedOptions(MainForm form)
    {
        form.GetCheckBox("checkoutRenewalCheckBox").Checked = false;
        form.GetCheckBox("checkoutNoBlockCheckBox").Checked = true;
        var date = form.GetDatePicker("checkoutDueDatePicker");
        date.Value = new DateTime(2026, 10, 28, 14, 30, 45);
        date.Checked = true;
        form.GetTextBox("checkoutItemPropertiesTextBox").Text = "test-properties";
        form.GetTextBox("checkoutTerminalPasswordTextBox").Text = "terminal-secret";
        form.GetComboBox("checkoutFeeAcknowledgedComboBox").SelectedIndex = 2;
        form.GetComboBox("checkoutCancelComboBox").SelectedIndex = 2;
    }
}
