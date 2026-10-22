using System.Globalization;
using Clc.Sip.Helpers;

namespace Clc.Sip.Tests.TestSupport;

internal sealed class WorkbenchFixture : IDisposable
{
    private readonly CommandProbe probe = new();
    private readonly MainForm form;
    private readonly ComboBox commands;
    private readonly Button submit;
    private readonly RichTextBox messageLog;
    private readonly TextBox itemBarcode;

    public WorkbenchFixture()
    {
        try
        {
            form = CreateConnectedForm(probe);
            commands = form.GetComboBox("commandComboBox");
            submit = form.GetButton("executeCommandButton");
            messageLog = form.GetRichTextBox("logRichTextBox");
            itemBarcode = form.GetTextBox("itemBarcodeTextBox");
        }
        catch
        {
            form?.SipClient?.Dispose();
            form?.Dispose();
            probe.Dispose();
            throw;
        }
    }

    public void SetText(string controlName, string value) => form.GetTextBox(controlName).Text = value;
    public void SetFlag(string controlName, bool value) => form.GetCheckBox(controlName).Checked = value;
    public void SelectValue<T>(string controlName, T value) => form.GetComboBox(controlName).SelectedItem = value;
    public void SetDateOverride(string controlName, bool enabled) => form.GetDatePicker(controlName).Checked = enabled;

    public void EnterDate(string controlName, DateTime value)
    {
        var picker = form.GetDatePicker(controlName);
        picker.GetMaskedTextBox("dateTimeEditor").Text = value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        picker.GetCheckBox("includeDateCheckBox").Checked = true;
    }

    public void ResetOptions(string command, string resetButtonName)
    {
        commands.SelectedItem = command;
        form.GetButton(resetButtonName).PerformClick();
    }

    // Every submission uses the public button and verifies the actual TCP request,
    // checksum and transcript entry, including when a test sends more than once.
    public string SubmitAndVerifyLog(string command)
    {
        commands.SelectedItem = command;
        Assert.IsTrue(submit.Visible && submit.Enabled, "The command's submit button must be available.");
        var previousCount = probe.Messages.Length;
        var previousLogCount = SentLogCount();
        var sharedItem = itemBarcode.Text;

        submit.PerformClick();

        var captured = probe.Messages;
        Assert.AreEqual(previousCount + 1, captured.Length, "The button should submit exactly one request.");
        var request = captured[^1];
        Assert.IsTrue(form.SipClient.CheckChecksum(request), "The captured request checksum is invalid.");
        Assert.AreEqual(previousLogCount + 1, SentLogCount(), "The request should add exactly one sent-message log entry.");
        StringAssert.Contains(messageLog.Text, request.TrimEnd('\r'));
        Assert.AreEqual(sharedItem, itemBarcode.Text, "Submitting must preserve the shared item barcode.");
        return request;
    }

    private int SentLogCount() => messageLog.Text.Split("--> ").Length - 1;

    internal static MainForm CreateConnectedForm(CommandProbe probe)
    {
        var form = new MainForm();
        try
        {
            foreach (var (name, value) in new[]
            {
                ("serverTextBox", "127.0.0.1"), ("portTextBox", probe.Port.ToString(CultureInfo.InvariantCulture)),
                ("aoTextBox", "AO-before"), ("apTextBox", "AP-before"),
                ("patronBarcodeTextBox", "patron"), ("patronPinTextBox", "pin"), ("itemBarcodeTextBox", "item"),
                ("usernameTextBox", "user"), ("passwordTextBox", "password"), ("locationCodeTextBox", "location")
            })
            {
                form.GetTextBox(name).Text = value;
            }
            form.GetComboBox("vendorProfileComboBox").Text = "vendor";
            WinFormsTestEnvironment.ShowOffscreen(form);
            form.GetButton("connectButton").PerformClick();
            Assert.IsTrue(form.SipClient?.IsConnected() == true, "The local test connection failed.");
            // These edits deliberately happen after connecting: requests must use current ribbon values.
            form.GetTextBox("aoTextBox").Text = "AO-live";
            form.GetTextBox("apTextBox").Text = "AP-live";
            return form;
        }
        catch
        {
            form.SipClient?.Dispose();
            form.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            form.SipClient?.Dispose();
            form.Dispose();
        }
        finally { probe.Dispose(); }
    }
}
