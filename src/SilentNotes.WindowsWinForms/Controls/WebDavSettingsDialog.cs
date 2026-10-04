using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using SilentNotes.WindowsWinForms.Services;
using Sunny.UI;
using VanillaCloudStorageClient;
using VanillaCloudStorageClient.CloudStorageProviders;

namespace SilentNotes.WindowsWinForms.Controls
{
    /// <summary>
    /// WinForms port of WebDavSettingsDialog: WebDAV server/username/password,
    /// transfer code, sync mode and data directory, with a live connection test.
    /// </summary>
    public class WebDavSettingsDialog : ThemedDialogForm
    {
        private static readonly Font UIAppFont = new Font("Microsoft YaHei UI", 9F);

        private readonly UITextBox _serverUrlBox;
        private readonly UITextBox _usernameBox;
        private readonly UITextBox _passwordBox;
        private readonly UITextBox _transferCodeBox;
        private readonly UIComboBox _syncModeBox;
        private readonly UITextBox _dataDirectoryBox;
        private readonly UILabel _statusText;
        private readonly UIButton _testConnectionButton;
        private readonly UIButton _saveButton;

        private string _currentDataDirectory;

        public string ServerUrl { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }
        public string TransferCode { get; private set; }
        public string SyncMode { get; private set; }

        /// <summary>Gets the custom data directory path. Null/empty means default.</summary>
        public string DataDirectory { get; private set; }

