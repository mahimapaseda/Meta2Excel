using MetaReceiptsLite.Updater;
using Xunit;

namespace MetaReceiptsLite.Tests
{
    public class UpdateModelsTests
    {
        [Fact]
        public void ReleaseAsset_DetectsSetupInstaller()
        {
            var asset = new ReleaseAsset
            {
                Name = "MetaReceiptsLite-Setup-v1.0.14.exe",
                Size = 4960000
            };

            Assert.True(asset.IsSetupInstaller);
            Assert.False(asset.IsZipArchive);
            Assert.Contains("MB", asset.FormattedSize);
        }

        [Fact]
        public void ReleaseAsset_DetectsZipArchive()
        {
            var asset = new ReleaseAsset
            {
                Name = "MetaReceiptsLite-v1.0.14.zip",
                Size = 4500000
            };

            Assert.False(asset.IsSetupInstaller);
            Assert.True(asset.IsZipArchive);
        }

        [Fact]
        public void UpdateCheckResult_CorrectlyFlagsAvailableUpdate()
        {
            var current = SemVersion.Parse("1.0.13");
            var release = new ReleaseInfo
            {
                TagName = "v1.0.14",
                Version = SemVersion.Parse("1.0.14")
            };
            var asset = new ReleaseAsset { Name = "MetaReceiptsLite-Setup-v1.0.14.exe" };

            var result = UpdateCheckResult.Success(current, release, asset);

            Assert.True(result.IsSuccess);
            Assert.True(result.HasUpdate);
            Assert.Equal(SemVersion.Parse("1.0.14"), result.LatestVersion);
        }

        [Fact]
        public void UpdateCheckResult_FlagsNoUpdate_WhenCurrentIsLatest()
        {
            var current = SemVersion.Parse("1.0.14");
            var release = new ReleaseInfo
            {
                TagName = "v1.0.14",
                Version = SemVersion.Parse("1.0.14")
            };

            var result = UpdateCheckResult.Success(current, release, null);

            Assert.True(result.IsSuccess);
            Assert.False(result.HasUpdate);
        }
    }
}

