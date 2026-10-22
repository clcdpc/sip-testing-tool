using System.Text;

// These tests share WinForms theme state, Console redirection and process culture.
[assembly: DoNotParallelize]

namespace Clc.Sip.Tests;

[TestClass]
public sealed class TestAssembly
{
    [AssemblyInitialize]
    public static void Initialize(TestContext _) => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
}
