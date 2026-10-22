using System.Runtime.InteropServices;
using Clc.Sip.Helpers;
using Clc.Sip.Tests.TestSupport;
using static Clc.Sip.Tests.TestSupport.WinFormsTestEnvironment;

namespace Clc.Sip.Tests;

[STATestClass]
[TestCategory("Regression")]
public sealed class TextBoxNavigationTests
{
    [ClassInitialize]
    public static void Initialize(TestContext _) => WinFormsTestEnvironment.Initialize();

    [TestMethod]
    [DataRow(false, true)]
    [DataRow(false, false)]
    [DataRow(true, true)]
    [DataRow(true, false)]
    public void TabReentrySelectsTheWholeValueForReplacement(bool dark, bool forward)
    {
        var originalTheme = AppTheme.IsDarkMode;
        using var form = new Form();
        using var before = new Button { TabIndex = 0, Top = 0 };
        using var field = new ThemedTextBox { TabIndex = 1, Top = 35, Text = "Existing value" };
        using var after = new Button { TabIndex = 2, Top = 70 };
        form.Controls.AddRange([before, field, after]);
        AppTheme.Attach(form);
        ShowOffscreen(form);
        try
        {
            AppTheme.SetDarkMode(dark);
            Assert.IsTrue(field.Focus());
            field.Select(2, 3);
            var source = forward ? before : after;
            Assert.IsTrue(source.Focus());
            TabToField(form, source, field, forward);
            Assert.AreEqual(0, field.SelectionStart);
            Assert.AreEqual(field.TextLength, field.SelectionLength);

            field.SelectedText = "Replacement";
            Assert.AreEqual("Replacement", field.Text, "Typing should replace the value, not append to it.");
            field.Select(3, 0);
            Assert.IsTrue(source.Focus());
            TabToField(form, source, field, forward);
            Assert.AreEqual(0, field.SelectionStart);
            Assert.AreEqual(field.TextLength, field.SelectionLength, "Each visit should select the current value.");
        }
        finally { AppTheme.SetDarkMode(originalTheme); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MouseClicksStillPlaceTheCaretAtTheClickedCharacter(bool dark)
    {
        var originalTheme = AppTheme.IsDarkMode;
        using var form = new Form();
        using var before = new Button { TabIndex = 0, Top = 0 };
        using var field = new ThemedTextBox { TabIndex = 1, Top = 35, Width = 240, Text = "Existing value" };
        form.Controls.AddRange([before, field]);
        AppTheme.Attach(form);
        ShowOffscreen(form);
        try
        {
            AppTheme.SetDarkMode(dark);
            Assert.IsTrue(field.Focus());
            field.SelectAll();
            Assert.IsTrue(before.Focus());
            ClickCharacter(field, 5);
            Assert.IsTrue(field.Focused);
            Assert.AreEqual(5, field.SelectionStart);
            Assert.AreEqual(0, field.SelectionLength);

            ClickCharacter(field, 2);
            Assert.AreEqual(2, field.SelectionStart);
            Assert.AreEqual(0, field.SelectionLength);
        }
        finally { AppTheme.SetDarkMode(originalTheme); }
    }

    private static void TabToField(Form form, Control source, Control field, bool forward)
    {
        if (forward)
        {
            var message = Message.Create(source.Handle, 0x0100, (IntPtr)Keys.Tab, IntPtr.Zero); // WM_KEYDOWN
            Assert.IsTrue(source.PreProcessMessage(ref message));
        }
        else
        {
            // Exercise the same backward traversal as Shift+Tab without changing global keyboard state.
            Assert.IsTrue(form.SelectNextControl(source, false, true, true, true));
        }
        Assert.IsTrue(field.Focused);
    }

    private static void ClickCharacter(TextBox field, int index)
    {
        var point = field.GetPositionFromCharIndex(index);
        var position = (IntPtr)((field.ClientSize.Height / 2 << 16) | point.X);
        _ = SendMessage(field.Handle, 0x0201, (IntPtr)1, position); // WM_LBUTTONDOWN, MK_LBUTTON
        _ = SendMessage(field.Handle, 0x0202, IntPtr.Zero, position); // WM_LBUTTONUP
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}
