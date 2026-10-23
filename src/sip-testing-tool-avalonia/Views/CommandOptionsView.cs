using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Clc.Sip.Desktop.Models;
using Clc.Sip.Desktop.ViewModels;

namespace Clc.Sip.Desktop.Views;

public sealed class CommandOptionsView : UserControl
{
    private MainWindowViewModel? model;
    private readonly StackPanel fields = new() { Spacing = 14 };

    public CommandOptionsView()
    {
        Content = fields;
        DataContextChanged += (_, _) =>
        {
            if (model != null) { model.PropertyChanged -= ModelChanged; }
            model = DataContext as MainWindowViewModel;
            if (model != null) { model.PropertyChanged += ModelChanged; }
            Rebuild();
        };
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedCommand)) { Rebuild(); }
    }

    private void Rebuild()
    {
        fields.Children.Clear();
        if (model == null) { return; }
        var command = model.SelectedCommand.Command;
        Control[] controls = command switch
        {
            SipCommand.Checkout =>
            [
                Pair(Flag("Allow renewals", nameof(CommandOptions.RenewalAllowed)),
                    Flag("No block", nameof(CommandOptions.NoBlock), "Reports an already completed offline checkout.")),
                Date("No-block due date/time"),
                Text("Item properties (CH)", nameof(CommandOptions.ItemProperties)),
                TerminalPassword(),
                Pair(Choice("Fee acknowledged (BO)", nameof(CommandOptions.FeeAcknowledged)), Choice("Cancel (BI)", nameof(CommandOptions.Cancel)))
            ],
            SipCommand.Checkin =>
            [
                Flag("No block", nameof(CommandOptions.NoBlock), "Reports an already completed offline checkin."),
                Date("Return date/time", "Unchecked uses the time of execution."),
                Text("Return location (AP)", nameof(CommandOptions.ReturnLocation), "Leave blank to use the shared Terminal (AP)."),
                Text("Item properties (CH)", nameof(CommandOptions.ItemProperties)),
                TerminalPassword(),
                Choice("Cancel (BI)", nameof(CommandOptions.Cancel))
            ],
            SipCommand.Renew =>
            [
                Flag("Allow third-party renewals", nameof(CommandOptions.ThirdPartyAllowed)),
                Flag("No block", nameof(CommandOptions.NoBlock), "Reports an already completed offline renewal."),
                Date("No-block due date/time"),
                Flag("Use shared item barcode (AB)", nameof(CommandOptions.IncludeItemBarcode), "Uncheck to omit AB when renewing by title."),
                Text("Title identifier (AJ)", nameof(CommandOptions.TitleIdentifier)),
                Text("Item properties (CH)", nameof(CommandOptions.ItemProperties)),
                TerminalPassword(),
                Choice("Fee acknowledged (BO)", nameof(CommandOptions.FeeAcknowledged))
            ],
            SipCommand.RenewAll => [TerminalPassword(), Choice("Fee acknowledged (BO)", nameof(CommandOptions.FeeAcknowledged))],
            SipCommand.PatronInformation or SipCommand.PatronStatus =>
            [
                Text("Language code", nameof(CommandOptions.LanguageCode), "Three digits; 000 requests the default language.", 3),
                TerminalPassword()
            ],
            SipCommand.FeePaid =>
            [
                Text("Currency code", nameof(CommandOptions.Currency), "Three letters, such as USD, CAD or EUR.", 3),
                TerminalPassword()
            ],
            SipCommand.Login =>
            [
                Text("User ID algorithm", nameof(CommandOptions.UidAlgorithm), "0 sends plain text; other identifiers describe already encoded values agreed with the server.", 1),
                Text("Password algorithm", nameof(CommandOptions.PwdAlgorithm), "Choosing an algorithm does not encrypt the visible credentials.", 1),
                new TextBlock { Classes = { "hint" }, Text = "Credentials come from the ribbon and are sent as entered. Choosing an algorithm identifier does not encrypt them." }
            ],
            SipCommand.AcsStatus =>
            [
                Choice("Self-check status", nameof(CommandOptions.AcsStatus), ["OK (0)", "Out of paper (1)", "About to shut down (2)"], ["0", "1", "2"]),
                Text("Maximum print width", nameof(CommandOptions.PrintWidth), "Number of characters, from 0 through 999.", 3),
                Text("Protocol version", nameof(CommandOptions.ProtocolVersion), "Four characters in x.xx format.", 4)
            ],
            _ => [TerminalPassword()]
        };
        fields.Children.AddRange(controls);
        var caption = command == SipCommand.AcsStatus ? "ACS status" : model.SelectedCommand.Name.ToLowerInvariant();
        var reset = new Button { Content = "Reset " + caption + " options", HorizontalAlignment = HorizontalAlignment.Left };
        reset.Bind(Button.CommandProperty, new Binding(nameof(MainWindowViewModel.ResetOptionsCommand)));
        fields.Children.Add(reset);
    }

    private static Control Pair(Control left, Control right)
    {
        left.Margin = new Thickness(0, 0, 8, 0);
        left.VerticalAlignment = right.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetColumn(right, 1);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Children = { left, right } };
    }

    private static Control Row(string label, Control field, string hint)
    {
        AutomationProperties.SetName(field, label);
        ToolTip.SetTip(field, hint);
        return new OptionalFieldRow { Label = label, Field = field };
    }

    private static Binding Option(string property) => new("Options." + property) { Mode = BindingMode.TwoWay };

    private static Control TerminalPassword() => Text("Terminal password (AC)", nameof(CommandOptions.TerminalPassword),
        "Separate from login credentials and the patron PIN. Blank preserves the command's existing AC behavior.");

    private static Control Text(string label, string property, string hint = "Blank omits this optional field.", int maxLength = 32767)
    {
        var field = new TextBox { Name = property + "Field", MaxLength = maxLength };
        field.Bind(TextBox.TextProperty, Option(property));
        return Row(label, field, hint);
    }

    private static Control Flag(string label, string property, string? hint = null)
    {
        var field = new CheckBox { Name = property + "Field", Content = label };
        field.Bind(CheckBox.IsCheckedProperty, Option(property));
        if (hint != null) { ToolTip.SetTip(field, hint); }
        return field;
    }

    private static Control Choice(string label, string property, string[]? labels = null, string[]? values = null)
    {
        var field = new ComboBox { Name = property + "Field", ItemsSource = labels ?? ["Omit field", "No (N)", "Yes (Y)"], HorizontalAlignment = HorizontalAlignment.Stretch };
        var binding = Option(property);
        binding.Converter = new ChoiceIndexConverter(values ?? ["", "N", "Y"]);
        field.Bind(ComboBox.SelectedIndexProperty, binding);
        return Row(label, field, "Omission and explicit values are sent distinctly.");
    }

    private static Control Date(string label, string hint = "Unchecked sends a blank date; check to include the entered date/time.")
    {
        var field = new OptionalDateTimeInput { Name = nameof(CommandOptions.DateText) + "Field" };
        field.Bind(OptionalDateTimeInput.DateTextProperty, Option(nameof(CommandOptions.DateText)));
        field.Bind(OptionalDateTimeInput.IsIncludedProperty, Option(nameof(CommandOptions.UseDate)));
        return Row(label, field, hint);
    }

    private sealed class ChoiceIndexConverter(string[] values) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Array.IndexOf(values, value as string);
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is int index && index >= 0 && index < values.Length ? values[index] : "";
    }
}
