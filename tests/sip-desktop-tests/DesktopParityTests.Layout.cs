using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Clc.Sip.Desktop.Models;
using Clc.Sip.Desktop.Tests.TestSupport;
using Clc.Sip.Desktop.ViewModels;
using Clc.Sip.Desktop.Views;

namespace Clc.Sip.Desktop.Tests;

public sealed partial class DesktopParityTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task CommandOptionsFitTheMinimumWindowWithoutScrolling(bool dark) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SessionTestData.SampleFields() };
        model.Preferences.DarkTheme = dark;
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            foreach (var size in new[] { new Size(1280, 820), new Size(1060, 720) })
            {
                window.Width = size.Width;
                window.Height = size.Height;
                foreach (var command in CommandChoice.All)
                {
                    model.SelectedCommand = command;
                    Layout(window);
                    var options = window.GetVisualDescendants().OfType<ScrollViewer>()
                        .Single(scroll => scroll.GetVisualDescendants().OfType<CommandOptionsView>().Any());
                    Save(window, $"{(dark ? "dark" : "light")}-{command.Command}-{size.Width}");
                    Assert.IsTrue(options.Extent.Height <= options.Viewport.Height + 1,
                        $"{command.Name} at {size}: options require {options.Extent.Height:0} px but only {options.Viewport.Height:0} px are visible.");
                }
            }
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(60));

    [TestMethod]
    public Task FieldTableHeadersAlignWithTheirCells() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            model.AppendTransaction(new() { Message = "2300020261022    083815AOCLC|AAP123|", Response = "24YYYY          00120261022    083815AOCLC|AAP123|" });
            model.AppendTransaction(new() { Message = "9300CNtest|", Response = response });
            foreach (var width in new[] { 1060, 1280, 1600 })
            {
                window.Width = width;
                foreach (var (line, caption) in new[] { (1, "Sent message 1"), (5, "Received message 4") })
                {
                    window.LogInspection.ExpandMessageAtOffset(model.LogDocument.GetLineByNumber(line).Offset);
                    Layout(window);
                    var panel = window.FindControl<MessageFieldsPanel>("MessageFields")!;
                    Assert.IsTrue(panel.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text?.StartsWith(caption + " · ", StringComparison.Ordinal) == true));
                    var headers = panel.GetVisualDescendants().OfType<TextBlock>()
                        .Where(text => text.Text is "Field" or "Description" or "Value").ToArray();
                    var cells = panel.GetVisualDescendants().OfType<SelectableTextBlock>().Take(3).ToArray();
                    Assert.AreEqual(3, headers.Length);
                    Assert.AreEqual(3, cells.Length);
                    Save(window, $"field-table-alignment-{width}-{line}");
                    for (var i = 0; i < 3; i++)
                    {
                        var headingX = headers[i].TranslatePoint(default, panel)!.Value.X;
                        var cellX = cells[i].TranslatePoint(default, panel)!.Value.X;
                        Assert.AreEqual(cellX, headingX, 1, headers[i].Text + " header is not aligned with its cells.");
                    }
                }
            }
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task NoticeChoiceLabelsAreReadableAndFitTheirEditors() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { SelectedCommand = Choice(SipCommand.NoticeUpdate) };
        var window = new MainWindow { DataContext = model, Width = 1060, Height = 720 };
        window.Show();
        try
        {
            foreach (var name in new[] { "NoticeStatusField", "NoticeMediumField", "NoticeTypeField" })
            {
                var field = window.FindControl<ComboBox>(name)!;
                foreach (var item in field.Items)
                {
                    field.SelectedItem = item;
                    Layout(window);
                    var text = field.GetVisualDescendants().OfType<TextBlock>().First(block => !string.IsNullOrEmpty(block.Text));
                    Assert.IsFalse(text.Text!.Contains("OrUnknown", StringComparison.Ordinal) || text.Text.Contains("Delivery", StringComparison.Ordinal) && !text.Text.Contains(' '));
                    Assert.IsTrue(TextWidth(text.Text, text.FontFamily, text.FontSize) <= text.Bounds.Width + 0.5, name + " clips " + text.Text);
                }
            }
            Save(window, "notice-choice-labels");
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task DateEditorTextAndDropDownFitAtNarrowAndWideWorkbenchSizes() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { SelectedCommand = Choice(SipCommand.Renew) };
        model.Options.DateText = "2026-11-30 14:23:45";
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            var columns = window.FindControl<ComboBox>("CommandPicker")!.GetVisualAncestors().OfType<Grid>().Last(grid => grid.ColumnDefinitions.Count == 3);
            foreach (var width in new[] { 280, 312, 500 })
            {
                columns.ColumnDefinitions[0].Width = new GridLength(width);
                Layout(window);
                var date = Field<OptionalDateTimeInput>(window, "DateTextField");
                var editor = Field<TextBox>(date, "DateTimeEditor");
                var button = date.GetVisualDescendants().OfType<Button>()
                    .Single(control => Avalonia.Automation.AutomationProperties.GetName(control) == "Choose calendar date");
                Assert.IsTrue(TextWidth(editor.Text!, editor.FontFamily, editor.FontSize) <= editor.Bounds.Width - editor.Padding.Left - editor.Padding.Right);
                var right = button.TranslatePoint(new Point(button.Bounds.Width, 0), date)!.Value.X;
                Assert.AreEqual(date.Bounds.Width - 1, right, 0.5, "The calendar button should fill the right edge of the editor.");
                Assert.AreEqual(editor.Bounds.Height, button.Bounds.Height, 0.5);
            }
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    private static double TextWidth(string text, FontFamily font, double size) =>
        new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(font), size, Brushes.Black).Width;

    [TestMethod]
    [DataRow(1060)]
    [DataRow(1280)]
    public Task RibbonEditorsAndActionButtonsAlign(int width) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SessionTestData.SampleFields() };
        var window = new MainWindow { DataContext = model, Width = width };
        window.Show();
        try
        {
            Layout(window);
            var first = window.FindControl<TextBox>("ServerField")!;
            var firstY = first.TranslatePoint(default, window)!.Value.Y;
            foreach (var name in new[] { "PortField", "UsernameField", "PasswordField", "PatronField", "ItemField" })
            {
                var field = window.FindControl<TextBox>(name)!;
                Assert.AreEqual(firstY, field.TranslatePoint(default, window)!.Value.Y, 0.5, name + " is not aligned.");
                Assert.AreEqual(first.Bounds.Height, field.Bounds.Height, 0.5, name + " has a different height.");
            }
            var profile = window.FindControl<AutoCompleteBox>("VendorProfileField")!;
            var profileBorder = profile.GetVisualAncestors().OfType<Border>().First();
            var location = window.FindControl<TextBox>("LocationCodeField")!;
            Assert.AreEqual(location.TranslatePoint(default, window)!.Value.Y, profileBorder.TranslatePoint(default, window)!.Value.Y, 0.5);
            Assert.AreEqual(location.Bounds.Height, profileBorder.Bounds.Height, 0.5);
            var connect = window.FindControl<Button>("ConnectButton")!;
            var login = window.FindControl<Button>("LoginButton")!;
            Assert.AreEqual(connect.TranslatePoint(default, window)!.Value.Y, login.TranslatePoint(default, window)!.Value.Y, 0.5);
            Assert.AreEqual(connect.Bounds.Size, login.Bounds.Size);
            Save(window, "ribbon-" + width);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task VendorProfileHasAnEditableDropDownWithoutSubmittingLogin() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SessionTestData.SampleFields() };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var profile = window.FindControl<AutoCompleteBox>("VendorProfileField")!;
            profile.Text = "CUSTOM";
            window.FindControl<Button>("VendorProfileDropDownButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout(window);
            Assert.IsTrue(profile.IsDropDownOpen);
            Assert.AreEqual("CUSTOM", profile.Text);
            Assert.AreEqual(0, model.LogDocument.TextLength);
            profile.SelectedItem = "ENVISIONWARE";
            Assert.AreEqual("ENVISIONWARE", model.Fields.VendorProfile);
            profile.IsDropDownOpen = false;
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task DefaultSettingsUseAlignedLabelsAndFitWithoutScrolling(bool dark) => ui.Dispatch(() =>
    {
        var window = new SettingsWindow(new AppPreferences { Defaults = SessionTestData.SampleFields() })
        { RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            var encoding = window.FindControl<AutoCompleteBox>("EncodingField")!;
            encoding.Text = "windows-1252";
            Assert.AreEqual("windows-1252", ((AppPreferences)window.DataContext!).EncodingName);
            window.FindControl<Button>("EncodingDropDownButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout(window);
            Assert.IsTrue(encoding.IsDropDownOpen);
            encoding.SelectedItem = "ISO-8859-1";
            Assert.AreEqual("ISO-8859-1", ((AppPreferences)window.DataContext!).EncodingName);
            encoding.IsDropDownOpen = false;
            encoding.Text = "UTF-8";
            foreach (var size in new[] { new Size(900, 600), new Size(800, 560) })
            {
                window.Width = size.Width;
                window.Height = size.Height;
                Layout(window);
                var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(viewer => viewer.Content is Grid);
                Save(window, $"settings-{(dark ? "dark" : "light")}-{size.Width}");
                Assert.IsTrue(scroll.Extent.Height <= scroll.Viewport.Height + 1, "Default settings require scrolling.");
                foreach (var row in window.GetVisualDescendants().OfType<OptionalFieldRow>())
                {
                    Assert.IsTrue(row.Field!.Bounds.X > 0, row.Label + " should fit beside its value.");
                    Assert.IsTrue(row.Field.Bounds.Right <= row.Bounds.Width + 0.5);
                }
            }
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    private static void Save(Window window, string name)
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, ".git")) && !File.Exists(Path.Combine(root.FullName, ".git"))) { root = root.Parent; }
        var directory = Path.Combine(root?.FullName ?? AppContext.BaseDirectory, "artifacts", "layout-comparison", "avalonia");
        Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame();
        Assert.IsNotNull(frame);
        frame.Save(Path.Combine(directory, name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }
}
