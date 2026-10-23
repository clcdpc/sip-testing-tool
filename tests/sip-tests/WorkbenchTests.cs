using System.Net;
using System.Net.Sockets;
using System.Text;
using Clc.Sip.Helpers;
using Clc.Sip.Tests.TestSupport;
using static Clc.Sip.Tests.TestSupport.WinFormsTestEnvironment;

namespace Clc.Sip.Tests;

[STATestClass]
[TestCategory("Regression")]
public sealed class WorkbenchTests
{
    [ClassInitialize]
    public static void Initialize(TestContext _) => WinFormsTestEnvironment.Initialize();

    [TestMethod(DisplayName = "Windows forms and resources")]
    public void FormsLoadResourcesAndKeepPasswordFieldsReadable()
    {
        using var form = new MainForm();
        Assert.AreEqual("SIP Testing Tool", form.Text);
        Assert.IsTrue(form.Icon != null, "Main form icon resource is missing.");
        var commands = form.GetComboBox("commandComboBox");
        Assert.AreEqual(12, commands.Items.Count);
        Assert.IsTrue(commands.Items.Contains("ACS Status") && commands.Items.Contains("Notice update"), "An existing SIP command is missing.");
        Assert.IsTrue(!form.GetButton("executeCommandButton").Enabled && !form.GetButton("loginButton").Enabled, "Commands must start disabled.");
        Assert.AreEqual("", form.GetTextBox("portTextBox").Text);
        ((ToolStripMenuItem)form.ContextMenuStrip.Items[0]).PerformClick();
        Assert.IsTrue(form.TopMost);
        using var settings = new SettingsForm();
        using var noticeForm = new NoticeUpdateForm(form);
        Assert.IsTrue(settings.Controls.Count > 0 && noticeForm.Controls.Count > 0, "Supporting form resources are missing.");
        foreach (var window in new Form[] { form, settings })
        {
            foreach (var name in new[] { "passwordTextBox", "patronPinTextBox" })
            {
                var input = window.GetTextBox(name);
                Assert.IsTrue(!input.UseSystemPasswordChar && input.PasswordChar == '\0', name + " should be readable and copyable like every other field.");
            }
        }
    }

