using System.Text;

// Avalonia owns a single UI session and application state for this assembly.
[assembly: DoNotParallelize]

namespace Clc.Sip.Desktop.Tests;

[TestClass]
public sealed class TestAssembly
{
    [AssemblyInitialize]
    public static void Initialize(TestContext _)
    {
        Velopack.VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}
