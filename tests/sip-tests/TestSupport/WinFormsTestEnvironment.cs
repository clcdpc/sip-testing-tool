using Clc.Sip.Helpers;

namespace Clc.Sip.Tests.TestSupport;

internal static class WinFormsTestEnvironment
{
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException("Windows Forms tests must run on an STA thread.");
        }
        // The test host needs the Windows Forms application's configuration.
        AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", typeof(WinFormsTestEnvironment).Assembly.Location + ".config");
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        AppTheme.Initialize();
        initialized = true;
    }

    public static void LayoutTree(Control root)
    {
        root.PerformLayout();
        foreach (Control child in root.Controls)
        {
            LayoutTree(child);
        }
    }

    public static System.Drawing.Rectangle BoundsIn(Control child, Control root)
    {
        var location = child.Location;
        for (var parent = child.Parent; parent != root; parent = parent.Parent)
        {
            location.Offset(parent.Location);
        }

        return new System.Drawing.Rectangle(location, child.Size);
    }

    public static void ShowOffscreen(Form form)
    {
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new System.Drawing.Point(-30000, -30000);
        form.Show();
        Application.DoEvents();
    }
}
