# In-App Auto-Updater

Meta Receipts Lite checks the latest GitHub release on startup and when the user asks it to check. It compares the release tag with the app version, shows release notes, downloads the setup installer, verifies the asset size and SHA-256 digest when GitHub provides one, and then offers to launch the installer.

## Public upstream repository

The default repository, `mahimapaseda/Meta2Excel`, is public. Update checks and downloads use anonymous GitHub requests; users do not need to create or paste a token. The application does not embed a GitHub credential.

## Private repository forks

Users pointing the updater at a private fork can open **Settings...** in the update dialog and enter a fine-grained personal access token. Grant only **Contents: Read-only** access to the fork. The token is stored in `%LOCALAPPDATA%\MetaReceiptsLite\updater.json` for the current user. Leave the token blank for public repositories.

Environment variables are also supported for managed installations:

```powershell
[System.Environment]::SetEnvironmentVariable("MRL_GITHUB_TOKEN", "<read-only token>", "User")
[System.Environment]::SetEnvironmentVariable("MRL_GITHUB_OWNER", "<owner>", "User")
[System.Environment]::SetEnvironmentVariable("MRL_GITHUB_REPO", "<repository>", "User")
```

The standard `GITHUB_TOKEN` environment variable is accepted as a fallback.

## Download behavior

- Public repositories download from each asset's `browser_download_url` without sending a token.
- Private repositories use the GitHub release-asset API. The updater handles both a direct binary response and a redirect to object storage, and never forwards the GitHub token to that storage URL.
- When the release metadata includes a SHA-256 digest, the downloaded installer must match it before the app enables **Install & Restart**.
- A mismatched or incomplete download is discarded.

GitHub documents that release asset requests can return either a direct `200` response or a `302` redirect, and that public assets can be read without authentication. See [GitHub's release asset API documentation](https://docs.github.com/en/rest/releases/assets).
