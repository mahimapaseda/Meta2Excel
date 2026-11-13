using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MetaReceiptsLite.Updater
{
    public sealed class UpdateDialog : Form
    {
        private static readonly Color ColorNavy = Color.FromArgb(31, 78, 120);
        private static readonly Color ColorExcel = Color.FromArgb(33, 115, 70);
        private static readonly Color ColorCanvas = Color.FromArgb(245, 247, 250);
        private static readonly Color ColorMuted = Color.FromArgb(100, 115, 130);

        private readonly UpdateCheckResult _checkResult;
        private readonly UpdateConfig _config;
        private readonly IUpdateService _updateService;

        private Label _lblHeader;
        private Label _lblVersions;
        private Label _lblAssetInfo;
        private TextBox _txtNotes;
        private ProgressBar _progressBar;
        private Label _lblProgressStatus;
        private Button _btnAction;
        private Button _btnCancel;
        private Button _btnSettings;

        private CancellationTokenSource _cts;
        private string _downloadedFilePath;
        private bool _downloadComplete;

        public UpdateDialog(UpdateCheckResult checkResult, UpdateConfig config, IUpdateService updateService = null)
        {
            _checkResult = checkResult ?? throw new ArgumentNullException(nameof(checkResult));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _updateService = updateService ?? new UpdateService();

            Text = "Software Update Available — Meta Receipts Lite";
            Width = 560;
            Height = 490;
            MinimumSize = new Size(500, 420);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = ColorCanvas;
            Font = SystemFonts.MessageBoxFont;

            BuildUi();
        }

        private void BuildUi()
        {
            // Header Banner
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = ColorNavy,
                Padding = new Padding(16, 12, 16, 12)
            };

            _lblHeader = new Label
            {
                Text = "A new version of Meta Receipts Lite is available!",
                ForeColor = Color.White,
                Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 12)
            };

            string currentStr = _checkResult.CurrentVersion?.ToString() ?? "Unknown";
            string newStr = _checkResult.LatestVersion?.ToString() ?? "Unknown";
            _lblVersions = new Label
            {
                Text = $"Current: v{currentStr}   ➔   New: v{newStr}",
                ForeColor = Color.FromArgb(200, 225, 250),
                Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(18, 38)
            };

            headerPanel.Controls.Add(_lblHeader);
            headerPanel.Controls.Add(_lblVersions);

            // Center Content
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 12, 18, 10),
                BackColor = ColorCanvas
            };

            var lblNotesTitle = new Label
            {
                Text = "Release Notes:",
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 50, 60),
                AutoSize = true,
                Location = new Point(18, 10)
            };

            _txtNotes = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 40, 50),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Location = new Point(18, 32),
                Size = new Size(508, 190),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderStyle = BorderStyle.FixedSingle,
                Text = NormalizeReleaseNotes(_checkResult.Release?.Body)
            };

            string assetName = _checkResult.TargetAsset?.Name ?? "Installer";
            string assetSize = _checkResult.TargetAsset?.FormattedSize ?? "";
            _lblAssetInfo = new Label
            {
                Text = $"Package: {assetName} ({assetSize})",
                ForeColor = ColorMuted,
                Font = new Font(Font.FontFamily, 8.5f, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(18, 230),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(18, 252),
                Size = new Size(508, 20),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Visible = false,
                Minimum = 0,
                Maximum = 100
            };

            _lblProgressStatus = new Label
            {
                Text = "Ready to download.",
                ForeColor = ColorNavy,
                Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(18, 276),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Visible = false
            };

            contentPanel.Controls.Add(lblNotesTitle);
            contentPanel.Controls.Add(_txtNotes);
            contentPanel.Controls.Add(_lblAssetInfo);
            contentPanel.Controls.Add(_progressBar);
            contentPanel.Controls.Add(_lblProgressStatus);

            // Bottom Buttons Panel
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = Color.FromArgb(236, 240, 244),
                Padding = new Padding(16, 10, 16, 10)
            };

            _btnSettings = new Button
            {
                Text = "⚙ Token Config",
                Width = 110,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(60, 70, 80),
                Cursor = Cursors.Hand,
                Location = new Point(16, 10),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            };
            _btnSettings.FlatAppearance.BorderColor = Color.FromArgb(200, 210, 220);
            _btnSettings.Click += (s, e) => ShowSettingsDialog();

            _btnAction = new Button
            {
                Text = "Update Now",
                Width = 125,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorExcel,
                ForeColor = Color.White,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Location = new Point(400, 10),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };
            _btnAction.FlatAppearance.BorderSize = 0;
            _btnAction.Click += async (s, e) => await HandleActionClickAsync();

            _btnCancel = new Button
            {
                Text = "Later",
                Width = 90,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(60, 70, 80),
                Cursor = Cursors.Hand,
                Location = new Point(300, 10),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };
            _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 210, 220);
            _btnCancel.Click += (s, e) =>
            {
                if (_cts != null) _cts.Cancel();
                DialogResult = DialogResult.Cancel;
                Close();
            };

            bottomPanel.Controls.Add(_btnSettings);
            bottomPanel.Controls.Add(_btnCancel);
            bottomPanel.Controls.Add(_btnAction);

            Controls.Add(contentPanel);
            Controls.Add(bottomPanel);
            Controls.Add(headerPanel);
        }

        private async Task HandleActionClickAsync()
        {
            if (_downloadComplete && !string.IsNullOrEmpty(_downloadedFilePath))
            {
                // Install & Restart phase
                try
                {
                    _updateService.LaunchInstaller(_downloadedFilePath, false);
                    Application.Exit();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this,
                        $"Failed to launch installer: {ex.Message}\nLocation: {_downloadedFilePath}",
                        "Launch Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            // Check if private repo requires token
            if (!_config.HasToken)
            {
                var ask = MessageBox.Show(this,
                    "No GitHub Personal Access Token is configured. If your repository is private, downloads may fail.\n\nWould you like to configure your token now?",
                    "GitHub Authentication", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ask == DialogResult.Yes)
                {
                    ShowSettingsDialog();
                }
            }

            var asset = _checkResult.TargetAsset;
            if (asset == null)
            {
                MessageBox.Show(this, "No downloadable asset was found in this release.", "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Start Download
            _btnAction.Enabled = false;
            _btnSettings.Enabled = false;
            _progressBar.Visible = true;
            _lblProgressStatus.Visible = true;
            _progressBar.Value = 0;
            _lblProgressStatus.Text = "Connecting to GitHub...";

            _cts = new CancellationTokenSource();

            var progress = new Progress<UpdateProgressReport>(report =>
            {
                _progressBar.Value = report.Percentage;
                _lblProgressStatus.Text = report.StatusMessage;
            });

            try
            {
                _downloadedFilePath = await _updateService.DownloadAssetAsync(_config, asset, null, progress, _cts.Token);
                _downloadComplete = true;

                _progressBar.Value = 100;
                _lblProgressStatus.Text = "Download complete! Ready to install.";
                _lblProgressStatus.ForeColor = ColorExcel;

                _btnAction.Text = "Install & Restart";
                _btnAction.BackColor = ColorExcel;
                _btnAction.Enabled = true;
                _btnCancel.Text = "Close";
            }
            catch (OperationCanceledException)
            {
                _lblProgressStatus.Text = "Download cancelled.";
                _lblProgressStatus.ForeColor = Color.FromArgb(160, 40, 40);
                _btnAction.Enabled = true;
                _btnSettings.Enabled = true;
            }
            catch (Exception ex)
            {
                _lblProgressStatus.Text = $"Error: {ex.Message}";
                _lblProgressStatus.ForeColor = Color.FromArgb(160, 40, 40);
                _btnAction.Enabled = true;
                _btnSettings.Enabled = true;
                MessageBox.Show(this,
                    $"Failed to download update asset:\n{ex.Message}\n\nPlease verify that your token is valid and has 'Contents: Read' permissions.",
                    "Download Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowSettingsDialog()
        {
            using (var dlg = new UpdateSettingsDialog(_config, _updateService))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    // Reload config in case it was updated
                    _config.GitHubToken = dlg.UpdatedConfig.GitHubToken;
                    _config.RepoOwner = dlg.UpdatedConfig.RepoOwner;
                    _config.RepoName = dlg.UpdatedConfig.RepoName;
                }
            }
        }

        private static string NormalizeReleaseNotes(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown)) return "No release notes provided for this version.";
            // Normalize CRLF
            var s = markdown.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
            return s.Trim();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_cts != null) { _cts.Dispose(); _cts = null; }
            }
            base.Dispose(disposing);
        }
    }
}

