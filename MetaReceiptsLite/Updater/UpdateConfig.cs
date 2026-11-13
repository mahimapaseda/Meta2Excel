using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace MetaReceiptsLite.Updater
{
    public sealed class UpdateConfig
    {
        public const string DefaultOwner = "mahimapaseda";
        public const string DefaultRepo = "Meta2Excel";

        // Built-in read-only token for private release asset downloads.
        // Scope: Contents: Read-only, Repository: Meta2Excel only.
        // Users can override via MRL_GITHUB_TOKEN env var or the in-app Settings dialog.
        private const string BuiltInToken =
            "github_pat_11BJUXX6A05rOFNvwGjVPu_" +
            "qfZuTXPC91ShWQLTSASKoGnEFZ7nlX4u3U7lxgOoGJsYCVXMWJB9ChKoRp9";

        public string GitHubToken { get; set; } = BuiltInToken;
        public string RepoOwner { get; set; } = DefaultOwner;
        public string RepoName { get; set; } = DefaultRepo;
        public bool CheckOnStartup { get; set; } = true;
        public string CustomApiBaseUrl { get; set; } = "https://api.github.com";

        public bool HasToken => !string.IsNullOrWhiteSpace(GitHubToken);

        public static string GetConfigFilePath()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(localAppData, "MetaReceiptsLite");
            return Path.Combine(dir, "updater.json");
        }

        public static string GetLocalExeConfigPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "updater.json");
        }

        /// <summary>
        /// Loads configuration from:
        /// 1. Environment variables (MRL_GITHUB_TOKEN / GITHUB_TOKEN, MRL_GITHUB_OWNER, MRL_GITHUB_REPO)
        /// 2. %LOCALAPPDATA%\MetaReceiptsLite\updater.json
        /// 3. ./updater.json (beside exe)
        /// </summary>
        public static UpdateConfig Load()
        {
            var config = new UpdateConfig();

            // 1. Try JSON configuration files
            var paths = new[] { GetConfigFilePath(), GetLocalExeConfigPath() };
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var json = File.ReadAllText(path);
                        var serializer = new JavaScriptSerializer();
                        var dict = serializer.Deserialize<Dictionary<string, object>>(json);
                        if (dict != null)
                        {
                            if (dict.TryGetValue("github_token", out var token) && token != null)
                                config.GitHubToken = token.ToString().Trim();
                            if (dict.TryGetValue("repo_owner", out var owner) && owner != null)
                                config.RepoOwner = owner.ToString().Trim();
                            if (dict.TryGetValue("repo_name", out var repo) && repo != null)
                                config.RepoName = repo.ToString().Trim();
                            if (dict.TryGetValue("check_on_startup", out var startup) && startup is bool b)
                                config.CheckOnStartup = b;
                            if (dict.TryGetValue("api_base_url", out var api) && api != null)
                                config.CustomApiBaseUrl = api.ToString().Trim();
                        }
                        break;
                    }
                    catch
                    {
                        // Ignore corrupt JSON file and proceed to env vars
                    }
                }
            }

            // 2. Environment variables override file configs
            var envToken = Environment.GetEnvironmentVariable("MRL_GITHUB_TOKEN") ??
                           Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrWhiteSpace(envToken))
                config.GitHubToken = envToken.Trim();

            var envOwner = Environment.GetEnvironmentVariable("MRL_GITHUB_OWNER") ??
                           Environment.GetEnvironmentVariable("GITHUB_OWNER");
            if (!string.IsNullOrWhiteSpace(envOwner))
                config.RepoOwner = envOwner.Trim();

            var envRepo = Environment.GetEnvironmentVariable("MRL_GITHUB_REPO") ??
                          Environment.GetEnvironmentVariable("GITHUB_REPO");
            if (!string.IsNullOrWhiteSpace(envRepo))
                config.RepoName = envRepo.Trim();

            return config;
        }

        public void Save()
        {
            try
            {
                var filePath = GetConfigFilePath();
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var dict = new Dictionary<string, object>
                {
                    ["github_token"] = GitHubToken ?? string.Empty,
                    ["repo_owner"] = string.IsNullOrWhiteSpace(RepoOwner) ? DefaultOwner : RepoOwner,
                    ["repo_name"] = string.IsNullOrWhiteSpace(RepoName) ? DefaultRepo : RepoName,
                    ["check_on_startup"] = CheckOnStartup,
                    ["api_base_url"] = CustomApiBaseUrl ?? "https://api.github.com"
                };

                var serializer = new JavaScriptSerializer();
                var json = serializer.Serialize(dict);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save updater configuration: {ex.Message}", ex);
            }
        }
    }
}

