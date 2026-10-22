using System.Text;
using Clc.Sip;

namespace Clc.Sip.Tests;

[TestClass]
[TestCategory("Regression")]
public sealed class ChecksumTests
{
    [TestMethod(DisplayName = "Known SIP checksum")]
    public void KnownSipChecksumMatchesProtocolExample() => Assert.AreEqual("FCA8", sip2.CalculateChecksum("9901002.00AY0AZ"));

    [TestMethod(DisplayName = "UTF-8 checksum")]
    public void Utf8ChecksumUsesEncodedBytes() => Assert.AreEqual("FCAC", sip2.CalculateChecksum("AB\u00e9AY0AZ"));

    [TestMethod(DisplayName = "Supplementary Unicode checksum")]
    public void SupplementaryUnicodeChecksumUsesEncodedBytes() => Assert.AreEqual("FB71", sip2.CalculateChecksum("AB\U0001F600AY0AZ"));

    [TestMethod(DisplayName = "Configured encoding checksum")]
    public void ChecksumUsesConfiguredEncoding() => Assert.AreEqual("FD2F", sip2.CalculateChecksum("AB\u00e9AY0AZ", Encoding.Latin1));

    [TestMethod(DisplayName = "Short and empty responses")]
    public void ShortAndEmptyResponsesHaveInvalidChecksums()
    {
        using var sip = new sip2();
        foreach (var value in new[] { null, "", " ", "98", "\r\n98\r\n", "AZ0000" })
        {
            Assert.IsFalse(sip.CheckChecksum(value));
        }
    }

    [TestMethod(DisplayName = "Valid and invalid checksums")]
    public void ResponseChecksumsAcceptValidAndRejectInvalidValues()
    {
        using var sip = new sip2();
        Assert.IsTrue(sip.CheckChecksum("98YAY0AZFDD1\r\n"));
        Assert.IsFalse(sip.CheckChecksum("98YAY0AZ0000\r"));
        Assert.IsTrue(sip.CheckChecksum("98YAY0AZfdd1\r"));
    }

    [TestMethod(DisplayName = "Sequence number wraps")]
    public void SequenceNumberWrapsAfterNine()
    {
        using var sip = new sip2();
        for (int i = 0; i < 12; i++)
        {
            Assert.AreEqual(i % 10, sip.GetSeqNumber());
        }
    }
}
