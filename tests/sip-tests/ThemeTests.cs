using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Clc.Sip.Helpers;
using Clc.Sip.Tests.TestSupport;
using static Clc.Sip.Tests.TestSupport.WinFormsTestEnvironment;

namespace Clc.Sip.Tests;

[STATestClass]
[TestCategory("Regression")]
public sealed class ThemeTests
{
    [ClassInitialize]
    public static void Initialize(TestContext _) => WinFormsTestEnvironment.Initialize();

    [TestMethod(DisplayName = "Theme preference survives relaunch and invalid files")]
    public void ThemePreferencesHandleRelaunchAndInvalidFiles()
    {
        var file = Path.Combine(Path.GetTempPath(), "sip-theme-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new ThemePreferenceStore(file);
            Assert.IsTrue(store.Load(true));
            store.Save(false);
            Assert.IsFalse(new ThemePreferenceStore(file).Load(true));
            store.Save(true);
            Assert.IsTrue(new ThemePreferenceStore(file).Load(false));
            File.WriteAllText(file, "{broken");
            Assert.IsFalse(store.Load(false));
            Assert.IsTrue(store.Load(true));
        }
        finally
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    [TestMethod(DisplayName = "Theme switches preserve the connected workbench")]
    public void ThemeSwitchesPreserveConnectionFieldsAndMessageHistory()
    {
        var originalTheme = AppTheme.IsDarkMode;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var server = Task.Run(() =>
        {
            using var peer = listener.AcceptTcpClient();
            for (var i = 0; i < 2; i++)
            {
                _ = SipRequest.ReadFrom(peer);
                peer.GetStream().Write(Encoding.ASCII.GetBytes("98YAY0AZFDD1\r"));
            }
        });
        AppTheme.SetDarkMode(false);
        using var form = new MainForm();
        ShowOffscreen(form);
        try
        {
            form.GetTextBox("serverTextBox").Text = "127.0.0.1";
            form.GetTextBox("portTextBox").Text = ((IPEndPoint)listener.LocalEndpoint).Port.ToString();
            form.GetTextBox("patronBarcodeTextBox").Text = "retained-patron";
            form.GetTextBox("itemBarcodeTextBox").Text = "retained-item";
            form.GetTextBox("aoTextBox").Text = "retained-institution";
            form.GetTextBox("apTextBox").Text = "retained-terminal";
            form.GetTextBox("usernameTextBox").Text = "retained-user";
            form.GetTextBox("passwordTextBox").Text = "retained-password";
            form.GetTextBox("locationCodeTextBox").Text = "retained-location";
            form.GetComboBox("vendorProfileComboBox").Text = "retained-custom-vendor";
            form.GetTextBox("patronPinTextBox").Text = "retained-pin";
            CheckoutTestData.ApplySelectedOptions(form);
            var execute = form.GetButton("executeCommandButton");
            Assert.IsFalse(execute.Enabled, "Disconnected commands must really be disabled in light mode.");
            form.GetButton("connectButton").PerformClick();
            execute.PerformClick();
            var session = form.SipClient;
            var log = form.GetRichTextBox("logRichTextBox");
            var history = log.Text;
            string[] sharedNames = { "serverTextBox", "portTextBox", "aoTextBox", "apTextBox", "usernameTextBox", "passwordTextBox", "locationCodeTextBox", "vendorProfileComboBox", "patronBarcodeTextBox", "patronPinTextBox", "itemBarcodeTextBox" };
            var sharedValues = sharedNames.ToDictionary(name => name, name => form.GetControl(name).Text);
            log.Select(8, 12);
            var menu = (ToolStripMenuItem)form.MainMenuStrip.Items.Find("darkModeMenuItem", true).Single();
            AppTheme.SetDarkMode(true);
            Assert.IsTrue(menu.Checked, "The View menu did not reflect the dark theme.");
            Assert.IsTrue(ReferenceEquals(session, form.SipClient) && session.IsConnected() && execute.Enabled, "Changing theme altered the connection or command state.");
            Assert.AreEqual(history, log.Text);
            Assert.AreEqual(8, log.SelectionStart);
            Assert.AreEqual(12, log.SelectionLength);
            foreach (var field in sharedValues)
            {
                Assert.AreEqual(field.Value, form.GetControl(field.Key).Text);
            }

            Assert.AreEqual("retained-patron", form.GetTextBox("patronBarcodeTextBox").Text);
            Assert.AreEqual("test-properties", form.GetTextBox("checkoutItemPropertiesTextBox").Text);
            Assert.IsTrue(form.GetDatePicker("checkoutDueDatePicker").Checked, "Changing theme reset the checkout date.");
            Assert.AreEqual(AppTheme.Input.ToArgb(), form.GetTextBox("serverTextBox").BackColor.ToArgb());
            Assert.AreEqual(AppTheme.Sent.ToArgb(), ColorAt(log, 0).ToArgb());
            Assert.AreEqual(AppTheme.Received.ToArgb(), ColorAt(log, log.Text.IndexOf("<--", StringComparison.Ordinal)).ToArgb());
            Assert.AreEqual(AppTheme.Separator.ToArgb(), ColorAt(log, log.Text.IndexOf('|')).ToArgb());
            Assert.IsTrue(Contrast(AppTheme.Text, AppTheme.Input) >= 4.5 && Contrast(AppTheme.Sent, AppTheme.LogBackground) >= 4.5
                && Contrast(AppTheme.Received, AppTheme.LogBackground) >= 4.5, "Dark message and input colors need readable contrast.");
            AppTheme.SetDarkMode(false);
            Assert.AreEqual(history, log.Text);
            Assert.IsTrue(!menu.Checked && execute.Enabled, "Returning to light mode altered command availability.");
            foreach (var field in sharedValues)
            {
                Assert.AreEqual(field.Value, form.GetControl(field.Key).Text);
            }

            Assert.AreEqual(AppTheme.Sent.ToArgb(), ColorAt(log, 0).ToArgb());
            execute.PerformClick();
            server.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            using var disconnected = new MainForm();
            AppTheme.SetDarkMode(true);
            Assert.IsTrue(!disconnected.GetButton("executeCommandButton").Enabled && !disconnected.GetButton("loginButton").Enabled,
                "Dark mode must not re-enable disconnected commands.");
        }
        finally
        {
            form.SipClient?.Dispose();
            AppTheme.SetDarkMode(originalTheme);
        }
    }

    [TestMethod(DisplayName = "Open dialogs follow live theme changes")]
    public void OpenDialogsFollowThemeChangesImmediately()
    {
        var originalTheme = AppTheme.IsDarkMode;
        using var form = new MainForm();
        using var settings = new SettingsForm();
        using var notice = new NoticeUpdateForm(form);
        ShowOffscreen(form);
        ShowOffscreen(settings);
        ShowOffscreen(notice);
        try
        {
            AppTheme.SetDarkMode(false);
            settings.GetCheckBox("darkThemeCheckBox").Checked = true;
            Assert.IsTrue(AppTheme.IsDarkMode, "The settings theme checkbox should apply immediately.");
            Assert.AreEqual(AppTheme.Input.ToArgb(), settings.GetTextBox("hostnameTextBox").BackColor.ToArgb());
            Assert.AreEqual(AppTheme.Input.ToArgb(), notice.GetComboBox("noticeMediumComboBox").BackColor.ToArgb());
            settings.GetCheckBox("darkThemeCheckBox").Checked = false;
            Assert.IsFalse(AppTheme.IsDarkMode, "The settings checkbox should also restore light mode immediately.");
            Assert.AreEqual(AppTheme.Input.ToArgb(), form.GetTextBox("serverTextBox").BackColor.ToArgb());
        }
        finally { AppTheme.SetDarkMode(originalTheme); }
    }

    [TestMethod(DisplayName = "Dark startup and repeated theme switches repaint input fields")]
    public void DarkStartupAndRepeatedThemeSwitchesRepaintInputs()
    {
        AppTheme.Initialize();
        var originalTheme = AppTheme.IsDarkMode;
        var nativeWindowColor = System.Drawing.SystemColors.Window.ToArgb();
        try
        {
            AppTheme.SetDarkMode(true);
            using var form = new MainForm();
            ShowOffscreen(form);
            form.GetTextBox("serverTextBox").Text = "Retain this value";
            var picker = form.GetDatePicker("checkoutDueDatePicker");
            picker.Value = new DateTime(2026, 10, 28, 14, 30, 45);
            picker.Checked = true;
            foreach (var dark in new[] { false, true, false, true, false })
            {
                AppTheme.SetDarkMode(dark);
                Application.DoEvents();
                foreach (var name in new[] { "serverTextBox", "passwordTextBox", "patronPinTextBox", "checkoutItemPropertiesTextBox" })
                {
                    ControlAppearanceAssertions.MatchCurrentTheme(form.GetTextBox(name));
                }

                ControlAppearanceAssertions.MatchCurrentTheme(picker.GetMaskedTextBox("dateTimeEditor"));
                Assert.AreEqual("Retain this value", form.GetTextBox("serverTextBox").Text);
                Assert.AreEqual(new DateTime(2026, 10, 28, 14, 30, 45), picker.Value);
                Assert.IsTrue(picker.Checked, "A theme change omitted the chosen date.");
                Assert.AreEqual(nativeWindowColor, System.Drawing.SystemColors.Window.ToArgb());
                using var settings = new SettingsForm();
                ShowOffscreen(settings);
                ControlAppearanceAssertions.MatchCurrentTheme(settings.GetTextBox("hostnameTextBox"));
            }
        }
        finally { AppTheme.SetDarkMode(originalTheme); }
    }

    [TestMethod(DisplayName = "Themed date editor validates, adjusts and selects calendar dates")]
    public void DateEditorValidatesAdjustsAndSelectsCalendarDates()
    {
        var originalTheme = AppTheme.IsDarkMode;
        using var form = new MainForm();
        ShowOffscreen(form);
        try
        {
            var picker = form.GetDatePicker("checkoutDueDatePicker");
            var editor = picker.GetMaskedTextBox("dateTimeEditor");
            picker.Value = new DateTime(2024, 2, 28, 14, 30, 45);
            picker.Checked = true;
            editor.Select(8, 2);
            KeyDown(editor, Keys.Up);
            Assert.AreEqual(new DateTime(2024, 2, 29, 14, 30, 45), picker.Value);
            editor.Select(14, 2);
            KeyDown(editor, Keys.Down);
            Assert.AreEqual(new DateTime(2024, 2, 29, 14, 29, 45), picker.Value);
            editor.Text = "2026-02-30 12:30:00";
            Assert.IsFalse(picker.TryGetValue(out _), "An impossible date must not reuse an earlier date.");
            KeyDown(editor, Keys.Escape);
            Assert.IsTrue(picker.TryGetValue(out _), "Escape should restore the last valid date.");
            editor.Text = "2026-10-__ __:__:__";
            Assert.IsFalse(picker.TryGetValue(out _), "Incomplete checked dates must be rejected.");
            picker.Checked = false;
            Assert.IsTrue(picker.TryGetValue(out _), "An omitted date should not require valid editor text.");
            picker.Value = new DateTime(2026, 10, 28, 14, 30, 45);
            AppTheme.SetDarkMode(true);
            KeyDown(editor, Keys.F4);
            var popup = DatePopup(picker);
            Assert.IsTrue(popup.Visible, "F4 should open the calendar.");
            var calendar = ((ToolStripControlHost)popup.Items[0]).Control;
            Assert.AreEqual(AppTheme.Input.ToArgb(), calendar.BackColor.ToArgb());
            AppTheme.SetDarkMode(false);
            Assert.AreEqual(AppTheme.Input.ToArgb(), calendar.BackColor.ToArgb());
            AppTheme.SetDarkMode(true);
            KeyDown(calendar.GetButton("calendarDay-2026-10-31"), Keys.Right);
            calendar.GetButton("calendarDay-2026-11-01").PerformClick();
            Assert.IsTrue(!popup.Visible && picker.Checked, "Selecting a date should close the calendar and include the date.");
            Assert.AreEqual(new DateTime(2026, 11, 1, 14, 30, 45), picker.Value);
        }
        finally { AppTheme.SetDarkMode(originalTheme); }
    }

    private static ToolStripDropDown DatePopup(ThemedDateTimePicker picker) =>
        (ToolStripDropDown)typeof(ThemedDateTimePicker).GetField("popup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(picker);

    private static void KeyDown(Control control, Keys key) => typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(control, [new KeyEventArgs(key)]);

    private static System.Drawing.Color ColorAt(RichTextBox log, int index)
    {
        var start = log.SelectionStart;
        var length = log.SelectionLength;
        log.Select(index, 1);
        var result = log.SelectionColor;
        log.Select(start, length);
        return result;
    }

    private static double Contrast(System.Drawing.Color a, System.Drawing.Color b)
    {
        static double Luminance(System.Drawing.Color color)
        {
            static double Channel(byte value) => value / 255d <= 0.04045 ? value / 255d / 12.92 : Math.Pow((value / 255d + 0.055) / 1.055, 2.4);
            return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        }
        var first = Luminance(a);
        var second = Luminance(b);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }
}
