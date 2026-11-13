using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MetaReceiptsLite.Updater;

namespace MetaReceiptsLite
{
    public sealed class MainForm : Form
    {
        private const int PreviewRowCap = 5000;

        // Ledger Light tokens from docs/DESIGN_SYSTEM.md. Solid fills keep rendering inexpensive.
        private static readonly Color ColorNavy = Color.FromArgb(15, 23, 42);       // #0F172A
        private static readonly Color ColorExcel = Color.FromArgb(21, 128, 61);    // #15803D
        private static readonly Color ColorCanvas = Color.FromArgb(248, 250, 252); // #F8FAFC
        private static readonly Color ColorPanel = Color.White;
        private static readonly Color ColorBorder = Color.FromArgb(226, 232, 240); // #E2E8F0
        private static readonly Color ColorMuted = Color.FromArgb(71, 85, 105);    // #475569
        private static readonly Color ColorElevated = Color.FromArgb(241, 245, 249); // #F1F5F9
        private readonly string _themePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MetaReceiptsLite", "theme.txt");
        private ThemePalette _theme;

        private readonly List<Receipt> _receipts = new List<Receipt>();
        private readonly List<LineItem> _items = new List<LineItem>();
        private readonly ReceiptDeduper _deduper = new ReceiptDeduper();

        private readonly UpdateConfig _updateConfig = UpdateConfig.Load();
        private readonly IUpdateService _updateService = new UpdateService();
        private readonly SemVersion _currentVersion = SemVersion.Parse("1.1.2");

        private Panel _header;
        private Label _brandTitle;
        private Label _brandSub;
        private FlowLayoutPanel _actions;
        private Button _btnAdd;
        private Button _btnExportExcel;
        private Button _btnExportCsv;
        private Button _btnClear;
        private Button _btnCancel;
        private Button _btnRemove;
        private Button _btnUpdate;
        private Button _btnTheme;
        private ContextMenuStrip _csvMenu;

        private Panel _content;
        private Panel _dropZone;
        private Label _dropTitle;
        private Label _dropHint;
        private Button _dropBrowse;

        private SplitContainer _split;
        private Label _lblReceipts;
        private Label _lblItems;
        private DataGridView _gridReceipts;
        private DataGridView _gridItems;
        private TextBox _filterCampaign;
        private Label _lblFilter;
        private StatusStrip _status;
        private ToolStripStatusLabel _statusLabel;

        private CancellationTokenSource _cts;
        private bool _busy;
        private bool _dragHighlight;

        public MainForm()
        {
            Text = "Meta Receipts Lite";
            _theme = LoadTheme();
            Width = 1180;
            Height = 760;
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            BackColor = _theme.Canvas;
            AllowDrop = true;
            DoubleBuffered = true;

            try
            {
                var ico = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppIcon.ico");
                if (File.Exists(ico))
                    Icon = new Icon(ico);
            }
            catch
            {
                // ignore missing icon
            }

            BuildUi();
            WireEvents();
            EnableFileDrop(this);
            UpdateStatus();
            SetBusy(false);
            UpdateEmptyState();
            ApplyTheme(this);
        }

        private void BuildUi()
        {
            BuildHeader();
            BuildContent();
            BuildStatus();

            Controls.Add(_content);
            Controls.Add(_header);
            Controls.Add(_status);
            _header.BringToFront();
            _status.BringToFront();
        }

        private void BuildHeader()
        {
            _header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 112,
                BackColor = ColorPanel,
                Padding = new Padding(18, 10, 18, 8)
            };

            _brandTitle = new Label
            {
                AutoSize = true,
                Text = "Meta Receipts Lite",
                ForeColor = ColorNavy,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 16f, FontStyle.Bold),
                Location = new Point(18, 10),
                BackColor = Color.Transparent
            };
            _brandSub = new Label
            {
                AutoSize = true,
                Text = "Turn Meta Ads receipt PDFs into a clean Excel or CSV ledger  ·  v" + _currentVersion,
                ForeColor = ColorMuted,
                Location = new Point(20, 40),
                BackColor = Color.Transparent
            };

