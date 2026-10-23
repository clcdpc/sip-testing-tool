using System.Text.Json;
using Clc.Sip.Desktop.Services;

namespace Clc.Sip.Desktop.Tests;

[TestClass]
public sealed class ReleaseVersionTests
{
    [TestMethod]
    public void ApplicationAndPackagingAcceptTheSameVersionSyntax()
    {
        using var stream = typeof(ReleaseVersionTests).Assembly.GetManifestResourceStream("ReleaseVersionCases.json")!;
        using var cases = JsonDocument.Parse(stream);
        foreach (var item in cases.RootElement.EnumerateArray())
        {
            var text = item.GetProperty("version").GetString()!;
            var valid = ReleaseVersion.TryParse(text, out var version);
            Assert.AreEqual(item.GetProperty("valid").GetBoolean(), valid, text);
            if (valid) { Assert.AreEqual(item.GetProperty("prerelease").GetBoolean(), version!.IsPrerelease, text); }
        }
        Assert.IsFalse(ReleaseVersion.TryParse(null, out _));
    }

    [TestMethod]
    public void SemanticPrecedenceOrdersBetaNumbersAndFinalReleases()
    {
        var ordered = new[]
        {
            "1.0.0-alpha", "1.0.0-alpha.1", "1.0.0-alpha.beta", "1.0.0-beta", "1.0.0-beta.2",
            "1.0.0-beta.11", "1.0.0-rc.1", "1.0.0", "2.0.0-beta.1", "2.0.0", "2.1.0", "2.1.1"
        };
        for (var i = 0; i < ordered.Length; i++)
        {
            for (var j = 0; j < ordered.Length; j++)
            {
                Assert.AreEqual(Math.Sign(i.CompareTo(j)), Math.Sign(ReleaseVersion.Parse(ordered[i]).CompareTo(ReleaseVersion.Parse(ordered[j]))),
                    $"{ordered[i]} vs {ordered[j]}");
            }
        }
    }

    [TestMethod]
    [DataRow("2.1.2-beta.1+one", "2.1.2-beta.1+two", 0)]
    [DataRow("2.1.2+one", "2.1.2", 0)]
    [DataRow("2.1.2-beta.999999999999999999999999999999", "2.1.2-beta.999999999999999999999999999998", 1)]
    [DataRow("2.1.2-beta.2", "2.1.2-beta.two", -1)]
    [DataRow("2.1.2-beta.2", "2.1.2-beta.2.1", -1)]
    [DataRow("2.1.2-Beta.1", "2.1.2-beta.1", -1)]
    [DataRow("2.1.10-beta.1", "2.1.9", 1)]
    public void ComparisonUsesSemanticVersionRules(string left, string right, int expected)
    {
        Assert.AreEqual(expected, Math.Sign(ReleaseVersion.Parse(left).CompareTo(ReleaseVersion.Parse(right))));
    }
}
