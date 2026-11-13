# In-App Auto-Updater for Private GitHub Releases

This document provides architectural documentation and setup instructions for the in-app auto-updater module implemented in **Meta Receipts Lite (`Meta2Excel`)**.

---

## 1. Overview & Architecture

When a GitHub repository is private, release assets cannot be downloaded via unauthenticated browser URLs (`browser_download_url`). 

The `MetaReceiptsLite.Updater` module solves this by implementing an authenticated update lifecycle:

```
[Application Startup / Manual Check]
                 │
                 ▼
     Query GitHub REST API
     GET /repos/{owner}/{repo}/releases/latest
     Headers: Authorization: Bearer <TOKEN>
              Accept: application/vnd.github+json
              X-GitHub-Api-Version: 2022-11-28
                 │
                 ▼
      Compare Semantic Version
      (Remote tag vs Local Assembly SemVer)
                 │
      ┌──────────┴──────────┐
      ▼                     ▼
 [Up to Date]       [Update Available]
                            │
                            ▼
                   Show Update Dialog
               (Release Notes & Asset Info)
                            │
               User clicks "Update Now"
                            │
                            ▼
          Private Release Asset Download
    1. GET /repos/.../releases/assets/{asset_id}
       (Accept: application/octet-stream, Auth: Bearer)
    2. GitHub API returns HTTP 302 to AWS S3 pre-signed URL
    3. Updater follows redirect to S3 WITHOUT Auth header
    4. Stream binary with progress tracking (MB & Speed)
                            │
                            ▼
            Verify Checksum & Integrity
                            │
                            ▼
             Launch Setup Installer & Exit
```

---

## 2. Crucial Technical Implementation: AWS S3 Redirect Handling

A common pitfall with downloading private release assets via the GitHub API:
1. An initial request to `https://api.github.com/repos/{owner}/{repo}/releases/assets/{asset_id}` with `Accept: application/octet-stream` and `Authorization: Bearer <TOKEN>` returns an **HTTP 302 Found** redirect pointing to an AWS S3 pre-signed storage URL (`https://objects.githubusercontent.com/...`).
2. If standard HTTP clients automatically follow the redirect and forward the `Authorization: Bearer <TOKEN>` header to AWS S3, **Amazon S3 rejects the request with HTTP 400 Bad Request** (`"Only one auth mechanism allowed; query params and Authorization header are mutually exclusive"`).
3. **Our Solution in `UpdateService.cs`:**
   - The initial request sets `AllowAutoRedirect = false`.
   - When HTTP 302/303/307 is received, `response.Headers.Location` is extracted.
   - The subsequent download stream request to Amazon S3 is sent **without** the `Authorization` header.

---

## 3. Configuration Setup (Where to Configure Token & Repo)

The updater checks configuration from three sources in priority order:

### Method A: In-App UI Settings (Simplest for End Users)
1. In the app, click **Check for Updates**.
2. Click **⚙ Token Config** (or if unconfigured, the app will offer to open it automatically).
3. Enter your **Personal Access Token**, **Repository Owner** (`mahimapaseda`), and **Repository Name** (`Meta2Excel`).
4. Click **Test Connection** to verify immediate connectivity, then click **Save**.
   * *Saved securely under:* `%LOCALAPPDATA%\MetaReceiptsLite\updater.json`

---

### Method B: Environment Variables (Recommended for CI/CD & Power Users)
Set the following environment variables on the user's machine:

```powershell
# In PowerShell (User scope):
[System.Environment]::SetEnvironmentVariable("MRL_GITHUB_TOKEN", "github_pat_11A...", "User")
[System.Environment]::SetEnvironmentVariable("MRL_GITHUB_OWNER", "mahimapaseda", "User")
[System.Environment]::SetEnvironmentVariable("MRL_GITHUB_REPO", "Meta2Excel", "User")

# Standard GITHUB_TOKEN also supported as a fallback:
# $env:GITHUB_TOKEN = "ghp_..."
```

---

### Method C: JSON Configuration File
Create an `updater.json` file in either:
* `%LOCALAPPDATA%\MetaReceiptsLite\updater.json` *(per-user)*
* Same folder as `MetaReceiptsLite.exe` *(portable mode)*

```json
{
  "github_token": "github_pat_11AXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
  "repo_owner": "mahimapaseda",
  "repo_name": "Meta2Excel",
  "check_on_startup": true,
  "api_base_url": "https://api.github.com"
}
```

---

## 4. How to Generate a Fine-Grained GitHub PAT

To create a secure read-only token:
1. In GitHub, go to **Settings → Developer settings → Personal access tokens → Fine-grained tokens**.
2. Click **Generate new token**.
3. **Token name:** `MetaReceiptsLite Client Updater`
4. **Expiration:** Select preferred duration (e.g. 90 days or 1 year).
5. **Repository access:** Select **Only select repositories** → choose `mahimapaseda/Meta2Excel`.
6. **Permissions:**
   * Under **Repository permissions**, find **Contents**.
   * Set **Contents** to **Access: Read-only**.
   *(No metadata, issues, pull request, or administrative write permissions are required).*
7. Click **Generate token** and copy the resulting string (`github_pat_...`).

---

## 5. File Structure of the Updater Module

```
MetaReceiptsLite/
  ├── Updater/
  │    ├── SemVersion.cs             # Robust SemVer 2.0.0 parsing & comparison
  │    ├── UpdateModels.cs           # ReleaseInfo, ReleaseAsset, UpdateCheckResult, Progress
  │    ├── UpdateConfig.cs           # Config loader (Env vars, LocalAppData, JSON)
  │    ├── UpdateService.cs          # GitHub REST API client, S3 redirect handling, streaming
  │    ├── UpdateDialog.cs           # UI modal (Release notes, download progress bar)
  │    └── UpdateSettingsDialog.cs   # In-app PAT & Repo configuration modal
  └── MainForm.cs                    # Integrated startup background check & update button
```
