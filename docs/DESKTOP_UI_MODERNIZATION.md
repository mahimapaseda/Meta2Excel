# Desktop UI Modernization Guide (WinForms .NET 4.8)

This guide documents how the **Meta Receipts Design System** maps to the native Windows desktop app in [`MetaReceiptsLite/MainForm.cs`](../MetaReceiptsLite/MainForm.cs).

---

## 1. Palette Mapping (C# `System.Drawing.Color`)

Replace the existing hardcoded colors in `MainForm.cs` with the design system tokens:

```csharp
// Current in MainForm.cs:
// private static readonly Color ColorNavy = Color.FromArgb(31, 78, 120);
// private static readonly Color ColorExcel = Color.FromArgb(33, 115, 70);

// Modernized Design System Tokens:
public static class ThemeTokens
{
    // High-Precision Dark Theme
    public static class Dark
    {
        public static readonly Color Canvas         = Color.FromArgb(11, 15, 23);     // #0B0F17
        public static readonly Color Surface        = Color.FromArgb(17, 24, 39);     // #111827
        public static readonly Color SurfaceElev    = Color.FromArgb(22, 32, 50);     // #162032
        public static readonly Color BorderSubtle   = Color.FromArgb(30, 41, 59);     // #1E293B
        public static readonly Color BorderStrong   = Color.FromArgb(51, 65, 85);     // #334155
        public static readonly Color TextPrimary    = Color.FromArgb(241, 245, 249);  // #F1F5F9
        public static readonly Color TextSecondary  = Color.FromArgb(148, 163, 184);  // #94A3B8
        public static readonly Color AccentPrimary  = Color.FromArgb(16, 185, 129);   // #10B981 (Emerald)
        public static readonly Color AccentMeta     = Color.FromArgb(24, 119, 242);   // #1877F2 (Meta Blue)
        public static readonly Color Warning        = Color.FromArgb(245, 158, 11);   // #F59E0B (Amber)
    }

    // Ledger Light Theme (matches Excel / Office standard)
    public static class Light
    {
        public static readonly Color Canvas         = Color.FromArgb(248, 250, 252);  // #F8FAFC
        public static readonly Color Surface        = Color.White;
        public static readonly Color SurfaceElev    = Color.FromArgb(241, 245, 249);  // #F1F5F9
        public static readonly Color BorderSubtle   = Color.FromArgb(226, 232, 240);  // #E2E8F0
        public static readonly Color BorderStrong   = Color.FromArgb(203, 213, 225);  // #CBD5E1
        public static readonly Color TextPrimary    = Color.FromArgb(15, 23, 42);     // #0F172A
        public static readonly Color TextSecondary  = Color.FromArgb(71, 85, 105);    // #475569
        public static readonly Color AccentPrimary  = Color.FromArgb(21, 128, 61);    // #15803D (Excel Green)
        public static readonly Color AccentMeta     = Color.FromArgb(24, 119, 242);
        public static readonly Color Warning        = Color.FromArgb(217, 119, 6);
    }
}
```

---

## 2. DataGridView Ledger Styling

In WinForms, `DataGridView` defaults to Windows 95/XP style 3D borders unless customized. Apply flat styling:

```csharp
private void ApplyModernGridStyling(DataGridView grid, bool isDark = false)
{
    var theme = isDark ? (dynamic)typeof(ThemeTokens.Dark) : typeof(ThemeTokens.Light);

    grid.BorderStyle = BorderStyle.None;
    grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
    grid.GridColor = isDark ? ThemeTokens.Dark.BorderSubtle : ThemeTokens.Light.BorderSubtle;
    grid.BackgroundColor = isDark ? ThemeTokens.Dark.Surface : ThemeTokens.Light.Surface;
    grid.EnableHeadersVisualStyles = false;

    // Header styling
    grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
    grid.ColumnHeadersDefaultCellStyle.BackColor = isDark ? ThemeTokens.Dark.SurfaceElev : ThemeTokens.Light.SurfaceElev;
    grid.ColumnHeadersDefaultCellStyle.ForeColor = isDark ? ThemeTokens.Dark.TextSecondary : ThemeTokens.Light.TextSecondary;
    grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
    grid.ColumnHeadersHeight = 36;

    // Row styling
    grid.RowTemplate.Height = 32;
    grid.DefaultCellStyle.BackColor = isDark ? ThemeTokens.Dark.Surface : ThemeTokens.Light.Surface;
    grid.DefaultCellStyle.ForeColor = isDark ? ThemeTokens.Dark.TextPrimary : ThemeTokens.Light.TextPrimary;
    grid.DefaultCellStyle.SelectionBackColor = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(226, 232, 240);
    grid.DefaultCellStyle.SelectionForeColor = isDark ? ThemeTokens.Dark.TextPrimary : ThemeTokens.Light.TextPrimary;

    // Numeric formatting for amounts
    if (grid.Columns.Contains("Amount"))
    {
        grid.Columns["Amount"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        grid.Columns["Amount"].DefaultCellStyle.Font = new Font("Consolas", 9.5f, FontStyle.Regular);
    }
}
```

---

## 3. High-DPI & Font Fallbacks

* Primary UI Font: `Segoe UI` (shipped with all Windows 10/11 installations).
* Numeric Figures & Invoice Hashes: `Consolas` or `Lucida Console` for guaranteed monospaced tabular alignment without needing external font files.
* DPI Awareness: Handled via `AutoScaleMode = AutoScaleMode.Dpi` in `MainForm.cs` and `<dpiAware>true/pm</dpiAware>` in the app manifest.

