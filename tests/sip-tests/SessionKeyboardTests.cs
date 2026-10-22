using System.Globalization;
using Clc.Sip.Tests.TestSupport;
using static Clc.Sip.Tests.TestSupport.WinFormsTestEnvironment;

namespace Clc.Sip.Tests;

[STATestClass]
[TestCategory("Regression")]
public sealed class SessionKeyboardTests
{
    [ClassInitialize]
    public static void Initialize(TestContext _) => WinFormsTestEnvironment.Initialize();

    [TestMethod]
    [DataRow("serverTextBox")]
    [DataRow("portTextBox")]
    [DataRow("aoTextBox")]
    [DataRow("apTextBox")]
    public void EnterInConnectionFieldsConnectsOnce(string fieldName)
    {
        using var probe = new CommandProbe();
        using var form = new MainForm();
        form.GetTextBox("serverTextBox").Text = "127.0.0.1";
        form.GetTextBox("portTextBox").Text = probe.Port.ToString(CultureInfo.InvariantCulture);
        form.GetTextBox("aoTextBox").Text = "current-institution";
        form.GetTextBox("apTextBox").Text = "current-terminal";
        var connectClicks = 0;
        form.GetButton("connectButton").Click += (_, _) => connectClicks++;
        ShowOffscreen(form);
        try
        {
            Assert.IsTrue(PressEnter(form.GetControl(fieldName)), "Enter should be consumed without a beep.");
            Assert.AreEqual(1, connectClicks);
            Assert.IsTrue(form.SipClient?.IsConnected() == true);
            Assert.AreEqual("current-institution", form.SipClient.AO);
            Assert.AreEqual("current-terminal", form.SipClient.AP);
            Assert.IsTrue(form.GetButton("loginButton").Enabled);
            Assert.IsEmpty(probe.Messages, "Connecting should not also log in or execute a command.");
            AssertTabMovesTo(form.GetButton("connectButton"), form.GetTextBox("usernameTextBox"));
        }
        finally { form.SipClient?.Dispose(); }
    }

    [TestMethod]
    [DataRow("usernameTextBox")]
    [DataRow("passwordTextBox")]
    [DataRow("locationCodeTextBox")]
    [DataRow("vendorProfileComboBox")]
    [DataRow("loginUidAlgorithmTextBox")]
    [DataRow("loginPwdAlgorithmTextBox")]
    public void EnterInLoginFieldsSendsOneLoginWithCurrentValues(string fieldName)
    {
        using var probe = new CommandProbe();
        using var form = WorkbenchFixture.CreateConnectedForm(probe);
        // Ribbon fields must log in even when a different workbench command is selected.
        if (fieldName.StartsWith("login", StringComparison.Ordinal))
        {
            form.GetComboBox("commandComboBox").SelectedItem = "Login";
        }
        form.GetTextBox("usernameTextBox").Text = "edited-user";
        form.GetTextBox("passwordTextBox").Text = "edited-password";
        form.GetTextBox("locationCodeTextBox").Text = "edited-location";
        form.GetComboBox("vendorProfileComboBox").Text = "custom-profile";
        try
        {
            Assert.IsTrue(PressEnter(form.GetControl(fieldName)));
            Assert.AreEqual("9300CNedited-user|COedited-password|CPedited-location|VPcustom-profile|",
                SipRequest.Payload(probe.CaptureSingleRequest(form.SipClient)));
            Assert.AreEqual(1, form.GetRichTextBox("logRichTextBox").Text.Split("--> ").Length - 1);
            AssertTabMovesTo(form.GetButton("loginButton"), form.GetTextBox("patronBarcodeTextBox"));
        }
        finally { form.SipClient.Dispose(); }
    }

    [TestMethod]
    public void EnterBeforeConnectingRespectsDisabledLogin()
    {
        using var form = new MainForm();
        form.GetComboBox("commandComboBox").SelectedItem = "Login";
        var loginClicks = 0;
        form.GetButton("loginButton").Click += (_, _) => loginClicks++;
        ShowOffscreen(form);
        foreach (var name in new[] { "usernameTextBox", "passwordTextBox", "locationCodeTextBox",
            "vendorProfileComboBox", "loginUidAlgorithmTextBox", "loginPwdAlgorithmTextBox" })
        {
            var field = form.GetControl(name);
            Assert.IsTrue(PressEnter(field));
            Assert.IsTrue(field.ContainsFocus, "A disabled Login button should leave focus in the field.");
        }
        Assert.AreEqual(0, loginClicks);
        Assert.IsNull(form.SipClient);
        Assert.AreEqual("", form.GetRichTextBox("logRichTextBox").Text);
    }

