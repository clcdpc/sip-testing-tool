using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Clc.Sip.Desktop.Models;
using Clc.Sip.Desktop.ViewModels;
using Clc.Sip.Desktop.Views;

namespace Clc.Sip.Desktop.Tests;

public sealed partial class DesktopParityTests
{
    [TestMethod]
    [DataRow(false, 280)]
    [DataRow(true, 280)]
    [DataRow(false, 312)]
    [DataRow(true, 312)]
    [DataRow(false, 500)]
    [DataRow(true, 500)]
    public Task DropdownLayoutStaysStableForEveryToolboxChoiceAndRebuiltPanel(bool dark, int panelWidth) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        model.Preferences.DarkTheme = dark;
        var window = new MainWindow { DataContext = model, Width = 1060, Height = 720 };
        window.Show();
        try
        {
            var picker = window.FindControl<AccessibleComboBox>("CommandPicker")!;
            var columns = picker.GetVisualAncestors().OfType<Grid>().Last(grid => grid.ColumnDefinitions.Count == 3);
            columns.ColumnDefinitions[0].Width = new GridLength(panelWidth);
            AssertStableDropdownInput(window, picker);
            var names = new HashSet<string>();
            foreach (var command in CommandChoice.All)
            {
                model.SelectedCommand = command;
                Layout(window);
                foreach (var choice in window.GetVisualDescendants().OfType<AccessibleComboBox>()
                    .Where(choice => choice.IsEffectivelyVisible && choice != picker).ToArray())
                {
                    names.Add(Peer(choice).GetName());
                    AssertStableDropdownInput(window, choice);
                }
                var scroll = window.GetVisualDescendants().OfType<ScrollViewer>()
                    .Single(view => view.GetVisualDescendants().OfType<CommandOptionsView>().Any());
                Assert.IsTrue(scroll.Extent.Width <= scroll.Viewport.Width + 1, command.Name + " overflows horizontally.");
                if (panelWidth >= 312)
                {
                    Assert.IsTrue(scroll.Extent.Height <= scroll.Viewport.Height + 1, command.Name + " no longer fits the minimum window.");
                }
            }
            foreach (var name in new[] { "Summary", "Self-check status", "Fee acknowledged (BO)", "Cancel (BI)", "Delivery status", "Delivery medium", "Notice type" })
            {
                Assert.Contains(name, names);
            }
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(60));

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public Task DropdownLayoutMeasuresTheLongestDisplayedOptionWithTheRealTheme(bool dark, bool useTemplate) => ui.Dispatch(() =>
    {
        DropdownDisplay[] options = [new("0", "About to shut down (2)"), new("A much longer underlying value that is never displayed", "OK"), new("1", "Out of paper (1)")];
        var template = useTemplate ? new FuncDataTemplate<DropdownDisplay>((item, _) =>
            new Border { Padding = new Thickness(9, 0), Child = new TextBlock { Text = item?.Label } }) : null;
        var binding = useTemplate ? null : new Binding(nameof(DropdownDisplay.Label));
        var choice = new AccessibleComboBox { ItemsSource = options, SelectedIndex = 1, ItemTemplate = template, DisplayMemberBinding = binding };
        var reference = new ComboBox { ItemsSource = options, SelectedIndex = 1, ItemTemplate = template, DisplayMemberBinding = binding };
        var window = new Window
        {
            Width = 600, Height = 200, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new StackPanel { Children = { choice, reference } }
        };
        window.Show();
        try
        {
            Layout(window);
            var longest = 0d;
            for (var index = 0; index < options.Length; index++)
            {
                reference.SelectedIndex = index;
                reference.Measure(Size.Infinity);
                longest = Math.Max(longest, reference.DesiredSize.Width);
            }
            Assert.AreEqual(Math.Ceiling(longest), choice.RequiredWidth, 0.01, "Include the displayed content, padding and themed indicator.");
            Layout(window);
            Assert.AreEqual(choice.RequiredWidth, choice.Bounds.Width, 1);
            var bounds = choice.Bounds;
            foreach (var index in new[] { 0, 2, 1 })
            {
                choice.SelectedIndex = index;
                Layout(window);
                Assert.AreEqual(bounds, choice.Bounds);
            }
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task DropdownLayoutStacksOnlyWhenTheLongestOptionCannotFitInline(bool dark) => ui.Dispatch(() =>
    {
        var choice = new AccessibleComboBox { ItemsSource = new[] { "OK (0)", "Out of paper (1)", "About to shut down (2)" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var row = new OptionalFieldRow { Label = "Self-check status", Field = choice, HorizontalAlignment = HorizontalAlignment.Left };
        var following = new TextBox();
        var window = new Window
        {
            Width = 600, Height = 240, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new StackPanel { Children = { row, following } }
        };
        NameScope.SetNameScope(window, new NameScope());
        window.Show();
        try
        {
            Layout(window);
            var required = choice.RequiredWidth;
            var caption = row.Children.OfType<TextBlock>().Single();
            caption.Measure(Size.Infinity);
            var threshold = caption.DesiredSize.Width + 8 + Math.Max(140, required);
            foreach (var width in new[] { threshold + 2, threshold - 2, required / 2 })
            {
                row.Width = width;
                Layout(window);
                Assert.AreEqual(width >= threshold, choice.Bounds.X > 0, "Only genuine space constraints should stack the label.");
                Assert.IsTrue(choice.Bounds.Right <= row.Bounds.Width + 1);
                var rowBounds = row.Bounds;
                var nextBounds = following.Bounds;
                AssertStableDropdownInput(window, choice);
                Assert.AreEqual(rowBounds, row.Bounds);
                Assert.AreEqual(nextBounds, following.Bounds, "Selecting an option must not move subsequent fields.");
            }
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task DropdownLayoutInvalidatesForOptionsAndStylingButNotSelection() => ui.Dispatch(() =>
    {
        var options = new ObservableCollection<DropdownDisplay> { new("0", "OK"), new("1", "Out of paper"), new("2", "About to shut down") };
        var presentations = new List<DropdownDisplay>();
        var template = new FuncDataTemplate<DropdownDisplay>((item, _) =>
        {
            if (item != null) { presentations.Add(item); }
            return new TextBlock { Text = item?.Label };
        });
        var choice = new AccessibleComboBox { ItemsSource = options, SelectedIndex = 0, ItemTemplate = template };
        var row = new OptionalFieldRow { Label = "Status", Field = choice };
        var window = new Window { Width = 600, Height = 180, Content = row };
        window.Show();
        try
        {
            Layout(window);
            var original = choice.RequiredWidth;
            presentations.Clear();
            choice.SelectedIndex = 1;
            Layout(window);
            Assert.AreEqual(original, choice.RequiredWidth);
            Assert.DoesNotContain(options[2], presentations, "Selection must not remeasure unselected options.");

            var longer = new DropdownDisplay("3", "A newly available option with a much longer display label");
            options.Add(longer);
            Layout(window);
            Assert.IsTrue(choice.RequiredWidth > original, "Adding a longer option must increase the reserved width.");
            Assert.Contains(longer, presentations);
            options.Remove(longer);
            Layout(window);
            Assert.AreEqual(original, choice.RequiredWidth, "Removing the longest option must release its width.");
            options[2] = longer;
            Layout(window);
            Assert.IsTrue(choice.RequiredWidth > original);

            choice.ItemsSource = new[] { options[0], options[1] };
            Layout(window);
            Assert.IsTrue(choice.RequiredWidth < original);
            var smaller = choice.RequiredWidth;
            choice.FontSize *= 1.5;
            Layout(window);
            Assert.IsTrue(choice.RequiredWidth > smaller);
            var fontWidth = choice.RequiredWidth;
            choice.Padding = new Thickness(20, 2);
            Layout(window);
            Assert.IsTrue(choice.RequiredWidth > fontWidth);
            presentations.Clear();
            window.RequestedThemeVariant = ThemeVariant.Dark;
            Layout(window);
            Assert.Contains(options[0], presentations, "Changing the theme must invalidate cached measurements.");
            choice.SelectionBoxItemTemplate = new FuncDataTemplate<DropdownDisplay>((item, _) =>
                new Border { Padding = new Thickness(30, 0), Child = new TextBlock { Text = item?.Label } });
            Layout(window);
            Assert.IsTrue(choice.RequiredWidth > fontWidth + 40, "Measure the selection template when it differs from the item template.");
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    private static void AssertStableDropdownInput(Window window, AccessibleComboBox choice)
    {
        choice.SelectedIndex = 0;
        Layout(window);
        Assert.IsTrue(choice.Focus());
        var peer = Peer(choice);
        var name = peer.GetName();
        var description = peer.GetHelpText();
        var bounds = choice.Bounds;
        var row = choice.GetVisualAncestors().OfType<OptionalFieldRow>().FirstOrDefault();
        var rowBounds = row?.Bounds;
        var required = choice.RequiredWidth;
        var notifications = 0;
        peer.PropertyChanged += (_, e) =>
        {
            if (e.Property == ValuePatternIdentifiers.ValueProperty) { notifications++; }
        };
        void AssertUnmoved()
        {
            Layout(window);
            Assert.AreEqual(bounds, choice.Bounds, name + " moved or resized.");
            Assert.AreEqual(rowBounds, row?.Bounds, name + " changed its label arrangement.");
            Assert.AreEqual(required, choice.RequiredWidth);
            Assert.AreEqual(name, peer.GetName());
            Assert.AreEqual(description, peer.GetHelpText());
        }
        foreach (var direction in new[] { 1, -1 })
        {
            for (var step = 0; step < choice.ItemCount - 1; step++)
            {
                var index = choice.SelectedIndex + direction;
                Press(window, direction > 0 ? Key.Down : Key.Up);
                AssertUnmoved();
                Assert.AreEqual(index, choice.SelectedIndex);
                Assert.IsTrue(choice.IsFocused);
            }
        }
        choice.IsDropDownOpen = true;
        AssertUnmoved();
        var mouseIndex = Math.Min(2, choice.ItemCount - 1);
        var item = (Control)choice.ContainerFromIndex(mouseIndex)!;
        Assert.IsNotNull(item);
        var popup = TopLevel.GetTopLevel(item)!;
        var point = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), popup)!.Value;
        popup.MouseDown(point, MouseButton.Left);
        popup.MouseUp(point, MouseButton.Left);
        AssertUnmoved();
        Assert.AreEqual(mouseIndex, choice.SelectedIndex);
        Assert.IsFalse(choice.IsDropDownOpen);
        Assert.IsTrue(choice.IsFocused);
        Assert.AreEqual((choice.ItemCount - 1) * 2 + 1, notifications, "Sizing must not add selection announcements.");
    }

    private sealed record DropdownDisplay(string Value, string Label)
    {
        public override string ToString() => Value;
    }
}
