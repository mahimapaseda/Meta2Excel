using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MetaReceiptsLite.Updater
{
    public sealed class UpdateSettingsDialog : Form
    {
        private static readonly Color ColorNavy = Color.FromArgb(31, 78, 120);
        private static readonly Color ColorExcel = Color.FromArgb(33, 115, 70);
        private static readonly Color ColorCanvas = Color.FromArgb(245, 247, 250);
        private static readonly Color ColorMuted = Color.FromArgb(90, 105, 120);

        private readonly UpdateConfig _config;
        private readonly IUpdateService _updateService;

        private TextBox _txtToken;
        private TextBox _txtOwner;
        private TextBox _txtRepo;
        private CheckBox _chkStartup;
        private Label _lblStatus;
        private Button _btnTest;
        private Button _btnSave;
        private Button _btnCancel;
        private CheckBox _chkShowToken;

        public UpdateConfig UpdatedConfig { get; private set; }

        public UpdateSettingsDialog(UpdateConfig config, IUpdateService updateService = null)
        {
            _config = config ?? UpdateConfig.Load();
            _updateService = updateService ?? new UpdateService();

            Text = "GitHub Repository & Token Configuration";
            Width = 520;
            Height = 390;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = ColorCanvas;
            Font = SystemFonts.MessageBoxFont;

            BuildUi();
            PopulateFields();
        }

        private void BuildUi()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = ColorNavy,
                Padding = new Padding(16, 10, 16, 10)
            };

            var lblTitle = new Label
            {
                Text = "Private Update Configuration",
                ForeColor = Color.White,
                Font = new Font(Font.FontFamily, 11f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 10)
            };

            var lblSubtitle = new Label
            {
                Text = "Configure Fine-Grained GitHub Personal Access Token (PAT) for private releases.",
                ForeColor = Color.FromArgb(200, 225, 250),
                Font = new Font(Font.FontFamily, 8.5f, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(16, 30)
            };

            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSubtitle);

            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 14, 20, 10),
                BackColor = ColorCanvas
            };

            int y = 14;

            // GitHub Token
            var lblToken = new Label
            {
                Text = "GitHub Personal Access Token (PAT):",
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 40, 50),
                AutoSize = true,
                Location = new Point(18, y)
            };
            panel.Controls.Add(lblToken);
            y += 22;

            _txtToken = new TextBox
            {
                Location = new Point(20, y),
                Width = 360,
                UseSystemPasswordChar = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9.5f)
            };
            panel.Controls.Add(_txtToken);

            _chkShowToken = new CheckBox
            {
                Text = "Show",
                Location = new Point(390, y + 2),
                AutoSize = true,
                ForeColor = ColorMuted
            };
            _chkShowToken.CheckedChanged += (s, e) =>
            {
                _txtToken.UseSystemPasswordChar = !_chkShowToken.Checked;
            };
            panel.Controls.Add(_chkShowToken);
            y += 32;

            var lblTokenHelp = new Label
            {
                Text = "Requires Read-Only permission on 'Repository Contents'. Leave blank if using public repo.",
                ForeColor = ColorMuted,
                Font = new Font(Font.FontFamily, 8f, FontStyle.Italic),
                AutoSize = true,
                Location = new Point(20, y)
            };
            panel.Controls.Add(lblTokenHelp);
            y += 28;

            // Repo Owner
            var lblOwner = new Label
            {
                Text = "Repository Owner / Organization:",
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 40, 50),
                AutoSize = true,
                Location = new Point(18, y)
            };
            panel.Controls.Add(lblOwner);
            y += 22;

            _txtOwner = new TextBox
            {
                Location = new Point(20, y),
                Width = 220,
                BorderStyle = BorderStyle.FixedSingle
            };
            panel.Controls.Add(_txtOwner);
            y += 32;

            // Repo Name
            var lblRepo = new Label
            {
                Text = "Repository Name:",
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 40, 50),
                AutoSize = true,
                Location = new Point(18, y)
            };
            panel.Controls.Add(lblRepo);
            y += 22;

            _txtRepo = new TextBox
            {
                Location = new Point(20, y),
                Width = 220,
                BorderStyle = BorderStyle.FixedSingle
            };
            panel.Controls.Add(_txtRepo);
            y += 30;

            // Startup Check Checkbox
            _chkStartup = new CheckBox
            {
                Text = "Check for updates automatically on app startup",
                Location = new Point(20, y),
                AutoSize = true,
                Checked = true
            };
            panel.Controls.Add(_chkStartup);
            y += 28;

            // Status message
            _lblStatus = new Label
            {
                Text = "",
                AutoSize = true,
                Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold),
                Location = new Point(20, y)
            };
            panel.Controls.Add(_lblStatus);

            // Bottom Buttons
            var bottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = Color.FromArgb(236, 240, 244),
                Padding = new Padding(16, 8, 16, 8)
            };

            _btnTest = new Button
            {
                Text = "Test Connection",
                Width = 120,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = ColorNavy,
                Cursor = Cursors.Hand,
                Location = new Point(16, 10),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            };
            _btnTest.FlatAppearance.BorderColor = Color.FromArgb(190, 205, 220);
            _btnTest.Click += async (s, e) => await TestConnectionAsync();

            _btnSave = new Button
            {
                Text = "Save",
                Width = 90,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorExcel,
                ForeColor = Color.White,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Location = new Point(395, 10),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += (s, e) => SaveAndClose();

            _btnCancel = new Button
            {
                Text = "Cancel",
                Width = 80,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(60, 70, 80),
                Cursor = Cursors.Hand,
                Location = new Point(305, 10),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };
            _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 210, 220);
            _btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            bottom.Controls.Add(_btnTest);
            bottom.Controls.Add(_btnCancel);
            bottom.Controls.Add(_btnSave);

            Controls.Add(panel);
            Controls.Add(bottom);
            Controls.Add(header);
        }

        private void PopulateFields()
        {
            _txtToken.Text = _config.GitHubToken ?? string.Empty;
            _txtOwner.Text = string.IsNullOrWhiteSpace(_config.RepoOwner) ? UpdateConfig.DefaultOwner : _config.RepoOwner;
            _txtRepo.Text = string.IsNullOrWhiteSpace(_config.RepoName) ? UpdateConfig.DefaultRepo : _config.RepoName;
            _chkStartup.Checked = _config.CheckOnStartup;
        }

        private async Task TestConnectionAsync()
        {
            _btnTest.Enabled = false;
            _lblStatus.ForeColor = ColorNavy;
            _lblStatus.Text = "Testing connection to GitHub REST API...";

            var testConfig = new UpdateConfig
            {
                GitHubToken = _txtToken.Text.Trim(),
                RepoOwner = _txtOwner.Text.Trim(),
                RepoName = _txtRepo.Text.Trim()
            };

            var result = await _updateService.CheckForUpdatesAsync(testConfig, new SemVersion(0, 0, 0));

            _btnTest.Enabled = true;
            if (result.IsSuccess)
            {
                _lblStatus.ForeColor = ColorExcel;
                _lblStatus.Text = $"✓ Connected! Latest release is {result.Release?.TagName} ({result.Release?.Assets?.Count ?? 0} assets found).";
            }
            else
            {
                _lblStatus.ForeColor = Color.FromArgb(170, 30, 30);
                _lblStatus.Text = $"✕ Connection Failed: {result.ErrorMessage}";
            }
        }

        private void SaveAndClose()
        {
            UpdatedConfig = new UpdateConfig
            {
                GitHubToken = _txtToken.Text.Trim(),
                RepoOwner = string.IsNullOrWhiteSpace(_txtOwner.Text) ? UpdateConfig.DefaultOwner : _txtOwner.Text.Trim(),
                RepoName = string.IsNullOrWhiteSpace(_txtRepo.Text) ? UpdateConfig.DefaultRepo : _txtRepo.Text.Trim(),
                CheckOnStartup = _chkStartup.Checked
            };

            try
            {
                UpdatedConfig.Save();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to save configuration: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
