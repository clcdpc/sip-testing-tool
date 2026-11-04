using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace Clc.Sip.Desktop.Views;

// Use the documented editor template part, rather than searching visual descendants.
public sealed class AccessibleAutoCompleteBox : AutoCompleteBox
{
    private readonly List<IDisposable> editorBindings = [];
    internal TextBox? Editor { get; private set; }
    protected override Type StyleKeyOverride => typeof(AutoCompleteBox);

    public AccessibleAutoCompleteBox()
    {
        // Keyboard focus belongs to the editor. Do not announce an extra named group.
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        foreach (var binding in editorBindings) { binding.Dispose(); }
        editorBindings.Clear();
        base.OnApplyTemplate(e);
        Editor = e.NameScope.Find<TextBox>("PART_TextBox");
        if (Editor == null) { return; }
        // Style priority preserves a template's explicit editor metadata. Rebind each
        // time the template is recreated, including when changing themes.
        editorBindings.Add(Editor.Bind(AutomationProperties.NameProperty,
            this.GetObservable(AutomationProperties.NameProperty), BindingPriority.Style));
        editorBindings.Add(Editor.Bind(AutomationProperties.LabeledByProperty,
            this.GetObservable(AutomationProperties.LabeledByProperty), BindingPriority.Style));
    }
}
