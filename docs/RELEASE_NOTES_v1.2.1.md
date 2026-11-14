# Meta Receipts Lite v1.2.1

## Hotfix: Application Startup Resolution

- Fixed a crash on startup caused by assigning transparent border colors to WinForms button appearance properties.
- Resolved `System.NotSupportedException: at System.Windows.Forms.FlatButtonAppearance.set_BorderColor(Color)`.
- Verified clean startup and form initialization on Windows .NET 4.8.
- Preserved modern desktop UI redesign (Segoe UI typography, rounded pill buttons, custom dark/light theme, and refined ledger tables).

## Downloads

- `MetaReceiptsLite-Setup-v1.2.1.exe` — recommended installer
- `MetaReceiptsLite-v1.2.1.zip` — portable app with dependencies
- `SHA256SUMS-v1.2.1.txt` — release asset checksums
