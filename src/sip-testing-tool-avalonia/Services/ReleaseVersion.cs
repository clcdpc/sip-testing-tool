using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Clc.Sip.Desktop.Services;

// System.Version discards prerelease identifiers. Keep SemVer precedence for update selection.
internal sealed class ReleaseVersion : IComparable<ReleaseVersion>
{
    private static readonly Regex Pattern = new(
        @"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(?<prerelease>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private readonly string[] core;
    private readonly string[] prerelease;

    public string Value { get; }
    public string DisplayValue => Value.Split('+')[0];
    public bool IsPrerelease => prerelease.Length > 0;
    public static ReleaseVersion Current => Parse(typeof(ReleaseVersion).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);

    private ReleaseVersion(string value, string[] core, string[] prerelease)
    {
        Value = value;
        this.core = core;
        this.prerelease = prerelease;
    }

    public static ReleaseVersion Parse(string value) => TryParse(value, out var version)
        ? version : throw new FormatException("Use a semantic version such as 2.1.2 or 2.1.2-beta.1.");

    public static bool TryParse(string? value, [NotNullWhen(true)] out ReleaseVersion? version)
    {
        version = null;
        if (value is null) { return false; }
        var match = Pattern.Match(value);
        if (!match.Success) { return false; }
        var identifiers = match.Groups["prerelease"].Success
            ? match.Groups["prerelease"].Value.Split('.') : [];
        if (identifiers.Any(identifier => IsNumeric(identifier) && identifier.Length > 1 && identifier[0] == '0'))
        {
            return false;
        }
        version = new ReleaseVersion(value, [match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value], identifiers);
        return true;
    }

    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null) { return 1; }
        for (var i = 0; i < core.Length; i++)
        {
            var comparison = CompareNumeric(core[i], other.core[i]);
            if (comparison != 0) { return comparison; }
        }
        if (IsPrerelease != other.IsPrerelease) { return IsPrerelease ? -1 : 1; }
        for (var i = 0; i < Math.Min(prerelease.Length, other.prerelease.Length); i++)
        {
            var left = prerelease[i];
            var right = other.prerelease[i];
            var leftNumeric = IsNumeric(left);
            var rightNumeric = IsNumeric(right);
            var comparison = leftNumeric && rightNumeric ? CompareNumeric(left, right)
                : leftNumeric != rightNumeric ? (leftNumeric ? -1 : 1)
                : string.CompareOrdinal(left, right);
            if (comparison != 0) { return comparison; }
        }
        return prerelease.Length.CompareTo(other.prerelease.Length);
    }

    private static bool IsNumeric(string value) => value.All(character => character is >= '0' and <= '9');

    // Length comparison avoids integer overflow for numeric prerelease identifiers.
    private static int CompareNumeric(string left, string right) => left.Length != right.Length
        ? left.Length.CompareTo(right.Length) : string.CompareOrdinal(left, right);
}
