using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Clc.Sip.Desktop.Models;
using Clc.Sip.Desktop.Services;
using Clc.Sip.Desktop.ViewModels;
using Clc.Sip.Desktop.Views;
using Clc.Sip.Desktop.Tests.TestSupport;
using static Clc.Sip.Desktop.Tests.TestSupport.SessionTestData;
using static Clc.Sip.Desktop.Tests.TestSupport.SipServer;

namespace Clc.Sip.Desktop.Tests;

[TestClass]
[TestCategory("Avalonia")]
public sealed class DesktopWorkbenchTests
{
    private static HeadlessUnitTestSession ui = null!;

    [ClassInitialize]
    public static void Initialize(TestContext _) => ui = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));

    [ClassCleanup]
    public static async Task Cleanup() => await ui.DisposeAsync();

    [TestMethod(DisplayName = "Ribbon alignment and command/log placement")]
    public Task SharedRibbonAndLogRemainUsableWhenResized() => ui.Dispatch(() =>
    {
        using var model = SampleModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            foreach (var size in new[] { new Size(1060, 720), new Size(1320, 860), new Size(1600, 1000) })
            {
                window.Width = size.Width;
                window.Height = size.Height;
                LayoutNow(window);
                var fieldNames = new[] { "ServerField", "UsernameField", "PatronField", "ItemField" };
                var controls = fieldNames.Select(name => window.FindControl<Control>(name)!).ToArray();
                var top = Position(controls[0], window).Y;
                foreach (var control in controls)
                {
                    Assert.IsTrue(Math.Abs(Position(control, window).Y - top) < 1, "Shared fields are not aligned.");
                    Assert.IsTrue(control.Bounds.Width > 60, "A shared field is too narrow.");
                }
                var picker = window.FindControl<ComboBox>("CommandPicker")!;
                var log = window.FindControl<TextEditor>("MessageLog")!;
                var execute = window.FindControl<Button>("ExecuteButton")!;
                Assert.IsTrue(Position(picker, window).X < Position(log, window).X, "Command workbench should be left of the log.");
                Assert.IsTrue(log.Bounds.Width > size.Width / 2, "The raw message log should retain most of the window width.");
                Assert.IsTrue(Position(execute, window).Y + execute.Bounds.Height < window.Bounds.Height, "Execute button is clipped.");
                Assert.IsTrue(log.Bounds.Height > 300, "Message log became too short.");
            }
        }
        finally { window.Close(); }

    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod(DisplayName = "Conditional fields and long raw message history")]
    public Task CommandPanelsAndLongMessageHistoryRemainUsable() => ui.Dispatch(() =>
    {
        using var model = SampleModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            var fee = window.FindControl<StackPanel>("FeePanel")!;
            var patron = window.FindControl<StackPanel>("PatronQueryPanel")!;
            var notice = window.FindControl<StackPanel>("NoticePanel")!;
            model.SelectedCommand = CommandChoice.All.Single(c => c.Command == SipCommand.FeePaid);
            model.FeeIdentifier = "retain-payment-input";
            LayoutNow(window);
            Assert.IsTrue(fee.IsVisible && !patron.IsVisible && !notice.IsVisible, "Fee panel selection is incorrect.");
            model.SelectedCommand = CommandChoice.All.Single(c => c.Command == SipCommand.NoticeUpdate);
            LayoutNow(window);
            Assert.IsTrue(notice.IsVisible && !fee.IsVisible && !patron.IsVisible, "Notice panel selection is incorrect.");
            model.SelectedCommand = CommandChoice.All.Single(c => c.Command == SipCommand.PatronInformation);
            model.SelectedSummary = SummaryChoice.All.Single(s => s.Key == "charged");
            LayoutNow(window);
            Assert.IsTrue(patron.IsVisible && !fee.IsVisible && !notice.IsVisible && model.UsesRange, "Patron panel selection is incorrect.");
            Assert.AreEqual("retain-payment-input", model.FeeIdentifier);
            var server = window.FindControl<TextBox>("ServerField")!;
            server.Text = "edited.example.test";
            Assert.AreEqual("edited.example.test", model.Fields.Server);
            for (var i = 0; i < 2000; i++)
            {
                model.AppendTransaction(new()
                {
                    Message = "99AY0AZ1234\r",
                    Response = "98OKAY0AZ5678\r"
                });
            }

            var log = window.FindControl<TextEditor>("MessageLog")!;
            log.SelectAll();
            Assert.AreEqual(model.LogDocument.TextLength, log.SelectionLength);
            Assert.IsFalse(log.SelectedText.Contains('\r'), "Log formatting retained carriage returns.");
            Assert.IsTrue(log.TextArea.TextView.LineTransformers.OfType<SipLogColorizer>().Any(), "Direction colors were not installed.");
            model.ClearLogCommand.Execute(null);
            Assert.AreEqual("P123", model.Fields.PatronBarcode);
            Assert.AreEqual(0, model.LogDocument.TextLength);
            AppendSampleLog(model);
            LayoutNow(window);
            Assert.IsTrue(log.TextArea.TextView.VisualLines.Count > 0, "The raw messages were not rendered.");
        }
        finally { window.Close(); }

    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod(DisplayName = "Settings edits are isolated until saved")]
    public Task SettingsEditsRemainIsolatedUntilSaved() => ui.Dispatch(() =>
    {
        var original = new AppPreferences { Defaults = SampleFields() };
        var window = new SettingsWindow(original);
        window.Show();
        try
        {
            var editing = (AppPreferences)window.DataContext!;
            editing.Defaults.Username = "unsaved-edit";
            Assert.AreEqual("test-user", original.Defaults.Username);
            foreach (var size in new[] { new Size(940, 640), new Size(800, 580) })
            {
                window.Width = size.Width;
                window.Height = size.Height;
                LayoutNow(window);
                var save = window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Save defaults");
                Assert.IsTrue(save.IsEffectivelyVisible && Position(save, window).Y + save.Bounds.Height < window.Bounds.Height,
                    "Settings actions were clipped at the supported window size.");
            }
        }
        finally { window.Close(); }
        Assert.AreEqual("test-user", original.Defaults.Username);

    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod(DisplayName = "Async commands keep the UI responsive")]
    public Task AsyncCommandsKeepTheUiResponsive() => ui.Dispatch(async () =>
    {
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new SipServer(async (stream, token) =>
        {
            _ = await ReadMessage(stream, token);
            received.SetResult();
            await release.Task.WaitAsync(token);
            await WriteMessage(stream, "941", token);
        });
        using var model = SampleModel();
        model.Fields.Server = "127.0.0.1";
        model.Fields.Port = server.Port.ToString();
        await model.ConnectCommand.ExecuteAsync(null);
        Assert.IsTrue(model.IsConnected && model.ExecuteCommand.CanExecute(null), "Connect did not enable commands.");
        model.SelectedCommand = CommandChoice.All.Single(c => c.Command == SipCommand.NoticeUpdate);
        model.Options.UseDate = true;
        model.Options.DateText = "invalid-date";
        await model.ExecuteCommand.ExecuteAsync(null);
        Assert.IsTrue(model.HasError && !model.IsBusy && model.LogDocument.TextLength == 0, "Invalid notice dates should be rejected before sending.");
        model.SelectedCommand = CommandChoice.All.Single(c => c.Command == SipCommand.Checkout);
        model.UpdateReady = true;
        Assert.IsTrue(model.RestartUpdateCommand.CanExecute(null), "A downloaded update should enable restart while idle.");
        var operation = model.ExecuteCommand.ExecuteAsync(null);
        await received.Task;
        Assert.IsTrue(model.IsBusy && !model.ExecuteCommand.CanExecute(null), "Overlapping commands are not blocked.");
        var dispatched = false;
        await Dispatcher.UIThread.InvokeAsync(() => dispatched = true);
        Assert.IsTrue(dispatched && !operation.IsCompleted, "The SIP request blocked the UI dispatcher.");
        Assert.IsFalse(model.RestartUpdateCommand.CanExecute(null), "An active request should block update restarts.");
        release.SetResult();
        await operation;
        Assert.IsTrue(!model.IsBusy && model.IsConnected && !model.HasError, "Command state was not restored.");
        Assert.IsTrue(model.LogDocument.Text.Contains("--> 11") && model.LogDocument.Text.Contains("<-- 941"), "The transaction was not logged.");
        await server.Completion;

        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task PendingConnectionsKeepCancelAndTheUiResponsiveAndCanBeRetried(bool cancel) => ui.Dispatch(async () =>
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        using var session = new SipSession((connection, token) =>
        {
            if (Interlocked.Increment(ref attempts) > 1) { return connection.ConnectAsync(token).AsTask(); }
            started.TrySetResult();
            return Task.Delay(Timeout.Infinite, token);
        }, connectTimeout: cancel ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(1));
        await using var server = new SipServer(async (stream, token) =>
        {
            _ = await ReadMessage(stream, token);
            await WriteMessage(stream, "941", token);
        });
        using var model = new MainWindowViewModel(session) { Fields = SampleFields() };
        model.Fields.Server = "127.0.0.1";
        model.Fields.Port = server.Port.ToString();
        model.AppendTransaction(new() { Message = "previous request", Response = "previous response" });
        var previousLog = model.LogDocument.Text;
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            LayoutNow(window);
            var operation = model.ConnectCommand.ExecuteAsync(null);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            LayoutNow(window);
            var cancelButton = window.FindControl<Button>("CancelConnectButton")!;
            Assert.IsTrue(model.IsBusy && model.IsConnecting && !model.IsConnected);
            Assert.IsTrue(cancelButton.IsEffectivelyVisible && cancelButton.IsEffectivelyEnabled,
                "A pending connection must leave its Cancel button usable.");
            Assert.IsFalse(model.ConnectCommand.CanExecute(null));
            var pulse = false;
            await Dispatcher.UIThread.InvokeAsync(() => pulse = true, DispatcherPriority.Background);
            Assert.IsTrue(pulse && !operation.IsCompleted, "The pending connection blocked the UI dispatcher.");
            var log = window.FindControl<TextEditor>("MessageLog")!;
            Assert.IsTrue(log.IsEffectivelyEnabled);
            log.SelectAll();
            Assert.AreEqual(previousLog, log.SelectedText);
            if (cancel) { cancelButton.Command!.Execute(null); }
            await operation.WaitAsync(TimeSpan.FromSeconds(3));
            LayoutNow(window);
            Assert.IsFalse(model.IsBusy || model.IsConnecting || model.IsConnected);
            Assert.IsTrue(model.ConnectCommand.CanExecute(null), "Connect did not become available for retry.");
            Assert.IsFalse(cancelButton.IsVisible);
            Assert.AreEqual(previousLog, model.LogDocument.Text);
            if (cancel)
            {
                Assert.IsFalse(model.HasError, "User cancellation should not display an error.");
                Assert.AreEqual("Connection cancelled", model.Status);
            }
            else
            {
                StringAssert.Contains(model.ErrorMessage!, "timed out");
            }
            await model.ConnectCommand.ExecuteAsync(null);
            Assert.IsTrue(model.IsConnected && !model.IsBusy && !model.HasError);
            await model.LoginCommand.ExecuteAsync(null);
            await server.Completion;
            StringAssert.Contains(model.LogDocument.Text, "<-- 941");
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(20));

    [TestMethod(DisplayName = "Checksum errors retain both raw messages")]
    public Task ChecksumFailuresRetainSentAndReceivedMessages() => ui.Dispatch(async () =>
    {
        await using var server = new SipServer(async (stream, token) =>
        {
            _ = await ReadMessage(stream, token);
            await stream.WriteAsync(Encoding.UTF8.GetBytes("941AY0AZ0000\r"), token);
        });
        using var model = SampleModel();
        model.Fields.Server = "127.0.0.1";
        model.Fields.Port = server.Port.ToString();
        await model.ConnectCommand.ExecuteAsync(null);
        await model.ExecuteCommand.ExecuteAsync(null);
        Assert.IsTrue(model.ErrorMessage?.Contains("checksum") == true, "Checksum error was not displayed.");
        Assert.IsTrue(model.LogDocument.Text.Contains("--> 11") && model.LogDocument.Text.Contains("<-- 941AY0AZ0000"), "Error discarded raw messages.");
        await server.Completion;

        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    private static MainWindowViewModel SampleModel() => new() { Fields = SampleFields() };

    private static void AppendSampleLog(MainWindowViewModel model)
    {
        model.IsConnected = true;
        model.Status = "Connected to sip.example.test:5002";
        model.AppendTransaction(new()
        {
            Message = "9300CNtest-user|COtest-secret|CPMAIN|VPITG|AY0AZEB7A",
            Response = "941AY0AZFEF8"
        });
        model.AppendTransaction(new()
        {
            Message = "6300020261021    104200  Y       AOCLC|AAP123|AD2468|BP6|BQ10|AY1AZE9C4",
            Response = "64              00020261021    104200000000003000000000000000AOCLC|AAP123|AELibrary Patron|BLY|CQY|BV0.00|AU10010011|AU10010022|AU10010033|AY1AZCF94"
        });
        model.AppendTransaction(new()
        {
            Message = "1720261021    104215AOCLC|ABI456|AC|AY2AZF3A7",
            Response = "1803000120261021    104215ABI456|AJThe Library Book|AQMAIN|CK001|AY2AZE714"
        });
    }

    private static void LayoutNow(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private static Point Position(Control control, Window window)
    {
        var position = control.TranslatePoint(default, window);
        Assert.IsNotNull(position, "Control is not in the window.");
        return position.Value;
    }
}
