using Clc.Sip;
using Clc.Sip.Helpers;
using Clc.Sip.Tests.TestSupport;
using static Clc.Sip.Tests.TestSupport.WinFormsTestEnvironment;

namespace Clc.Sip.Tests;

[STATestClass]
[TestCategory("Regression")]
public sealed class CommandOptionStateTests
{
    [ClassInitialize]
    public static void Initialize(TestContext _) => WinFormsTestEnvironment.Initialize();

    [TestMethod(DisplayName = "Command option state, resets, themes and scrolling")]
    public void CommandOptionsPersistResetAndFitAcrossThemes()
    {
        var originalTheme = AppTheme.IsDarkMode;
        using var form = new MainForm();
        ShowOffscreen(form);
        var commands = form.GetComboBox("commandComboBox");
        var startItem = form.GetTextBox("bpTextBox");
        var endItem = form.GetTextBox("bqTextBox");
        var amount = form.GetTextBox("amountTextBox");
        var patronBarcode = form.GetTextBox("patronBarcodeTextBox");
        var checkoutProperties = form.GetTextBox("checkoutItemPropertiesTextBox");
        var messageLog = form.GetRichTextBox("logRichTextBox");
        var split = form.GetSplitContainer("splitContainer1");
        var scroll = form.GetPanel("commandDetailsPanel");
        var cases = new (string Command, string Prefix, TextBox Field, string Initial)[]
        {
            ("Checkin", "checkin", form.GetTextBox("checkinPropertiesTextBox"), ""),
            ("Renew", "renew", form.GetTextBox("renewTitleTextBox"), ""),
            ("Renew all", "renewAll", form.GetTextBox("renewAllPasswordTextBox"), ""),
            ("Item information", "itemInfo", form.GetTextBox("itemInfoPasswordTextBox"), ""),
            ("Patron information", "patronInfo", form.GetTextBox("patronInfoPasswordTextBox"), ""),
            ("Patron status", "patronStatus", form.GetTextBox("patronStatusPasswordTextBox"), ""),
            ("End session", "endSession", form.GetTextBox("endSessionPasswordTextBox"), ""),
            ("Fee paid", "fee", form.GetTextBox("feeCurrencyTextBox"), "USD"),
            ("Notice update", "notice", form.GetTextBox("noticeCommentTextBox"), ""),
            ("Login", "login", form.GetTextBox("loginUidAlgorithmTextBox"), "0"),
            ("ACS Status", "acs", form.GetTextBox("acsPrintWidthTextBox"), "100")
        };
        var expected = new Dictionary<TextBox, string>();
        foreach (var (_, prefix, field, _) in cases)
        {
            expected[field] = prefix == "login" ? "A" : prefix == "acs" ? "080" : prefix;
            field.Text = expected[field];
        }
        patronBarcode.Text = "retain-patron";
        checkoutProperties.Text = "retain-checkout";
        amount.Text = "25";
        startItem.Text = "2";
        endItem.Text = "8";
        form.LogOutput(new SipTransaction { Message = "17AOtest|\r", Response = "18ABitem|\r" });
        var history = messageLog.Text;
        try
        {
            foreach (var dark in new[] { true, false })
            {
                AppTheme.SetDarkMode(dark);
                foreach (var (command, prefix, field, _) in cases)
                {
                    commands.SelectedItem = command;
                    var options = form.GetTableLayoutPanel(prefix + "OptionsLayout");
                    Assert.IsTrue(options.Visible, command + " options were not shown.");
                    Assert.AreEqual(expected[field], field.Text);
                    foreach (var (_, otherPrefix, _, _) in cases)
                    {
                        if (otherPrefix != prefix)
                        {
                            Assert.IsFalse(form.GetTableLayoutPanel(otherPrefix + "OptionsLayout").Visible, "Another command's options are visible.");
                        }
                    }

                    foreach (var input in options.Controls.OfType<TextBox>())
                    {
                        ControlAppearanceAssertions.MatchCurrentTheme(input);
                        Assert.IsTrue(!input.UseSystemPasswordChar && input.PasswordChar == '\0', "A command password is masked.");
                    }
                    foreach (var size in new[] { form.MinimumSize, new Size(1280, 820) })
                    {
                        form.Size = size;
                        split.SplitterDistance = 280;
                        LayoutTree(form);
                        Application.DoEvents();
                        Assert.IsFalse(scroll.HorizontalScroll.Visible, command + " needs horizontal scrolling.");
                        var reset = form.GetButton("reset" + char.ToUpperInvariant(prefix[0]) + prefix[1..] + "OptionsButton");
                        scroll.ScrollControlIntoView(reset);
                        Assert.IsTrue(scroll.ClientRectangle.Contains(BoundsIn(reset, scroll)), command + " reset is unreachable.");
                        Assert.IsTrue(messageLog.Height > 300, "Options displaced the log.");
                    }
                }
            }
            foreach (var (command, prefix, field, initial) in cases)
            {
                commands.SelectedItem = command;
                form.GetButton("reset" + char.ToUpperInvariant(prefix[0]) + prefix[1..] + "OptionsButton").PerformClick();
                expected[field] = initial;
                foreach (var (_, _, otherField, _) in cases)
                {
                    Assert.AreEqual(expected[otherField], otherField.Text);
                }
            }
            Assert.AreEqual("", amount.Text);
            Assert.AreEqual("1", startItem.Text);
            Assert.AreEqual("5", endItem.Text);
            Assert.IsTrue(!form.GetDatePicker("checkinReturnDatePicker").Checked && !form.GetDatePicker("noticeDeliveryDatePicker").Checked, "Reset should restore automatic return/delivery dates.");
            Assert.IsTrue(form.GetDatePicker("renewDueDatePicker").Checked && form.GetCheckBox("renewThirdPartyCheckBox").Checked && form.GetCheckBox("renewUseItemCheckBox").Checked && !form.GetCheckBox("renewNoBlockCheckBox").Checked, "Renew defaults changed.");
            Assert.AreEqual("retain-patron", patronBarcode.Text);
            Assert.AreEqual("retain-checkout", checkoutProperties.Text);
            Assert.AreEqual(history, messageLog.Text);
        }
        finally { AppTheme.SetDarkMode(originalTheme); }
    }

