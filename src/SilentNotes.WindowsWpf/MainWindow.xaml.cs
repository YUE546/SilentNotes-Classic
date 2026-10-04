using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using SilentNotes.Crypto;
using SilentNotes.Models;
using SilentNotes.Services;
using SilentNotes.WindowsWpf.Controls;
using SilentNotes.WindowsWpf.Services;
using SilentNotes.WindowsWpf.Workers;
using VanillaCloudStorageClient;

namespace SilentNotes.WindowsWpf
{
    public partial class MainWindow : Window
    {
        public static readonly RoutedUICommand NewNoteCommand = new RoutedUICommand("新建笔记", "NewNote", typeof(MainWindow));
        public static readonly RoutedUICommand SaveNoteCommand = new RoutedUICommand("保存笔记", "SaveNote", typeof(MainWindow));
        public static readonly RoutedUICommand FocusSearchCommand = new RoutedUICommand("聚焦搜索", "FocusSearch", typeof(MainWindow));
        public static readonly RoutedUICommand DeleteNoteCommand = new RoutedUICommand("删除笔记", "DeleteNote", typeof(MainWindow));
        public static readonly RoutedUICommand BoldCommand = new RoutedUICommand("粗体", "Bold", typeof(MainWindow));
        public static readonly RoutedUICommand ItalicCommand = new RoutedUICommand("斜体", "Italic", typeof(MainWindow));
        public static readonly RoutedUICommand UnderlineCommand = new RoutedUICommand("下划线", "Underline", typeof(MainWindow));
        public static readonly RoutedUICommand UndoCommand = new RoutedUICommand("撤销", "Undo", typeof(MainWindow));
        public static readonly RoutedUICommand RedoCommand = new RoutedUICommand("重做", "Redo", typeof(MainWindow));

        private readonly HtmlToFlowDocumentConverter _htmlToFlowDocumentConverter = new HtmlToFlowDocumentConverter();
        private readonly FlowDocumentToHtmlConverter _flowDocumentToHtmlConverter = new FlowDocumentToHtmlConverter();
        private readonly HtmlCompatibilityInspector _htmlCompatibilityInspector = new HtmlCompatibilityInspector();
        private readonly WindowsSynchronizationService _syncService;
        private readonly IInternetStateService _internetStateService;
        private readonly ISafeKeyService _safeKeyService;
        private readonly ICryptoRandomService _cryptoRandomService;
        private readonly ILogService _logService;
        private System.Threading.Timer _autoSyncTimer;
        private NoteRepositoryModel _repository;
        private NoteModel _selectedNote;
        private bool _loadingSelection;
        private bool _loadingEditorMetadata;
        private bool _showRecycleBin;
        private string _searchText = string.Empty;
        private string _selectedTag;
        private readonly Dictionary<Guid, string> _safeNoteTitles = new Dictionary<Guid, string>();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;

            _syncService = new WindowsSynchronizationService(
                App.Services.GetRequiredService<ISettingsService>(),
                App.Services.GetRequiredService<IRepositoryStorageService>(),
                App.Services.GetRequiredService<IDataProtectionService>(),
                App.Services.GetRequiredService<ICryptoRandomService>(),
                App.Services.GetRequiredService<ILogService>(),
                App.Services.GetRequiredService<IXmlFileService>());
            _internetStateService = App.Services.GetRequiredService<IInternetStateService>();
            _safeKeyService = App.Services.GetRequiredService<ISafeKeyService>();
            _cryptoRandomService = App.Services.GetRequiredService<ICryptoRandomService>();
            _logService = App.Services.GetRequiredService<ILogService>();

            // Periodic auto-sync timer (every 30 minutes)
            _autoSyncTimer = new System.Threading.Timer(
                async _ => await TryAutoSyncAsync(),
                null,
                System.Threading.Timeout.Infinite,
                System.Threading.Timeout.Infinite);

            Sidebar.NewNoteClicked += NewNoteButton_Click;
            Sidebar.NewChecklistClicked += NewChecklistButton_Click;
            Sidebar.DeleteNoteClicked += DeleteNoteButton_Click;
            Sidebar.RestoreNoteClicked += RestoreNoteButton_Click;
            Sidebar.PermanentDeleteNoteClicked += PermanentDeleteNoteButton_Click;
            Sidebar.EmptyRecycleBinClicked += EmptyRecycleBinButton_Click;
            Sidebar.ActiveNotesClicked += ActiveNotesButton_Click;
            Sidebar.RecycleBinClicked += RecycleBinButton_Click;
            Sidebar.SearchTextChanged += SearchTextBox_TextChanged;
            Sidebar.TagSelectionChanged += TagList_SelectionChanged;
            Sidebar.NoteSelectionChanged += NotesList_SelectionChanged;

            EditorView.TagsLostFocus += TagsTextBox_LostFocus;
            EditorView.TagsTagAdded += TagsTextBox_TagAdded;
            EditorView.DeleteTagRequested += DeleteTagRequested;
            EditorView.Toolbar.PinnedChanged += PinnedCheckBox_Changed;

            EditorView.Toolbar.UndoClicked += UndoButton_Click;
            EditorView.Toolbar.RedoClicked += RedoButton_Click;
            EditorView.Toolbar.Heading1Clicked += Heading1Button_Click;
            EditorView.Toolbar.Heading2Clicked += Heading2Button_Click;
            EditorView.Toolbar.Heading3Clicked += Heading3Button_Click;
            EditorView.Toolbar.BoldClicked += BoldButton_Click;
            EditorView.Toolbar.ItalicClicked += ItalicButton_Click;
            EditorView.Toolbar.UnderlineClicked += UnderlineButton_Click;
            EditorView.Toolbar.StrikethroughClicked += StrikethroughButton_Click;
            EditorView.Toolbar.BulletsClicked += BulletsButton_Click;
            EditorView.Toolbar.NumberingClicked += NumberingButton_Click;
            EditorView.Toolbar.BlockquoteClicked += BlockquoteButton_Click;
            EditorView.Toolbar.CodeBlockClicked += CodeBlockButton_Click;
            EditorView.Toolbar.HorizontalRuleClicked += HorizontalRuleButton_Click;
            EditorView.Toolbar.LinkClicked += LinkButton_Click;

            // Enter key handler for adding new checklist items
            EditorView.Editor.PreviewKeyDown += Editor_PreviewKeyDown;
        }

