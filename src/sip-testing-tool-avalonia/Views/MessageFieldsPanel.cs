using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Clc.Sip.Helpers;

namespace Clc.Sip.Desktop.Views;

public sealed record MessageFieldRow(SipLogDetail Field, int Depth)
{
    public string Name => new string(' ', Depth * 2) + (Depth > 0 ? "↳ " : "") + Field.Name;
}

public sealed class MessageFieldsPanel : UserControl
{
    private const string Columns = "28*,41*,31*";
    private readonly TextBlock title = new() { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private readonly ListBox table = new() { Classes = { "messageFields" }, Padding = new Thickness(0), BorderThickness = new Thickness(0) };
    public IReadOnlyList<MessageFieldRow> Rows { get; private set; } = [];
    public event Action? CollapseRequested;

    public MessageFieldsPanel()
    {
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(8, 4) };
        var copy = new Button { Content = "Copy table", Margin = new Thickness(6, 0) };
        copy.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard) { await clipboard.SetTextAsync(CopyText()); }
        };
        var collapse = new Button { Content = "− Collapse" };
        collapse.Click += (_, _) => CollapseRequested?.Invoke();
        Grid.SetColumn(copy, 1);
        Grid.SetColumn(collapse, 2);
        header.Children.Add(title);
        header.Children.Add(copy);
        header.Children.Add(collapse);
        layout.Children.Add(header);
        var labels = new Grid { ColumnDefinitions = new ColumnDefinitions(Columns), Margin = new Thickness(2, 4), HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var (name, column) in new[] { ("Field", 0), ("Description", 1), ("Value", 2) })
        {
            var label = new TextBlock { Text = name, FontWeight = FontWeight.SemiBold, Margin = new Thickness(4, 0, 8, 0) };
            Grid.SetColumn(label, column);
            labels.Children.Add(label);
        }
        Grid.SetRow(labels, 1);
        layout.Children.Add(labels);
        table.Bind(BackgroundProperty, new DynamicResourceExtension("LogBrush"));
        table.TemplateApplied += (_, args) =>
        {
            if (args.NameScope.Find<ScrollViewer>("PART_ScrollViewer") is not { } scroll) { return; }
            void AlignHeader() => labels.Width = Math.Max(0, scroll.Viewport.Width - 4);
            scroll.PropertyChanged += (_, change) =>
            {
                if (change.Property == ScrollViewer.ViewportProperty) { AlignHeader(); }
            };
            AlignHeader();
        };
        table.ItemTemplate = new FuncDataTemplate<MessageFieldRow>((row, _) =>
        {
            if (row == null) { return new TextBlock(); }
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(Columns), Margin = new Thickness(2, 4) };
            foreach (var (text, column) in new[] { (row.Name, 0), (row.Field.Description, 1), (row.Field.DisplayValue, 2) })
            {
                var cell = new SelectableTextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0, 8, 0) };
                if (row.Field.Children != null) { cell.FontWeight = FontWeight.SemiBold; }
                if (column == 2 && row.Field.State != SipLogValueState.Normal)
                {
                    cell.FontWeight = FontWeight.SemiBold;
                    cell.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension(row.Field.State == SipLogValueState.Set ? "InspectorSetBrush" : "InspectorInvalidBrush"));
                }
                Grid.SetColumn(cell, column);
                grid.Children.Add(cell);
            }
            var border = new Border { Child = grid, BorderThickness = new Thickness(0, 0, 0, 1) };
            border.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("LineBrush"));
            if (row.Field.Children != null) { border.Bind(Border.BackgroundProperty, new DynamicResourceExtension("InspectorGroupBrush")); }
            return border;
        });
        ScrollViewer.SetHorizontalScrollBarVisibility(table, Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
        Grid.SetRow(table, 2);
        layout.Children.Add(table);
        Content = layout;
        AddHandler(InputElement.KeyDownEvent, (_, args) =>
        {
            if (args.Key == Key.Escape) { CollapseRequested?.Invoke(); args.Handled = true; }
        }, RoutingStrategies.Tunnel);
    }

    public void ShowMessage(SipLogMessageDetails details, string? messageLabel = null)
    {
        var rows = new List<MessageFieldRow>();
        void Add(IEnumerable<SipLogDetail> fields, int depth)
        {
            foreach (var field in fields)
            {
                rows.Add(new(field, depth));
                if (field.Children != null) { Add(field.Children, depth + 1); }
            }
        }
        Add(details.Fields, 0);
        title.Text = messageLabel == null ? details.Name : messageLabel + " · " + details.Name;
        Rows = rows;
        // Assign once so expanding a long response does not repeatedly size existing rows.
        table.ItemsSource = rows;
        table.SelectedIndex = -1;
        if (rows.Count > 0) { table.ScrollIntoView(rows[0]); }
    }

    public string CopyText()
    {
        var result = new StringBuilder("Field\tDescription\tValue\n");
        foreach (var row in Rows)
        {
            static string Cell(string value) => value.Replace("\t", " ").Replace("\r", "").Replace("\n", " / ");
            result.Append(Cell(row.Name)).Append('\t').Append(Cell(row.Field.Description)).Append('\t').Append(Cell(row.Field.DisplayValue)).AppendLine();
        }
        return result.ToString();
    }
}
