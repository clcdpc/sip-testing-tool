using System.Globalization;
using System.Reflection;
using Clc.Sip.Tests.TestSupport;

namespace Clc.Sip.Tests;

[STATestClass]
[TestCategory("Form submission")]
public sealed partial class FormSubmissionTests
{
    [ClassInitialize]
    public static void Initialize(TestContext _) => WinFormsTestEnvironment.Initialize();

    public static IEnumerable<object[]> SubmissionCases =>
        AllSubmissionCases().Select(test => new object[] { test.Command, test.Label });

    [TestMethod]
    [DynamicData(nameof(SubmissionCases), DynamicDataDisplayName = nameof(SubmissionName))]
    public void CommandOptionsReachSubmittedRequest(string command, string option)
    {
        var test = AllSubmissionCases().Single(test => test.Command == command && test.Label == option);
        var previousCulture = CultureInfo.CurrentCulture;
        using var probe = new CommandProbe();
        using var form = WorkbenchFixture.CreateConnectedForm(probe);
        try
        {
            if (test.Culture != null)
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(test.Culture);
            }

            VerifyFormSubmission(form, probe, test);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            form.SipClient?.Dispose();
        }
    }

    public static string SubmissionName(MethodInfo _, object[] data) => $"{data[0]}: {data[1]}";

    private static void VerifyFormSubmission(MainForm form, CommandProbe probe, SubmissionCase test)
    {
        var context = $"{test.Command} / {test.Label}";
        var defaults = submissionDefaults[test.Command];
        var expectedDate = PrepareSubmissionForm(form, test, defaults);
        var messageLog = form.GetRichTextBox("logRichTextBox");
        var before = DateTime.Now;
        var previousCount = probe.Messages.Length;
        var previousLogCount = messageLog.Text.Split("--> ").Length - 1;
        var submit = form.GetButton(test.RibbonLogin ? "loginButton" : "executeCommandButton");
        Assert.IsTrue(submit.Visible && submit.Enabled, context + ": The submit button is not available.");
        submit.PerformClick();
        var after = DateTime.Now;
        var sent = probe.Messages;
        Assert.AreEqual(previousCount + 1, sent.Length, context);
        var message = sent[^1];
        var header = test.Header ?? defaults.Header;
        Assert.AreEqual(header, message[..header.Length], context);
        Assert.AreEqual(test.Variables ?? defaults.Variables, SipRequest.VariableFields(message, defaults.VariablesOffset), context);
        switch (test.Command)
        {
            case "Checkout":
                Assert.AreEqual(expectedDate ?? BlankDateWire, message.Substring(22, 18), context);
                break;
            case "Checkin":
                if (expectedDate == null)
                {
                    AssertSubmissionTime(message.Substring(21, 18), before, after, context);
                }
                else
                {
                    Assert.AreEqual(expectedDate, message.Substring(21, 18), context);
                }

                break;
            case "Renew":
                Assert.AreEqual(expectedDate, message.Substring(22, 18), context);
                break;
            case "Patron information":
                Assert.AreEqual(test.Summary ?? new string(' ', 10), message.Substring(23, 10), context);
                break;
            case "Fee paid":
                Assert.AreEqual(test.FixedTail ?? "0102USD", message.Substring(20, 7), context);
                break;
            case "Notice update":
                if (expectedDate == null)
                {
                    AssertSubmissionTime(message.Substring(22, 18), before, after, context);
                }
                else
                {
                    Assert.AreEqual(expectedDate, message.Substring(22, 18), context);
                }

                Assert.AreEqual(test.FixedTail ?? "000000", message.Substring(40, 6), context);
                break;
        }
        var timestampOffset = test.Command switch
        {
            "Checkout" or "Renew" or "Notice update" => 4,
            "Checkin" => 3,
            "Patron information" or "Patron status" => 5,
            "Login" or "ACS Status" => -1,
            _ => 2
        };
        if (timestampOffset >= 0)
        {
            AssertSubmissionTime(message.Substring(timestampOffset, 18), before, after, context);
        }

        Assert.IsTrue(form.SipClient.CheckChecksum(message), context + ": The submitted message has an invalid checksum.");
        var transcript = messageLog.Text;
        Assert.AreEqual(previousLogCount + 1, transcript.Split("--> ").Length - 1, context);
        Assert.IsTrue(transcript.Contains(message.TrimEnd('\r')), context + ": The submitted packet is missing from the message log.");
    }

    private static string PrepareSubmissionForm(MainForm form, SubmissionCase test, SubmissionSpec defaults)
    {
        var commands = form.GetComboBox("commandComboBox");
        commands.SelectedItem = test.Command;
        var prefix = defaults.ResetPrefix;
        form.GetButton("reset" + char.ToUpperInvariant(prefix[0]) + prefix[1..] + "OptionsButton").PerformClick();
        // Restore shared values independently of command resets. Test cases
        // can then edit them after connecting to verify the latest values win.
        foreach (var (name, value) in new[]
        {
            ("aoTextBox", "AO-live"), ("apTextBox", "AP-live"), ("patronBarcodeTextBox", "patron"),
            ("patronPinTextBox", "pin"), ("itemBarcodeTextBox", "item"), ("usernameTextBox", "user"),
            ("passwordTextBox", "password"), ("locationCodeTextBox", "location")
        })
        {
            SetText(form, name, value);
        }
        form.GetComboBox("vendorProfileComboBox").Text = "vendor";
        if (test.Command == "Fee paid")
        {
            SetText(form, "feeTypeTextBox", "1");
            SetText(form, "paymentTypeTextBox", "2");
            SetText(form, "amountTextBox", 12.5.ToString(CultureInfo.CurrentCulture));
        }
        test.Edit?.Invoke(form);
        var expectedDate = test.Date;
        if (expectedDate == null && test.Command == "Renew")
        {
            expectedDate = form.GetDatePicker("renewDueDatePicker").Value.ToString("yyyyMMdd    HHmmss", CultureInfo.InvariantCulture);
        }

        if (test.RibbonLogin)
        {
            commands.SelectedItem = "Item information";
        }

        return expectedDate;
    }

    private static void AssertSubmissionTime(string value, DateTime before, DateTime after, string context)
    {
        Assert.IsTrue(DateTime.TryParseExact(value, "yyyyMMdd    HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            && parsed >= before.AddSeconds(-1) && parsed <= after.AddSeconds(1), context + ": The packet timestamp did not come from this submission: " + value);
    }

    private static void SetText(MainForm form, string name, string value) => form.GetTextBox(name).Text = value;

    private static void SetFlag(MainForm form, string name, bool value) => form.GetCheckBox(name).Checked = value;

    private static void SetChoice(MainForm form, string name, int index) => form.GetComboBox(name).SelectedIndex = index;

    private static void SetDateText(MainForm form, string name, bool include, string text = SubmittedDateText)
    {
        var picker = form.GetDatePicker(name);
        picker.GetMaskedTextBox("dateTimeEditor").Text = text;
        picker.GetCheckBox("includeDateCheckBox").Checked = include;
    }
}