    [TestMethod]
    [DataRow("loginUidAlgorithmTextBox")]
    [DataRow("loginPwdAlgorithmTextBox")]
    public void EnterWithInvalidLoginOptionsFocusesTheFieldNeedingCorrection(string invalidFieldName)
    {
        using var probe = new CommandProbe();
        using var form = WorkbenchFixture.CreateConnectedForm(probe);
        var invalidField = form.GetTextBox(invalidFieldName);
        invalidField.Text = "";
        try
        {
            Assert.IsTrue(PressEnter(form.GetTextBox("usernameTextBox")));
            Assert.IsTrue(invalidField.Focused, "Validation must retain focus on the invalid field.");
            Assert.AreEqual("Login", form.GetComboBox("commandComboBox").SelectedItem);
            Assert.IsTrue(PressEnter(invalidField));
            Assert.IsTrue(invalidField.Focused);
            Assert.IsEmpty(probe.Messages);
        }
        finally { form.SipClient.Dispose(); }
    }

    [TestMethod]
    public void OpenVendorListAcceptsAChoiceBeforeEnterCanLogIn()
    {
        using var probe = new CommandProbe();
        using var form = WorkbenchFixture.CreateConnectedForm(probe);
        var profiles = form.GetComboBox("vendorProfileComboBox");
        try
        {
            Assert.IsTrue(profiles.Focus());
            profiles.DroppedDown = true;
            profiles.SelectedItem = "ENVISIONWARE";
            Assert.IsTrue(profiles.DroppedDown);
            var message = EnterMessage(profiles);
            // Leave the key available to the native list instead of submitting Login.
            Assert.IsFalse(profiles.PreProcessMessage(ref message));
            Assert.IsEmpty(probe.Messages);
            Assert.IsTrue(profiles.ContainsFocus, "Accepting a profile should keep focus in the list.");

            profiles.DroppedDown = false;
            Assert.IsTrue(PressEnter(profiles));
            StringAssert.Contains(probe.CaptureSingleRequest(form.SipClient), "VPENVISIONWARE|");
            AssertTabMovesTo(form.GetButton("loginButton"), form.GetTextBox("patronBarcodeTextBox"));
        }
        finally { form.SipClient.Dispose(); }
    }

    [TestMethod]
    [DataRow("patronBarcodeTextBox")]
    [DataRow("patronPinTextBox")]
    [DataRow("itemBarcodeTextBox")]
    [DataRow("checkoutTerminalPasswordTextBox")]
    [DataRow("logRichTextBox")]
    public void EnterElsewhereDoesNotConnectOrLogIn(string fieldName)
    {
        using var probe = new CommandProbe();
        using var form = WorkbenchFixture.CreateConnectedForm(probe);
        var client = form.SipClient;
        var sessionClicks = 0;
        form.GetButton("connectButton").Click += (_, _) => sessionClicks++;
        form.GetButton("loginButton").Click += (_, _) => sessionClicks++;
        try
        {
            PressEnter(form.GetControl(fieldName));
            Assert.AreEqual(0, sessionClicks);
            Assert.AreSame(client, form.SipClient);
            Assert.IsEmpty(probe.Messages);
        }
        finally { form.SipClient.Dispose(); }
    }

    [TestMethod]
    [DataRow("SIP")]
    [DataRow("ENVISIONWARE")]
    [DataRow("COMPRISE")]
    [DataRow("3M")]
    [DataRow("ITG")]
    public void VendorChoicesSendDocumentedProfileValues(string profile)
    {
        using var probe = new CommandProbe();
        using var form = WorkbenchFixture.CreateConnectedForm(probe);
        var profiles = form.GetComboBox("vendorProfileComboBox");
        try
        {
            Assert.IsTrue(profiles.Items.Contains(profile), "A documented Polaris vendor profile is missing.");
            Assert.AreEqual(ComboBoxStyle.DropDown, profiles.DropDownStyle, "Custom profiles must remain editable.");
            profiles.SelectedItem = profile;
            form.GetButton("loginButton").PerformClick();
            StringAssert.Contains(probe.CaptureSingleRequest(form.SipClient), "VP" + profile + "|");
        }
        finally { form.SipClient.Dispose(); }
    }

    private static void AssertTabMovesTo(Button button, Control nextField)
    {
        Assert.IsTrue(button.Focused, "Enter should focus " + button.Name + ".");
        var message = Message.Create(button.Handle, 0x0100, (IntPtr)Keys.Tab, IntPtr.Zero); // WM_KEYDOWN
        Assert.IsTrue(button.PreProcessMessage(ref message));
        Assert.IsTrue(nextField.ContainsFocus, "Tab should advance directly to " + nextField.Name + ".");
    }

    private static bool PressEnter(Control field)
    {
        Assert.IsTrue(field.Focus(), field.Name + " could not receive keyboard focus.");
        var message = EnterMessage(field);
        // Exercise WinForms key preprocessing, including the port's numeric key filter
        // and the editable combo's native handling, without sending global keystrokes.
        return field.PreProcessMessage(ref message);
    }

    private static Message EnterMessage(Control field) => Message.Create(field.Handle, 0x0100,
        (IntPtr)Keys.Enter, IntPtr.Zero); // WM_KEYDOWN
}