        private IRepositoryStorageService RepositoryStorageService
        {
            get { return App.Services.GetRequiredService<IRepositoryStorageService>(); }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Validate registry data directory on startup
            if (!Services.WindowsDataDirectoryService.HasValidRegistryPath())
            {
                string regPath = Services.WindowsDataDirectoryService.ReadFromRegistry();
                if (!string.IsNullOrEmpty(regPath))
                {
                    // Registry has a value but the directory does not exist
                    System.Windows.MessageBox.Show(
                        string.Format("数据目录 {0} 不存在，请重新指定。", regPath),
                        "SilentNotes",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                // Open settings dialog to let user specify a valid path
                SyncSettingsButton_Click(this, new RoutedEventArgs());
            }

            // Apply theme before loading repository so dynamically created controls use the right brushes
            var themeService = App.Services.GetRequiredService<WpfThemeService>();
            var settingsService = App.Services.GetRequiredService<ISettingsService>();
            var settings = settingsService.LoadSettingsOrDefault();
            themeService.ApplyTheme(settings.ThemeMode);
            themeService.ApplyWindowTheme(this);
            UpdateThemeIcon();

            LoadRepository();

            // Startup auto-sync (non-blocking)
            if (_syncService.HasCloudStorageConfigured && _syncService.HasTransferCode && ShouldAutoSync(settings.AutoSyncMode))
                TryAutoSyncAsync(showStatus: false);
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSelectedNote(showStatus: false);
            // Dispose auto-sync timer to allow clean exit
            _autoSyncTimer?.Dispose();
            _autoSyncTimer = null;
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSelectedNote();
            RepositoryStorageService.ClearCache();
            LoadRepository();
        }

        private void SyncSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSelectedNote();

            var dialog = new WebDavSettingsDialog
            {
                Owner = this,
            };

            // Pre-fill if already configured
            var settings = App.Services.GetRequiredService<ISettingsService>().LoadSettingsOrDefault();
            if (settings.Credentials != null)
            {
                dialog.Prefill(
                    settings.Credentials.Url,
                    settings.Credentials.Username,
                    settings.Credentials.UnprotectedPassword,
                    transferCode: settings.TransferCode,
                    syncMode: settings.AutoSyncMode.ToString(),
                    dataDirectory: settings.DataDirectory);
            }
            else
            {
                dialog.Prefill(null, null, null, transferCode: settings.TransferCode, syncMode: settings.AutoSyncMode.ToString(), dataDirectory: settings.DataDirectory);
            }

            if (dialog.ShowDialog() == true)
            {
                bool settingsChanged = false;

                // Save WebDAV credentials only if changed
                string newUrl = string.IsNullOrWhiteSpace(dialog.ServerUrl) ? null : dialog.ServerUrl;
                string newUsername = string.IsNullOrWhiteSpace(dialog.Username) ? null : dialog.Username;
                string newPassword = string.IsNullOrWhiteSpace(dialog.Password) ? null : dialog.Password;
                bool credentialsExist = settings.Credentials != null;
                bool newCredentialsExist = !string.IsNullOrEmpty(newUrl);

                if (newCredentialsExist)
                {
                    if (!credentialsExist
                        || !string.Equals(settings.Credentials.Url, newUrl, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(settings.Credentials.Username, newUsername, StringComparison.Ordinal)
                        || !string.Equals(settings.Credentials.UnprotectedPassword, newPassword, StringComparison.Ordinal))
                    {
                        settings.Credentials = new SerializeableCloudStorageCredentials
                        {
                            CloudStorageId = "webdav",
                            Url = newUrl,
                            Username = newUsername,
                            UnprotectedPassword = newPassword,
                        };
                        settingsChanged = true;
                    }
                }
                else if (credentialsExist)
                {
                    settings.Credentials = null;
                    settingsChanged = true;
                }

                // Save transfer code if changed
                string newTransferCode = string.IsNullOrEmpty(dialog.TransferCode)
                    ? null
                    : dialog.TransferCode.Replace(" ", string.Empty);
                if (!string.Equals(settings.TransferCode, newTransferCode, StringComparison.Ordinal))
                {
                    settings.TransferCode = newTransferCode;
                    settingsChanged = true;
                }

                // Save sync mode if changed
                string newSyncModeStr = (dialog.SyncModeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                if (!string.IsNullOrEmpty(newSyncModeStr))
                {
                    var newSyncMode = (AutoSynchronizationMode)Enum.Parse(typeof(AutoSynchronizationMode), newSyncModeStr);
                    if (settings.AutoSyncMode != newSyncMode)
                    {
                        settings.AutoSyncMode = newSyncMode;
                        settingsChanged = true;
                    }
                }

                // Handle data directory change
                string sourceDir = Services.WindowsDataDirectoryService.GetEffectiveDirectory();
                string newDir = dialog.DataDirectory;
                string targetDir = string.IsNullOrWhiteSpace(newDir)
                    ? Services.WindowsApplicationPaths.AppDataDirectory
                    : newDir;
                bool dirChanged = !string.Equals(sourceDir, targetDir, StringComparison.OrdinalIgnoreCase);

                if (dirChanged)
                {
                    settings.DataDirectory = string.IsNullOrWhiteSpace(newDir) ? null : newDir;
                    settingsChanged = true;

                    // Check if target directory already has data
                    string repoFileName = Models.NoteRepositoryModel.RepositoryFileName;
                    string targetRepoFile = System.IO.Path.Combine(targetDir, repoFileName);
                    bool targetHasData = System.IO.File.Exists(targetRepoFile);

                    if (targetHasData)
                    {
                        // Target already has data — just delete old directory
                        TryDeleteDirectory(sourceDir);
                    }
                    else
                    {
                        // Target has no data — copy from source then delete source
                        System.IO.Directory.CreateDirectory(targetDir);
                        CopyDirectoryContents(sourceDir, targetDir);
                        TryDeleteDirectory(sourceDir);
                    }

                    // Always update registry
                    Services.WindowsDataDirectoryService.WriteToRegistry(targetDir);
                }

                if (settingsChanged)
                    App.Services.GetRequiredService<ISettingsService>().TrySaveSettingsToLocalDevice(settings);

                // Reload the repository from new location
                if (dirChanged)
                {
                    RepositoryStorageService.ClearCache();
                    LoadRepository();
                }

                SetStatus(settingsChanged ? "设置已保存。" : "设置未更改。");
                SyncButton.IsEnabled = true;
            }
        }

        private void CopyDirectoryContents(string sourceDir, string targetDir)
        {
            if (!System.IO.Directory.Exists(sourceDir))
                return;

            // Copy files
            foreach (string srcFile in System.IO.Directory.GetFiles(sourceDir))
            {
                string tgtFile = System.IO.Path.Combine(targetDir, System.IO.Path.GetFileName(srcFile));
                System.IO.File.Copy(srcFile, tgtFile, overwrite: true);
            }

            // Copy subdirectories
            foreach (string srcSubDir in System.IO.Directory.GetDirectories(sourceDir))
            {
                string tgtSubDir = System.IO.Path.Combine(targetDir, System.IO.Path.GetFileName(srcSubDir));
                System.IO.Directory.CreateDirectory(tgtSubDir);
                CopyDirectoryContents(srcSubDir, tgtSubDir);
            }
        }

        private void TryDeleteDirectory(string dirPath)
        {
            try
            {
                if (System.IO.Directory.Exists(dirPath))
                {
                    foreach (string file in System.IO.Directory.GetFiles(dirPath))
                        System.IO.File.Delete(file);
                    foreach (string dir in System.IO.Directory.GetDirectories(dirPath))
                        System.IO.Directory.Delete(dir, true);
                    System.IO.Directory.Delete(dirPath);
                }
            }
            catch { }
        }

        private async void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_syncService.HasCloudStorageConfigured)
            {
                SetStatus("请先配置同步设置。", isError: true);
                return;
            }

            SyncButton.IsEnabled = false;
            SaveButton.IsEnabled = false;
            SetStatus("正在同步...");

            bool success = await _syncService.SyncAsync(message =>
            {
                // Update status via Dispatcher since this callback may come from background
                Dispatcher.Invoke(() => SetStatus(message, IsSyncErrorMessage(message)));
            });

            SyncButton.IsEnabled = true;
            SaveButton.IsEnabled = true;

            if (success)
            {
                // Reload the repository if changes were made
                RepositoryStorageService.ClearCache();
                LoadRepository();
                SetStatus("同步完成。");
            }
        }

        /// <summary>Checks whether auto-sync should run based on the selected mode.</summary>
        private bool ShouldAutoSync(AutoSynchronizationMode mode)
        {
            switch (mode)
            {
                case AutoSynchronizationMode.Always:
                    return true;
                case AutoSynchronizationMode.CostFreeInternetOnly:
                    return _internetStateService.IsInternetConnected();
                default:
                    return false;
            }
        }

        /// <summary>Triggers auto-sync in the background, optional periodic updates.</summary>
        private async Task TryAutoSyncAsync(bool showStatus = false)
        {
            try
            {
                if (showStatus)
                    Dispatcher.Invoke(() => SetStatus("自动同步中..."));

                bool success = await _syncService.SyncAsync(msg =>
                {
                    if (showStatus)
                        Dispatcher.Invoke(() => SetStatus(msg, IsSyncErrorMessage(msg)));
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Auto-sync error: " + ex.Message);
            }
        }

        private void RestoreBackupButton_Click(object sender, RoutedEventArgs e)
        {
            string location = App.Services.GetRequiredService<IRepositoryStorageService>().GetLocation();
            string backupDir = System.IO.Path.Combine(location, "sync_backups");
            if (!System.IO.Directory.Exists(backupDir) || !System.IO.Directory.EnumerateFiles(backupDir, "*.silentnotes").Any())
            {
                SetStatus("没有找到备份文件。", isError: true);
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择要恢复的备份文件",
                InitialDirectory = backupDir,
                Filter = "备份文件 (*.silentnotes)|*.silentnotes",
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string targetPath = System.IO.Path.Combine(location, SilentNotes.Models.NoteRepositoryModel.RepositoryFileName);
                    System.IO.File.Copy(dialog.FileName, targetPath, overwrite: true);
                    App.Services.GetRequiredService<IRepositoryStorageService>().ClearCache();
                    LoadRepository();
                    SetStatus("已从备份恢复。");
                }
                catch (Exception ex)
                {
                    SetStatus("恢复失败：" + ex.Message, isError: true);
                }
            }
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var resources = Application.Current.Resources;
            var dialog = new ThemedDialogWindow
            {
                Title = "帮助 - 快捷键",
                Width = 480,
                Height = 480,
                Owner = this,
                Background = (Brush)resources["SilentNotesBackgroundBrush"],
            };

            var shell = new Border
            {
                Margin = new Thickness(16),
                Padding = new Thickness(20),
                Background = (Brush)resources["SilentNotesPaperBrush"],
                BorderBrush = (Brush)resources["SilentNotesBorderBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };
            var stack = new StackPanel();
            shell.Child = stack;

            stack.Children.Add(new TextBlock
            {
                Text = "快捷键汇总",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12),
                Foreground = (Brush)resources["SilentNotesTextBrush"]
            });

            AddShortcutGroup(stack, "笔记操作",
                "Ctrl+N", "新建笔记",
                "Ctrl+S", "保存笔记",
                "Ctrl+F", "搜索笔记",
                "Delete", "删除笔记（移到回收站）");

            AddShortcutGroup(stack, "格式编辑",
                "Ctrl+B", "粗体",
                "Ctrl+I", "斜体",
                "Ctrl+U", "下划线",
                "Ctrl+Z", "撤销",
                "Ctrl+Y", "重做");

            AddShortcutGroup(stack, "清单操作",
                "Enter", "新建清单项",
                "Shift+Enter", "在清单项中换行");

            var closeBtn = new Button
            {
                Content = "关闭",
                IsCancel = true,
                Width = 70,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            stack.Children.Add(closeBtn);
            dialog.Content = shell;
            dialog.ShowDialog();
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var themeService = App.Services.GetRequiredService<WpfThemeService>();
                var settingsService = App.Services.GetRequiredService<ISettingsService>();
                var settings = settingsService.LoadSettingsOrDefault();
                ThemeMode newMode = themeService.IsDarkMode ? ThemeMode.Light : ThemeMode.Dark;
                settings.ThemeMode = newMode;
                settingsService.TrySaveSettingsToLocalDevice(settings);
                themeService.ApplyTheme(newMode);
                themeService.ApplyWindowTheme(this);
                UpdateThemeIcon();
                // Force window redraw
                this.InvalidateVisual();
                this.UpdateLayout();
                // Refresh tag panel colors
                RefreshTagPanelColors();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Theme toggle error: " + ex.Message);
            }
        }

        private void UpdateThemeIcon()
        {
            var themeService = App.Services.GetRequiredService<WpfThemeService>();
            if (themeService.IsDarkMode)
            {
                // Dark mode active → show sun icon (click to switch to light)
                ThemeIconMoon.Visibility = Visibility.Collapsed;
                ThemeIconSun.Visibility = Visibility.Visible;
            }
            else
            {
                // Light mode active → show moon icon (click to switch to dark)
                ThemeIconMoon.Visibility = Visibility.Visible;
                ThemeIconSun.Visibility = Visibility.Collapsed;
            }
        }

        private void RefreshTagPanelColors()
        {
            var resources = Application.Current.Resources;
            foreach (UIElement child in Sidebar.TagPanel.Children)
            {
                Button b = child as Button;
                if (b != null)
                {
                    bool isSel = string.Equals(b.Tag as string, Sidebar.SelectedTag, StringComparison.InvariantCultureIgnoreCase)
                        || (b.Tag == null && Sidebar.SelectedTag == null);
                    b.Foreground = isSel
                        ? (Brush)resources["SilentNotesPrimaryBrush"]
                        : (Brush)resources["SilentNotesTextBrush"];
                    b.BorderBrush = isSel
                        ? (Brush)resources["SilentNotesPrimaryBrush"]
                        : (Brush)resources["SilentNotesBorderBrush"];
                    b.Background = isSel
                        ? (Brush)resources["SilentNotesAccentSoftBrush"]
                        : (Brush)resources["SilentNotesPaperBrush"];
                }
            }
        }

        private static void AddShortcutGroup(StackPanel parent, string groupTitle, params string[] shortcuts)
        {
            var resources = Application.Current.Resources;
            parent.Children.Add(new TextBlock
            {
                Text = groupTitle,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 4),
                Foreground = (Brush)resources["SilentNotesSecondaryTextBrush"]
            });

            var grid = new Grid { Margin = new Thickness(12, 0, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            for (int i = 0; i < shortcuts.Length; i += 2)
            {
                string key = shortcuts[i];
                string desc = (i + 1 < shortcuts.Length) ? shortcuts[i + 1] : string.Empty;

                var keyText = new TextBlock
                {
                    Text = key,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = (Brush)resources["SilentNotesTextBrush"]
                };
                var descText = new TextBlock
                {
                    Text = desc,
                    Margin = new Thickness(0, 0, 0, 4),
                    Foreground = (Brush)resources["SilentNotesTextBrush"]
                };
                Grid.SetColumn(keyText, 0);
                Grid.SetColumn(descText, 2);
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                int row = grid.RowDefinitions.Count - 1;
                Grid.SetRow(keyText, row);
                Grid.SetRow(descText, row);
                grid.Children.Add(keyText);
                grid.Children.Add(descText);
            }

            parent.Children.Add(grid);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSelectedNote();
        }

        private void ActiveNotesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_showRecycleBin)
                return;

            SaveSelectedNote();
            _showRecycleBin = false;
            RefreshNoteList(_repository?.Notes.FirstOrDefault(note => !note.InRecyclingBin));
            UpdateRepositorySummary();
            UpdateModeControls();
        }

        private void RecycleBinButton_Click(object sender, RoutedEventArgs e)
        {
            if (_showRecycleBin)
                return;

            SaveSelectedNote();
            _showRecycleBin = true;
            RefreshNoteList(_repository?.Notes.FirstOrDefault(note => note.InRecyclingBin));
            UpdateRepositorySummary();
            UpdateModeControls();
        }

        private void NewNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_repository == null || Object.ReferenceEquals(_repository, NoteRepositoryModel.InvalidRepository))
                return;

            SaveSelectedNote();
            _showRecycleBin = false;
            Sidebar.SearchTextBox.Text = string.Empty;
            _searchText = string.Empty;

            var note = new NoteModel
            {
                HtmlContent = "<p>新笔记</p>",
            };
            if (!string.IsNullOrEmpty(_selectedTag))
                note.Tags.Add(_selectedTag);

            note.RefreshModifiedAt();
            _repository.Notes.Insert(0, note);
            _repository.RefreshOrderModifiedAt();
            RepositoryStorageService.TrySaveRepository(_repository);
            UpdateRepositorySummary();
            RefreshTagList();
            RefreshNoteList(note);
            UpdateModeControls();
            SetStatus("已新建笔记。");
        }