            _actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(0, 4, 0, 2),
                BackColor = ColorPanel
            };

            _btnAdd = MakeButton("&Add PDFs...", ColorExcel, Color.White, true);
            _btnExportExcel = MakeButton("Export E&xcel...", ColorNavy, Color.White, true);
            _btnExportCsv = MakeButton("Export C&SV  ▾", ColorPanel, ColorNavy, false);
            _btnClear = MakeButton("C&lear", ColorPanel, ColorMuted, false);
            _btnCancel = MakeButton("Ca&ncel", Color.FromArgb(190, 18, 60), Color.White, false);
            _btnRemove = MakeButton("&Remove selected", ColorPanel, ColorMuted, false);
            _btnUpdate = MakeButton("Check for &updates", ColorPanel, ColorMuted, false);
            _btnTheme = MakeButton("Dark mode", ColorPanel, ColorMuted, false);

            _csvMenu = new ContextMenuStrip();
            _csvMenu.Items.Add("Line items...", null, (s, e) => ExportCsv(CsvMode.LineItems));
            _csvMenu.Items.Add("Receipts...", null, (s, e) => ExportCsv(CsvMode.Receipts));
            _csvMenu.Items.Add("Both (folder)...", null, (s, e) => ExportCsv(CsvMode.BothFolder));
            _csvMenu.Items.Add("Both (zip)...", null, (s, e) => ExportCsv(CsvMode.BothZip));

            _actions.Controls.Add(_btnAdd);
            _actions.Controls.Add(_btnExportExcel);
            _actions.Controls.Add(_btnExportCsv);
            _actions.Controls.Add(_btnClear);
            _actions.Controls.Add(_btnCancel);
            _actions.Controls.Add(_btnRemove);
            _actions.Controls.Add(_btnUpdate);
            _actions.Controls.Add(_btnTheme);

            _header.Controls.Add(_actions);
            _header.Controls.Add(_brandTitle);
            _header.Controls.Add(_brandSub);
        }

        private void BuildContent()
        {
            _content = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 10, 12, 8),
                BackColor = ColorCanvas
            };

            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6,
                BackColor = ColorCanvas,
                Panel1MinSize = 100,
                Panel2MinSize = 120
                // Keep Visible=true always so Panel1/Panel2 children get a real layout size.
                // Hiding the split while empty leaves Panel1 controls stuck at 0x0.
            };

            // Receipts section — add Dock.Top before Dock.Fill
            var top = new Panel { Dock = DockStyle.Fill, BackColor = ColorCanvas, Padding = new Padding(0, 0, 0, 4) };
            _lblReceipts = SectionLabel("Receipts");
            var receiptsHost = CardHost();
            _gridReceipts = CreateGrid();
            // File first so the loaded PDF name is immediately visible
            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "File",
                HeaderText = "File",
                Width = 260,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            _gridReceipts.Columns.Add(Col("InvoiceNo", "Invoice No", 140));
            _gridReceipts.Columns.Add(Col("Date", "Date", 120));
            _gridReceipts.Columns.Add(Col("Reference", "Reference", 100));
            _gridReceipts.Columns.Add(Col("Currency", "Currency", 70));
            _gridReceipts.Columns.Add(Col("Billed", "Billed", 90));
            _gridReceipts.Columns.Add(Col("Rows", "Rows", 50));
            _gridReceipts.Columns.Add(Col("RowsTotal", "Rows total", 90));
            _gridReceipts.Columns.Add(Col("Check", "Check", 120));
            receiptsHost.Controls.Add(_gridReceipts);
            top.Controls.Add(_lblReceipts);
            top.Controls.Add(receiptsHost);

            // Line items section
            var bottom = new Panel { Dock = DockStyle.Fill, BackColor = ColorCanvas, Padding = new Padding(0, 4, 0, 0) };
            var filterBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = ColorCanvas
            };
            _lblItems = SectionLabel("Line items");
            _lblItems.Dock = DockStyle.Left;
            _lblItems.AutoSize = false;
            _lblItems.Width = 110;
            _lblItems.TextAlign = ContentAlignment.MiddleLeft;
            _lblFilter = new Label
            {
                Text = "Branch / campaign filter",
                AutoSize = true,
                ForeColor = ColorMuted,
                Location = new Point(120, 8)
            };
            _filterCampaign = new TextBox
            {
                Width = 240,
                Location = new Point(280, 5),
                BorderStyle = BorderStyle.FixedSingle
            };
            filterBar.Controls.Add(_lblItems);
            filterBar.Controls.Add(_lblFilter);
            filterBar.Controls.Add(_filterCampaign);

            var itemsHost = CardHost();
            _gridItems = CreateGrid();
            _gridItems.Columns.Add(Col("InvoiceNo", "Invoice No", 120));
            _gridItems.Columns.Add(Col("Branch", "Branch", 110));
            _gridItems.Columns.Add(Col("Campaign", "Campaign", 100));
            _gridItems.Columns.Add(Col("PeriodFrom", "Period from", 110));
            _gridItems.Columns.Add(Col("PeriodTo", "Period to", 110));
            _gridItems.Columns.Add(Col("AdSet", "Ad set", 140));
            _gridItems.Columns.Add(Col("Currency", "Currency", 60));
            _gridItems.Columns.Add(Col("Impressions", "Impressions", 80));
            _gridItems.Columns.Add(Col("Cost", "Cost", 70));
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CPM",
                HeaderText = "CPM",
                Width = 70,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            itemsHost.Controls.Add(_gridItems);
            bottom.Controls.Add(filterBar);
            bottom.Controls.Add(itemsHost);

            _split.Panel1.Controls.Add(top);
            _split.Panel2.Controls.Add(bottom);

            // Empty drop zone overlays the split (split stays visible underneath for layout)
            _dropZone = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorPanel,
                Visible = true
            };
            _dropZone.Paint += DropZone_Paint;

            _dropTitle = new Label
            {
                AutoSize = true,
                Text = "Start with your Meta receipt PDFs",
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 13f, FontStyle.Bold),
                ForeColor = ColorNavy,
                BackColor = Color.Transparent
            };
            _dropHint = new Label
            {
                AutoSize = true,
                Text = "Drag files or folders into this window, or browse. Your PDFs stay on this PC.",
                ForeColor = ColorMuted,
                BackColor = Color.Transparent
            };
            _dropBrowse = MakeButton("Browse PDFs...", ColorExcel, Color.White, true);
            _dropBrowse.Width = 140;
            _dropZone.AccessibleName = "Add Meta receipt PDF files";
            _dropZone.AccessibleDescription = "Drop PDF files or folders here, or browse to select them. Files are processed locally.";
            _filterCampaign.AccessibleName = "Filter line items";
            _filterCampaign.AccessibleDescription = "Filter by branch, campaign, or ad set.";

            _dropZone.Controls.Add(_dropTitle);
            _dropZone.Controls.Add(_dropHint);
            _dropZone.Controls.Add(_dropBrowse);
            _dropZone.Resize += (s, e) => LayoutDropZone();

            // Split first (back), drop zone last (front) while empty
            _content.Controls.Add(_split);
            _content.Controls.Add(_dropZone);
            LayoutDropZone();
            Resize += (s, e) => ApplySplitterDistance();
            Shown += async (s, e) =>
            {
                ApplySplitterDistance();
                if (_updateConfig.CheckOnStartup)
                {
                    await CheckForUpdatesAsync(manual: false);
                }
            };
        }

        private void BuildStatus()
        {
            _status = new StatusStrip
            {
                BackColor = ColorCanvas,
                SizingGrip = true,
                RenderMode = ToolStripRenderMode.System,
                ForeColor = ColorMuted
            };
            _statusLabel = new ToolStripStatusLabel("Ready")
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ColorMuted
            };
            _status.Items.Add(_statusLabel);
        }

        private ThemePalette LoadTheme()
        {
            try { return File.Exists(_themePath) && File.ReadAllText(_themePath).Trim().Equals("dark", StringComparison.OrdinalIgnoreCase) ? ThemePalette.Dark : ThemePalette.Light; }
            catch { return ThemePalette.Light; }
        }

        private void ToggleTheme()
        {
            _theme = _theme.IsDark ? ThemePalette.Light : ThemePalette.Dark;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_themePath));
                File.WriteAllText(_themePath, _theme.IsDark ? "dark" : "light");
            }
            catch { /* Theme preference is optional; keep the in-session choice. */ }
            ApplyTheme(this);
        }

        private void ApplyTheme(Control root)
        {
            root.BackColor = _theme.Canvas;
            root.ForeColor = _theme.Text;
            foreach (Control control in root.Controls)
            {
                if (control is DataGridView grid)
                {
                    grid.BackgroundColor = _theme.Surface;
                    grid.GridColor = _theme.Border;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = _theme.Elevated;
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = _theme.Muted;
                    grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = _theme.Elevated;
                    grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = _theme.Text;
                    grid.DefaultCellStyle.BackColor = _theme.Surface;
                    grid.DefaultCellStyle.ForeColor = _theme.Text;
                    grid.DefaultCellStyle.SelectionBackColor = _theme.Selection;
                    grid.DefaultCellStyle.SelectionForeColor = _theme.Text;
                    grid.AlternatingRowsDefaultCellStyle.BackColor = _theme.Elevated;
                    grid.AlternatingRowsDefaultCellStyle.ForeColor = _theme.Text;
                    grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = _theme.Selection;
                    grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = _theme.Text;
                }
                else if (control is Button button)
                {
                    StyleButton(button);
                    button.EnabledChanged -= ThemeButton_EnabledChanged;
                    button.EnabledChanged += ThemeButton_EnabledChanged;
                }
                else if (control is TextBox textBox)
                {
                    textBox.BackColor = _theme.Surface;
                    textBox.ForeColor = _theme.Text;
                }
                else if (control == _header)
                {
                    ApplyTheme(control);
                    control.BackColor = _theme.Surface;
                }
                else if (control == _dropZone)
                {
                    ApplyTheme(control);
                    control.BackColor = _theme.Surface;
                }
                else
                {
                    control.BackColor = _theme.Canvas;
                    control.ForeColor = _theme.Text;
                    ApplyTheme(control);
                }
            }

            _brandTitle.ForeColor = _theme.Text;
            _brandSub.ForeColor = _theme.Muted;
            _lblReceipts.ForeColor = _theme.Text;
            _lblItems.ForeColor = _theme.Text;
            _lblFilter.ForeColor = _theme.Muted;
            _dropTitle.ForeColor = _theme.Text;
            _dropHint.ForeColor = _theme.Muted;
            _status.BackColor = _theme.Canvas;
            _statusLabel.ForeColor = _theme.Muted;
            _btnTheme.Text = _theme.IsDark ? "Light mode" : "Dark mode";
            _btnTheme.AccessibleName = _theme.IsDark ? "Switch to light mode" : "Switch to dark mode";
            _dropZone.Invalidate();
        }

        private void ThemeButton_EnabledChanged(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button != null) StyleButton(button);
        }

        private void StyleButton(Button button)
        {
            bool primary = button == _btnAdd || button == _dropBrowse || button == _btnExportExcel;
            if (!button.Enabled)
            {
                button.BackColor = _theme.DisabledSurface;
                button.ForeColor = _theme.DisabledText;
            }
            else
            {
                button.BackColor = primary ? _theme.Accent : _theme.Surface;
                button.ForeColor = primary && _theme.IsDark ? Color.FromArgb(5, 35, 23) : (primary ? Color.White : _theme.Text);
            }
            button.FlatAppearance.BorderColor = _theme.Border;
            button.FlatAppearance.MouseOverBackColor = _theme.Elevated;
        }

        private static Label SectionLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 24,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9.5f, FontStyle.Bold),
                ForeColor = ColorNavy,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 0, 0)
            };
        }

        private void ApplySplitterDistance()
        {
            if (_split == null || !_split.IsHandleCreated) return;
            int h = _split.ClientSize.Height;
            int min = _split.Panel1MinSize + _split.Panel2MinSize + _split.SplitterWidth;
            if (h <= min) return;
            // ~32% for receipts so the File row stays on screen above line items
            int desired = Math.Max(_split.Panel1MinSize, Math.Min(h * 32 / 100, h - _split.Panel2MinSize));
            try
            {
                if (Math.Abs(_split.SplitterDistance - desired) > 8)
                    _split.SplitterDistance = desired;
            }
            catch (InvalidOperationException)
            {
                // SplitContainer rejects distances before it has a stable size
            }
        }

        private static Panel CardHost()
        {
            return new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorPanel,
                Padding = new Padding(1),
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private static Button MakeButton(string text, Color back, Color fore, bool primary)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(primary ? 124 : 96, 34),
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = fore,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 2, 8, 2),
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.FlatAppearance.BorderColor = ColorBorder;
            b.FlatAppearance.MouseOverBackColor = primary ? ControlPaint.Light(back, 0.12f) : ColorElevated;
            b.FlatAppearance.MouseDownBackColor = primary ? ControlPaint.Dark(back, 0.08f) : Color.FromArgb(226, 232, 240);
            b.AccessibleRole = AccessibleRole.PushButton;
            b.TabStop = true;
            return b;
        }

        private static DataGridViewTextBoxColumn Col(string name, string header, int width)
        {
            return new DataGridViewTextBoxColumn { Name = name, HeaderText = header, Width = width };
        }

        private DataGridView CreateGrid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                BackgroundColor = ColorPanel,
                BorderStyle = BorderStyle.None,
                GridColor = ColorBorder,
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = ColorElevated,
                    ForeColor = ColorMuted,
                    Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9f, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    SelectionBackColor = ColorElevated,
                    SelectionForeColor = ColorNavy,
                    Padding = new Padding(4, 2, 4, 2)
                },
                ColumnHeadersHeight = 36,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = ColorPanel,
                    ForeColor = ColorNavy,
                    SelectionBackColor = Color.FromArgb(219, 234, 254),
                    SelectionForeColor = ColorNavy,
                    Padding = new Padding(8, 2, 8, 2)
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(248, 250, 252),
                    ForeColor = ColorNavy,
                    SelectionBackColor = Color.FromArgb(219, 234, 254),
                    SelectionForeColor = ColorNavy
                },
                RowTemplate = { Height = 30 },
                StandardTab = true,
                TabStop = true
            };
            return g;
        }

        private void LayoutDropZone()
        {
            if (_dropZone == null) return;
            int cx = _dropZone.ClientSize.Width / 2;
            int cy = _dropZone.ClientSize.Height / 2;
            _dropTitle.Location = new Point(cx - _dropTitle.Width / 2, cy - 50);
            _dropHint.Location = new Point(cx - _dropHint.Width / 2, cy - 18);
            _dropBrowse.Location = new Point(cx - _dropBrowse.Width / 2, cy + 18);
        }

        private void DropZone_Paint(object sender, PaintEventArgs e)
        {
            var rect = _dropZone.ClientRectangle;
            rect.Inflate(-18, -18);
            using (var pen = new Pen(_dragHighlight ? ColorExcel : Color.FromArgb(203, 213, 225), _dragHighlight ? 2.5f : 1.5f))
            {
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                e.Graphics.DrawRectangle(pen, rect);
            }
        }

        private void WireEvents()
        {
            _btnAdd.Click += (s, e) => AddPdfsDialog();
            _dropBrowse.Click += (s, e) => AddPdfsDialog();
            _btnExportExcel.Click += (s, e) => ExportExcel();
            _btnExportCsv.Click += (s, e) =>
            {
                _csvMenu.Show(_btnExportCsv, new Point(0, _btnExportCsv.Height));
            };
            _btnClear.Click += (s, e) => ClearAll();
            _btnCancel.Click += (s, e) =>
            {
                if (_cts != null) _cts.Cancel();
            };
            _btnRemove.Click += (s, e) => RemoveSelectedReceipts();
            _btnUpdate.Click += async (s, e) => await CheckForUpdatesAsync(manual: true);
            _btnTheme.Click += (s, e) => ToggleTheme();
            _filterCampaign.TextChanged += (s, e) => RefreshItemGrid();
        }

        /// <summary>
        /// WinForms does not bubble file drops from children without AllowDrop.
        /// Wire every descendant so drops on labels, buttons, and grids are accepted.
        /// </summary>
        private void EnableFileDrop(Control root)
        {
            if (root == null) return;
            root.AllowDrop = true;
            root.DragEnter -= MainForm_DragEnter;
            root.DragOver -= MainForm_DragOver;
            root.DragDrop -= MainForm_DragDrop;
            root.DragLeave -= MainForm_DragLeave;
            root.DragEnter += MainForm_DragEnter;
            root.DragOver += MainForm_DragOver;
            root.DragDrop += MainForm_DragDrop;
            root.DragLeave += MainForm_DragLeave;
            foreach (Control child in root.Controls)
                EnableFileDrop(child);
        }

        private void SetDragHighlight(bool on)
        {
            if (_dragHighlight == on) return;
            _dragHighlight = on;
            if (_dropZone != null && _dropZone.Visible)
                _dropZone.Invalidate();
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                SetDragHighlight(true);
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void MainForm_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void MainForm_DragLeave(object sender, EventArgs e)
        {
            SetDragHighlight(false);
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            SetDragHighlight(false);
            if (e.Data == null) return;
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0)
            {
                MessageBox.Show(this,
                    "No files were received from the drop.",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var pdfs = ExpandPaths(paths);
            if (pdfs.Count == 0)
            {
                MessageBox.Show(this,
                    "No PDF files found. Drop .pdf files or a folder that contains PDFs.",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            BeginParse(pdfs);
        }

        private void AddPdfsDialog()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "PDF files (*.pdf)|*.pdf";
                dlg.Multiselect = true;
                dlg.Title = "Add Meta receipt PDFs";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    BeginParse(dlg.FileNames.ToList());
            }
        }

        private static List<string> ExpandPaths(IEnumerable<string> paths)
        {
            var result = new List<string>();
            foreach (var p in paths)
            {
                if (Directory.Exists(p))
                {
                    foreach (var f in Directory.GetFiles(p, "*.pdf"))
                        result.Add(f);
                }
                else if (File.Exists(p) && p.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(p);
                }
            }
            return result;
        }

        private async void BeginParse(List<string> files)
        {
            if (_busy || files == null || files.Count == 0) return;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            SetBusy(true);
            var errors = new List<string>();
            int added = 0;

            try
            {
                await Task.Run(() =>
                {
                    for (int i = 0; i < files.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        var file = files[i];
                        var name = Path.GetFileName(file);
                        BeginInvoke(new Action(() =>
                        {
                            _statusLabel.Text = "Parsing " + (i + 1) + "/" + files.Count + ": " + name;
                        }));

                        try
                        {
                            var receipt = ReceiptParser.ParseFile(file);
                            lock (_receipts)
                            {
                                string dupReason;
                                if (!_deduper.TryRegister(receipt, out dupReason))
                                {
                                    errors.Add(name + " — " + dupReason);
                                    continue;
                                }
                                _receipts.Add(receipt);
                                _items.AddRange(receipt.Items);
                                added++;
                            }
                        }
                        catch (InvalidOperationException ex)
                        {
                            errors.Add(name + " — " + ex.Message);
                        }
                        catch (IOException ex)
                        {
                            errors.Add(name + " — unreadable (" + ex.Message + ")");
                        }
                        catch (Exception ex)
                        {
                            errors.Add(name + " — " + ClassifyError(ex));
                        }
                    }
                }, token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                _statusLabel.Text = "Cancelled.";
            }
            finally
            {
                SetBusy(false);
                UpdateEmptyState();
                RefreshAllGrids();
                UpdateStatus();
                if (errors.Count > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Skipped " + errors.Count + " file(s):");
                    sb.AppendLine();
                    foreach (var err in errors)
                        sb.AppendLine("• " + err);
                    MessageBox.Show(this, sb.ToString(), "Import summary", MessageBoxButtons.OK,
                        errors.Count == files.Count ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                }
                else if (added > 0)
                {
                    _statusLabel.Text = "Added " + added + " receipt(s).";
                    UpdateStatus();
                }
                if (_cts != null) { _cts.Dispose(); _cts = null; }
            }
        }

        private static string ClassifyError(Exception ex)
        {
            var msg = ex.Message ?? string.Empty;
            if (msg.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0)
                return "password-protected";
            if (msg.IndexOf("Not a Meta receipt", StringComparison.OrdinalIgnoreCase) >= 0)
                return msg;
            return "unreadable (" + msg + ")";
        }

        private void RefreshAllGrids()
        {
            SuspendLayout();
            _gridReceipts.SuspendLayout();
            _gridItems.SuspendLayout();
            try
            {
                RefreshReceiptGrid();
                RefreshItemGrid();
            }
            finally
            {
                _gridItems.ResumeLayout();
                _gridReceipts.ResumeLayout();
                ResumeLayout(true);
            }
        }

        private void UpdateEmptyState()
        {
            bool empty = _receipts.Count == 0;
            _dropZone.Visible = empty;
            // Split stays Visible so its panels keep a real size; drop zone only covers it.
            if (empty)
            {
                _dropZone.BringToFront();
                LayoutDropZone();
            }
            else
            {
                _split.BringToFront();
                ApplySplitterDistance();
                _split.Panel1.PerformLayout();
                _split.Panel2.PerformLayout();
            }
        }

        private void RefreshReceiptGrid()
        {
            _gridReceipts.Rows.Clear();
            int n = Math.Min(_receipts.Count, PreviewRowCap);
            for (int i = 0; i < n; i++)
            {
                var r = _receipts[i];
                int idx = _gridReceipts.Rows.Add(
                    r.SourceFile,
                    r.InvoiceNo,
                    r.InvoiceDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    r.ReferenceNumber,
                    r.CurrencyCode,
                    CurrencyDetector.FormatMoney(r.AmountBilled, r.CurrencySymbol),
                    r.ItemCount.ToString(CultureInfo.InvariantCulture),
                    CurrencyDetector.FormatMoney(r.ItemsTotal, r.CurrencySymbol),
                    r.Check);
                _gridReceipts.Rows[idx].Tag = r;
            }
            if (n > 0)
            {
                _gridReceipts.ClearSelection();
                _gridReceipts.Rows[0].Selected = true;
                _gridReceipts.FirstDisplayedScrollingRowIndex = 0;
            }
        }

        private void RefreshItemGrid()
        {
            _gridItems.Rows.Clear();
            string filter = (_filterCampaign.Text ?? string.Empty).Trim();
            IEnumerable<LineItem> query = _items;
            if (filter.Length > 0)
            {
                query = _items.Where(it =>
                {
                    string branch = BranchResolver.ResolveName(it);
                    return (branch != null && branch.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                           (it.CampaignCode != null && it.CampaignCode.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                           (it.CampaignName != null && it.CampaignName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                           (it.AdSet != null && it.AdSet.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
                });
            }

            int count = 0;
            foreach (var it in query)
            {
                if (count >= PreviewRowCap) break;
                _gridItems.Rows.Add(
                    it.InvoiceNo,
                    BranchResolver.ResolveName(it),
                    it.CampaignDisplay,
                    it.PeriodFrom == default(DateTime) ? "" : it.PeriodFrom.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    it.PeriodTo == default(DateTime) ? "" : it.PeriodTo.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    it.AdSet,
                    it.CurrencyCode,
                    it.Impressions.ToString("N0", CultureInfo.InvariantCulture),
                    CurrencyDetector.FormatMoney(it.Cost, CurrencyDetector.SymbolForCode(it.CurrencyCode)),
                    it.Cpm.ToString("0.00", CultureInfo.InvariantCulture));
                count++;
            }
        }

        private void UpdateStatus()
        {
            var billed = _receipts
                .GroupBy(r => r.CurrencyCode)
                .Select(g => g.Key + " " + CurrencyDetector.FormatMoney(g.Sum(x => x.AmountBilled), CurrencyDetector.SymbolForCode(g.Key)))
                .ToList();
            string billedText = billed.Count == 0 ? "billed —" : "billed " + string.Join(", ", billed);
            long impressions = _receipts.Sum(r => r.TotalImpressions);
            string capNote = (_receipts.Count > PreviewRowCap || _items.Count > PreviewRowCap)
                ? " · preview capped at " + PreviewRowCap.ToString(CultureInfo.InvariantCulture) + " rows"
                : "";
            _statusLabel.Text = _receipts.Count + " receipt(s) · " + _items.Count + " line items · " +
                                billedText + " · " + impressions.ToString("N0", CultureInfo.InvariantCulture) +
                                " impressions" + capNote;
        }

        private void RemoveSelectedReceipts()
        {
            if (_busy) return;
            var selected = _gridReceipts.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => r.Tag as Receipt)
                .Where(r => r != null)
                .ToList();
            if (selected.Count == 0) return;

            foreach (var r in selected)
            {
                _receipts.Remove(r);
                _deduper.Unregister(r);
                if (r.Items != null)
                {
                    var set = new HashSet<LineItem>(r.Items);
                    _items.RemoveAll(it => set.Contains(it));
                }
            }

            RefreshAllGrids();
            UpdateStatus();
        }

        private void ClearAll()
        {
            if (_busy) return;
            _receipts.Clear();
            _items.Clear();
            _deduper.Clear();
            RefreshAllGrids();
            UpdateStatus();
        }

        private async void ExportExcel()
        {
            if (_busy) return;
            if (_receipts.Count == 0)
            {
                MessageBox.Show(this, "No receipts to export.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string path;
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "Excel workbook (*.xlsx)|*.xlsx";
                dlg.FileName = ExportFileName("MetaReceipts", ".xlsx");
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                path = dlg.FileName;
            }

            var receiptsSnap = new List<Receipt>(_receipts);
            var itemsSnap = new List<LineItem>(_items);
            await RunExportAsync("Exporting Excel...", token =>
            {
                token.ThrowIfCancellationRequested();
                ExcelExporter.Export(path, receiptsSnap, itemsSnap);
            }, "Excel exported successfully.").ConfigureAwait(true);
        }

        private enum CsvMode { LineItems, Receipts, BothFolder, BothZip }

        private async void ExportCsv(CsvMode mode)
        {
            if (_busy) return;
            if (_receipts.Count == 0)
            {
                MessageBox.Show(this, "No receipts to export.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string path = null;
            bool asZip = false;
            switch (mode)
            {
                case CsvMode.LineItems:
                    using (var dlg = new SaveFileDialog())
                    {
                        dlg.Filter = "CSV (*.csv)|*.csv";
                        dlg.FileName = ExportFileName("line-items", ".csv");
                        if (dlg.ShowDialog(this) != DialogResult.OK) return;
                        path = dlg.FileName;
                    }
                    break;
                case CsvMode.Receipts:
                    using (var dlg = new SaveFileDialog())
                    {
                        dlg.Filter = "CSV (*.csv)|*.csv";
                        dlg.FileName = ExportFileName("receipts", ".csv");
                        if (dlg.ShowDialog(this) != DialogResult.OK) return;
                        path = dlg.FileName;
                    }
                    break;
                case CsvMode.BothFolder:
                    using (var dlg = new FolderBrowserDialog())
                    {
                        dlg.Description = "Choose folder for CSV files";
                        if (dlg.ShowDialog(this) != DialogResult.OK) return;
                        path = dlg.SelectedPath;
                    }
                    break;
                case CsvMode.BothZip:
                    using (var dlg = new SaveFileDialog())
                    {
                        dlg.Filter = "ZIP (*.zip)|*.zip";
                        dlg.FileName = ExportFileName("meta-receipts-export", ".zip");
                        if (dlg.ShowDialog(this) != DialogResult.OK) return;
                        path = dlg.FileName;
                        asZip = true;
                    }
                    break;
            }

            var receiptsSnap = new List<Receipt>(_receipts);
            var itemsSnap = new List<LineItem>(_items);
            var chosenMode = mode;
            var chosenPath = path;
            var chosenZip = asZip;

            await RunExportAsync("Exporting CSV...", token =>
            {
                token.ThrowIfCancellationRequested();
                switch (chosenMode)
                {
                    case CsvMode.LineItems:
                        CsvExporter.ExportLineItems(chosenPath, itemsSnap);
                        break;
                    case CsvMode.Receipts:
                        CsvExporter.ExportReceipts(chosenPath, receiptsSnap);
                        break;
                    case CsvMode.BothFolder:
                        CsvExporter.ExportBoth(chosenPath, receiptsSnap, itemsSnap, false);
                        break;
                    case CsvMode.BothZip:
                        CsvExporter.ExportBoth(chosenPath, receiptsSnap, itemsSnap, chosenZip);
                        break;
                }
            }, "CSV export completed.").ConfigureAwait(true);
        }

        private async System.Threading.Tasks.Task RunExportAsync(string statusText, Action<CancellationToken> work, string successMessage)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            SetBusy(true);
            _statusLabel.Text = statusText;
            try
            {
                await Task.Run(() => work(token), token).ConfigureAwait(true);
                if (!token.IsCancellationRequested)
                {
                    UpdateStatus();
                    MessageBox.Show(this, successMessage, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (OperationCanceledException)
            {
                _statusLabel.Text = "Export cancelled.";
            }
            catch (IOException)
            {
                MessageBox.Show(this,
                    "Cannot write the file. It may be open in Excel. Close it and try again.",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Export failed: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
                UpdateStatus();
                if (_cts != null) { _cts.Dispose(); _cts = null; }
            }
        }

        private async Task CheckForUpdatesAsync(bool manual)
        {
            if (manual)
            {
                _statusLabel.Text = "Checking for updates on GitHub...";
                _btnUpdate.Enabled = false;
            }

            try
            {
                var result = await _updateService.CheckForUpdatesAsync(_updateConfig, _currentVersion);
                if (result.IsSuccess)
                {
                    if (result.HasUpdate)
                    {
                        _btnUpdate.Text = "Update to v" + result.LatestVersion + "!";
                        _btnUpdate.BackColor = ColorExcel;
                        _statusLabel.Text = "Update v" + result.LatestVersion + " is available!";

                        using (var dlg = new UpdateDialog(result, _updateConfig, _updateService))
                        {
                            dlg.ShowDialog(this);
                        }
                    }
                    else if (manual)
                    {
                        _statusLabel.Text = "Meta Receipts Lite is up to date (v" + _currentVersion + ").";
                        MessageBox.Show(this,
                            $"You are running the latest version of Meta Receipts Lite (v{_currentVersion}).",
                            "Up to Date", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else if (manual)
                {
                    _statusLabel.Text = "Update check failed.";
                    var msg = result.ErrorMessage;
                    if ((result.HttpStatusCode == 401 || result.HttpStatusCode == 404) && !_updateConfig.IsPublicDefaultRepository)
                    {
                        var ask = MessageBox.Show(this,
                            $"{msg}\n\nWould you like to review the repository and optional token settings?",
                            "Update Settings", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (ask == DialogResult.Yes)
                        {
                            using (var dlg = new UpdateSettingsDialog(_updateConfig, _updateService))
                            {
                                dlg.ShowDialog(this);
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show(this, $"Failed to check for updates:\n{msg}", "Update Check Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                if (manual)
                {
                    _statusLabel.Text = "Update check error.";
                    MessageBox.Show(this, $"Unexpected error checking for updates:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                if (manual && !_busy)
                {
                    _btnUpdate.Enabled = true;
                }
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _btnAdd.Enabled = !busy;
            _dropBrowse.Enabled = !busy;
            _btnExportExcel.Enabled = !busy;
            _btnExportCsv.Enabled = !busy;
            _btnClear.Enabled = !busy;
            _btnRemove.Enabled = !busy;
            _btnUpdate.Enabled = !busy;
            _btnCancel.Enabled = busy;
            UseWaitCursor = busy;
        }

        private static string ExportFileName(string prefix, string extension)
        {
            if (string.IsNullOrEmpty(extension)) extension = "";
            if (!extension.StartsWith(".", StringComparison.Ordinal))
                extension = "." + extension;
            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture);
            return prefix + "_" + stamp + extension;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_cts != null) { _cts.Dispose(); _cts = null; }
                if (_csvMenu != null) { _csvMenu.Dispose(); _csvMenu = null; }
            }
            base.Dispose(disposing);
        }
    }
}
