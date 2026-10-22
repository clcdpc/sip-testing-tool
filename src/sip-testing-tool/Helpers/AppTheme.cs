using System.Configuration;
using System.Runtime.InteropServices;

namespace Clc.Sip.Helpers;

public static class AppTheme
{
    private static readonly ThemePreferenceStore preferences = new();
    private static bool initialized;
    private static event EventHandler Changed;

    public static bool IsDarkMode { get; private set; }
    private static bool UseDarkPalette => IsDarkMode && !SystemInformation.HighContrast;
    public static Color Text => UseDarkPalette ? Color.FromArgb(230, 232, 235) : SystemColors.ControlText;
    public static Color Window => UseDarkPalette ? Color.FromArgb(30, 32, 36) : SystemColors.Control;
    public static Color Surface => UseDarkPalette ? Color.FromArgb(38, 41, 46) : SystemColors.Control;
    public static Color Input => UseDarkPalette ? Color.FromArgb(47, 51, 58) : SystemColors.Window;
    public static Color LogBackground => UseDarkPalette ? Color.FromArgb(23, 25, 29) : SystemColors.Window;
    public static Color Border => UseDarkPalette ? Color.FromArgb(71, 78, 89) : SystemColors.ControlDark;
    public static Color Sent => SystemInformation.HighContrast ? SystemColors.WindowText : UseDarkPalette ? Color.FromArgb(242, 139, 130) : Color.Crimson;
    public static Color Received => SystemInformation.HighContrast ? SystemColors.WindowText : UseDarkPalette ? Color.FromArgb(138, 180, 248) : Color.RoyalBlue;
    public static Color Separator => UseDarkPalette ? Color.FromArgb(165, 174, 188) : SystemColors.WindowText;

    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        ReloadPreference();
    }

    public static void ReloadPreference()
    {
        bool.TryParse(ConfigurationManager.AppSettings["dark_theme_enabled"], out var legacyDefault);
        SetDarkMode(preferences.Load(legacyDefault));
    }

    public static void SetDarkMode(bool enabled)
    {
        IsDarkMode = enabled;
        // Keep Win32's process-wide color mode stable. Changing it on live controls
        // can leave native edit brushes in the previous mode. Our palette owns the
        // client area; the title bar is updated separately below.
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static void SavePreference() => preferences.Save(IsDarkMode);

    public static void Attach(Form form, Action afterApply = null)
    {
        void Apply(object sender, EventArgs args)
        {
            form.SuspendLayout();
            try
            {
                ApplyControl(form, Window);
                if (form.ContextMenuStrip != null)
                {
                    ApplyToolStrip(form.ContextMenuStrip);
                }

                if (form.IsHandleCreated)
                {
                    var dark = UseDarkPalette ? 1 : 0;
                    _ = DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int));
                }
                afterApply?.Invoke();
            }
            finally
            {
                form.ResumeLayout();
                form.Invalidate(true);
            }
        }

        Changed += Apply;
        form.HandleCreated += Apply;
        form.Disposed += (_, _) =>
        {
            Changed -= Apply;
            form.HandleCreated -= Apply;
        };
        Apply(form, EventArgs.Empty);
    }

    private static void ApplyControl(Control control, Color parentBackground)
    {
        control.SuspendLayout();
        try
        {
            var background = control.Name == "logLayout" ? Border
                : control.Name is "ribbonLayout" or "workbenchLayout" or "logHeaderLayout" ? Surface : parentBackground;
            control.ForeColor = Text;
            switch (control)
            {
                case ToolStrip strip:
                    ApplyToolStrip(strip);
                    return;
                case RichTextBox log:
                    log.BackColor = LogBackground;
                    break;
                case TextBoxBase input:
                    input.BackColor = Input;
                    if (input is ThemedTextBox themedInput)
                    {
                        themedInput.RefreshTheme();
                    }

                    break;
                case ComboBox combo:
                    combo.BackColor = Input;
                    combo.FlatStyle = UseDarkPalette ? FlatStyle.Flat : FlatStyle.Standard;
                    if (combo is ThemedComboBox)
                    {
                        combo.DrawMode = UseDarkPalette ? DrawMode.OwnerDrawFixed : DrawMode.Normal;
                    }

                    break;
                case Button button:
                    button.FlatStyle = UseDarkPalette ? FlatStyle.Flat : FlatStyle.Standard;
                    button.UseVisualStyleBackColor = !UseDarkPalette;
                    button.BackColor = UseDarkPalette ? Color.FromArgb(54, 60, 69) : SystemColors.Control;
                    button.FlatAppearance.BorderColor = Border;
                    button.FlatAppearance.MouseOverBackColor = UseDarkPalette ? Color.FromArgb(69, 77, 89) : SystemColors.ControlLight;
                    button.FlatAppearance.MouseDownBackColor = UseDarkPalette ? Color.FromArgb(44, 51, 62) : SystemColors.ControlDark;
                    if (UseDarkPalette && button.Name == "executeCommandButton")
                    {
                        button.BackColor = Color.FromArgb(37, 87, 155);
                        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(46, 105, 184);
                    }
                    break;
                case CheckBox checkbox:
                    checkbox.FlatStyle = UseDarkPalette ? FlatStyle.Flat : FlatStyle.Standard;
                    checkbox.UseVisualStyleBackColor = !UseDarkPalette;
                    checkbox.BackColor = background;
                    break;
                case ThemedDateTimePicker picker:
                    picker.ApplyTheme();
                    return;
                default:
                    control.BackColor = background;
                    break;
            }
            foreach (Control child in control.Controls)
            {
                ApplyControl(child, control.BackColor);
            }
        }
        finally { control.ResumeLayout(true); }
    }

    private static void ApplyToolStrip(ToolStrip strip)
    {
        strip.BackColor = Surface;
        strip.ForeColor = Text;
        strip.Renderer = UseDarkPalette
            ? new ToolStripProfessionalRenderer(new DarkMenuColors()) { RoundedEdges = false }
            : new ToolStripProfessionalRenderer();
        foreach (ToolStripItem item in strip.Items)
        {
            item.ForeColor = Text;
            item.BackColor = Surface;
            if (item is ToolStripDropDownItem dropDown && dropDown.HasDropDownItems)
            {
                ApplyToolStrip(dropDown.DropDown);
            }
        }
    }

    private sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Surface;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Border;
        public override Color MenuItemSelected => Color.FromArgb(58, 70, 88);
        public override Color MenuItemSelectedGradientBegin => MenuItemSelected;
        public override Color MenuItemSelectedGradientEnd => MenuItemSelected;
        public override Color MenuItemPressedGradientBegin => MenuItemSelected;
        public override Color MenuItemPressedGradientMiddle => MenuItemSelected;
        public override Color MenuItemPressedGradientEnd => MenuItemSelected;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color MenuStripGradientBegin => Surface;
        public override Color MenuStripGradientEnd => Surface;
        public override Color StatusStripGradientBegin => Surface;
        public override Color StatusStripGradientEnd => Surface;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Surface;
        public override Color CheckBackground => MenuItemSelected;
        public override Color CheckSelectedBackground => MenuItemSelected;
        public override Color CheckPressedBackground => MenuItemSelected;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
