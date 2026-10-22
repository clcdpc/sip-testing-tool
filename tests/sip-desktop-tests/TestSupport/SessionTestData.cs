using Clc.Sip.Desktop.Models;

namespace Clc.Sip.Desktop.Tests.TestSupport;

internal static class SessionTestData
{
    public static SessionFields SampleFields() => new()
    {
        Server = "sip.example.test",
        Port = "5002",
        Ao = "CLC",
        Ap = "desk-1",
        Username = "test-user",
        Password = "test-secret",
        LocationCode = "MAIN",
        VendorProfile = "ITG",
        PatronBarcode = "P123",
        PatronPin = "2468",
        ItemBarcode = "I456",
        Bp = "6",
        Bq = "10"
    };
}
