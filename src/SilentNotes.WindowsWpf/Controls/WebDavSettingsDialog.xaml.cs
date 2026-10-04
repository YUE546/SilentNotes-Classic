using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VanillaCloudStorageClient;
using VanillaCloudStorageClient.CloudStorageProviders;

namespace SilentNotes.WindowsWpf.Controls
{
    public partial class WebDavSettingsDialog : ThemedDialogWindow
    {
        private string _currentDataDirectory;
        private bool _passwordVisible;

        public WebDavSettingsDialog()
        {
            InitializeComponent();
        }

        public string ServerUrl { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }
        public string TransferCode { get; private set; }
        public string SyncMode { get; private set; }

        /// <summary>Gets or sets the custom data directory path. Null/empty means default.</summary>
        public string DataDirectory { get; private set; }

        /// <summary>Sets pre-filled values for the form fields.</summary>
        public void Prefill(string url, string username, string password = null, string transferCode = null, string syncMode = null, string dataDirectory = null)
        {
            ServerUrlTextBox.Text = url ?? string.Empty;
            UsernameTextBox.Text = username ?? string.Empty;
            if (password != null)
            {
                PasswordBox.Password = password;
                PasswordVisibleBox.Text = password;
            }
            TransferCodeBox.Text = transferCode ?? string.Empty;
            DataDirectoryBox.Text = dataDirectory ?? "（默认位置）";
            _currentDataDirectory = dataDirectory;

            if (!string.IsNullOrEmpty(syncMode))
            {
                for (int i = 0; i < SyncModeBox.Items.Count; i++)
                {
                    if ((SyncModeBox.Items[i] as ComboBoxItem)?.Tag?.ToString() == syncMode)
                    {
                        SyncModeBox.SelectedIndex = i;
                        break;
                    }
                }
            }
            else
            {
                SyncModeBox.SelectedIndex = 1;
            }
        }

        private void Fields_TextChanged(object sender, RoutedEventArgs e)
        {
            if (sender == PasswordBox)
                PasswordVisibleBox.Text = PasswordBox.Password;
            else if (sender == PasswordVisibleBox)
                PasswordBox.Password = PasswordVisibleBox.Text;

            bool hasContent = !string.IsNullOrWhiteSpace(ServerUrlTextBox.Text)
                && !string.IsNullOrWhiteSpace(UsernameTextBox.Text)
                && PasswordBox.SecurePassword.Length > 0;
            TestConnectionButton.IsEnabled = hasContent;
            StatusText.Text = string.Empty;
        }

        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TestConnectionButton.IsEnabled = false;
                SaveButton.IsEnabled = false;
                SetStatus("正在测试连接...", isError: false);

                string url = ServerUrlTextBox.Text.Trim();
                string username = UsernameTextBox.Text.Trim();
                string password = PasswordBox.Password;

                bool success = await Task.Run(() => TestWebDavConnectionSync(url, username, password));

                if (success)
                {
                    SetStatus("连接成功！WebDAV 服务器可用。", isError: false);
                }
            }
            catch (Exception ex)
            {
                SetStatus("发生未知错误：" + ex.Message, isError: true);
            }
            finally
            {
                TestConnectionButton.IsEnabled = true;
                SaveButton.IsEnabled = true;
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
                SetStatus("认证失败：用户名或密码错误。", isError: true);
                return false;
            }
            catch (ConnectionFailedException ex)
            {
                SetStatus("连接失败：无法连接到服务器。请检查服务器地址和网络连接。\n" + ex.Message, isError: true);
                return false;
            }
            catch (CloudStorageException ex)
            {
                SetStatus("连接测试失败：" + ex.Message, isError: true);
                return false;
            }
            catch (Exception ex)
            {
                SetStatus("发生未知错误：" + ex.Message, isError: true);
                return false;
            }
        }

        private void SetStatus(string message, bool isError)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetStatus(message, isError));
                return;
            }
            StatusText.Text = message;
            StatusText.Foreground = isError
                ? (Brush)FindResource("SilentNotesDangerBrush")
                : (Brush)FindResource("SilentNotesSecondaryTextBrush");
        }

        private void BrowseDataDirButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择本地数据文件保存目录";
                dialog.ShowNewFolderButton = true;
                if (!string.IsNullOrEmpty(_currentDataDirectory) && System.IO.Directory.Exists(_currentDataDirectory))
                    dialog.SelectedPath = _currentDataDirectory;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _currentDataDirectory = dialog.SelectedPath;
                    DataDirectoryBox.Text = _currentDataDirectory;
                }
            }
        }

        private void ResetDataDirButton_Click(object sender, RoutedEventArgs e)
        {
            _currentDataDirectory = null;
            DataDirectoryBox.Text = "（默认位置）";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ServerUrl = ServerUrlTextBox.Text.Trim();
            Username = UsernameTextBox.Text.Trim();
            Password = PasswordBox.Password;
            TransferCode = TransferCodeBox.Text.Replace(" ", string.Empty);
            SyncMode = (SyncModeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "CostFreeInternetOnly";
            DataDirectory = _currentDataDirectory;
            DialogResult = true;
        }

        private void TogglePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            _passwordVisible = !_passwordVisible;
            PasswordBox.Visibility = _passwordVisible ? Visibility.Collapsed : Visibility.Visible;
            PasswordVisibleBox.Visibility = _passwordVisible ? Visibility.Visible : Visibility.Collapsed;
            TogglePasswordButton.Content = _passwordVisible ? "🙈" : "👁";

            if (_passwordVisible)
            {
                PasswordVisibleBox.Text = PasswordBox.Password;
                PasswordVisibleBox.Focus();
                PasswordVisibleBox.SelectionStart = PasswordVisibleBox.Text.Length;
            }
            else
            {
                PasswordBox.Password = PasswordVisibleBox.Text;
                PasswordBox.Focus();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
