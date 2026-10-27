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

        private readonly List<Receipt> _receipts = new List<Receipt>();
        private readonly List<LineItem> _items = new List<LineItem>();
        private readonly HashSet<string> _transactionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private ToolStrip _toolbar;
        private ToolStripButton _btnAdd;
        private ToolStripButton _btnExportExcel;
        private ToolStripDropDownButton _btnExportCsv;
        private ToolStripButton _btnClear;
        private ToolStripButton _btnCancel;
        private ToolStripButton _btnRemove;
        private ToolStripLabel _hint;
        private SplitContainer _split;
        private DataGridView _gridReceipts;
        private DataGridView _gridItems;
        private TextBox _filterCampaign;
        private Label _lblFilter;
        private StatusStrip _status;
        private ToolStripStatusLabel _statusLabel;

        private CancellationTokenSource _cts;
        private bool _busy;

        public MainForm()
        {
            Text = "Meta Receipts -> Excel";
            Width = 1000;
            Height = 640;
            MinimumSize = new Size(800, 480);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            AllowDrop = true;

            BuildUi();
            WireEvents();
            UpdateStatus();
            SetBusy(false);
        }

        private void BuildUi()
        {
            _toolbar = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                RenderMode = ToolStripRenderMode.System
            };
            _btnAdd = new ToolStripButton("Add PDFs...");
            _btnExportExcel = new ToolStripButton("Export Excel...");
            _btnExportCsv = new ToolStripDropDownButton("Export CSV");
            _btnExportCsv.DropDownItems.Add("Line items...", null, (s, e) => ExportCsv(CsvMode.LineItems));
            _btnExportCsv.DropDownItems.Add("Receipts...", null, (s, e) => ExportCsv(CsvMode.Receipts));
            _btnExportCsv.DropDownItems.Add("Both (folder)...", null, (s, e) => ExportCsv(CsvMode.BothFolder));
            _btnExportCsv.DropDownItems.Add("Both (zip)...", null, (s, e) => ExportCsv(CsvMode.BothZip));
            _btnClear = new ToolStripButton("Clear");
            _btnCancel = new ToolStripButton("Cancel");
            _btnRemove = new ToolStripButton("Remove selected receipt");
            _hint = new ToolStripLabel("  Drag & drop Meta receipt PDFs anywhere in this window");
            _hint.ForeColor = SystemColors.GrayText;

            _toolbar.Items.AddRange(new ToolStripItem[]
            {
                _btnAdd, _btnExportExcel, _btnExportCsv, _btnClear, _btnCancel,
                new ToolStripSeparator(), _btnRemove, _hint
            });

            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 260
            };

            _gridReceipts = CreateGrid();
            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "InvoiceNo", HeaderText = "Invoice No", Width = 140 });
            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date", Width = 120 });
            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Reference", HeaderText = "Reference", Width = 100 });
            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Currency", HeaderText = "Currency", Width = 70 });
            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Billed", HeaderText = "Billed", Width = 90 });
            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rows", HeaderText = "Rows", Width = 50 });
            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "RowsTotal", HeaderText = "Rows total", Width = 90 });
            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Check", HeaderText = "Check", Width = 120 });
            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "File", HeaderText = "File", Width = 220, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            var bottomPanel = new Panel { Dock = DockStyle.Fill };
            var filterPanel = new Panel { Dock = DockStyle.Top, Height = 28 };
            _lblFilter = new Label
            {
                Text = "Campaign filter:",
                AutoSize = true,
                Left = 4,
                Top = 6
            };
            _filterCampaign = new TextBox
            {
                Left = 110,
                Top = 3,
                Width = 280
            };
            filterPanel.Controls.Add(_lblFilter);
            filterPanel.Controls.Add(_filterCampaign);

            _gridItems = CreateGrid();
            _gridItems.Dock = DockStyle.Fill;
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "InvoiceNo", HeaderText = "Invoice No", Width = 130 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Campaign", HeaderText = "Campaign", Width = 100 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "PeriodFrom", HeaderText = "Period from", Width = 120 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "PeriodTo", HeaderText = "Period to", Width = 120 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "AdSet", HeaderText = "Ad set", Width = 160 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Currency", HeaderText = "Currency", Width = 70 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Impressions", HeaderText = "Impressions", Width = 90 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cost", HeaderText = "Cost", Width = 80 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "CPM", HeaderText = "CPM", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            bottomPanel.Controls.Add(_gridItems);
            bottomPanel.Controls.Add(filterPanel);

            _split.Panel1.Controls.Add(_gridReceipts);
            _split.Panel2.Controls.Add(bottomPanel);

            _status = new StatusStrip { RenderMode = ToolStripRenderMode.System };
            _statusLabel = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _status.Items.Add(_statusLabel);

            Controls.Add(_split);
            Controls.Add(_toolbar);
            Controls.Add(_status);
            _toolbar.Dock = DockStyle.Top;
            _status.Dock = DockStyle.Bottom;
        }

        private static DataGridView CreateGrid()
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
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.FixedSingle
            };
            return g;
        }

        private void WireEvents()
        {
            _btnAdd.Click += (s, e) => AddPdfsDialog();
            _btnExportExcel.Click += (s, e) => ExportExcel();
            _btnClear.Click += (s, e) => ClearAll();
            _btnCancel.Click += (s, e) =>
            {
                if (_cts != null) _cts.Cancel();
            };
            _btnRemove.Click += (s, e) => RemoveSelectedReceipts();
            _filterCampaign.TextChanged += (s, e) => RefreshItemGrid();
            DragEnter += MainForm_DragEnter;
            DragDrop += MainForm_DragDrop;
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
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
                                if (!string.IsNullOrEmpty(receipt.TransactionId) &&
                                    _transactionIds.Contains(receipt.TransactionId))
                                {
                                    errors.Add(name + " — duplicate (Transaction ID " + receipt.TransactionId + ")");
                                    continue;
                                }

                                if (!string.IsNullOrEmpty(receipt.TransactionId))
                                    _transactionIds.Add(receipt.TransactionId);
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
                if (!string.IsNullOrEmpty(r.TransactionId))
                    _transactionIds.Remove(r.TransactionId);
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
            _transactionIds.Clear();
            RefreshAllGrids();
            UpdateStatus();
        }

        private void ExportExcel()
        {
            if (_receipts.Count == 0)
            {
                MessageBox.Show(this, "No receipts to export.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "Excel workbook (*.xlsx)|*.xlsx";
                dlg.FileName = "MetaReceipts.xlsx";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    ExcelExporter.Export(dlg.FileName, _receipts, _items);
                    MessageBox.Show(this, "Excel exported successfully.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            }
        }

        private enum CsvMode { LineItems, Receipts, BothFolder, BothZip }

        private void ExportCsv(CsvMode mode)
        {
            if (_receipts.Count == 0)
            {
                MessageBox.Show(this, "No receipts to export.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                switch (mode)
                {
                    case CsvMode.LineItems:
                        using (var dlg = new SaveFileDialog())
                        {
                            dlg.Filter = "CSV (*.csv)|*.csv";
                            dlg.FileName = "line-items.csv";
                            if (dlg.ShowDialog(this) != DialogResult.OK) return;
                            CsvExporter.ExportLineItems(dlg.FileName, _items);
                        }
                        break;
                    case CsvMode.Receipts:
                        using (var dlg = new SaveFileDialog())
                        {
                            dlg.Filter = "CSV (*.csv)|*.csv";
                            dlg.FileName = "receipts.csv";
                            if (dlg.ShowDialog(this) != DialogResult.OK) return;
                            CsvExporter.ExportReceipts(dlg.FileName, _receipts);
                        }
                        break;
                    case CsvMode.BothFolder:
                        using (var dlg = new FolderBrowserDialog())
                        {
                            dlg.Description = "Choose folder for CSV files";
                            if (dlg.ShowDialog(this) != DialogResult.OK) return;
                            CsvExporter.ExportBoth(dlg.SelectedPath, _receipts, _items, false);
                        }
                        break;
                    case CsvMode.BothZip:
                        using (var dlg = new SaveFileDialog())
                        {
                            dlg.Filter = "ZIP (*.zip)|*.zip";
                            dlg.FileName = "meta-receipts-export.zip";
                            if (dlg.ShowDialog(this) != DialogResult.OK) return;
                            CsvExporter.ExportBoth(dlg.FileName, _receipts, _items, true);
                        }
                        break;
                }
                MessageBox.Show(this, "CSV export completed.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _btnAdd.Enabled = !busy;
            _btnExportExcel.Enabled = !busy;
            _btnExportCsv.Enabled = !busy;
            _btnClear.Enabled = !busy;
            _btnRemove.Enabled = !busy;
            _btnCancel.Enabled = busy;
            UseWaitCursor = busy;
        }
    }
}
