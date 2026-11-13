using System;
using System.Text.RegularExpressions;

namespace MetaReceiptsLite.Updater
{
    /// <summary>
    /// Semantic Versioning (SemVer 2.0.0) parser and comparator.
    /// Handles versions like "v1.0.13", "1.2.0-beta.1", "2.0.0+build123".
    /// </summary>
    public sealed class SemVersion : IComparable<SemVersion>, IEquatable<SemVersion>
    {
        private static readonly Regex RxSemVer = new Regex(
            @"^[vV]?(?<major>\d+)(?:\.(?<minor>\d+))?(?:\.(?<patch>\d+))?(?:-(?<pre>[0-9A-Za-z\.-]+))?(?:\+(?<build>[0-9A-Za-z\.-]+))?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string Prerelease { get; }
        public string BuildMetadata { get; }
        public string RawString { get; }

        public SemVersion(int major, int minor = 0, int patch = 0, string prerelease = null, string buildMetadata = null)
        {
            Major = Math.Max(0, major);
            Minor = Math.Max(0, minor);
            Patch = Math.Max(0, patch);
            Prerelease = prerelease ?? string.Empty;
            BuildMetadata = buildMetadata ?? string.Empty;
            RawString = Format();
        }

        public static bool TryParse(string input, out SemVersion version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var match = RxSemVer.Match(input.Trim());
            if (!match.Success) return false;

            int major = int.Parse(match.Groups["major"].Value);
            int minor = match.Groups["minor"].Success ? int.Parse(match.Groups["minor"].Value) : 0;
            int patch = match.Groups["patch"].Success ? int.Parse(match.Groups["patch"].Value) : 0;
            string pre = match.Groups["pre"].Success ? match.Groups["pre"].Value : string.Empty;
            string build = match.Groups["build"].Success ? match.Groups["build"].Value : string.Empty;

            version = new SemVersion(major, minor, patch, pre, build);
            return true;
        }

        public static SemVersion Parse(string input)
        {
            if (TryParse(input, out var version))
                return version;
            throw new FormatException($"Invalid semantic version string: '{input}'");
        }

        public int CompareTo(SemVersion other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;

            int c = Major.CompareTo(other.Major);
            if (c != 0) return c;

            c = Minor.CompareTo(other.Minor);
            if (c != 0) return c;

            c = Patch.CompareTo(other.Patch);
            if (c != 0) return c;

            // Release version has higher precedence than prerelease version
            bool thisHasPre = !string.IsNullOrEmpty(Prerelease);
            bool otherHasPre = !string.IsNullOrEmpty(other.Prerelease);

            if (!thisHasPre && otherHasPre) return 1;  // 1.0.0 > 1.0.0-beta
            if (thisHasPre && !otherHasPre) return -1; // 1.0.0-beta < 1.0.0
            if (thisHasPre && otherHasPre)
                return string.Compare(Prerelease, other.Prerelease, StringComparison.OrdinalIgnoreCase);

            return 0;
        }

        public bool Equals(SemVersion other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Major == other.Major && Minor == other.Minor && Patch == other.Patch &&
                   string.Equals(Prerelease, other.Prerelease, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj) => Equals(obj as SemVersion);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Major;
                hash = (hash * 397) ^ Minor;
                hash = (hash * 397) ^ Patch;
                hash = (hash * 397) ^ (Prerelease?.ToLowerInvariant().GetHashCode() ?? 0);
                return hash;
            }
        }

        public static bool operator >(SemVersion left, SemVersion right) => left?.CompareTo(right) > 0;
        public static bool operator <(SemVersion left, SemVersion right) => left?.CompareTo(right) < 0;
        public static bool operator >=(SemVersion left, SemVersion right) => left?.CompareTo(right) >= 0;
        public static bool operator <=(SemVersion left, SemVersion right) => left?.CompareTo(right) <= 0;
        public static bool operator ==(SemVersion left, SemVersion right) => Equals(left, right);
        public static bool operator !=(SemVersion left, SemVersion right) => !Equals(left, right);

        public override string ToString() => Format();

        private string Format()
        {
            string s = $"{Major}.{Minor}.{Patch}";
            if (!string.IsNullOrEmpty(Prerelease)) s += $"-{Prerelease}";
            if (!string.IsNullOrEmpty(BuildMetadata)) s += $"+{BuildMetadata}";
            return s;
        }
    }
}