    [TestMethod(DisplayName = "Ribbon layout and resizing")]
    public void RibbonAndLogRemainUsableWhenResized()
    {
        using var form = new MainForm();
        string[] shared = { "serverTextBox", "portTextBox", "aoTextBox", "apTextBox", "usernameTextBox", "passwordTextBox", "locationCodeTextBox", "vendorProfileComboBox", "patronBarcodeTextBox", "patronPinTextBox", "itemBarcodeTextBox" };
        foreach (var size in new[] { form.MinimumSize, new System.Drawing.Size(1280, 820), new System.Drawing.Size(1600, 1000) })
        {
            form.Size = size;
            LayoutTree(form);
            var split = form.GetSplitContainer("splitContainer1");
            Assert.AreEqual(Orientation.Vertical, split.Orientation);
            Assert.IsTrue(split.Panel2.Width > split.Panel1.Width, "The message log should receive most of the width.");
            foreach (var name in shared)
            {
                var field = form.GetControl(name);
                Assert.IsTrue(field.Width >= 60, name + " is too narrow.");
                Assert.IsTrue(field.Parent.ClientRectangle.Contains(field.Bounds), name + " extends beyond its shared group.");
                Assert.IsTrue(BoundsIn(field, form).Bottom < BoundsIn(split, form).Top, name + " is not in the shared ribbon.");
            }
            var firstRow = new[] { "serverTextBox", "usernameTextBox", "patronBarcodeTextBox", "itemBarcodeTextBox" }.Select(name => BoundsIn(form.GetControl(name), form).Top).Distinct();
            Assert.AreEqual(1, firstRow.Count());
            var execute = form.GetButton("executeCommandButton");
            Assert.IsTrue(execute.Parent.ClientRectangle.Contains(execute.Bounds), "Execute is clipped.");
            Assert.IsTrue(form.GetRichTextBox("logRichTextBox").Height > 300, "The transcript is too small.");
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OptionalFieldsUseInlineLabelsAndReflowWithoutClipping(bool dark)
    {
        var previousTheme = AppTheme.IsDarkMode;
        AppTheme.SetDarkMode(dark);
        try
        {
            using var form = new MainForm();
            ShowOffscreen(form);
            var commands = form.GetComboBox("commandComboBox");
            var split = form.GetSplitContainer("splitContainer1");
            foreach (var command in commands.Items.Cast<string>().ToArray())
            {
                commands.SelectedItem = command;
                foreach (var width in new[] { 280, 312, 500 })
                {
                    split.SplitterDistance = width;
                    LayoutTree(form);
                    foreach (var row in Descendants(form).OfType<Panel>().Where(control => control.Visible && control.Name.EndsWith("Row", StringComparison.Ordinal)))
                    {
                        var label = row.Controls.OfType<Label>().Single();
                        var input = row.Controls.Cast<Control>().Single(control => control != label);
                        Assert.IsTrue(input.Width > 0 && input.Height > 0, input.Name + " must remain visible.");
                        Assert.IsTrue(row.ClientRectangle.Contains(input.Bounds), $"{command} at {width}: {input.Name} {input.Bounds} is clipped by {row.ClientRectangle}.");
                        Assert.IsTrue(row.ClientRectangle.Contains(label.Bounds), label.Text + " is clipped.");
                        Assert.IsFalse(input.Bounds.IntersectsWith(label.Bounds), label.Text + " overlaps its input.");
                    }
                    if (command == "Renew")
                    {
                        var title = form.GetTextBox("renewTitleTextBox");
                        Assert.IsTrue(title.Left > 0, "The short title label should fit beside its input.");
                        var date = form.GetDatePicker("renewDueDatePicker");
                        Assert.AreEqual(width == 500, date.Left > 0, "Date inputs need a full row until the workbench is wide enough.");
                        var fee = form.GetComboBox("renewFeeComboBox");
                        var reset = form.GetButton("resetRenewOptionsButton");
                        Assert.IsTrue(reset.Top - fee.Parent.Bottom < 24, $"At {width}: resizing should reclaim space above Reset; gap is {reset.Top - fee.Parent.Bottom}.");
                        title.Text = "retained while resizing";
                        if (width == 312)
                        {
                            using var image = new Bitmap(form.Width, form.Height);
                            form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                            image.Save(Path.Combine(AppContext.BaseDirectory, dark ? "optional-fields-dark.png" : "optional-fields-light.png"));
                        }
                    }
                }
            }
            commands.SelectedItem = "Renew";
            Assert.AreEqual("retained while resizing", form.GetTextBox("renewTitleTextBox").Text);
        }
        finally { AppTheme.SetDarkMode(previousTheme); }

        static IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;
                foreach (var descendant in Descendants(child)) { yield return descendant; }
            }
        }
    }

    [TestMethod(DisplayName = "Command fields and log survive selection")]
    public void CommandFieldsAndMessageHistorySurviveSelectionChanges()
    {
        using var form = new MainForm();
        var commands = form.GetComboBox("commandComboBox");
        var summary = form.GetComboBox("patronSummaryComboBox");
        var startItem = form.GetTextBox("bpTextBox");
        var endItem = form.GetTextBox("bqTextBox");
        var amount = form.GetTextBox("amountTextBox");
        Assert.AreEqual(7, summary.Items.Count);
        Assert.IsTrue(startItem.Enabled, "Optional ranges should be editable with or without a summary.");
        commands.SelectedItem = "Patron information";
        summary.SelectedIndex = 1;
        startItem.Text = "2";
        endItem.Text = "8";
        commands.SelectedItem = "Fee paid";
        amount.Text = "7.50";
        form.LogOutput(new SipTransaction { Message = "17AOtest|\r", Response = "18ABitem|\r" });
        commands.SelectedItem = "Checkout";
        commands.SelectedItem = "Patron information";
        Assert.AreEqual(1, summary.SelectedIndex);
        Assert.AreEqual("2", startItem.Text);
        Assert.AreEqual("8", endItem.Text);
        Assert.IsTrue(startItem.Enabled, "A selected summary must allow ranges.");
        commands.SelectedItem = "Fee paid";
        Assert.AreEqual("7.50", amount.Text);
        var log = form.GetRichTextBox("logRichTextBox");
        Assert.AreEqual("--> 17AOtest|\n<-- 18ABitem|", log.Text);
        form.LogOutput(new SipTransaction { Message = "99\r", Response = "98\r" });
        Assert.IsTrue(log.Text.EndsWith("\n\n--> 99\n<-- 98"), "Sent/received history must append in order.");
    }

