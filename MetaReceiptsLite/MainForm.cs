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

namespace MetaReceiptsLite
{
    public sealed class MainForm : Form
    {
        private const int PreviewRowCap = 5000;

        // Brand palette (matches Excel export header; cheap solid fills for old GPUs)
        private static readonly Color ColorNavy = Color.FromArgb(31, 78, 120);      // #1F4E78
        private static readonly Color ColorExcel = Color.FromArgb(33, 115, 70);     // #217346
        private static readonly Color ColorCanvas = Color.FromArgb(245, 247, 250);  // #F5F7FA
        private static readonly Color ColorPanel = Color.White;
        private static readonly Color ColorBorder = Color.FromArgb(210, 218, 226);
        private static readonly Color ColorMuted = Color.FromArgb(90, 105, 120);

        private readonly List<Receipt> _receipts = new List<Receipt>();
        private readonly List<LineItem> _items = new List<LineItem>();
        private readonly ReceiptDeduper _deduper = new ReceiptDeduper();

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
            Text = "Meta Receipts -> Excel";
            Width = 1040;
            Height = 680;
            MinimumSize = new Size(860, 520);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            BackColor = ColorCanvas;
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
            UpdateStatus();
            SetBusy(false);
            UpdateEmptyState();
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
                Height = 86,
                BackColor = ColorNavy,
                Padding = new Padding(16, 10, 16, 10)
            };

