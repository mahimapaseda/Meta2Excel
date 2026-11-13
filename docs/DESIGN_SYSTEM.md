# Meta Receipts Design System (MRL-DS)

A high-precision, ledger-focused design system for **Meta Receipts Lite (Meta2Excel)**. Designed specifically for financial controllers, media buyers, and accounting teams requiring dense data clarity, instant visual verification, and zero decorative bloat.

---

## 1. Architecture: Three-Layer Token Structure

```
Primitive Tokens (assets/design-tokens.json)
  ├── Colors (Slate neutral scale, Ledger Emeralds, Meta Electric, Warnings)
  ├── Typography Scales (Display, Tabular Monospace, Body)
  └── Spatial Metrics (4px grid, radii, line-heights)
        ↓
Semantic Tokens (assets/design-tokens.css)
  ├── Intent Aliases (--bg-canvas, --text-primary, --accent-primary)
  └── Theme Overrides (High-Precision Dark vs Ledger Light)
        ↓
Component Tokens
  ├── Header & Topbar (--header-bg, --header-border)
  ├── File Staging Dropzone (--dropzone-bg, --dropzone-border)
  ├── Ledger Tables (--table-header-bg, --table-row-hover)
  └── Action Buttons (--btn-primary-bg, --btn-secondary-bg)
```

---

## 2. Core Palette

| Role | Token | Hex (Dark) | Hex (Light) | Purpose |
| :--- | :--- | :--- | :--- | :--- |
| **Canvas** | `--bg-canvas` | `#0B0F17` | `#F8FAFC` | Primary backdrop foundation |
| **Surface** | `--bg-surface` | `#111827` | `#FFFFFF` | Cards, panels, toolbars |
| **Elevated Surface** | `--bg-surface-elev` | `#162032` | `#F1F5F9` | Table headers, stat blocks |
| **Primary Accent** | `--accent-primary` | `#10B981` | `#15803D` | Balanced balances, verified data, Excel heritage |
| **Source Heritage** | `--accent-meta` | `#1877F2` | `#1877F2` | Meta Ads provenance markers |
| **Warning / Dup** | `--status-warning` | `#F59E0B` | `#D97706` | Deduplicated transactions, missing VAT |
| **Border Subtle** | `--border-subtle` | `#1E293B` | `#E2E8F0` | Dividers, row borders |
| **Border Strong** | `--border-strong` | `#334155` | `#CBD5E1` | Card outlines, container borders |

---

## 3. Typographic System

1. **Display & Headings:** `Plus Jakarta Sans`, tight tracking (`-0.02em` to `-0.03em`), bold structural presence without being theatrical.
2. **Tabular Data Figures & IDs:** `JetBrains Mono` / `Consolas` (`font-variant-numeric: tabular-nums`). All monetary figures, invoice numbers, dates, and campaign hashes are aligned with monospace precision.
3. **Body & Microcopy:** `Inter`, neutral and legible at small desktop scales (12px - 14px).

---

## 4. Anti-Template Principles

1. **Information density over whitespace:** Accounting tools need quick scanning. We avoid loose, 80px padded empty spaces where users have to scroll repeatedly to see 10 rows.
2. **No arbitrary decoration:** Every visual badge communicates a real transaction state (e.g., `VERIFIED` vs `DUP`).
3. **Restrained motion:** Animations are limited to state responses (dropzone drag active, theme toggle, row transitions). No disorienting fade-ups or parallax scrolls.
4. **Air-gapped verification:** Visible security guarantees reinforcing that customer financial receipt PDFs never leave local CPU memory.

