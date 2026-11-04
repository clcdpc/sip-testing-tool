using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Search;
using CommunityToolkit.Mvvm.Input;

namespace Clc.Sip.Desktop.Views;

internal static class LogSearchAccessibility
{
    public static void Attach(SearchPanel panel)
    {
        var commands = new List<IRelayCommand>();
        AutomationProperties.SetAccessibilityView(panel, AccessibilityView.Raw);
        panel.PropertyChanged += (_, args) =>
        {
            if (args.Property == SearchPanel.IsReplaceModeProperty)
            {
                foreach (var command in commands) { command.NotifyCanExecuteChanged(); }
            }
        };
        panel.TemplateApplied += (_, args) =>
        {
            commands.Clear();
            var search = args.NameScope.Find<TextBox>("PART_searchTextBox");
            Name(search, "Find in message log");
            Name(args.NameScope.Find<TextBox>("PART_replaceTextBox"), "Replace with");
            Name(args.NameScope.Find<ToggleButton>("Expander"), "Show replacement controls");
            // The installed template has no names for these buttons. Public commands
            // identify their actions; toggle tooltips supply localized option names.
            // InnerRightContent is not a visual child until the TextBox is styled.
            // Read that public content property so the toggles are named before focus.
            var controls = panel.GetVisualDescendants().OfType<Control>();
            if (search?.InnerRightContent is Control options)
            {
                controls = controls.Concat(options.GetVisualDescendants().OfType<Control>().Prepend(options));
            }
            var templateControls = controls.Distinct().ToArray();
            foreach (var button in templateControls.OfType<Button>())
            {
                var name = button.Command == SearchCommands.FindNext ? "Next match" :
                    button.Command == SearchCommands.FindPrevious ? "Previous match" :
                    button.Command == SearchCommands.CloseSearchPanel ? "Close search" :
                    button.Command == SearchCommands.ReplaceNext ? "Replace next match" :
                    button.Command == SearchCommands.ReplaceAll ? "Replace all matches" : null;
                if (name != null)
                {
                    Name(button, name);
                    if (button.Command is RoutedCommand routed)
                    {
                        // AvaloniaEdit 12's ICommand adapter caches the last focused
                        // target and has no CanExecuteChanged implementation. Query
                        // and execute its public command against this panel instead.
                        var command = new RelayCommand(() => routed.Execute(button.CommandParameter, panel),
                            () => routed.CanExecute(button.CommandParameter, panel));
                        commands.Add(command);
                        button.Command = command;
                    }
                }
                else if (button is ToggleButton && ToolTip.GetTip(button) is string tip)
                {
                    Name(button, tip);
                    // A string tooltip also becomes HelpText, repeating the name.
                    // Keep the same visible tooltip without that duplicate description.
                    ToolTip.SetTip(button, new TextBlock { Text = tip });
                }
                if (button.Content is Path graphic) { AutomationProperties.SetAccessibilityView(graphic, AccessibilityView.Raw); }
            }
            foreach (var control in templateControls)
            {
                if (control is Path || (control.TemplatedParent == panel && control is Panel or Border or ContentPresenter))
                {
                    AutomationProperties.SetAccessibilityView(control, AccessibilityView.Raw);
                }
            }
        };
    }

    private static void Name(Control? control, string name)
    {
        if (control != null && string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)))
        {
            AutomationProperties.SetName(control, name);
        }
    }
}