            _brandTitle = new Label
            {
                AutoSize = true,
                Text = "Meta Receipts -> Excel",
                ForeColor = Color.White,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 14f, FontStyle.Bold),
                Location = new Point(16, 12),
                BackColor = Color.Transparent
            };
            _brandSub = new Label
            {
                AutoSize = true,
                Text = "Convert Meta Ads receipt PDFs into Excel or CSV",
                ForeColor = Color.FromArgb(190, 210, 230),
                Location = new Point(18, 40),
                BackColor = Color.Transparent
            };

            _actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(8, 2, 8, 2),
                BackColor = Color.FromArgb(24, 62, 96)
            };

            _btnAdd = MakeButton("Add PDFs...", ColorExcel, Color.White, true);
            _btnExportExcel = MakeButton("Export Excel...", Color.FromArgb(46, 100, 150), Color.White, true);
            _btnExportCsv = MakeButton("Export CSV ▾", Color.FromArgb(46, 100, 150), Color.White, true);
            _btnClear = MakeButton("Clear", Color.FromArgb(70, 90, 110), Color.White, false);
            _btnCancel = MakeButton("Cancel", Color.FromArgb(150, 70, 70), Color.White, false);
            _btnRemove = MakeButton("Remove selected", Color.FromArgb(70, 90, 110), Color.White, false);

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
                SplitterDistance = 280,
                SplitterWidth = 6,
                BackColor = ColorCanvas,
                Panel1MinSize = 120,
                Panel2MinSize = 120
            };

            // Receipts section
            var top = new Panel { Dock = DockStyle.Fill, BackColor = ColorCanvas, Padding = new Padding(0, 0, 0, 4) };
            _lblReceipts = SectionLabel("Receipts");
            var receiptsHost = CardHost();
            _gridReceipts = CreateGrid();
            _gridReceipts.Columns.Add(Col("InvoiceNo", "Invoice No", 140));
            _gridReceipts.Columns.Add(Col("Date", "Date", 120));
            _gridReceipts.Columns.Add(Col("Reference", "Reference", 100));
            _gridReceipts.Columns.Add(Col("Currency", "Currency", 70));
            _gridReceipts.Columns.Add(Col("Billed", "Billed", 90));
            _gridReceipts.Columns.Add(Col("Rows", "Rows", 50));
            _gridReceipts.Columns.Add(Col("RowsTotal", "Rows total", 90));
            _gridReceipts.Columns.Add(Col("Check", "Check", 120));
            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "File",
                HeaderText = "File",
                Width = 220,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            receiptsHost.Controls.Add(_gridReceipts);
            top.Controls.Add(receiptsHost);
            top.Controls.Add(_lblReceipts);

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
                Text = "Campaign filter",
                AutoSize = true,
                ForeColor = ColorMuted,
                Location = new Point(120, 8)
            };
            _filterCampaign = new TextBox
            {
                Width = 260,
                Location = new Point(230, 5),
                BorderStyle = BorderStyle.FixedSingle
            };
            filterBar.Controls.Add(_lblItems);
            filterBar.Controls.Add(_lblFilter);
            filterBar.Controls.Add(_filterCampaign);

            var itemsHost = CardHost();
            _gridItems = CreateGrid();
            _gridItems.Columns.Add(Col("InvoiceNo", "Invoice No", 130));
            _gridItems.Columns.Add(Col("Campaign", "Campaign", 110));
            _gridItems.Columns.Add(Col("PeriodFrom", "Period from", 120));
            _gridItems.Columns.Add(Col("PeriodTo", "Period to", 120));
            _gridItems.Columns.Add(Col("AdSet", "Ad set", 160));
            _gridItems.Columns.Add(Col("Currency", "Currency", 70));
            _gridItems.Columns.Add(Col("Impressions", "Impressions", 90));
            _gridItems.Columns.Add(Col("Cost", "Cost", 80));
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CPM",
                HeaderText = "CPM",
                Width = 80,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            itemsHost.Controls.Add(_gridItems);
            bottom.Controls.Add(itemsHost);
            bottom.Controls.Add(filterBar);

            _split.Panel1.Controls.Add(top);
            _split.Panel2.Controls.Add(bottom);

            // Empty drop zone (shown when no data)
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
                Text = "Drop Meta receipt PDFs here",
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 13f, FontStyle.Bold),
                ForeColor = ColorNavy,
                BackColor = Color.Transparent
            };
            _dropHint = new Label
            {
                AutoSize = true,
                Text = "or click Browse to select files / folders — processing stays off the UI thread",
                ForeColor = ColorMuted,
                BackColor = Color.Transparent
            };
            _dropBrowse = MakeButton("Browse PDFs...", ColorExcel, Color.White, true);
            _dropBrowse.Width = 140;

            _dropZone.Controls.Add(_dropTitle);
            _dropZone.Controls.Add(_dropHint);
            _dropZone.Controls.Add(_dropBrowse);
            _dropZone.Resize += (s, e) => LayoutDropZone();

            _content.Controls.Add(_dropZone);
            _content.Controls.Add(_split);
            LayoutDropZone();
        }

        private void BuildStatus()
        {
            _status = new StatusStrip
            {
                BackColor = Color.FromArgb(236, 240, 244),
                SizingGrip = true,
                RenderMode = ToolStripRenderMode.System
            };
            _statusLabel = new ToolStripStatusLabel("Ready")
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ColorMuted
            };
            _status.Items.Add(_statusLabel);
        }

        private static Label SectionLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                ForeColor = ColorNavy,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 0, 0)
            };
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
                MinimumSize = new Size(primary ? 110 : 90, 28),
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = fore,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 2, 8, 2),
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.12f);
            b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(back, 0.08f);
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
                    BackColor = ColorNavy,
                    ForeColor = Color.White,
                    Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    SelectionBackColor = ColorNavy,
                    SelectionForeColor = Color.White,
                    Padding = new Padding(4, 2, 4, 2)
                },
                ColumnHeadersHeight = 28,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = ColorPanel,
                    ForeColor = Color.FromArgb(30, 40, 50),
                    SelectionBackColor = Color.FromArgb(210, 228, 242),
                    SelectionForeColor = Color.FromArgb(20, 40, 60),
                    Padding = new Padding(4, 1, 4, 1)
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(248, 250, 252),
                    ForeColor = Color.FromArgb(30, 40, 50),
                    SelectionBackColor = Color.FromArgb(210, 228, 242),
                    SelectionForeColor = Color.FromArgb(20, 40, 60)
                },
                RowTemplate = { Height = 24 }
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
            using (var pen = new Pen(_dragHighlight ? ColorExcel : ColorBorder, _dragHighlight ? 2.5f : 1.5f))
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
            _filterCampaign.TextChanged += (s, e) => RefreshItemGrid();

            DragEnter += MainForm_DragEnter;
            DragDrop += MainForm_DragDrop;
            DragLeave += (s, e) => SetDragHighlight(false);
            _dropZone.AllowDrop = true;
            _dropZone.DragEnter += MainForm_DragEnter;
            _dropZone.DragDrop += MainForm_DragDrop;
            _dropZone.DragLeave += (s, e) => SetDragHighlight(false);
            _content.AllowDrop = true;
            _content.DragEnter += MainForm_DragEnter;
            _content.DragDrop += MainForm_DragDrop;
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
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                SetDragHighlight(true);
            }
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            SetDragHighlight(false);
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0) return;
            var pdfs = ExpandPaths(paths);
            if (pdfs.Count > 0)
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
                RefreshAllGrids();
                UpdateStatus();
                UpdateEmptyState();
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
                ResumeLayout();
            }
            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            bool empty = _receipts.Count == 0;
            _dropZone.Visible = empty;
            _split.Visible = !empty;
            if (empty)
            {
                _dropZone.BringToFront();
                LayoutDropZone();
            }
        }

        private void RefreshReceiptGrid()
        {
            _gridReceipts.Rows.Clear();
            int n = Math.Min(_receipts.Count, PreviewRowCap);
            for (int i = 0; i < n; i++)
            {
                var r = _receipts[i];
                _gridReceipts.Rows.Add(
                    r.InvoiceNo,
                    r.InvoiceDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    r.ReferenceNumber,
                    r.CurrencyCode,
                    CurrencyDetector.FormatMoney(r.AmountBilled, r.CurrencySymbol),
                    r.ItemCount.ToString(CultureInfo.InvariantCulture),
                    CurrencyDetector.FormatMoney(r.ItemsTotal, r.CurrencySymbol),
                    r.Check,
                    r.SourceFile);
                _gridReceipts.Rows[i].Tag = r;
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
                    (it.CampaignCode != null && it.CampaignCode.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (it.CampaignName != null && it.CampaignName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (it.AdSet != null && it.AdSet.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            int count = 0;
            foreach (var it in query)
            {
                if (count >= PreviewRowCap) break;
                _gridItems.Rows.Add(
                    it.InvoiceNo,
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

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _btnAdd.Enabled = !busy;
            _dropBrowse.Enabled = !busy;
            _btnExportExcel.Enabled = !busy;
            _btnExportCsv.Enabled = !busy;
            _btnClear.Enabled = !busy;
            _btnRemove.Enabled = !busy;
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