    [TestMethod(DisplayName = "Workbench dispatch over TCP")]
    public void WorkbenchButtonsDispatchSupportedCommandsOverTcp()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var expected = new[] { "11", "09", "29", "65", "17", "63", "23", "35", "37", "93", "99" };
        var server = Task.Run(() =>
        {
            using var peer = listener.AcceptTcpClient();
            var requests = new List<string>();
            foreach (var _ in expected)
            {
                requests.Add(SipRequest.ReadFrom(peer));
                peer.GetStream().Write(Encoding.ASCII.GetBytes("98YAY0AZFDD1\r"));
            }
            return requests;
        });
        using var form = new MainForm();
        ShowOffscreen(form);
        form.GetTextBox("serverTextBox").Text = "127.0.0.1";
        form.GetTextBox("portTextBox").Text = ((IPEndPoint)listener.LocalEndpoint).Port.ToString();
        form.GetTextBox("aoTextBox").Text = "test-institution";
        form.GetTextBox("apTextBox").Text = "test-terminal";
        form.GetTextBox("usernameTextBox").Text = "test-user";
        form.GetTextBox("passwordTextBox").Text = "test-password";
        form.GetTextBox("locationCodeTextBox").Text = "test-location";
        form.GetComboBox("vendorProfileComboBox").Text = "test-vendor";
        form.GetTextBox("patronBarcodeTextBox").Text = "test-patron";
        form.GetTextBox("patronPinTextBox").Text = "test-pin";
        form.GetTextBox("itemBarcodeTextBox").Text = "test-item";
        form.GetTextBox("feeTypeTextBox").Text = "1";
        form.GetTextBox("paymentTypeTextBox").Text = "0";
        form.GetTextBox("amountTextBox").Text = "7.50";
        form.GetTextBox("feeIdentifierTextBox").Text = "test-fee";
        form.GetTextBox("transactionIdTextBox").Text = "test-transaction";
        form.GetComboBox("patronSummaryComboBox").SelectedIndex = 1;
        form.GetTextBox("bpTextBox").Text = "2";
        form.GetTextBox("bqTextBox").Text = "8";
        form.GetButton("connectButton").PerformClick();
        try
        {
            Assert.IsTrue(form.SipClient != null && form.GetButton("executeCommandButton").Enabled, "Connecting must enable execution.");
            var commands = form.GetComboBox("commandComboBox");
            foreach (var name in new[] { "Checkout", "Checkin", "Renew", "Renew all", "Item information", "Patron information", "Patron status", "End session", "Fee paid", "Login", "ACS Status" })
            {
                commands.SelectedItem = name;
                form.GetButton("executeCommandButton").PerformClick();
            }
            var requests = server.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.IsTrue(requests[i].StartsWith(expected[i]), "Wrong dispatcher for command " + i);
            }

            Assert.IsTrue(requests[0].Contains("AAtest-patron|") && requests[0].Contains("ABtest-item|"), "Checkout did not use shared fields.");
            Assert.IsTrue(requests[5].Contains("BP2|BQ8|"), "Patron ranges were lost.");
            Assert.IsTrue(requests[8].Contains("CGtest-fee|") && requests[8].Contains("BKtest-transaction|"), "Fee fields were lost.");
            Assert.IsTrue(requests[9].Contains("CNtest-user|") && requests[9].Contains("CPtest-location|") && requests[9].Contains("VPtest-vendor|"), "Login fields were lost.");
            Assert.AreEqual("09N", requests[1][..3]);
            Assert.AreEqual("29YN", requests[2][..4]);
            Assert.AreEqual("APtest-terminal|AOtest-institution|ABtest-item|AC|", SipRequest.VariableFields(requests[1], 39));
            Assert.AreEqual("AOtest-institution|AAtest-patron|ADtest-pin|ABtest-item|", SipRequest.VariableFields(requests[2], 40));
            Assert.AreEqual("AOtest-institution|AAtest-patron|ADtest-pin|", SipRequest.VariableFields(requests[3], 20));
            Assert.AreEqual("AOtest-institution|ABtest-item|AC|", SipRequest.VariableFields(requests[4], 20));
            Assert.AreEqual("AOtest-institution|AAtest-patron|AC|ADtest-pin|", SipRequest.VariableFields(requests[6], 23));
            Assert.AreEqual("AOtest-institution|AAtest-patron|ADtest-pin|", SipRequest.VariableFields(requests[7], 20));
            Assert.AreEqual("9901002.00", requests[10][..10]);
            Assert.AreEqual(expected.Length, form.GetRichTextBox("logRichTextBox").Text.Split("--> ").Length - 1);
        }
        finally { form.SipClient?.Dispose(); }
    }
}
