using System.Net;
using System.Net.Sockets;
using System.Text;
using Clc.Sip.Helpers;
using Clc.Sip.Tests.TestSupport;
using static Clc.Sip.Tests.TestSupport.WinFormsTestEnvironment;

namespace Clc.Sip.Tests;

[STATestClass]
[TestCategory("Regression")]
public sealed class CheckoutTests
{
    [ClassInitialize]
    public static void Initialize(TestContext _) => WinFormsTestEnvironment.Initialize();

    [TestMethod(DisplayName = "Checkout fields and compatible defaults over TCP")]
    public void CheckoutSerializesOptionsAndCompatibleDefaults()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var server = Task.Run(() =>
        {
            using var peer = listener.AcceptTcpClient();
            var requests = new List<string>();
            for (var i = 0; i < 4; i++)
            {
                requests.Add(SipRequest.ReadFrom(peer));
                peer.GetStream().Write(Encoding.ASCII.GetBytes("98YAY0AZFDD1\r"));
            }
            return requests;
        });
        using var client = new sip2("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, "test", "desk");
        client.Connect();
        _ = client.ItemCheckOut("patron", "pin", "item");
        _ = client.ItemCheckOut("patron", "pin", "item", new DateTime(2026, 10, 28, 14, 30, 45),
            renewalOk: "N", itemProperties: "test-properties", feeAcknowledged: "Y", noBlock: "Y", cancel: "Y", terminalPassword: "terminal-secret");
        _ = client.ItemCheckOut("patron", "pin", "item", cancel: "N");
        _ = client.ItemCheckOut("patron", "pin", "item", feeAcknowledged: "");
        var sent = server.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        Assert.AreEqual("11YN", sent[0][..4]);
        Assert.AreEqual(new string(' ', 18), sent[0].Substring(22, 18));
        Assert.AreEqual("AOtest|AApatron|ABitem|AC|ADpin|BON|", SipRequest.VariableFields(sent[0], fixedLength: 40));
        Assert.AreEqual("11NY", sent[1][..4]);
        Assert.AreEqual("20261028    143045", sent[1].Substring(22, 18));
        Assert.AreEqual("AOtest|AApatron|ABitem|ACterminal-secret|ADpin|CHtest-properties|BOY|BIY|", SipRequest.VariableFields(sent[1], fixedLength: 40));
        Assert.AreEqual("AOtest|AApatron|ABitem|AC|ADpin|BON|BIN|", SipRequest.VariableFields(sent[2], fixedLength: 40));
        Assert.AreEqual("AOtest|AApatron|ABitem|AC|ADpin|", SipRequest.VariableFields(sent[3], fixedLength: 40));
        foreach (var message in sent)
        {
            Assert.IsTrue(client.CheckChecksum(message), "Checkout options broke the request checksum.");
        }
    }

    [TestMethod(DisplayName = "Checkout option state, reset and scrolling")]
    public void CheckoutOptionsPersistResetAndFitScrollableWorkbench()
    {
        using var form = new MainForm();
        ShowOffscreen(form);
        var commands = form.GetComboBox("commandComboBox");
        var options = form.GetTableLayoutPanel("checkoutFieldsLayout");
        var date = form.GetDatePicker("checkoutDueDatePicker");
        Assert.IsTrue(options.Visible, "Checkout options should appear for the initial command.");
        Assert.IsTrue(form.GetCheckBox("checkoutRenewalCheckBox").Checked, "Default renewal policy changed.");
        Assert.IsTrue(!form.GetCheckBox("checkoutNoBlockCheckBox").Checked && !date.Checked, "No-block should be disabled by default.");
        Assert.AreEqual(1, form.GetComboBox("checkoutFeeAcknowledgedComboBox").SelectedIndex);
        Assert.AreEqual(0, form.GetComboBox("checkoutCancelComboBox").SelectedIndex);
        form.GetTextBox("patronBarcodeTextBox").Text = "retain-patron";
        form.LogOutput(new()
        {
            Message = "11test\r",
            Response = "12test\r"
        });
        var history = form.GetRichTextBox("logRichTextBox").Text;
        CheckoutTestData.ApplySelectedOptions(form);
        commands.SelectedItem = "Item information";
        Assert.IsFalse(options.Visible, "Checkout options leaked into another command.");
        commands.SelectedItem = "Checkout";
        Assert.IsTrue(options.Visible && date.Checked && form.GetCheckBox("checkoutNoBlockCheckBox").Checked, "Switching commands lost checkout options.");
        Assert.AreEqual("test-properties", form.GetTextBox("checkoutItemPropertiesTextBox").Text);
        Assert.AreEqual("terminal-secret", form.GetTextBox("checkoutTerminalPasswordTextBox").Text);
        var terminalPassword = form.GetTextBox("checkoutTerminalPasswordTextBox");
        Assert.IsTrue(!terminalPassword.UseSystemPasswordChar && terminalPassword.PasswordChar == '\0', "The terminal password should use ordinary readable text.");
        Assert.AreEqual(2, form.GetComboBox("checkoutFeeAcknowledgedComboBox").SelectedIndex);
        var panel = form.GetPanel("commandDetailsPanel");
        var reset = form.GetButton("resetCheckoutOptionsButton");
        foreach (var size in new[] { form.MinimumSize, new System.Drawing.Size(1280, 820) })
        {
            form.Size = size;
            form.GetSplitContainer("splitContainer1").SplitterDistance = 280;
            LayoutTree(form);
            Application.DoEvents();
            Assert.IsFalse(panel.HorizontalScroll.Visible, "Checkout fields should fit the narrow workbench.");
            foreach (var child in options.Controls.Cast<Control>())
            {
                Assert.IsTrue(child.Right <= options.ClientSize.Width, child.Name + " is clipped horizontally.");
            }

            panel.ScrollControlIntoView(reset);
            Assert.IsTrue(panel.ClientRectangle.Contains(BoundsIn(reset, panel)), "The last option should be reachable by scrolling.");
            var execute = form.GetButton("executeCommandButton");
            Assert.IsTrue(execute.Parent.ClientRectangle.Contains(execute.Bounds), "Execute should remain outside the scrolling options.");
            Assert.IsTrue(form.GetRichTextBox("logRichTextBox").Height > 300, "Checkout options displaced the message log.");
        }
        reset.PerformClick();
        Assert.IsTrue(form.GetCheckBox("checkoutRenewalCheckBox").Checked && !form.GetCheckBox("checkoutNoBlockCheckBox").Checked && !date.Checked,
            "Reset did not restore the checkout flags and blank date.");
        Assert.AreEqual("", form.GetTextBox("checkoutItemPropertiesTextBox").Text);
        Assert.AreEqual("", form.GetTextBox("checkoutTerminalPasswordTextBox").Text);
        Assert.AreEqual(1, form.GetComboBox("checkoutFeeAcknowledgedComboBox").SelectedIndex);
        Assert.AreEqual(0, form.GetComboBox("checkoutCancelComboBox").SelectedIndex);
        Assert.AreEqual("retain-patron", form.GetTextBox("patronBarcodeTextBox").Text);
        Assert.AreEqual(history, form.GetRichTextBox("logRichTextBox").Text);
    }