        public WebDavSettingsDialog()
        {
            Text = "同步设置";
            Width = 480;
            Height = 410;

            // Absolute layout on a fill panel: deterministic, no TLP row growth quirks.
            Panel content = new Panel { Dock = DockStyle.Fill, Tag = "window" };

            _serverUrlBox = new UITextBox { Left = 112, Top = 16, Width = 348, Font = UIAppFont, Watermark = "https://…" };
            _usernameBox = new UITextBox { Left = 112, Top = 54, Width = 348, Font = UIAppFont };
            _passwordBox = new UITextBox { Left = 112, Top = 92, Width = 348, Font = UIAppFont, PasswordChar = '●' };
            _transferCodeBox = new UITextBox { Left = 112, Top = 130, Width = 348, Font = UIAppFont };
            _syncModeBox = new UIComboBox { Left = 112, Top = 168, Width = 200, Font = UIAppFont };
            _syncModeBox.Items.Add("每次手动同步");            // Never
            _syncModeBox.Items.Add("有网络时自动同步");        // CostFreeInternetOnly
            _syncModeBox.Items.Add("始终自动同步");            // Always
            _dataDirectoryBox = new UITextBox { Left = 112, Top = 206, Width = 178, Font = UIAppFont, ReadOnly = true };
            UIButton browseButton = new UIButton { Text = "浏览…", Left = 298, Top = 206, Width = 76, Height = 28, Tag = "window", Font = UIAppFont };
            browseButton.Click += BrowseDataDirButton_Click;
            UIButton resetDirButton = new UIButton { Text = "重置", Left = 382, Top = 206, Width = 76, Height = 28, Tag = "window", Font = UIAppFont };
            resetDirButton.Click += delegate { _currentDataDirectory = null; _dataDirectoryBox.Text = "（默认位置）"; };

            _testConnectionButton = new UIButton { Text = "测试连接", Left = 112, Top = 254, Width = 96, Height = 30, Tag = "accent", Font = UIAppFont };
            _testConnectionButton.Click += TestConnectionButton_Click;
            _testConnectionButton.Enabled = false;

            _statusText = new UILabel
            {
                AutoSize = false,
                Left = 20,
                Top = 296,
                Width = 440,
                Height = 40,
                Tag = "secondary",
                Font = UIAppFont,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            _saveButton = new UIButton { Text = "保存", Left = 266, Top = 254, Width = 90, Height = 30, Tag = "accent", Font = UIAppFont };
            _saveButton.Click += SaveButton_Click;
            UIButton cancelButton = new UIButton { Text = "取消", Left = 364, Top = 254, Width = 90, Height = 30, Tag = "window", Font = UIAppFont };
            cancelButton.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

            content.Controls.Add(_serverUrlBox);
            content.Controls.Add(_usernameBox);
            content.Controls.Add(_passwordBox);
            content.Controls.Add(_transferCodeBox);
            content.Controls.Add(_syncModeBox);
            content.Controls.Add(_dataDirectoryBox);
            content.Controls.Add(browseButton);
            content.Controls.Add(resetDirButton);
            content.Controls.Add(_testConnectionButton);
            content.Controls.Add(_saveButton);
            content.Controls.Add(cancelButton);
            content.Controls.Add(_statusText);
            AddCaption(content, "服务器地址", 20);
            AddCaption(content, "用户名", 58);
            AddCaption(content, "密码", 96);
            AddCaption(content, "传输码", 134);
            AddCaption(content, "自动同步", 172);
            AddCaption(content, "数据目录", 210);

            Controls.Add(content);
            AcceptButton = _saveButton;
            CancelButton = cancelButton;

            // Subscribe on the UITextBox-typed fields: SunnyUI hides Control.TextChanged
            // with a "new" event that only raises through this type, and a Control-typed
            // reference would bind to the base event, which never fires.
            _serverUrlBox.TextChanged += delegate { UpdateTestButtonState(); };
            _usernameBox.TextChanged += delegate { UpdateTestButtonState(); };
            _passwordBox.TextChanged += delegate { UpdateTestButtonState(); };
        }

        private static void AddCaption(Control host, string caption, int top)
        {
            host.Controls.Add(new UILabel
            {
                Text = caption,
                Left = 20,
                Top = top + 5,
                AutoSize = true,
                Font = UIAppFont,
            });
        }

        private void UpdateTestButtonState()
        {
            bool hasContent = !string.IsNullOrWhiteSpace(_serverUrlBox.Text)
                && !string.IsNullOrWhiteSpace(_usernameBox.Text)
                && !string.IsNullOrEmpty(_passwordBox.Text);
            _testConnectionButton.Enabled = hasContent;
            _statusText.Text = string.Empty;
        }

        /// <summary>Sets pre-filled values for the form fields.</summary>
        public void Prefill(string url, string username, string password, string transferCode, string syncMode, string dataDirectory)
        {
            _serverUrlBox.Text = url ?? string.Empty;
            _usernameBox.Text = username ?? string.Empty;
            _passwordBox.Text = password ?? string.Empty;
            _transferCodeBox.Text = transferCode ?? string.Empty;
            _dataDirectoryBox.Text = dataDirectory ?? "（默认位置）";
            _currentDataDirectory = dataDirectory;

            int modeIndex = 1;
            if (!string.IsNullOrEmpty(syncMode))
            {
                if (string.Equals(syncMode, "Never", StringComparison.Ordinal))
                    modeIndex = 0;
                else if (string.Equals(syncMode, "Always", StringComparison.Ordinal))
                    modeIndex = 2;
            }
            _syncModeBox.SelectedIndex = modeIndex;
            UpdateTestButtonState();
        }

        private async void TestConnectionButton_Click(object sender, EventArgs e)
        {
            try
            {
                _testConnectionButton.Enabled = false;
                _saveButton.Enabled = false;
                SetStatus("正在测试连接...", false);

                string url = _serverUrlBox.Text.Trim();
                string username = _usernameBox.Text.Trim();
                string password = _passwordBox.Text;

                bool success = await Task.Run(delegate { return TestWebDavConnectionSync(url, username, password); });
                if (success)
                    SetStatus("连接成功！WebDAV 服务器可用。", false);
            }
            catch (Exception ex)
            {
                SetStatus("发生未知错误：" + ex.Message, true);
            }
            finally
            {
                _testConnectionButton.Enabled = true;
                _saveButton.Enabled = true;
            }
        }

        private bool TestWebDavConnectionSync(string url, string username, string password)
        {
            try
            {
                var client = new WebdavCloudStorageClient(false);
                var credentials = new CloudStorageCredentials
                {
                    CloudStorageId = "webdav",
                    Url = url,
                    Username = username,
                    UnprotectedPassword = password,
                };

                client.ListFileNamesAsync(credentials)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
                return true;
            }
            catch (AccessDeniedException)
            {
                SetStatus("认证失败：用户名或密码错误。", true);
                return false;
            }
            catch (ConnectionFailedException ex)
            {
                SetStatus("连接失败：无法连接到服务器。请检查服务器地址和网络连接。\n" + ex.Message, true);
                return false;
            }
            catch (CloudStorageException ex)
            {
                SetStatus("连接测试失败：" + ex.Message, true);
                return false;
            }
            catch (Exception ex)
            {
                SetStatus("发生未知错误：" + ex.Message, true);
                return false;
            }
        }

        private void SetStatus(string message, bool isError)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(delegate { SetStatus(message, isError); }));
                return;
            }
            _statusText.Text = message;
            if (Theme != null)
                _statusText.ForeColor = isError ? Theme.Danger : Theme.TextSecondary;
            else
                _statusText.ForeColor = isError ? Color.Firebrick : Color.Gray;
        }

        private void BrowseDataDirButton_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择本地数据文件保存目录";
                dialog.ShowNewFolderButton = true;
                if (!string.IsNullOrEmpty(_currentDataDirectory) && System.IO.Directory.Exists(_currentDataDirectory))
                    dialog.SelectedPath = _currentDataDirectory;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _currentDataDirectory = dialog.SelectedPath;
                    _dataDirectoryBox.Text = _currentDataDirectory;
                }
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            ServerUrl = _serverUrlBox.Text.Trim();
            Username = _usernameBox.Text.Trim();
            Password = _passwordBox.Text;
            TransferCode = _transferCodeBox.Text.Replace(" ", string.Empty);
            SyncMode = _syncModeBox.SelectedIndex == 0 ? "Never"
                : (_syncModeBox.SelectedIndex == 2 ? "Always" : "CostFreeInternetOnly");
            DataDirectory = _currentDataDirectory;
            DialogResult = DialogResult.OK;
        }
    }
}
