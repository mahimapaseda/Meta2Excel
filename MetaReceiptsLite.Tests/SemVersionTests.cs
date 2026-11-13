using MetaReceiptsLite.Updater;
using Xunit;

namespace MetaReceiptsLite.Tests
{
    public class SemVersionTests
    {
        [Theory]
        [InlineData("v1.0.13", 1, 0, 13, "")]
        [InlineData("V2.1.0", 2, 1, 0, "")]
        [InlineData("0.9.5", 0, 9, 5, "")]
        [InlineData("1.0.0-beta.1", 1, 0, 0, "beta.1")]
        public void TryParse_ValidInputs_ParsesExpectedParts(string input, int major, int minor, int patch, string pre)
        {
            Assert.True(SemVersion.TryParse(input, out var ver));
            Assert.Equal(major, ver.Major);
            Assert.Equal(minor, ver.Minor);
            Assert.Equal(patch, ver.Patch);
            Assert.Equal(pre, ver.Prerelease);
        }

        [Theory]
        [InlineData("1.0.14", "1.0.13")]
        [InlineData("1.1.0", "1.0.99")]
        [InlineData("2.0.0", "1.99.99")]
        [InlineData("1.0.0", "1.0.0-rc.1")]
        [InlineData("v1.0.13", "v1.0.12")]
        public void CompareTo_NewerVersions_AreGreaterThanOlder(string newer, string older)
        {
            var vNewer = SemVersion.Parse(newer);
            var vOlder = SemVersion.Parse(older);

            Assert.True(vNewer > vOlder);
            Assert.True(vNewer >= vOlder);
            Assert.False(vNewer < vOlder);
            Assert.True(vNewer != vOlder);
        }

        [Fact]
        public void Equals_IdenticalVersions_AreEqual()
        {
            var v1 = SemVersion.Parse("v1.0.13");
            var v2 = SemVersion.Parse("1.0.13");

            Assert.Equal(v1, v2);
            Assert.True(v1 == v2);
        }
    }
}