        private void NewChecklistButton_Click(object sender, RoutedEventArgs e)
        {
            if (_repository == null || Object.ReferenceEquals(_repository, NoteRepositoryModel.InvalidRepository))
                return;

            SaveSelectedNote();
            _showRecycleBin = false;
            Sidebar.SearchTextBox.Text = string.Empty;
            _searchText = string.Empty;

            var note = new NoteModel
            {
                NoteType = NoteType.Checklist,
                HtmlContent = "<p>新项目</p>",
            };
            if (!string.IsNullOrEmpty(_selectedTag))
                note.Tags.Add(_selectedTag);

            note.RefreshModifiedAt();
            _repository.Notes.Insert(0, note);
            _repository.RefreshOrderModifiedAt();
            RepositoryStorageService.TrySaveRepository(_repository);
            UpdateRepositorySummary();
            RefreshTagList();
            RefreshNoteList(note);
            UpdateModeControls();
            SetStatus("已新建清单。");
        }

        private void DeleteNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNote == null || _showRecycleBin)
                return;

            System.Windows.MessageBoxResult result = MessageBox.Show(
                "要将当前笔记移到回收站吗？",
                "SilentNotes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                System.Windows.MessageBoxResult.No);
            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            _selectedNote.InRecyclingBin = true;
            _selectedNote.RefreshMetaModifiedAt();
            _repository.RefreshOrderModifiedAt();
            bool saved = RepositoryStorageService.TrySaveRepository(_repository);
            NoteModel nextNote = _repository.Notes.FirstOrDefault(note => !note.InRecyclingBin);
            UpdateRepositorySummary();
            RefreshTagList();
            RefreshNoteList(nextNote);
            SetStatus(saved ? "已移到回收站。" : "移动到回收站失败。", !saved);
        }

        private void RestoreNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNote == null || !_showRecycleBin)
                return;

            _selectedNote.InRecyclingBin = false;
            _selectedNote.RefreshMetaModifiedAt();
            _repository.RefreshOrderModifiedAt();
            bool saved = RepositoryStorageService.TrySaveRepository(_repository);
            NoteModel nextNote = _repository.Notes.FirstOrDefault(note => note.InRecyclingBin);
            RefreshNoteList(nextNote);
            UpdateRepositorySummary();
            RefreshTagList();
            SetStatus(saved ? "已恢复笔记。" : "恢复笔记失败。", !saved);
        }

        private void PermanentDeleteNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNote == null || !_showRecycleBin)
                return;

            System.Windows.MessageBoxResult result = MessageBox.Show(
                "要永久删除当前笔记吗？此操作不能撤销。",
                "SilentNotes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);
            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            NoteModel noteToDelete = _selectedNote;
            _repository.DeletedNotes.AddIdOrRefreshDeletedAt(noteToDelete.Id);
            _repository.Notes.Remove(noteToDelete);
            _repository.RefreshOrderModifiedAt();
            bool saved = RepositoryStorageService.TrySaveRepository(_repository);
            NoteModel nextNote = _repository.Notes.FirstOrDefault(note => note.InRecyclingBin);
            RefreshNoteList(nextNote);
            UpdateRepositorySummary();
            RefreshTagList();
            SetStatus(saved ? "已永久删除笔记。" : "永久删除失败。", !saved);
        }

        private void EmptyRecycleBinButton_Click(object sender, RoutedEventArgs e)
        {
            if (_repository == null || !_showRecycleBin)
                return;

            int count = _repository.Notes.Count(note => note.InRecyclingBin);
            if (count == 0)
                return;

            System.Windows.MessageBoxResult result = MessageBox.Show(
                string.Format("要永久删除回收站中的 {0} 条笔记吗？此操作不能撤销。", count),
                "SilentNotes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);
            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            for (int index = _repository.Notes.Count - 1; index >= 0; index--)
            {
                NoteModel note = _repository.Notes[index];
                if (!note.InRecyclingBin)
                    continue;

                _repository.DeletedNotes.AddIdOrRefreshDeletedAt(note.Id);
                _repository.Notes.RemoveAt(index);
            }
            _repository.RefreshOrderModifiedAt();
            bool saved = RepositoryStorageService.TrySaveRepository(_repository);
            RefreshNoteList(null);
            UpdateRepositorySummary();
            RefreshTagList();
            SetStatus(saved ? "已清空回收站。" : "清空回收站失败。", !saved);
        }

        private void TagList_SelectionChanged(object sender, EventArgs e)
        {
            if (_repository == null)
                return;

            SaveSelectedNote(showStatus: false);
            _selectedTag = Sidebar.SelectedTag;
            RefreshNoteList(_selectedNote);
            UpdateRepositorySummary();
        }

        private void TagsTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveSelectedMetadata();
        }

        private void TagsTextBox_TagAdded(object sender, EventArgs e)
        {
            if (_loadingEditorMetadata || _selectedNote == null || _showRecycleBin)
                return;

            string tagToAdd = (EditorView.TagsText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(tagToAdd))
                return;

            if (_selectedNote.Tags.Contains(tagToAdd, StringComparer.InvariantCultureIgnoreCase))
            {
                SetStatus(string.Format("标签 \"{0}\" 已存在。", tagToAdd), true);
                return;
            }

            _selectedNote.Tags.Add(tagToAdd);
            _selectedNote.Tags.Sort(StringComparer.InvariantCultureIgnoreCase);
            _selectedNote.RefreshMetaModifiedAt();
            bool saved = RepositoryStorageService.TrySaveRepository(_repository);
            EditorView.TagsText = string.Empty;
            RefreshVisibleSelectedItemTitle();
            RefreshTagList();
            RefreshNoteList(_selectedNote);
            UpdateRepositorySummary();
            UpdateTagSuggestions();
            SetStatus(saved ? "已添加标签。" : "保存笔记属性失败。", !saved);
        }

        private void DeleteTagRequested(object sender, EventArgs e)
        {
            if (_loadingEditorMetadata || _selectedNote == null || _showRecycleBin)
                return;

            string tagToDelete = (EditorView.TagsText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(tagToDelete))
                return;

            int tagIndex = _selectedNote.Tags.FindIndex(tag => string.Equals(tag, tagToDelete, StringComparison.InvariantCultureIgnoreCase));
            if (tagIndex == -1)
            {
                SetStatus(string.Format("标签 \"{0}\" 不存在。", tagToDelete), true);
                return;
            }

            _selectedNote.Tags.RemoveAt(tagIndex);
            _selectedNote.RefreshMetaModifiedAt();
            bool saved = RepositoryStorageService.TrySaveRepository(_repository);
            EditorView.TagsText = string.Empty;
            EditorView.IsDeleteMode = false;
            RefreshVisibleSelectedItemTitle();
            RefreshTagList();
            RefreshNoteList(_selectedNote);
            UpdateRepositorySummary();
            UpdateTagSuggestions();
            SetStatus(saved ? "已删除标签。" : "保存笔记属性失败。", !saved);
        }

        private void PinnedCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            SaveSelectedMetadata();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_repository == null)
                return;

            SaveSelectedNote(showStatus: false);
            _searchText = Sidebar.SearchTextBox.Text ?? string.Empty;
            UpdateRepositorySummary();
            RefreshNoteList(_selectedNote);
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (EditorView.Editor.CanUndo)
                EditorView.Editor.Undo();
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            if (EditorView.Editor.CanRedo)
                EditorView.Editor.Redo();
        }

        private void Undo_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (EditorView.Editor.CanUndo)
                EditorView.Editor.Undo();
        }

        private void Redo_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (EditorView.Editor.CanRedo)
                EditorView.Editor.Redo();
        }

        private void BoldButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteEditorCommand(EditingCommands.ToggleBold);
        }

        private void ItalicButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteEditorCommand(EditingCommands.ToggleItalic);
        }

        private void UnderlineButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteEditorCommand(EditingCommands.ToggleUnderline);
        }

        private void BulletsButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteEditorCommand(EditingCommands.ToggleBullets);
        }

        private void NumberingButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteEditorCommand(EditingCommands.ToggleNumbering);
        }

        private void Heading1Button_Click(object sender, RoutedEventArgs e)
        {
            SetParagraphHeading("h1", 22);
        }

        private void Heading2Button_Click(object sender, RoutedEventArgs e)
        {
            SetParagraphHeading("h2", 20);
        }

        private void Heading3Button_Click(object sender, RoutedEventArgs e)
        {
            SetParagraphHeading("h3", 18);
        }

        private void SetParagraphHeading(string headingTag, double fontSize)
        {
            if (EditorView.Editor.IsReadOnly || _selectedNote == null)
                return;
            EditorView.Editor.Focus();

            Paragraph paragraph = EditorView.Editor.Selection.Start.Paragraph;
            if (paragraph == null)
                return;

            double currentSize = paragraph.FontSize;
            bool isH1 = Math.Abs(currentSize - 22) < 0.1;
            bool isH2 = Math.Abs(currentSize - 20) < 0.1;
            bool isH3 = Math.Abs(currentSize - 18) < 0.1;

            if ((headingTag == "h1" && isH1) || (headingTag == "h2" && isH2) || (headingTag == "h3" && isH3))
            {
                paragraph.ClearValue(Paragraph.FontSizeProperty);
                paragraph.ClearValue(Paragraph.FontWeightProperty);
                paragraph.ClearValue(Paragraph.ForegroundProperty);
                paragraph.Tag = null;
            }
            else
            {
                paragraph.FontSize = fontSize;
                paragraph.FontWeight = FontWeights.SemiBold;

                switch (headingTag)
                {
                    case "h1":
                        paragraph.Foreground = new SolidColorBrush(Color.FromArgb(255, 70, 130, 180));
                        break;
                    case "h2":
                        paragraph.Foreground = new SolidColorBrush(Color.FromArgb(217, 70, 130, 180));
                        break;
                    case "h3":
                        paragraph.Foreground = new SolidColorBrush(Color.FromArgb(179, 70, 130, 180));
                        break;
                }

                paragraph.Tag = headingTag;
            }
        }

        private void NotesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingSelection)
                return;

            SaveSelectedNote();
            NoteListItem selectedItem = Sidebar.NotesList.SelectedItem as NoteListItem;
            SelectNote(selectedItem?.Note);
        }

        private void LoadRepository()
        {
            RepositoryStorageLoadResult loadResult = RepositoryStorageService.LoadRepositoryOrDefault(out _repository);
            if (Object.ReferenceEquals(_repository, NoteRepositoryModel.InvalidRepository))
            {
                Sidebar.NotesList.ItemsSource = null;
                Sidebar.PopulateTagPanel(new List<string>(), null);
                SelectNote(null);
                Sidebar.RepositorySummaryText.Text = "本地仓库无法读取，已停止编辑以避免覆盖原文件。";
                UpdateModeControls();
                SetStatus("仓库加载失败。", isError: true);
                return;
            }

            RefreshTagList();
            RefreshNoteList(_repository.Notes.FirstOrDefault(note => note.InRecyclingBin == _showRecycleBin));
            UpdateRepositorySummary();
            UpdateModeControls();

            SetStatus(loadResult == RepositoryStorageLoadResult.CreatedNewEmptyRepository
                ? "已创建新的本地仓库。"
                : "已加载本地仓库。");
        }

        private void RefreshNoteList(NoteModel noteToSelect)
        {
            var items = _repository.Notes
                .Where(note => note.InRecyclingBin == _showRecycleBin)
                .Where(MatchesTag)
                .Where(MatchesSearch)
                .OrderByDescending(note => note.IsPinned)
                .Select(note =>
                {
                    var item = new NoteListItem(note);
                    string cachedTitle;
                    if (note.SafeId.HasValue && _safeNoteTitles.TryGetValue(note.Id, out cachedTitle))
                        item.SetCustomTitle(cachedTitle);
                    return item;
                })
                .ToList();

            _loadingSelection = true;
            try
            {
                Sidebar.NotesList.ItemsSource = items;
                Sidebar.NotesList.SelectedItem = items.FirstOrDefault(item => ReferenceEquals(item.Note, noteToSelect));
                noteToSelect = (Sidebar.NotesList.SelectedItem as NoteListItem)?.Note;
            }
            finally
            {
                _loadingSelection = false;
            }

            bool isEmpty = items.Count == 0;
            Sidebar.EmptyListText.Text = _showRecycleBin
                ? "回收站为空。"
                : (string.IsNullOrWhiteSpace(_searchText) && string.IsNullOrEmpty(_selectedTag)
                    ? "还没有笔记，点击「新建笔记」开始。"
                    : "没有匹配的笔记。");
            Sidebar.EmptyListText.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
            Sidebar.NotesList.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;

            SelectNote(noteToSelect);
        }

        private void RefreshTagList()
        {
            if (_repository == null || Object.ReferenceEquals(_repository, NoteRepositoryModel.InvalidRepository))
                return;

            var tags = _repository.Notes
                .Where(note => note.InRecyclingBin == _showRecycleBin)
                .SelectMany(note => note.Tags)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.InvariantCultureIgnoreCase)
                .OrderBy(tag => tag, StringComparer.InvariantCultureIgnoreCase)
                .ToList();

            Sidebar.PopulateTagPanel(tags, _selectedTag);
            _selectedTag = Sidebar.SelectedTag;
        }

        private void UpdateTagSuggestions()
        {
            if (_repository == null || Object.ReferenceEquals(_repository, NoteRepositoryModel.InvalidRepository) || _selectedNote == null)
            {
                EditorView.SetTagSuggestions(Enumerable.Empty<string>());
                return;
            }

            List<string> allTags = _repository.CollectActiveTags();
            var suggestions = allTags.Where(tag => !_selectedNote.Tags.Contains(tag, StringComparer.InvariantCultureIgnoreCase));
            EditorView.SetTagSuggestions(suggestions);
        }

        private void UpdateRepositorySummary()
        {
            if (_repository == null)
                return;

            int activeCount = _repository.Notes.Count(note => !note.InRecyclingBin);
            int recycleBinCount = _repository.Notes.Count(note => note.InRecyclingBin);
            int visibleCount = _repository.Notes.Count(note => note.InRecyclingBin == _showRecycleBin && MatchesTag(note) && MatchesSearch(note));
            Sidebar.RepositorySummaryText.Text = string.IsNullOrWhiteSpace(_searchText)
                ? string.Format("{0} 条活动笔记，{1} 条回收站笔记。当前视图 {2} 条。", activeCount, recycleBinCount, visibleCount)
                : string.Format("当前视图找到 {0} 条；总计 {1} 条活动笔记，{2} 条回收站笔记。", visibleCount, activeCount, recycleBinCount);
            Sidebar.ListTitleText.Text = _showRecycleBin ? "回收站" : "笔记列表";
        }

        private void UpdateModeControls()
        {
            bool hasRepository = _repository != null && !Object.ReferenceEquals(_repository, NoteRepositoryModel.InvalidRepository);
            Sidebar.SetModeButtons(_showRecycleBin);
            EditorView.Toolbar.Visibility = _showRecycleBin ? Visibility.Collapsed : Visibility.Visible;
            EditorView.IsTagsReadOnly = _showRecycleBin || !hasRepository;
            EditorView.Toolbar.PinnedCheckBoxControl.IsEnabled = hasRepository && !_showRecycleBin;
            SaveButton.IsEnabled = hasRepository && !_showRecycleBin;
            Sidebar.ActiveNotesButton.FontWeight = _showRecycleBin ? FontWeights.Normal : FontWeights.SemiBold;
            Sidebar.RecycleBinButton.FontWeight = _showRecycleBin ? FontWeights.SemiBold : FontWeights.Normal;
        }

        private void SelectNote(NoteModel note)
        {
            _selectedNote = note;
            EditorView.Editor.Document = new FlowDocument { FontSize = 15 };

            if (note == null)
            {
                EditorView.Editor.IsReadOnly = true;
                LoadEditorMetadata(null);
                EditorView.EditorTitleText.Text = "编辑器";
                EditorView.EditorInfoText.Text = _showRecycleBin ? "回收站为空，或没有匹配搜索条件的笔记。" : "请选择一条笔记。";
                return;
            }

            LoadEditorMetadata(note);
            EditorView.EditorTitleText.Text = BuildTitle(note);
            if (_showRecycleBin)
            {
                EditorView.Editor.IsReadOnly = true;
                EditorView.EditorInfoText.Text = string.Format("回收站笔记只读。最后修改：{0:g}", note.ModifiedAt.ToLocalTime());
                EditorView.Editor.Document = _htmlToFlowDocumentConverter.Convert(note.HtmlContent, note.NoteType == NoteType.Checklist);
                return;
            }

            // Handle safe notes
            if (note.SafeId.HasValue)
            {
                if (_safeKeyService.IsSafeOpen(note.SafeId.Value))
                {
                    // Safe is open - decrypt content and allow editing
                    string unlockedContent = DecryptSafeNoteContent(note);
                    if (unlockedContent != null)
                    {
                        bool canEditSafe = _htmlCompatibilityInspector.CanEditWithoutConversion(unlockedContent);
                        EditorView.Editor.IsReadOnly = !canEditSafe;
                        EditorView.EditorInfoText.Text = canEditSafe
                            ? string.Format("安全箱笔记 · 最后修改：{0:g}", note.ModifiedAt.ToLocalTime())
                            : "安全箱笔记（只读模式）";
                        EditorView.Editor.Document = canEditSafe
                            ? _htmlToFlowDocumentConverter.Convert(unlockedContent, note.NoteType == NoteType.Checklist)
                            : new FlowDocument(new Paragraph(new Run(BuildPlainText(unlockedContent))));
                        // Update title from decrypted content for better readability
                        string unlockedTitle = BuildPlainText(unlockedContent);
                        string displayTitle = string.IsNullOrWhiteSpace(unlockedTitle)
                            ? "无标题笔记"
                            : (unlockedTitle.Length > 80 ? unlockedTitle.Substring(0, 80) + "..." : unlockedTitle);
                        EditorView.EditorTitleText.Text = displayTitle;
                        // Cache the decrypted title for safe notes so it survives list refreshes
                        if (!string.IsNullOrEmpty(unlockedTitle))
                            _safeNoteTitles[note.Id] = unlockedTitle;
                        // Also update sidebar list item title
                        NoteListItem selectedItem = Sidebar.NotesList.SelectedItem as NoteListItem;
                        if (selectedItem != null)
                        {
                            selectedItem.SetCustomTitle(unlockedTitle);
                            Sidebar.NotesList.Items.Refresh();
                        }
                        HookChecklistEvents();
                        return;
                    }
                }

                // Safe is not open or decryption failed - show lock message
                EditorView.Editor.IsReadOnly = true;
                EditorView.EditorInfoText.Text = "这条笔记位于安全箱中，点击上方「安全箱」按钮输入密码解锁后可编辑。";
                EditorView.Editor.Document = new FlowDocument { FontSize = 15 };
                EditorView.Editor.Document.Blocks.Add(new Paragraph(new Run("🔒 安全箱笔记已锁定")));
                EditorView.Editor.Document.Blocks.Add(new Paragraph(new Run("请点击工具栏中的「安全箱」按钮输入密码解锁。")));
                return;
            }

            bool canEdit = _htmlCompatibilityInspector.CanEditWithoutConversion(note.HtmlContent);
            EditorView.Editor.IsReadOnly = !canEdit;
            EditorView.EditorInfoText.Text = canEdit
                ? string.Format("最后修改：{0:g}", note.ModifiedAt.ToLocalTime())
                : "这条笔记包含当前原生编辑器不支持的 HTML 内容，暂时只读以避免格式损坏。";
            EditorView.Editor.Document = canEdit
                ? _htmlToFlowDocumentConverter.Convert(note.HtmlContent, note.NoteType == NoteType.Checklist)
                : new FlowDocument(new Paragraph(new Run(BuildPlainText(note.HtmlContent))));

            // Hook checklist checkbox events for auto-save on toggle
            HookChecklistEvents();
        }

        private void SaveSelectedNote(bool showStatus = true)
        {
            if (_selectedNote == null || EditorView.Editor.IsReadOnly)
                return;

            // Check if it's a locked safe note
            if (_selectedNote.SafeId.HasValue && !_safeKeyService.IsSafeOpen(_selectedNote.SafeId.Value))
                return;

            string html = _flowDocumentToHtmlConverter.Convert(EditorView.Editor.Document);

            // If it's a safe note, encrypt the content before storing
            if (_selectedNote.SafeId.HasValue)
            {
                string encrypted = EncryptSafeNoteContent(html);
                if (encrypted == html) // encryption failed
                    return;
                html = encrypted;
            }

            if (html == _selectedNote.HtmlContent)
                return;

            _selectedNote.HtmlContent = html;
            _selectedNote.RefreshModifiedAt();
            bool saved = RepositoryStorageService.TrySaveRepository(_repository);
            RefreshVisibleSelectedItemTitle();
            RefreshTagList();
            UpdateRepositorySummary();
            if (showStatus)
                SetStatus(saved ? "已保存笔记。" : "保存失败。", !saved);
        }

        private void SaveSelectedMetadata()
        {
            if (_loadingEditorMetadata || _selectedNote == null || _showRecycleBin)
                return;

            bool changed = false;

            if (_selectedNote.IsPinned != EditorView.Toolbar.PinnedCheckBoxControl.IsChecked.GetValueOrDefault())
            {
                _selectedNote.IsPinned = EditorView.Toolbar.PinnedCheckBoxControl.IsChecked.GetValueOrDefault();
                changed = true;
            }

            if (!changed)
                return;

            _selectedNote.RefreshMetaModifiedAt();
            bool saved = RepositoryStorageService.TrySaveRepository(_repository);
            RefreshVisibleSelectedItemTitle();
            RefreshTagList();
            RefreshNoteList(_selectedNote);
            UpdateRepositorySummary();
            UpdateTagSuggestions();
            SetStatus(saved ? "已保存笔记属性。" : "保存笔记属性失败。", !saved);
        }

        private void StrikethroughButton_Click(object sender, RoutedEventArgs e)
        {
            if (EditorView.Editor.IsReadOnly || _selectedNote == null)
                return;
            EditorView.Editor.Focus();

            var selection = EditorView.Editor.Selection;
            if (selection.IsEmpty)
                return;

            TextDecorationCollection current = selection.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
            if (current == TextDecorations.Strikethrough)
                selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
            else
                selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Strikethrough);
        }

        private void BlockquoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (EditorView.Editor.IsReadOnly || _selectedNote == null)
                return;
            EditorView.Editor.Focus();

            Paragraph paragraph = EditorView.Editor.Selection.Start.Paragraph;
            if (paragraph == null)
                return;

            if (paragraph.Tag as string == "blockquote")
            {
                paragraph.ClearValue(Paragraph.MarginProperty);
                paragraph.ClearValue(Paragraph.PaddingProperty);
                paragraph.ClearValue(Paragraph.BackgroundProperty);
                paragraph.ClearValue(Paragraph.BorderBrushProperty);
                paragraph.ClearValue(Paragraph.BorderThicknessProperty);
                paragraph.Tag = null;
            }
            else
            {
                paragraph.Margin = new Thickness(12, 4, 0, 4);
                paragraph.Padding = new Thickness(8, 4, 8, 4);
                paragraph.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
                paragraph.BorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
                paragraph.BorderThickness = new Thickness(3, 0, 0, 0);
                paragraph.Tag = "blockquote";
            }
        }

        private void CodeBlockButton_Click(object sender, RoutedEventArgs e)
        {
            if (EditorView.Editor.IsReadOnly || _selectedNote == null)
                return;
            EditorView.Editor.Focus();

            Paragraph paragraph = EditorView.Editor.Selection.Start.Paragraph;
            if (paragraph == null)
                return;

            if (paragraph.Tag as string == "pre")
            {
                paragraph.ClearValue(Paragraph.FontFamilyProperty);
                paragraph.ClearValue(Paragraph.BackgroundProperty);
                paragraph.ClearValue(Paragraph.PaddingProperty);
                paragraph.Tag = null;
            }
            else
            {
                paragraph.FontFamily = new FontFamily("Consolas");
                paragraph.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
                paragraph.Padding = new Thickness(8);
                paragraph.Tag = "pre";
            }
        }

        private void HorizontalRuleButton_Click(object sender, RoutedEventArgs e)
        {
            if (EditorView.Editor.IsReadOnly || _selectedNote == null)
                return;
            EditorView.Editor.Focus();

            Paragraph currentParagraph = EditorView.Editor.Selection.Start.Paragraph;
            if (currentParagraph == null)
                return;

            var hr = new BlockUIContainer
            {
                Child = new Border
                {
                    Height = 1,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                    Margin = new Thickness(0, 8, 0, 8),
                },
                Tag = "hr"
            };

            // Insert after current paragraph
            var blocks = EditorView.Editor.Document.Blocks;
            Block afterBlock = currentParagraph.NextBlock;
            if (afterBlock != null)
                blocks.InsertBefore(afterBlock, hr);
            else
                blocks.Add(hr);
        }

        private void LinkButton_Click(object sender, RoutedEventArgs e)
        {
            if (EditorView.Editor.IsReadOnly || _selectedNote == null)
                return;
            EditorView.Editor.Focus();
            var resources = Application.Current.Resources;

            // Check if the selection is inside a Hyperlink
            Hyperlink existingLink = FindHyperlink(EditorView.Editor.Selection.Start);
            string existingUrl = existingLink?.NavigateUri?.ToString() ?? string.Empty;

            // Simple prompt for URL input using a WPF dialog
            var dialog = new ThemedDialogWindow
            {
                Title = "插入链接",
                Width = 400,
                Height = 180,
                Owner = this,
                Background = (Brush)resources["SilentNotesBackgroundBrush"],
            };
            var shell = new Border
            {
                Margin = new Thickness(16),
                Padding = new Thickness(20),
                Background = (Brush)resources["SilentNotesPaperBrush"],
                BorderBrush = (Brush)resources["SilentNotesBorderBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };
            var stack = new StackPanel();
            shell.Child = stack;
            stack.Children.Add(new TextBlock { Text = "请输入链接 URL：", Margin = new Thickness(0, 0, 0, 8) });
            var urlBox = new TextBox { Text = existingUrl, Height = 28 };
            stack.Children.Add(urlBox);
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var okBtn = new Button { Content = "确定", IsDefault = true, Width = 70, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
            var cancelBtn = new Button { Content = "取消", IsCancel = true, Width = 70, Height = 28 };
            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            stack.Children.Add(btnPanel);
            dialog.Content = shell;
            urlBox.Focus();
            urlBox.SelectAll();

            okBtn.Click += (o, args) =>
            {
                if (!string.IsNullOrWhiteSpace(urlBox.Text))
                    dialog.DialogResult = true;
            };

            bool? result = dialog.ShowDialog();
            if (result != true || string.IsNullOrWhiteSpace(urlBox.Text))
                return;

            string input = urlBox.Text.Trim();
            string url = input;
            if (!url.StartsWith("http://") && !url.StartsWith("https://") && !url.StartsWith("mailto:"))
                url = "https://" + url;

            Paragraph paragraph = EditorView.Editor.Selection.Start.Paragraph;
            if (paragraph == null)
                return;

            if (existingLink != null)
            {
                // Update existing link's URI
                existingLink.NavigateUri = new Uri(url, UriKind.RelativeOrAbsolute);
            }
            else if (!EditorView.Editor.Selection.IsEmpty)
            {
                // Get selected text, delete selection, create hyperlink
                string selectedText = EditorView.Editor.Selection.Text;
                if (string.IsNullOrEmpty(selectedText))
                {
                    // Insert a hyperlink with the URL as display text
                    Hyperlink hl = new Hyperlink(new Run(url))
                    {
                        NavigateUri = new Uri(url, UriKind.RelativeOrAbsolute)
                    };
                    paragraph.Inlines.Add(hl);
                }
                else
                {
                    EditorView.Editor.Selection.Text = string.Empty;
                    Hyperlink hl = new Hyperlink(new Run(selectedText))
                    {
                        NavigateUri = new Uri(url, UriKind.RelativeOrAbsolute)
                    };
                    // Add after the current position
                    paragraph.Inlines.Add(hl);
                }
            }
            else
            {
                // No selection and no existing link, insert URL as hyperlink text
                Hyperlink hl = new Hyperlink(new Run(url))
                {
                    NavigateUri = new Uri(url, UriKind.RelativeOrAbsolute)
                };
                paragraph.Inlines.Add(hl);
            }
        }

        private static Hyperlink FindHyperlink(TextPointer position)
        {
            while (position != null)
            {
                Hyperlink link = position.Parent as Hyperlink;
                if (link != null)
                    return link;
                position = position.GetNextContextPosition(LogicalDirection.Forward);
            }
            return null;
        }

        private void SafeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_repository == null || Object.ReferenceEquals(_repository, NoteRepositoryModel.InvalidRepository))
                return;

            bool hasAnySafe = _repository.Safes.Count > 0;
            ShowSafePasswordDialog(hasAnySafe);
        }

        private void CloseSafe()
        {
            bool hasOpenSafe = _repository.Safes.Any(s => _safeKeyService.IsSafeOpen(s.Id));
            if (!hasOpenSafe)
                return;

            try
            {
                _logService.Info("开始关闭安全箱流程...");
                SaveSelectedNote();
                _logService.Info("安全箱笔记已保存，准备关闭安全箱...");
                _safeKeyService.CloseAllSafes();
                _logService.Info("安全箱已关闭，清除缓存的标题...");
                _safeNoteTitles.Clear();
                SetStatus("安全箱已关闭。");
                RefreshNoteList(_selectedNote?.SafeId.HasValue == true ? _selectedNote : null);
                if (_selectedNote?.SafeId.HasValue == true)
                {
                    _logService.Info("刷新当前安全箱笔记的显示状态...");
                    SelectNote(_selectedNote);
                }
                _logService.Info("关闭安全箱流程完成。");
            }
            catch (Exception ex)
            {
                _logService.Error($"关闭安全箱时出错: {ex}");
                SetStatus("关闭安全箱时发生错误，请查看日志确认详情。", isError: true);
            }
        }

        /// <summary>
        /// Shows a dialog for creating a new safe or opening an existing one.
        /// </summary>
        /// <param name="existingSafe">True if a safe already exists (open mode), false to create a new safe.</param>
        private void ShowSafePasswordDialog(bool existingSafe)
        {
            var resources = Application.Current.Resources;
            var dialog = new ThemedDialogWindow
            {
                Title = existingSafe ? "打开安全箱" : "创建安全箱",
                Width = 420,
                Height = existingSafe ? 340 : 370,
                Owner = this,
                Background = (Brush)resources["SilentNotesBackgroundBrush"],
            };

            var shell = new Border
            {
                Margin = new Thickness(16),
                Padding = new Thickness(20),
                Background = (Brush)resources["SilentNotesPaperBrush"],
                BorderBrush = (Brush)resources["SilentNotesBorderBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };

            var stack = new StackPanel();
            shell.Child = stack;

            stack.Children.Add(new TextBlock
            {
                Text = existingSafe ? "打开安全箱" : "创建安全箱",
                Style = (Style)resources["SectionTitleTextStyle"]
            });

            stack.Children.Add(new TextBlock
            {
                Text = existingSafe
                    ? "输入安全箱密码以解锁受保护的笔记。"
                    : "设置一个至少 5 个字符的密码，用于保护安全箱中的笔记。",
                Style = (Style)resources["SecondaryTextStyle"],
                Margin = new Thickness(0, 6, 0, 16),
                TextWrapping = TextWrapping.Wrap
            });

            // Password
            stack.Children.Add(new TextBlock
            {
                Text = existingSafe ? "输入安全箱密码：" : "设置安全箱密码（至少 5 个字符）：",
                Margin = new Thickness(0, 0, 0, 6)
            });
            var passwordBox = new System.Windows.Controls.PasswordBox();
            stack.Children.Add(passwordBox);

            // Password confirmation (only for new safe)
            System.Windows.Controls.PasswordBox confirmBox = null;
            if (!existingSafe)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "确认密码：",
                    Margin = new Thickness(0, 12, 0, 6)
                });
                confirmBox = new System.Windows.Controls.PasswordBox();
                stack.Children.Add(confirmBox);
            }

            var errorText = new TextBlock
            {
                Foreground = (Brush)resources["SilentNotesDangerBrush"],
                Margin = new Thickness(0, 12, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            };
            stack.Children.Add(errorText);

            // Buttons
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            Button lockBtn = null;
            if (existingSafe)
            {
                lockBtn = new Button
                {
                    Content = "锁定",
                    MinWidth = 80,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                btnPanel.Children.Add(lockBtn);
            }

            var okBtn = new Button
            {
                Content = existingSafe ? "解锁" : "创建",
                IsDefault = true,
                MinWidth = 80,
                Style = (Style)resources["PrimaryButtonStyle"]
            };
            btnPanel.Children.Add(okBtn);
            stack.Children.Add(btnPanel);
            dialog.Content = shell;
            dialog.KeyDown += (sender2, e2) =>
            {
                if (e2.Key == System.Windows.Input.Key.Escape)
                {
                    dialog.DialogResult = false;
                    dialog.Close();
                }
            };
            passwordBox.Focus();

            if (lockBtn != null)
            {
                lockBtn.Click += (sender2, e2) =>
                {
                    CloseSafe();
                    dialog.DialogResult = true;
                    dialog.Close();
                };
            }

            okBtn.Click += (sender2, e2) =>
            {
                string password = passwordBox.Password;
                if (string.IsNullOrEmpty(password) || password.Length < 5)
                {
                    errorText.Text = "密码至少需要 5 个字符。";
                    return;
                }

                if (!existingSafe)
                {
                    string confirm = confirmBox?.Password ?? string.Empty;
                    if (password != confirm)
                    {
                        errorText.Text = "两次输入的密码不一致。";
                        return;
                    }
                }

                SecureString securePassword = CryptoUtils.StringToSecureString(password);

                if (existingSafe)
                {
                    // Try to open all existing safes (defensively close first, like original OpenSafeViewModel)
                    bool anyOpened = false;
                    int safesCount = _repository.Safes.Count;
                    foreach (var safe in _repository.Safes)
                    {
                        // Log the actual exception from direct decryption for debugging
                        try
                        {
                            byte[] testEncrypted = CryptoUtils.Base64StringToBytes(safe.SerializeableKey);
                            ICryptor testCryptor = new Cryptor(SafeModel.CryptorPackageName, null);
                            bool testNeedsReEnc;
                            byte[] testDecrypted = testCryptor.Decrypt(testEncrypted, securePassword, out testNeedsReEnc);
                            _logService.Info($"直接解密安全箱成功: needsReEnc={testNeedsReEnc}");
                        }
                        catch (Exception testEx)
                        {
                            _logService.Error($"直接解密安全箱失败, 实际异常: {testEx}");
                        }

                        _safeKeyService.CloseSafe(safe.Id);
                        bool needsReEncryption;
                        if (_safeKeyService.TryOpenSafe(safe, securePassword, out needsReEncryption))
                        {
                            anyOpened = true;
                            if (needsReEncryption)
                            {
                                var settings = App.Services.GetRequiredService<ISettingsService>().LoadSettingsOrDefault();
                                // Re-encrypt with updated parameters
                                byte[] key;
                                safe.SerializeableKey = SafeModel.EncryptKey(
                                    _safeKeyService.TryGetKey(safe.Id, out key) ? key : new byte[32],
                                    securePassword,
                                    _cryptoRandomService,
                                    settings.SelectedEncryptionAlgorithm,
                                    settings.SelectedKdfAlgorithm);
                                safe.RefreshModifiedAt();
                                RepositoryStorageService.TrySaveRepository(_repository);
                            }
                        }
                    }

                    if (anyOpened)
                    {
                        dialog.DialogResult = true;
                        dialog.Close();
                        SetStatus("安全箱已解锁。");
                        // Decrypt titles for all safe notes whose safe is now open
                        foreach (var note in _repository.Notes)
                        {
                            if (note.SafeId.HasValue && _safeKeyService.IsSafeOpen(note.SafeId.Value) && !string.IsNullOrEmpty(note.HtmlContent))
                            {
                                string decrypted = DecryptSafeNoteContent(note);
                                if (decrypted != null)
                                {
                                    string title = BuildPlainText(decrypted);
                                    if (!string.IsNullOrEmpty(title))
                                        _safeNoteTitles[note.Id] = title;
                                }
                            }
                        }
                        // Refresh the note list to show decrypted titles for all safe notes
                        RefreshNoteList(_selectedNote);
                        // Refresh the current note if it's now unlockable
                        if (_selectedNote?.SafeId.HasValue == true && _safeKeyService.IsSafeOpen(_selectedNote.SafeId.Value))
                            SelectNote(_selectedNote);

                    }
                    else
                    {
                        // Log diagnostic info to help debug safe opening failures
                        _logService.Info($"安全箱打开失败: 仓库中有 {_repository.Safes.Count} 个安全箱");
                        foreach (var s in _repository.Safes)
                        {
                            _logService.Info($"  安全箱 {s.Id}: 有密钥={!string.IsNullOrEmpty(s.SerializeableKey)}, 密钥长度={s.SerializeableKey?.Length ?? 0}");
                            if (!string.IsNullOrEmpty(s.SerializeableKey))
                            {
                                try
                                {
                                    byte[] raw = CryptoUtils.Base64StringToBytes(s.SerializeableKey);
                                    string header = CryptoUtils.BytesToString(raw).Substring(0, Math.Min(25, raw.Length));
                                    _logService.Info($"  密钥头内容: '{header}'");
                                }
                                catch (Exception ex)
                                {
                                    _logService.Error($"  密钥 Base64 解析失败", ex);
                                }
                            }
                        }
                        errorText.Text = "密码错误，无法打开安全箱。请查看日志文件获取详细信息。";
                    }
                }
                else
                {
                    // Create a new safe
                    try
                    {
                        SafeModel safe = new SafeModel();
                        var settings = App.Services.GetRequiredService<ISettingsService>().LoadSettingsOrDefault();
                        string algorithm = settings.SelectedEncryptionAlgorithm;
                        string kdfAlgorithm = settings.SelectedKdfAlgorithm;

                        // Generate a 256-bit random key
                        byte[] key = _cryptoRandomService.GetRandomBytes(32);
                        safe.SerializeableKey = SafeModel.EncryptKey(key, securePassword, _cryptoRandomService, algorithm, kdfAlgorithm);

                        // Verify the key can be decrypted
                        bool dummyNeedsReEncryption;
                        if (!_safeKeyService.TryOpenSafe(safe, securePassword, out dummyNeedsReEncryption))
                        {
                            errorText.Text = "安全箱创建失败，无法验证密钥。";
                            return;
                        }

                        _repository.Safes.Add(safe);
                        RepositoryStorageService.TrySaveRepository(_repository);
                        dialog.DialogResult = true;
                        dialog.Close();
                        SetStatus("安全箱已创建并解锁。");
                    }
                    catch (Exception ex)
                    {
                        errorText.Text = "创建安全箱失败：" + ex.Message;
                    }
                }

                securePassword.Clear();
            };

            dialog.ShowDialog();
        }

        /// <summary>
        /// Decrypts the content of a safe note using the safe key from SafeKeyService.
        /// </summary>
        private string DecryptSafeNoteContent(NoteModel note)
        {
            if (!note.SafeId.HasValue || string.IsNullOrEmpty(note.HtmlContent))
                return null;

            byte[] safeKey;
            if (!_safeKeyService.TryGetKey(note.SafeId.Value, out safeKey))
                return null;

            try
            {
                Cryptor cryptor = new Cryptor(NoteModel.CryptorPackageName, null);
                byte[] binaryContent = CryptoUtils.Base64StringToBytes(note.HtmlContent);
                byte[] unlockedBinary = cryptor.Decrypt(binaryContent, safeKey);
                return CryptoUtils.BytesToString(unlockedBinary);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Encrypts the content of a safe note using the safe key from SafeKeyService.
        /// </summary>
        private string EncryptSafeNoteContent(string unlockedContent)
        {
            if (_selectedNote == null || !_selectedNote.SafeId.HasValue)
                return unlockedContent;

            byte[] safeKey;
            if (!_safeKeyService.TryGetKey(_selectedNote.SafeId.Value, out safeKey))
                return unlockedContent;

            try
            {
                string algorithm = App.Services.GetRequiredService<ISettingsService>().LoadSettingsOrDefault().SelectedEncryptionAlgorithm;
                Cryptor cryptor = new Cryptor(NoteModel.CryptorPackageName, _cryptoRandomService);
                byte[] binaryContent = CryptoUtils.StringToBytes(unlockedContent);
                byte[] lockedBinary = cryptor.Encrypt(binaryContent, safeKey, algorithm, null);
                return CryptoUtils.BytesToBase64String(lockedBinary);
            }
            catch
            {
                return unlockedContent;
            }
        }

        /// <summary>
        /// Scans the current editor document for checklist CheckBox controls and hooks events
        /// to auto-save when toggled.
        /// </summary>
        private void HookChecklistEvents()
        {
            foreach (Block block in EditorView.Editor.Document.Blocks)
            {
                Paragraph p = block as Paragraph;
                if (p == null) continue;
                string tag = p.Tag as string;
                if (tag != "checklist" && tag != "checklist-done") continue;

                foreach (Inline inline in p.Inlines)
                {
                    InlineUIContainer icu = inline as InlineUIContainer;
                    CheckBox cb = icu?.Child as CheckBox;
                    if (cb != null)
                    {
                        cb.Checked -= ChecklistCheckBox_Changed;
                        cb.Unchecked -= ChecklistCheckBox_Changed;
                        cb.Checked += ChecklistCheckBox_Changed;
                        cb.Unchecked += ChecklistCheckBox_Changed;
                    }
                }
            }
        }

        /// <summary>
        /// Handles checklist checkbox toggle events and saves the note.
        /// When checked, applies strikethrough to the text; when unchecked, removes it.
        /// </summary>
        private void ChecklistCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            if (cb == null) return;

            // Find the parent paragraph
            Paragraph paragraph = FindParentParagraph(cb);
            if (paragraph != null)
            {
                bool isChecked = cb.IsChecked == true;
                paragraph.Tag = isChecked ? "checklist-done" : "checklist";

                // Apply or remove strikethrough on all Run elements in the paragraph
                foreach (Inline inline in paragraph.Inlines)
                {
                    Run run = inline as Run;
                    if (run != null)
                    {
                        if (isChecked)
                            run.TextDecorations = TextDecorations.Strikethrough;
                        else
                            run.TextDecorations = null;
                    }
                }
            }

            SaveSelectedNote(false);
        }

        /// <summary>
        /// Finds the parent Paragraph of a given CheckBox via the logical tree.
        /// CheckBox → InlineUIContainer → Paragraph
        /// </summary>
        private static Paragraph FindParentParagraph(CheckBox cb)
        {
            InlineUIContainer icu = cb.Parent as InlineUIContainer;
            if (icu != null)
                return icu.Parent as Paragraph;
            return null;
        }

        /// <summary>
        /// Handles Enter key in checklist paragraphs to insert a new unchecked checklist item.
        /// </summary>
        private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift)
            {
                Paragraph currentParagraph = EditorView.Editor.Selection.Start.Paragraph;
                if (currentParagraph != null)
                {
                    string tag = currentParagraph.Tag as string;
                    if (tag == "checklist" || tag == "checklist-done")
                    {
                        e.Handled = true;

                        // Create new unchecked checklist item
                        Paragraph newItem = CreateNewChecklistItem();

                        // Insert after current paragraph
                        Block nextBlock = currentParagraph.NextBlock;
                        if (nextBlock != null)
                            EditorView.Editor.Document.Blocks.InsertBefore(nextBlock, newItem);
                        else
                            EditorView.Editor.Document.Blocks.Add(newItem);

                        // Move caret to the new paragraph
                        EditorView.Editor.CaretPosition = newItem.ContentStart;
                        EditorView.Editor.Focus();

                        // Hook events for the new checkbox
                        foreach (Inline inline in newItem.Inlines)
                        {
                            InlineUIContainer icu = inline as InlineUIContainer;
                            CheckBox cb = icu?.Child as CheckBox;
                            if (cb != null)
                            {
                                cb.Checked -= ChecklistCheckBox_Changed;
                                cb.Unchecked -= ChecklistCheckBox_Changed;
                                cb.Checked += ChecklistCheckBox_Changed;
                                cb.Unchecked += ChecklistCheckBox_Changed;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates a new empty checklist paragraph with an unchecked CheckBox.
        /// </summary>
        private static Paragraph CreateNewChecklistItem()
        {
            Paragraph p = new Paragraph
            {
                Tag = "checklist"
            };

            CheckBox cb = new CheckBox
            {
                IsChecked = false,
                Tag = "checklist-cb",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            };
            p.Inlines.Add(new InlineUIContainer(cb) { Tag = "checklist-icu" });

            return p;
        }

        private void ExecuteEditorCommand(RoutedUICommand command)
        {
            if (EditorView.Editor.IsReadOnly || _selectedNote == null)
                return;

            EditorView.Editor.Focus();
            command.Execute(null, EditorView.Editor);
        }

        private void NewNote_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            NewNoteButton_Click(sender, e);
        }

        private void SaveNote_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveSelectedNote();
        }

        private void FocusSearch_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Sidebar.SearchTextBox.Focus();
            Sidebar.SearchTextBox.SelectAll();
        }

        private void DeleteNote_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            DeleteNoteButton_Click(sender, e);
        }

        private void Bold_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ExecuteEditorCommand(EditingCommands.ToggleBold);
        }

        private void Italic_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ExecuteEditorCommand(EditingCommands.ToggleItalic);
        }

        private void Underline_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ExecuteEditorCommand(EditingCommands.ToggleUnderline);
        }

        private bool MatchesSearch(NoteModel note)
        {
            if (string.IsNullOrWhiteSpace(_searchText))
                return true;

            string haystack = BuildPlainText(note.HtmlContent);
            foreach (string tag in note.Tags)
                haystack += " " + tag;

            return haystack.IndexOf(_searchText, StringComparison.InvariantCultureIgnoreCase) >= 0;
        }

        private bool MatchesTag(NoteModel note)
        {
            if (string.IsNullOrEmpty(_selectedTag))
                return true;

            return note.Tags.Any(tag => string.Equals(tag, _selectedTag, StringComparison.InvariantCultureIgnoreCase));
        }

        private void LoadEditorMetadata(NoteModel note)
        {
            _loadingEditorMetadata = true;
            try
            {
                EditorView.TagsText = string.Empty;
                EditorView.IsDeleteMode = false;
                EditorView.Toolbar.PinnedCheckBoxControl.IsChecked = note != null && note.IsPinned;
                UpdateTagSuggestions();
            }
            finally
            {
                _loadingEditorMetadata = false;
            }
        }

        private static List<string> ParseTags(string tagsText)
        {
            return (tagsText ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim())
                .Where(tag => tag.Length > 0)
                .Distinct(StringComparer.InvariantCultureIgnoreCase)
                .OrderBy(tag => tag, StringComparer.InvariantCultureIgnoreCase)
                .ToList();
        }

        private static bool TagsEqual(IList<string> first, IList<string> second)
        {
            if (first.Count != second.Count)
                return false;

            for (int index = 0; index < first.Count; index++)
            {
                if (!string.Equals(first[index], second[index], StringComparison.InvariantCultureIgnoreCase))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Applies font scale to the editor. Headings scale proportionally.
        /// Base font size is 15pt. Headings: H1=26, H2=22, H3=18 (at scale 1.0).
        /// </summary>
        private void RefreshVisibleSelectedItemTitle()
        {
            NoteListItem selectedItem = Sidebar.NotesList.SelectedItem as NoteListItem;
            if (selectedItem == null)
                return;

            // For safe notes with cached decrypted title, preserve it instead of using encrypted content
            string cachedTitle;
            if (selectedItem.Note.SafeId.HasValue && _safeNoteTitles.TryGetValue(selectedItem.Note.Id, out cachedTitle))
            {
                selectedItem.SetCustomTitle(cachedTitle);
            }
            else
            {
                selectedItem.RefreshDisplay();
            }
            Sidebar.NotesList.Items.Refresh();
            EditorView.EditorTitleText.Text = selectedItem.Title;
        }

        private void SetStatus(string message, bool isError = false)
        {
            StatusBar.SetMessage(message, isError);
        }

        private static string BuildTitle(NoteModel note)
        {
            // Try to extract first heading (H1/H2/H3) as title
            string heading = ExtractFirstHeading(note.HtmlContent);
            if (!string.IsNullOrWhiteSpace(heading))
                return heading.Length > 80 ? heading.Substring(0, 80) + "..." : heading;

            // Fallback to first line of plain text
            string text = BuildPlainText(note.HtmlContent);
            if (string.IsNullOrWhiteSpace(text))
                return "无标题笔记";
            return text.Length > 80 ? text.Substring(0, 80) + "..." : text;
        }

        private static string ExtractFirstHeading(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return null;

            // Match <h1>...</h1>, <h2>...</h2>, or <h3>...</h3>
            var match = Regex.Match(html, @"<h[123][^>]*>(.*?)</h[123]>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success)
            {
                string headingHtml = match.Groups[1].Value;
                string headingText = Regex.Replace(headingHtml, "<.*?>", " ");
                headingText = WebUtility.HtmlDecode(headingText);
                headingText = Regex.Replace(headingText, "\\s+", " ").Trim();
                return string.IsNullOrWhiteSpace(headingText) ? null : headingText;
            }
            return null;
        }

        private static string BuildPlainText(string html)
        {
            string withoutTags = Regex.Replace(html ?? string.Empty, "<.*?>", " ");
            string decoded = WebUtility.HtmlDecode(withoutTags);
            return Regex.Replace(decoded, "\\s+", " ").Trim();
        }

        private static bool IsSyncErrorMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;
            return message.Contains("失败")
                || message.Contains("错误")
                || message.Contains("无效")
                || message.Contains("请先")
                || message.Contains("没有找到");
        }

        /// <summary>
        /// Extracts the body line for note list display.
        /// Shows the first non-heading paragraph content.
        /// </summary>
        private static string BuildBodyLine(NoteModel note)
        {
            if (string.IsNullOrWhiteSpace(note.HtmlContent))
                return null;

            // Remove headings and get remaining content
            string withoutHeadings = Regex.Replace(note.HtmlContent, @"<h[123][^>]*>.*?</h[123]>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            string plainText = BuildPlainText(withoutHeadings);

            if (string.IsNullOrWhiteSpace(plainText))
                return null;

            // Take first 60 characters as body preview
            return plainText.Length > 60 ? plainText.Substring(0, 60) + "..." : plainText;
        }

        private sealed class NoteListItem
        {
            public NoteListItem(NoteModel note)
            {
                Note = note;
                RefreshDisplay();
            }

            public NoteModel Note { get; }

            public string Title { get; private set; }

            public string BodyLine { get; private set; }

            public string SecondaryLine { get; private set; }

            public bool IsPinned { get { return Note.IsPinned; } }

            public void RefreshDisplay()
            {
                Title = BuildTitle(Note);
                BodyLine = BuildBodyLine(Note);
                List<string> parts = new List<string>();
                if (Note.NoteType == NoteType.Checklist)
                    parts.Add("清单");
                if (Note.IsPinned)
                    parts.Add("置顶");
                if (Note.Tags.Count > 0)
                    parts.Add(string.Join(" · ", Note.Tags));
                parts.Add(Note.ModifiedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
                SecondaryLine = string.Join("  ·  ", parts);
            }

            public void SetCustomTitle(string customTitle)
            {
                Title = string.IsNullOrWhiteSpace(customTitle) ? "无标题笔记" :
                    (customTitle.Length > 80 ? customTitle.Substring(0, 80) + "..." : customTitle);
            }
        }
    }
}