    [TestMethod(DisplayName = "Checkout options execute from the workbench")]
    public void CheckoutSubmitsCurrentOptionsAndRejectsInvalidDates()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var server = Task.Run(() =>
        {
            using var peer = listener.AcceptTcpClient();
            var requests = new List<string>();
            for (var i = 0; i < 4; i++)
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
        form.GetTextBox("aoTextBox").Text = "test";
        form.GetTextBox("patronBarcodeTextBox").Text = "patron";
        form.GetTextBox("patronPinTextBox").Text = "pin";
        form.GetTextBox("itemBarcodeTextBox").Text = "item";
        form.GetButton("connectButton").PerformClick();
        try
        {
            var execute = form.GetButton("executeCommandButton");
            var dueDate = form.GetDatePicker("checkoutDueDatePicker");
            dueDate.Checked = true;
            dueDate.GetMaskedTextBox("dateTimeEditor").Text = "2026-02-30 12:30:00";
            execute.PerformClick();
            Assert.IsTrue(form.GetStatusStrip("statusStrip1").Items[0].Text.Contains("valid no-block due date/time"), "Invalid dates should stop checkout before a request is sent.");
            form.GetButton("resetCheckoutOptionsButton").PerformClick();
            execute.PerformClick();
            CheckoutTestData.ApplySelectedOptions(form);
            execute.PerformClick();
            form.GetComboBox("checkoutFeeAcknowledgedComboBox").SelectedIndex = 0;
            form.GetComboBox("checkoutCancelComboBox").SelectedIndex = 0;
            form.GetDatePicker("checkoutDueDatePicker").Checked = false;
            execute.PerformClick();
            form.GetButton("resetCheckoutOptionsButton").PerformClick();
            execute.PerformClick();
            var sent = server.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            Assert.AreEqual("11YN", sent[0][..4]);
            Assert.AreEqual(new string(' ', 18), sent[0].Substring(22, 18));
            Assert.AreEqual("AOtest|AApatron|ABitem|AC|ADpin|BON|", SipRequest.VariableFields(sent[0], fixedLength: 40));
            Assert.AreEqual("11NY", sent[1][..4]);
            Assert.AreEqual("20261028    143045", sent[1].Substring(22, 18));
            Assert.AreEqual("AOtest|AApatron|ABitem|ACterminal-secret|ADpin|CHtest-properties|BOY|BIY|", SipRequest.VariableFields(sent[1], fixedLength: 40));
            Assert.AreEqual(new string(' ', 18), sent[2].Substring(22, 18));
            Assert.IsTrue(!SipRequest.VariableFields(sent[2], fixedLength: 40).Contains("BO") && !SipRequest.VariableFields(sent[2], fixedLength: 40).Contains("BI"), "Omitted flags were sent.");
            Assert.AreEqual(SipRequest.VariableFields(sent[0], fixedLength: 40), SipRequest.VariableFields(sent[3], fixedLength: 40));
            Assert.AreEqual("11YN", sent[3][..4]);
            var log = form.GetRichTextBox("logRichTextBox").Text;
            Assert.AreEqual(4, log.Split("--> ").Length - 1);
            Assert.IsTrue(log.Contains("CHtest-properties|BOY|BIY|"), "The selected checkout fields did not reach the raw message log.");
        }
        finally { form.SipClient?.Dispose(); }
    }
}
