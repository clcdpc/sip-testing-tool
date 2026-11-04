using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Clc.Sip.Desktop.Models;
using Clc.Sip.Desktop.Tests.TestSupport;
using Clc.Sip.Desktop.ViewModels;
using Clc.Sip.Desktop.Views;
using CommunityToolkit.Mvvm.Input;
using static Clc.Sip.Desktop.Tests.TestSupport.SessionTestData;

namespace Clc.Sip.Desktop.Tests;

public sealed partial class DesktopParityTests
{
    private const string SearchTranscript = "--> alpha beta alphaBeta\n<-- ALPHA alpha\n\n--> beta alpha\n<-- omega";
    private static TextBox SearchInput(TextEditor log) => log.SearchPanel.GetVisualDescendants().OfType<TextBox>()
        .Single(input => input.Name == "PART_searchTextBox");
    private static string SearchStatus(TextEditor log) => log.SearchPanel.GetVisualDescendants().OfType<TextBlock>()
        .Single(text => text.Name == "PART_MessageContent").Text!;

    [TestMethod]
    [DataRow("shortcut")]
    [DataRow("button")]
    [DataRow("menu")]
    public Task FindActionsFocusSearchAllowTypingAndReplaceAnExistingTerm(string activation) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields(), SelectedCommand = Choice(SipCommand.ItemInformation) };
        model.LogDocument.Text = SearchTranscript;
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            Assert.AreSame(window.FindControl<Button>("FindLogButton")!.Command, window.FindControl<MenuItem>("FindLogMenuItem")!.Command);
            var baseRenderers = log.TextArea.TextView.BackgroundRenderers.Count;
            Activate(window, activation, "FindLogButton", "FindLogMenuItem", Key.F);
            Layout(window);
            Assert.IsTrue(log.SearchPanel.IsOpened);
            Assert.IsTrue(SearchInput(log).IsFocused);
            window.KeyTextInput("alpha");
            Layout(window);
            Assert.AreEqual("alpha", log.SearchPanel.SearchPattern);
            Assert.AreEqual("alpha", log.SelectedText);
            Assert.AreEqual(baseRenderers + 1, log.TextArea.TextView.BackgroundRenderers.Count, "Search should add its existing highlight renderer.");
            StringAssert.Contains(SearchStatus(log), "5");
            window.FindControl<TextBox>("ServerField")!.Focus();
            Activate(window, activation, "FindLogButton", "FindLogMenuItem", Key.F);
            Layout(window);
            Assert.AreEqual("alpha", SearchInput(log).SelectedText);
            window.KeyTextInput("omega");
            Assert.AreEqual("omega", log.SearchPanel.SearchPattern);
            Assert.AreEqual("omega", log.SelectedText);
            Assert.AreEqual(SearchTranscript, log.Text);
            Assert.IsTrue(log.IsReadOnly);
            log.SearchPanel.IsReplaceMode = true;
            Assert.IsFalse(log.SearchPanel.IsReplaceMode, "Read-only log search cannot expose replacement.");
            Press(window, Key.Escape);
            Layout(window);
            Assert.IsFalse(log.SearchPanel.IsOpened);
            Assert.IsTrue(log.TextArea.IsFocused);
            Assert.AreEqual("omega", log.SelectedText);
            Assert.AreEqual(baseRenderers, log.TextArea.TextView.BackgroundRenderers.Count);
            Assert.AreEqual(SipCommand.ItemInformation, model.SelectedCommand.Command);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task FindNavigationMovesOncePerKeyAndWrapsFromSearchAndTranscript(bool transcriptFocus) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        model.LogDocument.Text = SearchTranscript;
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            var original = log.CaretOffset;
            Press(window, Key.F3);
            Press(window, Key.F3, RawInputModifiers.Shift);
            Assert.AreEqual(original, log.CaretOffset);
            Assert.IsFalse(log.SearchPanel.IsOpened);
            Press(window, Key.F, ShortcutModifier | RawInputModifiers.Shift);
            Layout(window);
            window.KeyTextInput("alpha");
            var positions = Regex.Matches(SearchTranscript, "alpha", RegexOptions.IgnoreCase).Select(match => match.Index).ToArray();
            Assert.AreEqual(positions[0], log.SelectionStart);
            if (transcriptFocus) { log.TextArea.Focus(); }
            for (var i = 1; i <= positions.Length * 2; i++)
            {
                Press(window, Key.F3);
                Assert.AreEqual(positions[i % positions.Length], log.SelectionStart, "F3 must advance exactly one result.");
                Assert.AreEqual(5, log.SelectionLength);
            }
            for (var i = 1; i <= positions.Length; i++)
            {
                Press(window, Key.F3, RawInputModifiers.Shift);
                Assert.AreEqual(positions[(positions.Length - i) % positions.Length], log.SelectionStart);
            }
            ActivateAction(window, "FindNextMenuItem");
            Assert.AreEqual(positions[1], log.SelectionStart);
            ActivateAction(window, "FindPreviousMenuItem");
            Assert.AreEqual(positions[0], log.SelectionStart);
            log.TextArea.Focus();
            var content = log.Text;
            window.KeyTextInput("do not replace");
            Press(window, Key.Delete);
            Assert.AreEqual(content, log.Text);
            Press(window, Key.Escape);
            var caret = log.CaretOffset;
            Press(window, Key.F3);
            Assert.AreEqual(caret, log.CaretOffset);
            Assert.IsFalse(window.FindControl<MenuItem>("FindNextMenuItem")!.Command!.CanExecute(null));
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false, false, false, "alpha", 5)]
    [DataRow(true, false, false, "alpha", 4)]
    [DataRow(false, true, false, "alpha", 4)]
    [DataRow(true, true, false, "alpha", 3)]
    [DataRow(true, false, true, @"\balpha\b", 3)]
    [DataRow(false, false, true, "alpha|omega", 6)]
    public Task FindUsesAvaloniaEditCaseWordAndRegexOptions(bool matchCase, bool wholeWords, bool regex, string pattern, int count) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        model.LogDocument.Text = SearchTranscript;
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            Press(window, Key.F, ShortcutModifier | RawInputModifiers.Shift);
            Layout(window);
            log.SearchPanel.MatchCase = matchCase;
            log.SearchPanel.WholeWords = wholeWords;
            log.SearchPanel.UseRegex = regex;
            window.KeyTextInput(pattern);
            Layout(window);
            StringAssert.Contains(SearchStatus(log), count.ToString());
            Assert.IsTrue(log.SelectionLength > 0);
            Assert.AreEqual(SearchTranscript, log.Text);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task EmptyMissingAndInvalidSearchPatternsLeaveTheLogUsable() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        model.LogDocument.Text = SearchTranscript;
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            Press(window, Key.F, ShortcutModifier | RawInputModifiers.Shift);
            Layout(window);
            var caret = log.CaretOffset;
            Press(window, Key.F3);
            Assert.AreEqual(caret, log.CaretOffset);
            window.KeyTextInput("missing");
            StringAssert.Contains(SearchStatus(log), "No matches");
            Press(window, Key.F3);
            Press(window, Key.F3, RawInputModifiers.Shift);
            Assert.AreEqual(0, log.SelectionLength);
            SearchInput(log).Text = "";
            Press(window, Key.F3);
            log.SearchPanel.UseRegex = true;
            SearchInput(log).Text = "[";
            Press(window, Key.F3);
            Assert.AreEqual(SearchTranscript, log.Text);
            SearchInput(log).Text = "omega";
            Assert.AreEqual("omega", log.SelectedText);
            Assert.IsFalse(model.HasError);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task LongLogSearchRetainsTheCurrentMatchWhileMessagesArriveAndResumesFollowingAfterClose() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        model.LogDocument.Text = "--> needle\n<-- needle\n\n" + string.Join("\n\n", Enumerable.Range(0, 1500).Select(index => $"--> 99 {index}\n<-- 98 {index}"));
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            log.ScrollToEnd();
            Press(window, Key.F, ShortcutModifier | RawInputModifiers.Shift);
            Layout(window);
            log.CaretOffset = 0;
            window.KeyTextInput("needle");
            Layout(window);
            Assert.AreEqual("needle", log.SelectedText);
            var start = log.SelectionStart;
            var scroll = log.VerticalOffset;
            model.AppendTransaction(new() { Message = "99 needle", Response = "98 needle" });
            Layout(window);
            Assert.AreEqual(scroll, log.VerticalOffset, 1, "Appending must not pull an active search to the end.");
            Assert.AreEqual(start, log.SelectionStart);
            StringAssert.Contains(SearchStatus(log), "4");
            Press(window, Key.F3, RawInputModifiers.Shift);
            StringAssert.Contains(log.SelectedText, "needle");
            Assert.IsTrue(log.SelectionStart > model.LogDocument.TextLength - 30, "New received messages must be searchable.");
            Press(window, Key.F3);
            Assert.AreEqual(start, log.SelectionStart, "Wrap must include new sent and received matches.");
            log.TextArea.Focus();
            Press(window, Key.Escape);
            Assert.IsFalse(log.SearchPanel.IsOpened);
            model.AppendTransaction(new() { Message = "99 next", Response = "98 next" });
            Layout(window);
            Assert.IsTrue(log.VerticalOffset > scroll + 1000, "Following resumes on the next append after closing search.");
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task NativePickerBlocksSearchNavigationEscapeAndPanelShortcuts() => ui.Dispatch(async () =>
    {
        var provider = new ExportStorageProvider();
        using var model = new MainWindowViewModel();
        model.LogDocument.Text = SearchTranscript;
        var window = new MainWindow { DataContext = model };
        TestStorageProviderFactory.Windows.Add(window, provider);
        window.Show();
        try
        {
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            Press(window, Key.F, ShortcutModifier | RawInputModifiers.Shift);
            Layout(window);
            window.KeyTextInput("alpha");
            log.TextArea.Focus();
            var selection = log.SelectionStart;
            Press(window, Key.S, ShortcutModifier | RawInputModifiers.Shift);
            var export = (IAsyncRelayCommand)window.FindControl<Button>("ExportLogButton")!.Command!;
            Assert.HasCount(1, provider.Saves);
            Press(window, Key.F3);
            Press(window, Key.F3, RawInputModifiers.Shift);
            Press(window, Key.Escape);
            Press(window, Key.F6);
            Press(window, Key.F6, RawInputModifiers.Shift);
            Press(window, Key.F, ShortcutModifier | RawInputModifiers.Shift);
            Assert.AreEqual(selection, log.SelectionStart);
            Assert.IsTrue(log.SearchPanel.IsOpened && log.TextArea.IsFocused);
            Assert.IsTrue(SearchInput(log).SelectedText.Length == 0);
            provider.Result.SetResult(null);
            await export.ExecutionTask!;
            Press(window, Key.F3);
            Assert.IsTrue(log.SelectionStart > selection);
            Press(window, Key.Escape);
            Assert.IsFalse(log.SearchPanel.IsOpened);
        }
        finally { provider.Result.TrySetResult(null); window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task SearchAndInspectionCoexistInBothThemesAndWindowSizes(bool dark) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.Preferences.DarkTheme = dark;
        model.LogDocument.Text = "--> 6300020261103    101500000AOCLC|AAP123|AD2468|\n<-- " + response;
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            var content = log.Text;
            var fieldOffset = content.IndexOf("AA2187", StringComparison.Ordinal);
            var pinOffset = content.IndexOf("<-- ", StringComparison.Ordinal) + 6;
            window.LogInspection.InspectOffset(pinOffset, true);
            Assert.IsNotNull(window.LogInspection.PinnedField);
            Press(window, Key.F, ShortcutModifier | RawInputModifiers.Shift);
            Layout(window);
            window.KeyTextInput("2187");
            Assert.IsTrue(log.TextArea.TextView.LineTransformers.OfType<SipLogColorizer>().Any());
            log.TextArea.Focus();
            Press(window, Key.Enter, RawInputModifiers.Control);
            Layout(window);
            var inspector = window.FindControl<MessageFieldsPanel>("MessageFields")!;
            Assert.IsTrue(inspector.IsVisible && inspector.IsKeyboardFocusWithin);
            Press(window, Key.F, ShortcutModifier | RawInputModifiers.Shift);
            Layout(window);
            Assert.IsTrue(SearchInput(log).IsFocused);
            foreach (var size in new[] { new Size(1060, 720), new Size(1280, 820) })
            {
                window.Width = size.Width;
                window.Height = size.Height;
                Layout(window);
                var panel = log.SearchPanel;
                var location = panel.TranslatePoint(default, log)!.Value;
                Assert.IsTrue(location.X >= 0 && location.Y >= 0);
                Assert.IsTrue(location.X + panel.Bounds.Width <= log.Bounds.Width + 1);
                Assert.IsTrue(SearchInput(log).Bounds.Width > 100);
                Assert.AreEqual(dark ? ThemeVariant.Dark : ThemeVariant.Light, panel.ActualThemeVariant);
                Assert.IsTrue(window.FindControl<Grid>("MessageLogHeader")!.Bounds.Height <= 50);
                SaveFrame(window, $"shortcuts-search-inspector-{(dark ? "dark" : "light")}-{size.Width}");
            }
            Press(window, Key.Escape);
            Layout(window);
            Assert.IsFalse(log.SearchPanel.IsOpened);
            Assert.IsTrue(inspector.IsVisible, "Closing search must preserve an expanded inspector.");
            Assert.AreEqual(content, log.Text);
            window.LogInspection.InspectOffset(fieldOffset);
            Assert.IsNotNull(window.LogInspection.HoveredField);
            window.LogInspection.InspectOffset(pinOffset, true);
            Assert.IsNotNull(window.LogInspection.PinnedField);
            Press(window, Key.M, ShortcutModifier | RawInputModifiers.Shift);
            Press(window, Key.Escape);
            Assert.IsFalse(inspector.IsVisible, "Normal inspector Escape remains available outside search.");
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
}
