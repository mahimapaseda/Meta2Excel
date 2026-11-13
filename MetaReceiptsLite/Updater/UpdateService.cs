using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace MetaReceiptsLite.Updater
{
    public interface IUpdateService
    {
        Task<UpdateCheckResult> CheckForUpdatesAsync(UpdateConfig config, SemVersion currentVersion, CancellationToken cancellationToken = default);
        Task<string> DownloadAssetAsync(UpdateConfig config, ReleaseAsset asset, string destinationFolder = null, IProgress<UpdateProgressReport> progress = null, CancellationToken cancellationToken = default);
        bool VerifyChecksum(string filePath, string expectedSha256Hex);
        void LaunchInstaller(string installerPath, bool silent = false);
    }

    public sealed class UpdateService : IUpdateService
    {
        private const string GitHubApiVersion = "2022-11-28";
        private const int DownloadBufferSize = 64 * 1024; // 64 KB streaming buffer

        static UpdateService()
        {
            // Ensure TLS 1.2 is enabled for secure GitHub communications on .NET 4.8
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync(
            UpdateConfig config,
            SemVersion currentVersion,
            CancellationToken cancellationToken = default)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (currentVersion == null) throw new ArgumentNullException(nameof(currentVersion));

            string url = $"{config.CustomApiBaseUrl.TrimEnd('/')}/repos/{config.RepoOwner}/{config.RepoName}/releases/latest";

            using (var handler = new HttpClientHandler())
            using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(25) })
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                request.Headers.Add("X-GitHub-Api-Version", GitHubApiVersion);
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("MetaReceiptsLite-Updater", currentVersion.ToString()));

                if (config.ShouldUseGitHubToken)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.GitHubToken);
                }

                HttpResponseMessage response;
                try
                {
                    response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return UpdateCheckResult.Failure(cancellationToken.IsCancellationRequested
                        ? "Check for updates was cancelled."
                        : "Connection timed out while checking for updates.");
                }
                catch (Exception ex)
                {
                    return UpdateCheckResult.Failure($"Network error checking for updates: {ex.Message}");
                }

                int statusCode = (int)response.StatusCode;
                if (!response.IsSuccessStatusCode)
                {
                    string errBody = string.Empty;
                    try { errBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false); } catch { }

                    if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        return UpdateCheckResult.Failure(
                            "Authentication failed. Please verify that your GitHub Token has read access to the private repository.",
                            statusCode);
                    }
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return UpdateCheckResult.Failure(
                            $"Repository '{config.RepoOwner}/{config.RepoName}' not found, has no releases, or your token lacks read permissions.",
                            statusCode);
                    }

                    return UpdateCheckResult.Failure($"GitHub API returned HTTP {statusCode}: {response.ReasonPhrase}", statusCode);
                }

                try
                {
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var release = ParseReleaseJson(json);
                    if (release == null)
                    {
                        return UpdateCheckResult.Failure("Failed to parse release information from GitHub response.");
                    }

                    // Select target asset (prefer Setup.exe for native installation)
                    var target = SelectTargetAsset(release.Assets);

                    return UpdateCheckResult.Success(currentVersion, release, target);
                }
                catch (Exception ex)
                {
                    return UpdateCheckResult.Failure($"Error parsing release metadata: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Downloads an asset from a public or private GitHub Release and verifies GitHub's digest when available.
        /// </summary>
        public async Task<string> DownloadAssetAsync(
            UpdateConfig config,
            ReleaseAsset asset,
            string destinationFolder = null,
            IProgress<UpdateProgressReport> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            if (string.IsNullOrWhiteSpace(destinationFolder))
            {
                destinationFolder = Path.Combine(Path.GetTempPath(), "MetaReceiptsLite_Updates");
            }
            if (!Directory.Exists(destinationFolder))
                Directory.CreateDirectory(destinationFolder);

            string destinationPath = Path.Combine(destinationFolder, asset.Name);
            string tempDownloadPath = destinationPath + ".downloading";

            // Delete previous incomplete downloads
            if (File.Exists(tempDownloadPath))
            {
                try { File.Delete(tempDownloadPath); } catch { }
            }

            try
            {
                bool downloaded = false;
                bool useAuthenticatedApi = config.ShouldUseGitHubToken;

                // Public releases work anonymously through browser_download_url. This avoids sending
                // credentials for the public upstream and follows GitHub's asset redirect normally.
                if (!useAuthenticatedApi && !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
                {
                    using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) })
                    using (var request = new HttpRequestMessage(HttpMethod.Get, asset.BrowserDownloadUrl))
                    {
                        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("MetaReceiptsLite-Updater", "1.0"));
                        using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                        {
                            response.EnsureSuccessStatusCode();
                            await SaveResponseContentAsync(response.Content, tempDownloadPath, asset.Size, progress, cancellationToken).ConfigureAwait(false);
                            downloaded = true;
                        }
                    }
                }
                else
                {
                    // Private assets use the API endpoint. GitHub may return either a redirect to
                    // object storage or the asset bytes directly; support both response forms.
                    string downloadUrl = asset.ApiUrl;

                    using (var handler = new HttpClientHandler { AllowAutoRedirect = false })
                    using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) })
                    using (var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl))
                    {
                        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
                        request.Headers.Add("X-GitHub-Api-Version", GitHubApiVersion);
                        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("MetaReceiptsLite-Updater", "1.0"));
                        if (useAuthenticatedApi)
                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.GitHubToken);

                        using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                        {
                            if (response.StatusCode == HttpStatusCode.Redirect ||
                                response.StatusCode == HttpStatusCode.Found ||
                                response.StatusCode == HttpStatusCode.SeeOther ||
                                response.StatusCode == HttpStatusCode.TemporaryRedirect)
                            {
                                var location = response.Headers.Location;
                                if (location == null)
                                    throw new InvalidOperationException("GitHub returned an asset redirect without a download location.");
                                downloadUrl = location.IsAbsoluteUri ? location.AbsoluteUri : new Uri(new Uri(asset.ApiUrl), location).AbsoluteUri;
                            }
                            else if (response.IsSuccessStatusCode)
                            {
                                if (string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
                                    throw new InvalidOperationException("GitHub returned asset metadata instead of the installer file.");
                                await SaveResponseContentAsync(response.Content, tempDownloadPath, asset.Size, progress, cancellationToken).ConfigureAwait(false);
                                downloaded = true;
                            }
                            else
                            {
                                throw new InvalidOperationException($"GitHub could not start the asset download (HTTP {(int)response.StatusCode} {response.ReasonPhrase}).");
                            }
                        }
                    }

                    if (!downloaded)
                    {
                        // Never forward a GitHub token to the signed object-storage URL.
                        using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) })
                        using (var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl))
                        {
                            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("MetaReceiptsLite-Updater", "1.0"));
                            using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                            {
                                response.EnsureSuccessStatusCode();
                                await SaveResponseContentAsync(response.Content, tempDownloadPath, asset.Size, progress, cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                }

                var downloadedSize = new FileInfo(tempDownloadPath).Length;
                if (asset.Size > 0 && downloadedSize != asset.Size)
                    throw new InvalidDataException($"The downloaded file is incomplete ({downloadedSize} of {asset.Size} bytes). Please retry.");

                if (!string.IsNullOrWhiteSpace(asset.Sha256Digest))
                {
                    const string digestPrefix = "sha256:";
                    if (asset.Sha256Digest.StartsWith(digestPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        var expectedHash = asset.Sha256Digest.Substring(digestPrefix.Length);
                        if (!VerifyChecksum(tempDownloadPath, expectedHash))
                            throw new InvalidDataException("GitHub's SHA-256 digest does not match the downloaded file. The installer was discarded.");
                    }
                }

                if (File.Exists(destinationPath)) File.Delete(destinationPath);
                File.Move(tempDownloadPath, destinationPath);
                return destinationPath;
            }
            catch
            {
                try { if (File.Exists(tempDownloadPath)) File.Delete(tempDownloadPath); } catch { }
                throw;
            }
        }

        private static async Task SaveResponseContentAsync(
            HttpContent content,
            string destinationPath,
            long expectedBytes,
            IProgress<UpdateProgressReport> progress,
            CancellationToken cancellationToken)
        {
            long totalBytes = content.Headers.ContentLength ?? expectedBytes;
            var stopwatch = Stopwatch.StartNew();
            long bytesReceived = 0;

            using (var contentStream = await content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, DownloadBufferSize, true))
            {
                byte[] buffer = new byte[DownloadBufferSize];
                int read;
                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                    bytesReceived += read;

                    if (progress != null)
                    {
                        double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                        double speedMBps = elapsedSeconds > 0 ? (bytesReceived / (1024.0 * 1024.0)) / elapsedSeconds : 0.0;
                        int percentage = totalBytes > 0 ? (int)((bytesReceived * 100) / totalBytes) : 0;
                        progress.Report(new UpdateProgressReport
                        {
                            BytesReceived = bytesReceived,
                            TotalBytes = totalBytes,
                            Percentage = Math.Min(100, Math.Max(0, percentage)),
                            MegaBytesReceived = bytesReceived / (1024.0 * 1024.0),
                            TotalMegaBytes = totalBytes / (1024.0 * 1024.0),
                            SpeedMegaBytesPerSec = speedMBps,
                            StatusMessage = $"Downloading: {bytesReceived / (1024 * 1024.0):F1} MB / {totalBytes / (1024 * 1024.0):F1} MB ({speedMBps:F2} MB/s)"
                        });
                    }
                }
            }
        }

        public bool VerifyChecksum(string filePath, string expectedSha256Hex)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;
            if (string.IsNullOrWhiteSpace(expectedSha256Hex)) return true; // If no checksum provided, pass

            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = sha.ComputeHash(stream);
                var sb = new StringBuilder();
                foreach (byte b in hash)
                    sb.Append(b.ToString("x2"));

                return string.Equals(sb.ToString(), expectedSha256Hex.Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }

        public void LaunchInstaller(string installerPath, bool silent = false)
        {
            if (string.IsNullOrWhiteSpace(installerPath))
                throw new ArgumentNullException(nameof(installerPath));
            if (!File.Exists(installerPath))
                throw new FileNotFoundException("Installer not found at specified path.", installerPath);

            // Unblock Mark-of-the-Web (Zone.Identifier) so Windows SmartScreen does not trigger
            try
            {
                var zoneStream = installerPath + ":Zone.Identifier";
                if (File.Exists(zoneStream))
                {
                    File.Delete(zoneStream);
                }
            }
            catch
            {
                // Best-effort: ignore if filesystem doesn't support alternate data streams
            }

            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            };

            if (silent)
            {
                // Inno Setup supports /SILENT or /VERYSILENT
                psi.Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS";
            }

            Process.Start(psi);
        }

        private static ReleaseAsset SelectTargetAsset(List<ReleaseAsset> assets)
        {
            if (assets == null || assets.Count == 0) return null;

            // 1. Prefer Setup installer: MetaReceiptsLite-Setup-v*.exe
            var setup = assets.Find(a => a.IsSetupInstaller);
            if (setup != null) return setup;

            // 2. Any executable
            var exe = assets.Find(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            if (exe != null) return exe;

            // 3. Fallback to portable zip
            var zip = assets.Find(a => a.IsZipArchive);
            if (zip != null) return zip;

            return assets[0];
        }

        private static ReleaseInfo ParseReleaseJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            var serializer = new JavaScriptSerializer();
            var dict = serializer.Deserialize<Dictionary<string, object>>(json);
            if (dict == null) return null;

            var release = new ReleaseInfo();

            if (dict.TryGetValue("tag_name", out var tag) && tag != null)
            {
                release.TagName = tag.ToString();
                if (SemVersion.TryParse(release.TagName, out var ver))
                    release.Version = ver;
            }

            if (dict.TryGetValue("name", out var name) && name != null)
                release.Name = name.ToString();

            if (dict.TryGetValue("body", out var body) && body != null)
                release.Body = body.ToString();

            if (dict.TryGetValue("prerelease", out var pre) && pre is bool bPre)
                release.Prerelease = bPre;

            if (dict.TryGetValue("draft", out var draft) && draft is bool bDraft)
                release.Draft = bDraft;

            if (dict.TryGetValue("html_url", out var html) && html != null)
                release.HtmlUrl = html.ToString();

            if (dict.TryGetValue("published_at", out var pub) && pub != null)
            {
                if (DateTime.TryParse(pub.ToString(), out var dt))
                    release.PublishedAt = dt;
            }

            if (dict.TryGetValue("assets", out var rawAssets) && rawAssets is ArrayList assetList)
            {
                foreach (var item in assetList)
                {
                    if (item is Dictionary<string, object> aDict)
                    {
                        var asset = new ReleaseAsset();
                        if (aDict.TryGetValue("id", out var id) && id != null)
                            asset.Id = Convert.ToInt64(id);
                        if (aDict.TryGetValue("name", out var aName) && aName != null)
                            asset.Name = aName.ToString();
                        if (aDict.TryGetValue("size", out var aSize) && aSize != null)
                            asset.Size = Convert.ToInt64(aSize);
                        if (aDict.TryGetValue("content_type", out var ct) && ct != null)
                            asset.ContentType = ct.ToString();
                        if (aDict.TryGetValue("url", out var aUrl) && aUrl != null)
                            asset.ApiUrl = aUrl.ToString();
                        if (aDict.TryGetValue("browser_download_url", out var bUrl) && bUrl != null)
                            asset.BrowserDownloadUrl = bUrl.ToString();
                        if (aDict.TryGetValue("digest", out var digest) && digest != null)
                            asset.Sha256Digest = digest.ToString();

                        release.Assets.Add(asset);
                    }
                }
            }

            return release;
        }
    }
}