    [TestMethod(DisplayName = "Invalid command options do not send requests")]
    public void InvalidCommandOptionsDoNotSendRequests()
    {
        using var probe = new CommandProbe();
        using var form = WorkbenchFixture.CreateConnectedForm(probe);
        var feeType = form.GetTextBox("feeTypeTextBox");
        var amount = form.GetTextBox("amountTextBox");
        var printWidth = form.GetTextBox("acsPrintWidthTextBox");
        var protocolVersion = form.GetTextBox("acsVersionTextBox");
        try
        {
            foreach (var (command, name) in new[] { ("Checkin", "checkinReturnDatePicker"), ("Renew", "renewDueDatePicker"), ("Notice update", "noticeDeliveryDatePicker") })
            {
                var date = form.GetDatePicker(name);
                date.Checked = true;
                date.GetMaskedTextBox("dateTimeEditor").Text = "2026-02-30 12:00:00";
                SelectAndExecute(form, command);
            }
            form.GetTextBox("patronStatusLanguageTextBox").Text = "xx";
            SelectAndExecute(form, "Patron status");
            form.GetTextBox("bpTextBox").Text = "8";
            form.GetTextBox("bqTextBox").Text = "2";
            SelectAndExecute(form, "Patron information");
            feeType.Text = "100";
            form.GetTextBox("paymentTypeTextBox").Text = "0";
            amount.Text = "1";
            SelectAndExecute(form, "Fee paid");
            feeType.Text = "1";
            amount.Text = "NaN";
            SelectAndExecute(form, "Fee paid");
            amount.Text = "1";
            form.GetTextBox("feeCurrencyTextBox").Text = "XX";
            SelectAndExecute(form, "Fee paid");
            form.GetTextBox("loginUidAlgorithmTextBox").Text = "";
            form.GetButton("loginButton").PerformClick();
            Assert.AreEqual("Login", form.GetComboBox("commandComboBox").Text);
            printWidth.Text = "-1";
            SelectAndExecute(form, "ACS Status");
            printWidth.Text = "100";
            protocolVersion.Text = "2.0";
            SelectAndExecute(form, "ACS Status");
            Assert.AreEqual(0, probe.Messages.Length);
            Assert.AreEqual("", form.GetRichTextBox("logRichTextBox").Text);
            protocolVersion.Text = "2.00";
            SelectAndExecute(form, "ACS Status");
            Assert.AreEqual(1, probe.Messages.Length);
            Assert.IsTrue(form.GetStatusStrip("statusStrip1").Items[0].Text.StartsWith("Connected to "), "A successful request should clear the earlier validation message.");
        }
        finally { form.SipClient?.Dispose(); }
    }

    [TestMethod(DisplayName = "Status and login field widths are validated")]
    public void StatusAndLoginRejectInvalidFixedWidthFields()
    {
        using var client = new sip2();
        foreach (var action in new Action[]
        {
            () => client.AcsStatus("3"), () => client.AcsStatus(maxPrintWidth: -1), () => client.AcsStatus(maxPrintWidth: 1000),
            () => client.AcsStatus(protocolVersion: "2.0"), () => client.Login("", "", uidAlgorithm: ""), () => client.Login("", "", pwdAlgorithm: "|"),
        })
        {
            Assert.Throws<ArgumentException>(action, "An invalid fixed field was accepted.");
        }
    }

    private static void SelectAndExecute(MainForm form, string command)
    {
        form.GetComboBox("commandComboBox").SelectedItem = command;
        form.GetButton("executeCommandButton").PerformClick();
    }
}
