# Meta Receipts Lite v1.0.21

## Update reliability

- Renamed the update dialog's misleading **Update Now** settings button to **Settings...**. The primary **Update Now** button now clearly starts the download.
- Public releases now use anonymous GitHub requests; the public app no longer bundles a personal access token or asks users to configure one.
- Improved release downloads to handle both GitHub's redirect and direct asset responses.
- Verify downloaded file size and GitHub's SHA-256 digest before offering the installer.
- Show token settings only for authentication errors on a non-default repository.

## Downloads

- `MetaReceiptsLite-Setup-v1.0.21.exe` — recommended installer
- `MetaReceiptsLite-v1.0.21.zip` — portable app with dependencies
- `SHA256SUMS-v1.0.21.txt` — release asset checksums

## Requirements

Windows 10/11 and .NET Framework 4.8. The installer does not require administrator rights.
