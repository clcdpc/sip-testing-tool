using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Clc.Sip.Desktop.Models;
using Clc.Sip.Desktop.Services;
using Clc.Sip.Desktop.ViewModels;
using Clc.Sip.Desktop.Views;
using static Clc.Sip.Desktop.Tests.TestSupport.SessionTestData;
using Path = Avalonia.Controls.Shapes.Path;

namespace Clc.Sip.Desktop.Tests;

public sealed partial class DesktopParityTests
{
    private static AutomationPeer Peer(Control control) => ControlAutomationPeer.CreatePeerForElement(control);

    private static void AssertNamed(Control control, string? expected = null)
    {
        var name = Peer(control).GetName();
        Assert.IsFalse(string.IsNullOrWhiteSpace(name), control.GetType().Name + " has no accessible name.");
        Assert.AreNotEqual(control.GetType().Name, name);
        Assert.AreNotEqual(control.GetType().FullName, name);
        Assert.IsFalse(name.Contains("Avalonia.", StringComparison.Ordinal), name);
        if (expected != null) { Assert.AreEqual(expected, name); }
    }

    private static IEnumerable<AutomationPeer> AutomationDescendants(AutomationPeer peer)
    {
        foreach (var child in peer.GetChildren())
        {
            yield return child;
            foreach (var descendant in AutomationDescendants(child)) { yield return descendant; }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task AccessibilitySettingsInputsUseTheirVisibleCaptions(bool dark) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        model.Preferences.DarkTheme = dark;
        var main = new MainWindow { DataContext = model };
        var settings = new SettingsWindow(model.Preferences);
        main.Show();
        settings.Show();
        try
        {
            Layout(settings);
            var rows = settings.GetVisualDescendants().OfType<OptionalFieldRow>().ToArray();
            var textRows = rows.Where(row => row.Field is TextBox).ToArray();
            CollectionAssert.AreEquivalent(new[]
            {
                "Server", "Port", "Institution (AO)", "Terminal (AP)", "Username", "Password", "Location code (CP)",
                "Vendor profile (VP)", "Patron barcode", "Patron PIN", "Item barcode", "GitHub update repository"
            }, textRows.Select(row => row.Label).ToArray());
            foreach (var row in rows)
            {
                var input = row.Field!;
                AssertNamed(input, row.Label);
                Assert.AreSame(row.Children.OfType<TextBlock>().Single(), AutomationProperties.GetLabeledBy(input));
                Assert.IsFalse(Peer(row).IsControlElement());
                Assert.IsFalse(Peer(row).IsContentElement());
                Assert.IsTrue(AutomationDescendants(Peer(row)).Contains(Peer(input)), "Raw layout must retain its input.");
                AssertNamed(row.Children.OfType<TextBlock>().Single(), row.Label);
                input.BringIntoView();
                input.Focus();
                var focused = input is AccessibleAutoCompleteBox autocomplete ? autocomplete.Editor! : input;
                Assert.IsTrue(Peer(focused).HasKeyboardFocus());
                AssertNamed(focused, row.Label);
            }
            foreach (var checkbox in settings.GetVisualDescendants().OfType<CheckBox>())
            {
                AssertNamed(checkbox, (string)checkbox.Content!);
                var toggle = Peer(checkbox).GetProvider<IToggleProvider>()!;
                var previous = checkbox.IsChecked;
                toggle.Toggle();
                Assert.AreEqual(previous == true ? ToggleState.Off : ToggleState.On, toggle.ToggleState);
                toggle.Toggle();
                Assert.AreEqual(previous, checkbox.IsChecked);
            }
        }
        finally { settings.Close(); main.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task AccessibilityFieldRowPreservesNamesAndLabelsAcrossReflowAndReplacement(bool combo) => ui.Dispatch(() =>
    {
        Control input = combo ? new ComboBox { ItemsSource = new[] { "First", "Second" } } : new TextBox();
        var row = new OptionalFieldRow { Label = "Visible caption", Field = input };
        var window = new Window { Content = row, Height = 150 };
        window.Show();
        try
        {
            foreach (var width in new[] { 500, 150 })
            {
                window.Width = width;
                Layout(window);
                AssertNamed(input, "Visible caption");
                Assert.AreEqual(width == 500, input.Bounds.X > 0, "Exercise both caption positions.");
            }
            row.Label = "Updated caption";
            AssertNamed(input, "Updated caption");
            AutomationProperties.SetName(input, "Explicit accessible name");
            row.Label = "Another caption";
            AssertNamed(input, "Explicit accessible name");
            var replacement = new TextBox();
            var existingLabel = new TextBlock { Text = "Existing label" };
            AutomationProperties.SetLabeledBy(replacement, existingLabel);
            row.Field = replacement;
            Assert.IsNull(AutomationProperties.GetLabeledBy(input), "Do not retain a removed row's caption.");
            AssertNamed(replacement, "Existing label");
            row.Label = "Changed again";
            Assert.AreSame(existingLabel, AutomationProperties.GetLabeledBy(replacement));
            var labelSource = new ContentControl();
            var boundInput = new TextBox();
            boundInput.Bind(AutomationProperties.LabeledByProperty, new Binding(nameof(ContentControl.Content)) { Source = labelSource });
            row.Field = boundInput;
            labelSource.Content = existingLabel;
            Layout(window);
            AssertNamed(boundInput, "Existing label");
            Assert.AreSame(existingLabel, AutomationProperties.GetLabeledBy(boundInput), "Preserve a label binding even when it starts empty.");
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task AccessibilityAllTwelveCommandPanelsNameStaticAndGeneratedOptions() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Assert.HasCount(12, CommandChoice.All);
            foreach (var command in CommandChoice.All)
            {
                model.SelectedCommand = command;
                model.SelectedSummary = SummaryChoice.All[1];
                Layout(window);
                var rows = window.GetVisualDescendants().OfType<OptionalFieldRow>().Where(row => row.IsEffectivelyVisible).ToArray();
                Assert.IsTrue(rows.Length > 0, command.Name);
                foreach (var row in rows)
                {
                    var input = row.Field!;
                    AssertNamed(input, AutomationProperties.GetName(input) ?? row.Label);
                    Assert.IsFalse(Peer(row).IsControlElement(), row.Label);
                    Assert.IsTrue(AutomationDescendants(Peer(row)).Contains(Peer(input)));
                    if (input is TextBox or ComboBox)
                    {
                        input.BringIntoView();
                        Assert.IsTrue(input.Focus(), row.Label);
                        Assert.IsTrue(Peer(input).HasKeyboardFocus());
                    }
                    if (input is OptionalDateTimeInput date)
                    {
                        AssertDateAccessibility(date, row.Label);
                    }
                }
                foreach (var checkbox in window.GetVisualDescendants().OfType<CheckBox>().Where(control => control.IsEffectivelyVisible))
                {
                    AssertNamed(checkbox);
                    Assert.IsNotNull(Peer(checkbox).GetProvider<IToggleProvider>());
                }
                var labels = rows.Select(row => row.Label).ToArray();
                var expected = command.Command switch
                {
                    SipCommand.FeePaid => new[] { "Fee type", "Payment type", "Payment amount", "Fee identifier (CG)", "Transaction ID (BK)", "Currency code", "Terminal password (AC)" },
                    SipCommand.NoticeUpdate => new[] { "Delivery status", "Delivery date/time", "Delivery medium", "Notice type", "Comment (DE)", "Terminal password (AC)" },
                    SipCommand.PatronInformation => new[] { "Summary", "Start item (BP)", "End item (BQ)", "Language code", "Terminal password (AC)" },
                    _ => labels
                };
                CollectionAssert.AreEquivalent(expected, labels, command.Name);
            }
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    private static void AssertDateAccessibility(OptionalDateTimeInput date, string label)
    {
        date.FocusEditor();
        Assert.IsTrue(date.Editor.IsFocused);
        AssertNamed(date.Editor, label + " (" + CommandOptions.DateFormat + ")");
        var include = date.GetVisualDescendants().OfType<CheckBox>().Single();
        var calendar = date.GetVisualDescendants().OfType<Button>().Single(button => button is not ToggleButton);
        AssertNamed(include, "Include " + label);
        AssertNamed(calendar, "Choose " + label + " in calendar");
        var toggle = Peer(include).GetProvider<IToggleProvider>()!;
        var previous = date.IsIncluded;
        toggle.Toggle();
        Assert.AreEqual(!previous, date.IsIncluded);
        Assert.AreEqual(previous ? ToggleState.Off : ToggleState.On, toggle.ToggleState);
        toggle.Toggle();
        Assert.IsTrue(AutomationDescendants(Peer(date)).Contains(Peer(date.Editor)));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task AccessibilityAutocompleteNamesTheFocusedEditorAndSurvivesRetemplating(bool settingsField) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        var window = settingsField ? (Window)new SettingsWindow(model.Preferences) : new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var input = window.GetVisualDescendants().OfType<AccessibleAutoCompleteBox>().Single();
            var name = settingsField ? "Character encoding" : "Vendor Profile";
            var text = input.Text;
            var originalTemplate = input.Template;
            for (var template = 0; template < 2; template++)
            {
                foreach (var open in new[] { false, true })
                {
                    input.Focus();
                    input.IsDropDownOpen = open;
                    Layout(window);
                    Assert.IsTrue(input.Editor!.IsFocused);
                    Assert.IsTrue(Peer(input.Editor).HasKeyboardFocus());
                    AssertNamed(input.Editor, name);
                    Assert.IsFalse(Peer(input).IsControlElement(), "Only the editor should be announced on entry.");
                    Assert.IsTrue(AutomationDescendants(Peer(input)).Contains(Peer(input.Editor)));
                    Assert.AreEqual(open, input.IsDropDownOpen);
                }
                input.IsDropDownOpen = false;
                if (template == 0)
                {
                    var oldEditor = input.Editor;
                    input.Template = null;
                    Layout(window);
                    input.Template = originalTemplate;
                    Layout(window);
                    Assert.AreNotSame(oldEditor, input.Editor);
                    Assert.AreEqual(text, input.Text);
                }
            }
            AutomationProperties.SetName(input, "Updated accessible name");
            AssertNamed(input.Editor!, "Updated accessible name");
            AutomationProperties.SetName(input.Editor!, "Explicit editor name");
            AutomationProperties.SetName(input, "Changed parent name");
            AssertNamed(input.Editor!, "Explicit editor name");
            input.Editor!.ClearValue(AutomationProperties.NameProperty);
            AssertNamed(input.Editor, "Changed parent name");
            input.Focus();
            input.IsDropDownOpen = true;
            Layout(window);
            Press(window, Key.Down);
            Press(window, Key.Enter);
            Assert.IsFalse(input.IsDropDownOpen, "Enter must still accept a suggestion.");
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task AccessibilityConnectionErrorsNotifyRepeatReplaceClearAndDismiss() => ui.Dispatch(async () =>
    {
        using var session = new SipSession((_, _) => Task.FromException(new TimeoutException("Connection timed out.")));
        using var model = new MainWindowViewModel(session) { Fields = SampleFields() };
        model.Fields.Server = "";
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var server = window.FindControl<TextBox>("ServerField")!;
            var port = window.FindControl<TextBox>("PortField")!;
            var originalHelp = Peer(server).GetHelpText();
            var descriptions = new List<string>();
            Peer(server).PropertyChanged += (_, args) =>
            {
                if (args.Property == AutomationElementIdentifiers.HelpTextProperty) { descriptions.Add((string)args.NewValue!); }
            };
            var banner = Peer(window.FindControl<TextBlock>("ErrorMessageText")!);
            Assert.AreEqual(AutomationLiveSetting.Assertive, banner.GetLiveSetting());
            var changes = new List<string?>();
            banner.PropertyChanged += (_, args) =>
            {
                if (args.Property == AutomationElementIdentifiers.NameProperty) { changes.Add(args.NewValue as string); }
            };
            for (var failure = 0; failure < 2; failure++)
            {
                Press(window, Key.N, ShortcutModifier);
                await model.ConnectCommand.ExecutionTask!;
                Layout(window);
                Assert.IsTrue(server.IsFocused);
                AssertNamed(server, "SIP Server");
                Assert.AreEqual("Server is required.", Peer(server).GetHelpText());
                Assert.AreEqual("Server is required.", banner.GetName());
            }
            Assert.AreEqual(2, descriptions.Count(message => message == "Server is required."));
            Assert.AreEqual(2, changes.Count(message => message == "Server is required."));
            server.Text = "localhost";
            Layout(window);
            Assert.IsFalse(model.HasError, "Editing an invalid value clears its obsolete error.");
            Assert.AreEqual(originalHelp, Peer(server).GetHelpText());
            port.Text = "invalid";
            await model.ConnectCommand.ExecuteAsync(null);
            Layout(window);
            Assert.IsTrue(port.IsFocused);
            Assert.AreEqual(model.ErrorMessage, Peer(port).GetHelpText());
            Assert.AreEqual(originalHelp, Peer(server).GetHelpText());
            var dismiss = window.FindControl<Button>("DismissErrorButton")!;
            AssertNamed(dismiss, "Dismiss error");
            Peer(dismiss).GetProvider<IInvokeProvider>()!.Invoke();
            Layout(window);
            Assert.IsFalse(Peer(port).GetHelpText().Contains("Port must", StringComparison.Ordinal));
            Assert.IsFalse(model.HasError);
            await model.ConnectCommand.ExecuteAsync(null);
            Assert.AreEqual(model.ErrorMessage, Peer(port).GetHelpText());
            port.Text = "5002";
            await model.ConnectCommand.ExecuteAsync(null);
            Layout(window);
            Assert.AreEqual("Connection timed out.", model.ErrorMessage);
            Assert.AreEqual("Connection timed out.", banner.GetName());
            Assert.AreNotEqual(model.ErrorMessage, Peer(server).GetHelpText(), "Operational failures must not mark a field invalid.");
            Assert.AreNotEqual(model.ErrorMessage, Peer(port).GetHelpText());
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(SipCommand.FeePaid, "AmountField")]
    [DataRow(SipCommand.PatronInformation, "LanguageCodeField")]
    [DataRow(SipCommand.Renew, "DateTextField")]
    [DataRow(SipCommand.NoticeUpdate, "NoticeDateField")]
    [DataRow(SipCommand.Login, "UidAlgorithmField")]
    [DataRow(SipCommand.AcsStatus, "PrintWidthField")]
    public Task AccessibilityCommandValidationDescribesTheActualFocusedField(SipCommand command, string fieldName) => ui.Dispatch(async () =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields(), SelectedCommand = Choice(command), IsConnected = true };
        model.FeeType = model.PaymentType = "0";
        model.Amount = "invalid";
        model.Options.LanguageCode = "invalid";
        model.Options.UseDate = true;
        model.Options.DateText = "invalid";
        model.Options.UidAlgorithm = "";
        model.Options.PrintWidth = "invalid";
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            await model.ExecuteCommand.ExecuteAsync(null);
            Layout(window);
            var control = Field<Control>(window, fieldName);
            var editor = control is OptionalDateTimeInput date ? date.Editor : control;
            Assert.IsTrue(editor.IsFocused, fieldName);
            AssertNamed(editor);
            Assert.AreEqual(model.ErrorMessage, Peer(editor).GetHelpText());
            Assert.IsTrue(model.HasError);
            var error = model.ErrorMessage;
            if (control is OptionalDateTimeInput dateInput)
            {
                Peer(dateInput.GetVisualDescendants().OfType<CheckBox>().Single()).GetProvider<IToggleProvider>()!.Toggle();
                Assert.IsFalse(model.HasError, "Removing a date override resolves its validation error.");
            }
            else { model.DismissErrorCommand.Execute(null); }
            Assert.AreNotEqual(error, Peer(editor).GetHelpText());
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task AccessibilitySettingsValidationIdentifiesItsEditorAndClearsStaleErrors(bool repository) => ui.Dispatch(() =>
    {
        var window = new SettingsWindow();
        window.Show();
        try
        {
            Layout(window);
            var editing = (AppPreferences)window.DataContext!;
            editing.EncodingName = repository ? "UTF-8" : "invalid-encoding";
            editing.UpdateFeedUrl = repository ? "not a repository URL" : "";
            var input = repository ? window.FindControl<TextBox>("UpdateRepositoryField")! : window.FindControl<AccessibleAutoCompleteBox>("EncodingField")!.Editor!;
            var banner = window.FindControl<TextBlock>("SettingsError")!;
            Assert.AreEqual(AutomationLiveSetting.Assertive, Peer(banner).GetLiveSetting());
            var notifications = new List<string?>();
            Peer(banner).PropertyChanged += (_, args) =>
            {
                if (args.Property == AutomationElementIdentifiers.NameProperty) { notifications.Add(args.NewValue as string); }
            };
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Peer(window.FindControl<Button>("SaveDefaultsButton")!).GetProvider<IInvokeProvider>()!.Invoke();
                Layout(window);
                Assert.IsTrue(input.IsFocused);
                Assert.IsFalse(string.IsNullOrEmpty(banner.Text));
                Assert.AreEqual(banner.Text, Peer(input).GetHelpText());
            }
            Assert.AreEqual(2, notifications.Count(message => message == banner.Text));
            var error = banner.Text;
            input.Text = repository ? "https://github.com/clcdpc/sip-testing-tool" : "UTF-8";
            Layout(window);
            Assert.AreEqual("", banner.Text);
            Assert.AreNotEqual(error, Peer(input).GetHelpText());
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task AccessibilitySearchNamesActionsAndExposesToggleStatesAfterRetemplating(bool dark) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.Preferences.DarkTheme = dark;
        model.LogDocument.Text = SearchTranscript;
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            Press(window, Key.F, ShortcutModifier);
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            var panel = log.SearchPanel;
            var template = panel.Template;
            for (var pass = 0; pass < 2; pass++)
            {
                panel.Reactivate();
                Assert.IsTrue(SearchInput(log).IsFocused);
                AssertNamed(SearchInput(log), "Find in message log");
                window.KeyTextInput("alpha");
                var buttons = panel.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible && button.Focusable).ToArray();
                Assert.HasCount(6, buttons);
                var visited = new HashSet<Control>();
                foreach (var _ in buttons)
                {
                    Press(window, Key.Tab);
                    var focused = window.FocusManager!.GetFocusedElement() as Control;
                    Assert.IsTrue(focused != null && buttons.Contains(focused), "Tab should reach the search toolbar actions.");
                    AssertNamed(focused!);
                    visited.Add(focused!);
                }
                Assert.HasCount(6, visited, "Every search action must remain reachable by keyboard.");
                foreach (var button in buttons)
                {
                    AssertNamed(button);
                    Assert.AreNotEqual(Peer(button).GetName(), Peer(button).GetHelpText(), "Do not repeat the tooltip as a description.");
                    if (button is ToggleButton toggle)
                    {
                        var provider = Peer(toggle).GetProvider<IToggleProvider>()!;
                        var state = provider.ToggleState;
                        provider.Toggle();
                        Assert.AreNotEqual(state, provider.ToggleState);
                        Assert.AreEqual(toggle.IsChecked == true ? ToggleState.On : ToggleState.Off, provider.ToggleState);
                        Assert.AreEqual(toggle.IsChecked, Peer(button).GetName() switch
                        {
                            "Match case" => panel.MatchCase,
                            "Match whole words" => panel.WholeWords,
                            "Use regular expressions" => panel.UseRegex,
                            _ => throw new AssertFailedException("Unexpected search toggle.")
                        });
                        provider.Toggle();
                    }
                }
                CollectionAssert.AreEquivalent(new[] { "Match case", "Match whole words", "Use regular expressions", "Previous match", "Next match", "Close search" }, buttons.Select(button => Peer(button).GetName()).ToArray());
                foreach (var path in panel.GetVisualDescendants().OfType<Path>())
                {
                    Assert.IsFalse(Peer(path).IsControlElement());
                    Assert.IsFalse(Peer(path).IsContentElement());
                }
                Assert.IsTrue(AutomationDescendants(Peer(panel)).Contains(Peer(SearchInput(log))));
                var next = buttons.Single(button => Peer(button).GetName() == "Next match");
                var previous = buttons.Single(button => Peer(button).GetName() == "Previous match");
                var start = log.SelectionStart;
                Peer(next).GetProvider<IInvokeProvider>()!.Invoke();
                Assert.AreNotEqual(start, log.SelectionStart);
                Peer(previous).GetProvider<IInvokeProvider>()!.Invoke();
                Assert.AreEqual(start, log.SelectionStart);
                if (pass == 0)
                {
                    panel.Template = null;
                    Layout(window);
                    panel.Template = template;
                    Layout(window);
                }
            }
            var close = panel.GetVisualDescendants().OfType<Button>().Single(button => Peer(button).GetName() == "Close search");
            Peer(close).GetProvider<IInvokeProvider>()!.Invoke();
            Assert.IsFalse(panel.IsOpened);
            Assert.IsTrue(log.TextArea.IsFocused);
            AssertNamed(log.TextArea, "Sent and Received SIP Message Log");
            Assert.AreEqual(SearchTranscript, model.LogDocument.Text);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task AccessibilityEscapeCancelsSettingsAfterClosingAnActiveDropdown(bool dropdown) => ui.Dispatch(async () =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        var main = new MainWindow { DataContext = model };
        main.Show();
        var settings = new SettingsWindow(model.Preferences);
        try
        {
            Layout(main);
            var opener = main.FindControl<TextBox>("ServerField")!;
            opener.Focus();
            var result = settings.ShowDialog<AppPreferences?>(main);
            Layout(settings);
            var editing = (AppPreferences)settings.DataContext!;
            editing.Defaults.Server = "unsaved.example";
            editing.DarkTheme = true;
            Assert.AreEqual(ThemeVariant.Dark, main.ActualThemeVariant);
            if (dropdown)
            {
                var input = settings.FindControl<AccessibleAutoCompleteBox>("EncodingField")!;
                input.Focus();
                input.IsDropDownOpen = true;
                Layout(settings);
                Press(settings, Key.Escape);
                Assert.IsFalse(input.IsDropDownOpen);
                Assert.IsFalse(result.IsCompleted, "The first Escape belongs to the open dropdown.");
                Assert.IsTrue(input.Editor!.IsFocused);
            }
            else
            {
                settings.FindControl<TextBox>("DefaultPortField")!.Focus();
                Press(settings, Key.Enter);
                Assert.IsFalse(result.IsCompleted, "Enter in a normal field must not save or cancel.");
            }
            settings.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            main.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Assert.IsNull(await result);
            Assert.AreEqual("", model.Preferences.Defaults.Server);
            Assert.IsFalse(model.Preferences.DarkTheme);
            Assert.AreEqual(ThemeVariant.Light, main.ActualThemeVariant);
            Assert.IsTrue(opener.IsFocused, "Cancel must restore the owner's previous focus.");
            Assert.IsFalse(model.HasError);
        }
        finally { settings.Close(); main.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task AccessibilitySettingsMenuRegainsFocusAfterEscape() => ui.Dispatch(async () =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        var main = new MainWindow { DataContext = model };
        main.Show();
        try
        {
            Layout(main);
            var menu = main.FindControl<MenuItem>("SettingsMenuItem")!;
            menu.Focus(NavigationMethod.Tab);
            Press(main, Key.Down);
            Layout(main);
            Assert.IsTrue(main.FindControl<MenuItem>("DefaultSettingsMenuItem")!.Focus());
            Press(main, Key.Enter);
            var settings = (SettingsWindow)main.OwnedWindows.Single();
            Layout(settings);
            settings.FindControl<TextBox>("DefaultPortField")!.Focus();
            settings.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            main.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Layout(main);
            Assert.IsFalse(settings.IsVisible);
            Assert.IsTrue(menu.IsFocused, "Return to the visible menu that opened settings; focused: " + main.FocusManager!.GetFocusedElement());
            Press(main, Key.P, ShortcutModifier);
            Assert.IsTrue(main.FindControl<TextBox>("PatronField")!.IsFocused);
        }
        finally { foreach (var dialog in main.OwnedWindows.ToArray()) { dialog.Close(); } main.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
}
