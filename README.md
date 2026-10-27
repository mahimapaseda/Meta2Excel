# Meta Receipts Lite

Lightweight Windows desktop app that converts **Meta (Facebook) Ads receipt PDFs** into an Excel workbook (`.xlsx`) or CSV files.

Designed for **low-spec PCs**: .NET Framework 4.8 WinForms, small footprint, sequential PDF parsing, and UI work kept off the UI thread.

## Requirements

### Clients (end users)

- Windows 10 / 11 (Windows 7 SP1 possible with .NET Framework 4.8 installed)
- **.NET Framework 4.8** (pre-installed on Windows 10/11)
- No Office/Excel required to *create* workbooks (Excel/LibreOffice only needed to open the output)

**Two ways to get the app:**

| Method | What you get |
|--------|----------------|
| **Setup installer** (recommended) | `MetaReceiptsLite-Setup-v*.exe` — installs under `%LOCALAPPDATA%\Programs\MetaReceiptsLite`, Start Menu shortcut, optional Desktop shortcut, uninstall via **Settings → Apps**. **No admin rights** required. |
| **Portable ZIP** | Unzip and run `MetaReceiptsLite.exe` (keep DLLs beside it). No Start Menu / Apps entry unless you create shortcuts yourself. |

Running the portable `.exe` alone does **not** “install” the app into Windows — use the Setup for that.

**Windows 7 SP1:** install [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48) offline installer first. The Setup does not bundle the .NET Framework runtime.

### Developers

- [.NET SDK 8+](https://dotnet.microsoft.com/download) (builds net48)
- Optional: [.NET Framework 4.8 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48) — or rely on the `Microsoft.NETFramework.ReferenceAssemblies` package (already referenced)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (free) — required to compile `MetaReceiptsLite-Setup-*.exe`

> **Note:** Use the NuGet package **`PdfPig`** (namespace `UglyToad.PdfPig`). Do **not** reference package id `UglyToad.PdfPig` on nuget.org — that ID was taken over by unrelated packages.

## Build, test, publish

```powershell
cd Meta2Excel
dotnet restore
dotnet build -c Release
dotnet test -c Release

# One-shot: Release build + portable ZIP + Setup.exe → .\dist\
.\scripts\Build-Release.ps1 -Version 1.0.4
```

Manual alternatives:

```powershell
# Deployable folder (exe + DLLs):
explorer .\MetaReceiptsLite\bin\Release\net48

# Portable zip only:
Compress-Archive -Path .\MetaReceiptsLite\bin\Release\net48\* -DestinationPath .\dist\MetaReceiptsLite.zip -Force
```

Target size: **under 15 MB** for the Release folder / portable zip.

Optional client warm-up (admin, once per machine):

```powershell
# Speeds subsequent cold starts (ngen / Native Image Generator)
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\ngen.exe" install MetaReceiptsLite.exe
```

## Usage

1. Run `MetaReceiptsLite.exe`
2. **Add PDFs...** or drag & drop PDF files / folders onto the window
3. Review receipts and line items (campaign filter available)
4. **Export Excel...** or **Export CSV** (line items / receipts / both as folder or zip)

Duplicate receipts (same Transaction ID) are skipped with a summary dialog. Per-file errors never stop the batch.

## Excel workbook sheets

| Sheet | Contents |
|-------|----------|
| Receipts | Header fields, billed amount, check, source file |
| Line Items | Campaign / ad set rows; CPM as Excel formula |
| Campaign Summary | Per campaign + currency with SUMIFS and share of spend |
| Daily Spend | Amount billed by **payment date** (not true per-day campaign spend) + PNG chart(s) |

## Performance budgets (how to verify)

Budgets from the product requirements:

| Metric | Target |
|--------|--------|
| Cold start | &lt; 2 s on 4 GB RAM / HDD |
| Peak working set | &lt; 150 MB for 50 receipts |
| Parse one 2–3 page receipt | &lt; 300 ms |
| UI | Never freezes (work on background thread) |

### Cold start

```powershell
$exe = ".\MetaReceiptsLite\bin\Release\net48\MetaReceiptsLite.exe"
Measure-Command { $p = Start-Process $exe -PassThru; Start-Sleep -Milliseconds 500; if (!$p.HasExited) { $p.CloseMainWindow() | Out-Null; $p.WaitForExit(3000) } }
# Or time until the window appears with a stopwatch while launching from Explorer.
```

### Peak working set (50 receipts)

```powershell
# Repeat sample PDFs to reach ~50 files, then in PowerShell while the app loads them:
Get-Process MetaReceiptsLite | Select-Object WorkingSet64, @{n='WS_MB';e={[math]::Round($_.WorkingSet64/1MB,1)}}
```

### Parse time

Use a small console snippet or temporary Stopwatch around `ReceiptParser.ParseFile` in a unit test; target &lt; 300 ms for one Oct-16 sample PDF on a dual-core / HDD machine.

Verified on the development machine (re-check on a 4 GB / HDD client):

| Check | Result |
|-------|--------|
| `dotnet build -c Release` folder size | **14.28 MB** (&lt; 15 MB) |
| Parse one Oct-16 sample (3-page, warm avg ×5) | **~55 ms** (&lt; 300 ms) |
| Acceptance samples | 4 receipts × 20 rows, billed $24+$11+$11+$11, all `Check=OK`, Daily Spend day total $57 |
| `dotnet test -c Release` | 28 passed |

Working-set and cold-start should be re-measured on the target low-spec HDD PC with Process Explorer / Task Manager while loading ~50 receipts (repeat samples). Design constraints: sequential parse, dispose PdfDocument immediately, no AdjustToContents, preview grid capped at 5,000 rows.

## Project layout

```
MetaReceiptsLite/          WinForms app (net48)
  Program.cs, MainForm.cs, AppIcon.ico
  Models.cs, ReceiptParser.cs, CurrencyDetector.cs
  ExcelExporter.cs, CsvExporter.cs, DailySpendChart.cs
MetaReceiptsLite.Tests/    xUnit tests + Samples/*.pdf
installer/                 Inno Setup script (MetaReceiptsLite.iss)
scripts/Build-Release.ps1  Build ZIP + Setup.exe into dist/
dist/                      Packaging output (gitignored)
```

## License / libraries

- [PdfPig](https://github.com/UglyToad/PdfPig) — PDF text extraction  
- [ClosedXML](https://github.com/ClosedXML/ClosedXML) — `.xlsx` writing  
- System.Drawing (GDI+) — daily spend chart PNGs  
