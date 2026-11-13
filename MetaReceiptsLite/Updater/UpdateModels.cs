using System;
using System.Collections.Generic;

namespace MetaReceiptsLite.Updater
{
    public sealed class ReleaseInfo
    {
        public string TagName { get; set; } = string.Empty;
        public SemVersion Version { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool Prerelease { get; set; }
        public bool Draft { get; set; }
        public DateTime PublishedAt { get; set; }
        public string HtmlUrl { get; set; } = string.Empty;
        public List<ReleaseAsset> Assets { get; set; } = new List<ReleaseAsset>();
    }

    public sealed class ReleaseAsset
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string ApiUrl { get; set; } = string.Empty;
        public string BrowserDownloadUrl { get; set; } = string.Empty;

        public bool IsSetupInstaller =>
            Name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0 &&
            Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

        public bool IsZipArchive =>
            Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

        public bool IsChecksumFile =>
            Name.IndexOf("SHA256", StringComparison.OrdinalIgnoreCase) >= 0;

        public string FormattedSize
        {
            get
            {
                if (Size < 1024) return $"{Size} B";
                if (Size < 1024 * 1024) return $"{Size / 1024.0:F1} KB";
                return $"{Size / (1024.0 * 1024.0):F2} MB";
            }
        }
    }

    public sealed class UpdateCheckResult
    {
        public bool IsSuccess { get; set; }
        public bool HasUpdate { get; set; }
        public SemVersion CurrentVersion { get; set; }
        public SemVersion LatestVersion { get; set; }
        public ReleaseInfo Release { get; set; }
        public ReleaseAsset TargetAsset { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int HttpStatusCode { get; set; }

        public static UpdateCheckResult Success(SemVersion current, ReleaseInfo release, ReleaseAsset target)
        {
            bool hasUpdate = release != null && release.Version != null && release.Version > current;
            return new UpdateCheckResult
            {
                IsSuccess = true,
                HasUpdate = hasUpdate,
                CurrentVersion = current,
                LatestVersion = release?.Version,
                Release = release,
                TargetAsset = target,
                HttpStatusCode = 200
            };
        }

        public static UpdateCheckResult Failure(string message, int statusCode = 0)
        {
            return new UpdateCheckResult
            {
                IsSuccess = false,
                HasUpdate = false,
                ErrorMessage = message,
                HttpStatusCode = statusCode
            };
        }
    }

    public sealed class UpdateProgressReport
    {
        public long BytesReceived { get; set; }
        public long TotalBytes { get; set; }
        public int Percentage { get; set; }
        public double MegaBytesReceived { get; set; }
        public double TotalMegaBytes { get; set; }
        public double SpeedMegaBytesPerSec { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
    }
}
