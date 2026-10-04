using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using SilentNotes.Crypto;
using SilentNotes.Models;
using SilentNotes.Services;
using SilentNotes.WindowsWinForms.Controls;
using SilentNotes.WindowsWinForms.Services;
using SilentNotes.WindowsWinForms.Workers;
using Sunny.UI;
using VanillaCloudStorageClient;

namespace SilentNotes.WindowsWinForms.Views
{
    /// <summary>
    /// WinForms port of the WPF MainWindow: two-column workspace (find notes on the
    /// left, edit on the right), non-blocking status bar feedback, dangerous actions
    /// only in recycle-bin mode. Content is built on SunnyUI controls (each marked
    /// Style=Custom and colored via WinFormsThemeService) on a native-frame window,
    /// so the system title bar, caption buttons and window animations stay intact.
    /// </summary>
    public class MainForm : Form
    {
        private readonly WindowsSynchronizationService _syncService;
        private readonly IInternetStateService _internetStateService;
        private readonly ISafeKeyService _safeKeyService;
        private readonly ICryptoRandomService _cryptoRandomService;
        private readonly ILogService _logService;
        private readonly HtmlCompatibilityInspector _htmlCompatibilityInspector = new HtmlCompatibilityInspector();
        private System.Threading.Timer _autoSyncTimer;

        private NoteRepositoryModel _repository;
        private NoteModel _selectedNote;
        private NoteModel _pendingSelectNote;
        private bool _loadingSelection;
        private bool _loadingEditorMetadata;
        private bool _contentDirty;
        private bool _showRecycleBin;
        private string _searchText = string.Empty;
        private string _selectedTag;
        private string _editorTitleBase = "编辑器";
        private List<string> _tagSuggestions = new List<string>();
        private readonly Dictionary<Guid, string> _safeNoteTitles = new Dictionary<Guid, string>();
        private List<NoteListItem> _listItems = new List<NoteListItem>();

        // SunnyUI controls do not inherit the container font; assign explicitly.
        private static readonly Font UIAppFont = new Font("Microsoft YaHei UI", 9F);
        private static readonly Font UISmallFont = new Font("Microsoft YaHei UI", 8.5F);
        private static readonly Font UIEditorTitleFont = new Font("Microsoft YaHei UI", 13.5F, FontStyle.Bold);
        private static readonly Font UIListTitleFont = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
        private static readonly Font UIListBodyFont = new Font("Microsoft YaHei UI", 8F);
        private readonly UIToolTip _toolTip = new UIToolTip();

        // Top bar
        private UILabel _repositorySummaryLabel;
        private UIButton _safeButton;
        private UIButton _syncButton;
        private UIButton _settingsButton;
        private UIButton _moreButton;
        private UIButton _saveButton;
        private UIContextMenuStrip _moreMenu;

        // Sidebar
        private Panel _modeTogglePanel;
        private Panel _activeModeButtons;
        private Panel _recycleBinModeButtons;
        private SegmentedToggle _modeToggle;
        private UIButton _newNoteButton;
        private UIButton _newChecklistButton;
        private UIButton _deleteNoteButton;
        private UIButton _restoreButton;
        private UIButton _permanentDeleteButton;
        private UIButton _emptyBinButton;
        private UITextBox _searchBox;
        private FlowLayoutPanel _tagPanel;
        private UILabel _emptyListLabel;
        private UIListBox _notesList;

        // Editor area
        private UILabel _editorTitleLabel;
        private UILabel _editorInfoLabel;
        private UISymbolButton _pinnedButton;
        private bool _pinnedState;
        private UITextBox _tagsTextBox;
        private FlowLayoutPanel _noteTagPanel;
        private UIListBox _tagSuggestionList;
        private bool _loadingTagSuggestions;
        private UIPanel _toolbar;
        private HtmlEditorControl _editor;
        // Formatting toolbar buttons that mirror an editor state, keyed by
        // queryCommandState command ("Bold", "InsertUnorderedList"...) or block tag
        // ("H1", "BLOCKQUOTE", "PRE") for the FormatBlock buttons.
        private readonly Dictionary<string, UIButton> _toolbarStateButtons = new Dictionary<string, UIButton>();
        private bool _toolbarStatePending;
        private bool _wordCountPending;

        // Status bar
        private UIPanel _statusPanel;
        private UILabel _statusLabel;
        private UILabel _wordCountLabel;

        public MainForm()
        {
            Text = "SilentNotes";
            Font = new Font("Microsoft YaHei UI", 9F);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 520);
            Size = new Size(1120, 760);

            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            _syncService = new WindowsSynchronizationService(
                Program.Services.GetRequiredService<ISettingsService>(),
                Program.Services.GetRequiredService<IRepositoryStorageService>(),
                Program.Services.GetRequiredService<IDataProtectionService>(),
                Program.Services.GetRequiredService<ICryptoRandomService>(),
                Program.Services.GetRequiredService<ILogService>(),
                Program.Services.GetRequiredService<IXmlFileService>());
            _internetStateService = Program.Services.GetRequiredService<IInternetStateService>();
            _safeKeyService = Program.Services.GetRequiredService<ISafeKeyService>();
            _cryptoRandomService = Program.Services.GetRequiredService<ICryptoRandomService>();
            _logService = Program.Services.GetRequiredService<ILogService>();

            // Periodic auto-sync timer (every 30 minutes)
            _autoSyncTimer = new System.Threading.Timer(
                async _ => await TryAutoSyncAsync(false),
                null,
                System.Threading.Timeout.Infinite,
                System.Threading.Timeout.Infinite);

            BuildUi();
            WireEvents();

            // Kill the native ListBox erase-then-redraw flicker on hover/selection by
            // making the painting composited (double buffered) at the Win32 level.
            // Only the inner listbox: compositing the outer panel too makes scrolling janky.
            EnableComposited(_notesList.ListBox);
        }

        private IRepositoryStorageService RepositoryStorageService
        {
            get { return Program.Services.GetRequiredService<IRepositoryStorageService>(); }
        }

        private WinFormsThemeService ThemeService
        {
            get { return Program.Services.GetRequiredService<WinFormsThemeService>(); }
        }

        #region UI construction

        private void BuildUi()
        {
            BuildStatusBar();
            BuildTopBar();
            BuildSplit();
            BuildMoreMenu();
        }

        private void BuildStatusBar()
        {
            _statusPanel = new UIPanel { Dock = DockStyle.Bottom, Height = 30, Tag = "panel", Radius = 0 };
            _wordCountLabel = new UILabel
            {
                Dock = DockStyle.Right,
                Width = 90,
                Text = string.Empty,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 10, 0),
                Font = UISmallFont,
                Tag = "secondary",
            };
            _statusLabel = new UILabel
            {
                Dock = DockStyle.Fill,
                Text = "就绪",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Font = UISmallFont,
                Tag = "secondary",
            };
            _statusPanel.Controls.Add(_statusLabel);
            _statusPanel.Controls.Add(_wordCountLabel);
            Controls.Add(_statusPanel);
        }

        private void BuildTopBar()
        {
            UIPanel topPanel = new UIPanel { Dock = DockStyle.Top, Height = 52, Tag = "window", Radius = 0 };

            _repositorySummaryLabel = new UILabel
            {
                Text = string.Empty,
                AutoSize = false,
                Width = 480,
                Height = 34,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom,
                Font = UISmallFont,
                Tag = "secondary",
            };

            UIPanel topButtons = new UIPanel { Dock = DockStyle.Right, Width = 264, Radius = 0, Tag = "window" };
            _saveButton = CreateTextButton("保存", 0, "action", 64, 32);
            _moreButton = CreateIconButton(FontAwesomeIcons.fa_ellipsis_h, "更多", "ghost", 30, 30);
            _settingsButton = CreateIconButton(FontAwesomeIcons.fa_cog, "设置", "ghost", 30, 30);
            _safeButton = CreateIconButton(FontAwesomeIcons.fa_lock, "安全箱", "ghost", 30, 30);
            _syncButton = CreateIconButton(FontAwesomeIcons.fa_refresh, "同步", "ghost", 30, 30);
            UIButton[] visualOrderRightToLeft = new UIButton[]
            {
                _saveButton, _moreButton, _settingsButton, _safeButton, _syncButton,
            };
            foreach (UIButton button in visualOrderRightToLeft)
                topButtons.Controls.Add(button);
            EventHandler relayout = delegate
            {
                int x = topButtons.ClientSize.Width - 8;
                foreach (UIButton button in visualOrderRightToLeft)
                {
                    button.Left = x - button.Width;
                    button.Top = Math.Max(2, (topButtons.ClientSize.Height - button.Height) / 2);
                    x = button.Left - 8;
                }
            };
            topButtons.Resize += relayout;
            topButtons.HandleCreated += relayout;

            _repositorySummaryLabel.Dock = DockStyle.Fill;
            topPanel.Controls.Add(_repositorySummaryLabel);
            topPanel.Controls.Add(topButtons);
            Controls.Add(topPanel);
            topPanel.BringToFront();
            _statusPanel.SendToBack();
        }

        private void BuildMoreMenu()
        {
            _moreMenu = new UIContextMenuStrip { Style = UIStyle.Custom, Font = UIAppFont };
            AppendMenuItem(_moreMenu, "重载仓库", FontAwesomeIcons.fa_refresh, ReloadButton_Click);
            AppendMenuItem(_moreMenu, "恢复备份", FontAwesomeIcons.fa_history, RestoreBackupButton_Click);
            AppendMenuItem(_moreMenu, "帮助与快捷键", FontAwesomeIcons.fa_question_circle, HelpButton_Click);
            AppendMenuItem(_moreMenu, "切换浅色/深色", FontAwesomeIcons.fa_adjust, ThemeToggleButton_Click);
            _moreButton.Click += delegate
            {
                _moreMenu.Show(_moreButton, new Point(_moreButton.Width - _moreMenu.Width, _moreButton.Height + 2));
            };
        }

        private void AppendMenuItem(UIContextMenuStrip menu, string text, int symbol, EventHandler onClick)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text)
            {
                // The symbol is kept so RefreshMoreMenuTheme can rebuild the bitmap
                // with current palette colors on every theme change.
                Tag = symbol,
                Image = FontImageHelper.CreateImage(symbol, 14, ThemeService.TextSecondary),
            };
            item.Click += onClick;
            menu.Items.Add(item);
        }

        private void RefreshMoreMenuTheme()
        {
            // The context menu is not in the form's control tree, so the theme walk
            // never reaches it; colors and the prerendered icon bitmaps are stale
            // after a theme switch unless refreshed here.
            WinFormsThemeService theme = ThemeService;
            _moreMenu.BackColor = theme.SurfacePaper;
            _moreMenu.ForeColor = theme.TextPrimary;
            foreach (ToolStripItem item in _moreMenu.Items)
            {
                item.ForeColor = theme.TextPrimary;
                if (item.Tag is int)
                    item.Image = FontImageHelper.CreateImage((int)item.Tag, 14, theme.TextSecondary);
            }
        }

        private UIButton CreateIconButton(int symbol, string tip, string tag, int width, int height)
        {
            UIButton button = new UISymbolButton
            {
                Symbol = symbol,
                SymbolSize = 16,
                Tag = tag ?? string.Empty,
                Radius = 6,
                Font = UIAppFont,
            };
            if (!string.IsNullOrEmpty(tip))
                _toolTip.SetToolTip(button, tip);
            if (width > 0)
            {
                button.Width = width;
                button.Height = height;
            }
            return button;
        }

        private UIButton CreateTextButton(string text, int symbol, string tag, int width, int height)
        {
            UIButton button = symbol != 0
                ? new UISymbolButton { Symbol = symbol, SymbolSize = 16 }
                : new UIButton();
            button.Text = text;
            button.Tag = tag ?? string.Empty;
            button.Radius = 6;
            button.Font = UIAppFont;
            if (width > 0)
            {
                button.Width = width;
                button.Height = height;
            }
            else
            {
                Size preferred = TextRenderer.MeasureText(text, UIAppFont);
                button.Width = preferred.Width + (symbol != 0 ? 40 : 26);
                button.Height = height > 0 ? height : 30;
            }
            return button;
        }

        private void BuildSplit()
        {
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterWidth = 1,
            };
            BuildSidebar(split.Panel1);
            BuildEditorArea(split.Panel2);
            // Min sizes and SplitterDistance are applied in OnLoad: at this point the
            // control still has its default size and setting them throws.
            Controls.Add(split);
            split.BringToFront();
            _splitContainer = split;
        }

        private void ApplySplitLayout()
        {
            _splitContainer.Panel1MinSize = 240;
            _splitContainer.Panel2MinSize = 320;
            _splitContainer.FixedPanel = FixedPanel.Panel1;
            int maxDistance = _splitContainer.Width - _splitContainer.Panel2MinSize - _splitContainer.SplitterWidth;
            if (maxDistance >= _splitContainer.Panel1MinSize)
                _splitContainer.SplitterDistance = Math.Min(300, maxDistance);
        }

        private SplitContainer _splitContainer;

        private void BuildSidebar(Panel panel)
        {
            panel.Padding = new Padding(10, 6, 10, 6);

            // SunnyUI UIListBox: its own OnDrawItem paints background and item text,
            // then raises DrawItem, so our handler repaints the full row rect (which
            // covers the default text) with the two-line note card.
            _notesList = new UIListBox
            {
                Dock = DockStyle.Fill,
                ItemHeight = 56,
                Style = UIStyle.Custom,
                Font = UIAppFont,
            };

            _emptyListLabel = new UILabel
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "还没有笔记。",
                Visible = false,
                Font = UIAppFont,
                Tag = "secondary",
            };

            // Tag filter pills live in a clipped host: AutoScroll's system horizontal
            // scrollbar falls outside the host and stays hidden (Shift+wheel scrolls).
            Panel tagPanelHost = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.Transparent, Tag = "bare" };
            _tagPanel = new FlowLayoutPanel
            {
                Top = 0,
                Left = 0,
                Width = 300,
                Height = 60,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(0, 2, 0, 2),
                BackColor = Color.Transparent,
                Font = UIAppFont,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            tagPanelHost.Controls.Add(_tagPanel);
            tagPanelHost.Resize += delegate { _tagPanel.Width = tagPanelHost.ClientSize.Width; };

            _searchBox = new UITextBox
            {
                Dock = DockStyle.Top,
                Height = 34,
                Watermark = "搜索笔记",
                Symbol = FontAwesomeIcons.fa_search,
                SymbolSize = 16,
                Font = UIAppFont,
            };

            // Mode toggle stays visible in both modes so there is always a way back.
            _modeTogglePanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = Color.Transparent,
                Padding = new Padding(4, 5, 4, 5),
            };
            _modeToggle = new SegmentedToggle(new[] { "活动笔记", "回收站" })
            {
                Dock = DockStyle.Fill,
                Font = UIAppFont,
            };
            _modeTogglePanel.Controls.Add(_modeToggle);

            _activeModeButtons = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.Transparent };
            _recycleBinModeButtons = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.Transparent, Visible = false };

            // Docking order: last added ends up topmost.
            panel.Controls.Add(_notesList);
            panel.Controls.Add(_emptyListLabel);
            panel.Controls.Add(tagPanelHost);
            panel.Controls.Add(_searchBox);
            panel.Controls.Add(_activeModeButtons);
            panel.Controls.Add(_recycleBinModeButtons);
            panel.Controls.Add(_modeTogglePanel);

            BuildActiveModeButtons();
            BuildRecycleBinModeButtons();
        }

        private void BuildActiveModeButtons()
        {
            _newNoteButton = CreateTextButton("新建笔记", FontAwesomeIcons.fa_plus, "action", 0, 30);
            _newNoteButton.Click += NewNoteButton_Click;
            _newChecklistButton = CreateTextButton("清单", FontAwesomeIcons.fa_list_ul, "window", 0, 30);
            _newChecklistButton.Click += NewChecklistButton_Click;
            _deleteNoteButton = CreateIconButton(FontAwesomeIcons.fa_trash, "移到回收站（Delete）", "ghost", 30, 30);
            _deleteNoteButton.Click += DeleteNoteButton_Click;
            FlowLayoutPanel row = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                BackColor = Color.Transparent,
                Font = UIAppFont,
            };
            row.Controls.Add(_newNoteButton);
            row.Controls.Add(_newChecklistButton);
            row.Controls.Add(_deleteNoteButton);
            _activeModeButtons.Controls.Add(row);
        }

        private void BuildRecycleBinModeButtons()
        {
            _restoreButton = CreateTextButton("恢复", FontAwesomeIcons.fa_undo, "window", 0, 30);
            _toolTip.SetToolTip(_restoreButton, "恢复选中的笔记");
            _restoreButton.Click += RestoreNoteButton_Click;
            _permanentDeleteButton = CreateTextButton("永久删除", FontAwesomeIcons.fa_trash, "danger", 0, 30);
            _toolTip.SetToolTip(_permanentDeleteButton, "彻底删除选中的笔记，不可恢复");
            _permanentDeleteButton.Click += PermanentDeleteNoteButton_Click;
            _emptyBinButton = CreateTextButton("清空", FontAwesomeIcons.fa_recycle, "danger", 0, 30);
            _toolTip.SetToolTip(_emptyBinButton, "清空回收站中的全部笔记");
            _emptyBinButton.Click += EmptyRecycleBinButton_Click;
            FlowLayoutPanel row = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                BackColor = Color.Transparent,
                Font = UIAppFont,
            };
            row.Controls.Add(_restoreButton);
            row.Controls.Add(_permanentDeleteButton);
            row.Controls.Add(_emptyBinButton);
            _recycleBinModeButtons.Controls.Add(row);
        }

        private void BuildEditorArea(Panel panel)
        {
            _editor = new HtmlEditorControl { Dock = DockStyle.Fill };

            // Title row: note title + pinned toggle
            Panel titleRow = new Panel { Dock = DockStyle.Top, Height = 40 };
            _pinnedButton = (UISymbolButton)CreateIconButton(FontAwesomeIcons.fa_star, "置顶", "ghost", 28, 28);
            _pinnedButton.Click += delegate
            {
                if (_loadingEditorMetadata || _selectedNote == null || _showRecycleBin)
                    return;
                _pinnedState = !_pinnedState;
                RefreshPinnedButton();
                SaveSelectedMetadata();
            };
            _editorTitleLabel = new UILabel
            {
                Dock = DockStyle.Fill,
                Text = "编辑器",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 4, 0, 0),
                Font = UIEditorTitleFont,
                AutoEllipsis = true,
            };
            _pinnedButton.Dock = DockStyle.Right;
            titleRow.Controls.Add(_editorTitleLabel);
            titleRow.Controls.Add(_pinnedButton);

            _editorInfoLabel = new UILabel
            {
                Dock = DockStyle.Top,
                Height = 22,
                Text = "请选择一条笔记。",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0),
                Font = UISmallFont,
                Tag = "secondary",
            };

            // Tag row: caption, input, current note's tags as removable chips
            Panel tagRow = new Panel { Dock = DockStyle.Top, Height = 36 };
            _noteTagPanel = new FlowLayoutPanel
            {
                WrapContents = false,
                AutoScroll = false,
                Padding = new Padding(4, 4, 4, 2),
                BackColor = Color.Transparent,
                Font = UIAppFont,
            };
            _tagsTextBox = new UITextBox
            {
                Width = 180,
                Watermark = "输入标签后回车",
                Anchor = AnchorStyles.Left,
                Font = UIAppFont,
            };
            UILabel tagCaption = new UILabel
            {
                Text = "标签",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Tag = "secondary",
            };
            tagCaption.Font = new Font("Microsoft YaHei UI", 9F);
            tagRow.Controls.Add(_noteTagPanel);
            tagRow.Controls.Add(_tagsTextBox);
            tagRow.Controls.Add(tagCaption);
            tagRow.Resize += delegate
            {
                tagCaption.Left = 12;
                tagCaption.Top = (tagRow.ClientSize.Height - tagCaption.Height) / 2;
                _tagsTextBox.Left = tagCaption.Right + 8;
                _tagsTextBox.Top = (tagRow.ClientSize.Height - _tagsTextBox.Height) / 2;
                _noteTagPanel.Left = _tagsTextBox.Right + 8;
                _noteTagPanel.Top = 0;
                _noteTagPanel.Width = Math.Max(0, tagRow.ClientSize.Width - _noteTagPanel.Left - 8);
                _noteTagPanel.Height = tagRow.ClientSize.Height;
            };

            _tagSuggestionList = new UIListBox
            {
                Dock = DockStyle.Top,
                Height = 70,
                ItemHeight = 28,
                Style = UIStyle.Custom,
                Visible = false,
                Font = UIAppFont,
            };

            // Format toolbar: icon buttons grouped by UILine separators. Buttons that
            // mirror an editor state pass their command/block-tag as the last argument.
            _toolbar = new UIPanel { Dock = DockStyle.Top, Height = 44, Tag = "window", Radius = 0 };
            FlowLayoutPanel toolbarRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(6, 8, 6, 6), BackColor = Color.Transparent };
            AddToolbarTextButton(toolbarRow, "H1", "标题 1", delegate { _editor.SetHeading(1, true); }, "H1");
            AddToolbarTextButton(toolbarRow, "H2", "标题 2", delegate { _editor.SetHeading(2, true); }, "H2");
            AddToolbarTextButton(toolbarRow, "H3", "标题 3", delegate { _editor.SetHeading(3, true); }, "H3");
            AddToolbarSeparator(toolbarRow);
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_bold, "粗体 (Ctrl+B)", delegate { _editor.ToggleBold(); }, "Bold");
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_italic, "斜体 (Ctrl+I)", delegate { _editor.ToggleItalic(); }, "Italic");
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_underline, "下划线 (Ctrl+U)", delegate { _editor.ToggleUnderline(); }, "Underline");
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_strikethrough, "删除线", delegate { _editor.ToggleStrikethrough(); }, "StrikeThrough");
            AddToolbarSeparator(toolbarRow);
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_list_ul, "无序列表", delegate { _editor.ToggleUnorderedList(); }, "InsertUnorderedList");
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_list_ol, "有序列表", delegate { _editor.ToggleOrderedList(); }, "InsertOrderedList");
            AddToolbarSeparator(toolbarRow);
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_quote_left, "引用", delegate { _editor.SetBlockquote(true); }, "BLOCKQUOTE");
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_code, "代码块", delegate { _editor.SetCodeBlock(true); }, "PRE");
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_minus, "分割线", delegate { _editor.InsertHorizontalRule(); }, null);
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_link, "链接", delegate { LinkButton_Click(); }, null);
            AddToolbarSeparator(toolbarRow);
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_undo, "撤销 (Ctrl+Z)", delegate { _editor.Undo(); }, null);
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_repeat, "重做 (Ctrl+Y)", delegate { _editor.Redo(); }, null);
            _toolbar.Controls.Add(toolbarRow);

            panel.Controls.Add(_editor);
            panel.Controls.Add(_toolbar);
            panel.Controls.Add(_tagSuggestionList);
            panel.Controls.Add(tagRow);
            panel.Controls.Add(_editorInfoLabel);
            panel.Controls.Add(titleRow);
        }

        private void AddToolbarIconButton(FlowLayoutPanel toolbar, int symbol, string tip, EventHandler onClick, string stateKey)
        {
            UIButton button = new UISymbolButton
            {
                Symbol = symbol,
                SymbolSize = 16,
                Radius = 4,
                Width = 28,
                Height = 28,
                Tag = "tool",
                Margin = new Padding(1, 0, 1, 0),
                Font = UIAppFont,
            };
            _toolTip.SetToolTip(button, tip);
            button.Click += onClick;
            RegisterToolbarStateButton(button, stateKey);
            toolbar.Controls.Add(button);
        }

        private void AddToolbarTextButton(FlowLayoutPanel toolbar, string text, string tip, EventHandler onClick, string stateKey)
        {
            UIButton button = new UIButton
            {
                Text = text,
                Radius = 4,
                Width = 30,
                Height = 28,
                Tag = "tool",
                Margin = new Padding(1, 0, 1, 0),
                Font = UISmallFont,
            };
            _toolTip.SetToolTip(button, tip);
            button.Click += onClick;
            RegisterToolbarStateButton(button, stateKey);
            toolbar.Controls.Add(button);
        }

        private void RegisterToolbarStateButton(UIButton button, string stateKey)
        {
            if (string.IsNullOrEmpty(stateKey))
                return;
            _toolbarStateButtons[stateKey] = button;
        }

        /// <summary>Refreshes the pressed look of the stateful toolbar buttons.</summary>
        private void UpdateToolbarState()
        {
            if (!_editor.IsReady || _toolbarStateButtons.Count == 0)
                return;

            string blockTag = _editor.GetCurrentBlockTag();
            foreach (KeyValuePair<string, UIButton> pair in _toolbarStateButtons)
            {
                bool active;
                switch (pair.Key)
                {
                    case "H1":
                    case "H2":
                    case "H3":
                    case "BLOCKQUOTE":
                    case "PRE":
                        active = string.Equals(blockTag, pair.Key, StringComparison.OrdinalIgnoreCase);
                        break;
                    default:
                        active = _editor.QueryState(pair.Key);
                        break;
                }
                SetToolbarButtonActive(pair.Value, active);
            }
        }

        private void SetToolbarButtonActive(UIButton button, bool active)
        {
            UISymbolButton symbolButton = button as UISymbolButton;
            WinFormsThemeService theme = ThemeService;
            button.Style = UIStyle.Custom;
            if (active)
            {
                button.FillColor = theme.AccentSoft;
                button.RectColor = theme.Accent;
                button.ForeColor = theme.Accent;
                button.FillHoverColor = theme.AccentSoft;
                button.RectHoverColor = theme.Accent;
                button.ForeHoverColor = theme.Accent;
                if (symbolButton != null)
                {
                    symbolButton.SymbolColor = theme.Accent;
                    symbolButton.SymbolHoverColor = theme.Accent;
                    symbolButton.SymbolPressColor = theme.Accent;
                }
            }
            else
            {
                // Re-run the theme service for the quiet "tool" resting look.
                theme.Apply(button);
            }
        }

        private static void AddToolbarSeparator(FlowLayoutPanel toolbar)
        {
            UILine line = new UILine
            {
                Direction = UILine.LineDirection.Vertical,
                Width = 1,
                Height = 24,
                Margin = new Padding(6, 0, 6, 0),
            };
            toolbar.Controls.Add(line);
        }

        private void WireEvents()
        {
            _syncButton.Click += SyncButton_Click;
            _settingsButton.Click += SyncSettingsButton_Click;
            _safeButton.Click += SafeButton_Click;
            _saveButton.Click += delegate { SaveSelectedNote(); };

            _modeToggle.SelectedIndexChanged += ModeToggle_SelectedIndexChanged;
            _searchBox.TextChanged += SearchTextBox_TextChanged;
            _notesList.SelectedIndexChanged += NotesList_SelectedIndexChanged;
            _notesList.DrawItem += NotesList_DrawItem;

            _tagsTextBox.TextChanged += TagsTextBox_TextChanged;

            _editor.EditorReady += Editor_Ready;
            _editor.ContentChanged += Editor_ContentChanged;
            _editor.SelectionChanged += Editor_SelectionChanged;

            _tagsTextBox.KeyDown += TagsTextBox_KeyDown;
            _tagsTextBox.LostFocus += delegate { HideTagSuggestions(); SaveSelectedMetadata(); };
            _tagSuggestionList.SelectedIndexChanged += TagSuggestionList_SelectedIndexChanged;
            _tagSuggestionList.DrawItem += TagSuggestionList_DrawItem;
        }

        private void RefreshModeButtonColors()
        {
            _modeToggle.SelectedIndex = _showRecycleBin ? 1 : 0;
        }

        private void RefreshPinnedButton()
        {
            UISymbolButton button = _pinnedButton as UISymbolButton;
            if (button == null)
                return;

            button.Style = UIStyle.Custom;
            WinFormsThemeService theme = ThemeService;
            if (_pinnedState)
            {
                button.SymbolColor = theme.Action;
                button.SymbolHoverColor = theme.Action;
                button.RectColor = theme.AccentSoft;
                button.FillColor = theme.AccentSoft;
            }
            else
            {
                button.SymbolColor = theme.TextSecondary;
                button.SymbolHoverColor = theme.Accent;
                button.RectColor = Color.Transparent;
                button.FillColor = Color.Transparent;
            }
        }

        private void PopulateNoteTagPanel()
        {
            _noteTagPanel.Controls.Clear();
            if (_selectedNote == null || _showRecycleBin)
                return;

            foreach (string tag in _selectedNote.Tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                    continue;
                _noteTagPanel.Controls.Add(CreateNoteTagChip(tag));
            }
        }

        private Control CreateNoteTagChip(string tag)
        {
            FlowLayoutPanel chip = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0, 0, 6, 0),
                BackColor = Color.Transparent,
                Font = UIAppFont,
            };

            UIButton nameButton = new UIButton
            {
                Text = tag,
                AutoSize = false,
                Width = TextRenderer.MeasureText(tag, UIAppFont).Width + 18,
                Height = 24,
                Radius = 12,
                Style = UIStyle.Custom,
                Font = UIAppFont,
                Margin = Padding.Empty,
            };
            nameButton.Click += delegate
            {
                // Clicking a chip filters the note list by that tag
                _selectedTag = tag;
                RefreshTagButtonColors();
                RefreshNoteList(_selectedNote);
                UpdateRepositorySummary();
            };

            UISymbolButton removeButton = new UISymbolButton
            {
                Symbol = FontAwesomeIcons.fa_times,
                SymbolSize = 16,
                Width = 22,
                Height = 24,
                Radius = 12,
                Style = UIStyle.Custom,
                Margin = new Padding(0, 0, 0, 0),
                Font = UIAppFont,
            };
            _toolTip.SetToolTip(removeButton, "删除标签 \"" + tag + "\"");
            removeButton.Click += delegate { DeleteTagByName(tag); };

            chip.Controls.Add(nameButton);
            chip.Controls.Add(removeButton);
            return chip;
        }

        private void RefreshNoteTagChipColors()
        {
            WinFormsThemeService theme = ThemeService;
            foreach (Control child in _noteTagPanel.Controls)
            {
                FlowLayoutPanel chip = child as FlowLayoutPanel;
                if (chip == null)
                    continue;

                foreach (Control part in chip.Controls)
                {
                    UISymbolButton removeButton = part as UISymbolButton;
                    if (removeButton != null)
                    {
                        removeButton.SymbolColor = theme.TextSecondary;
                        removeButton.SymbolHoverColor = theme.Danger;
                        removeButton.SymbolPressColor = theme.Danger;
                        removeButton.FillColor = theme.SurfacePaper;
                        removeButton.RectColor = theme.SurfacePaper;
                        removeButton.FillHoverColor = theme.DangerSoft;
                        removeButton.RectHoverColor = theme.DangerSoft;
                        continue;
                    }

                    UIButton nameButton = part as UIButton;
                    if (nameButton != null)
                    {
                        nameButton.FillColor = theme.SurfacePaper;
                        nameButton.ForeColor = theme.TextPrimary;
                        nameButton.RectColor = theme.BorderSubtle;
                        nameButton.FillHoverColor = theme.AccentSoft;
                        nameButton.RectHoverColor = theme.Accent;
                        nameButton.ForeHoverColor = theme.Accent;
                    }
                }
            }
        }

        #endregion

        #region Flicker suppression

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_COMPOSITED = 0x02000000;

        private static void EnableComposited(Control control)
        {
            if (control == null)
                return;

            EventHandler onCreated = null;
            onCreated = delegate
            {
                try
                {
                    IntPtr handle = control.Handle;
                    int exStyle = GetWindowLong(handle, GWL_EXSTYLE);
                    SetWindowLong(handle, GWL_EXSTYLE, exStyle | WS_EX_COMPOSITED);
                }
                catch { }
                control.HandleCreated -= onCreated;
            };
            control.HandleCreated += onCreated;
            if (control.IsHandleCreated)
                onCreated(null, EventArgs.Empty);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        #endregion

        #region Lifecycle

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplySplitLayout();

            // Validate registry data directory on startup (deferred so the form is visible)
            BeginInvoke(new Action(delegate
            {
                if (!WindowsDataDirectoryService.HasValidRegistryPath())
                {
                    string regPath = WindowsDataDirectoryService.ReadFromRegistry();
                    if (!string.IsNullOrEmpty(regPath))
                    {
                        MessageBox.Show(
                            string.Format("数据目录 {0} 不存在，请重新指定。", regPath),
                            "SilentNotes",
                            System.Windows.Forms.MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                    SyncSettingsButton_Click(this, EventArgs.Empty);
                }
            }));

            // Apply theme before loading repository
            ISettingsService settingsService = Program.Services.GetRequiredService<ISettingsService>();
            var settings = settingsService.LoadSettingsOrDefault();
            ThemeService.ApplyTheme(settings.ThemeMode);
            ApplyThemeToUi();

            LoadRepository();

            // Startup auto-sync (non-blocking)
            if (_syncService.HasCloudStorageConfigured && _syncService.HasTransferCode && ShouldAutoSync(settings.AutoSyncMode))
                TryAutoSyncAsync(false);

            // Periodic auto-sync: the timer callback checks ShouldAutoSync itself.
            _autoSyncTimer.Change(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_contentDirty && _selectedNote != null && !_showRecycleBin)
            {
                DialogResult choice = ThemedConfirmDialog.ShowSavePrompt(
                    this, ThemeService, "未保存的更改",
                    string.Format("笔记 \"{0}\" 有未保存的更改，是否保存？", _editorTitleBase));
                if (choice == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                if (choice == DialogResult.Yes)
                {
                    bool saved = SaveSelectedNote(false);
                    if (!saved)
                    {
                        SetStatus("保存失败，已取消关闭窗口。", true);
                        e.Cancel = true;
                        return;
                    }
                }
            }

            if (_autoSyncTimer != null)
            {
                _autoSyncTimer.Dispose();
                _autoSyncTimer = null;
            }
            base.OnFormClosing(e);
        }

        private void ApplyThemeToUi()
        {
            WinFormsThemeService theme = ThemeService;
            theme.Apply(this);
            theme.ApplyWindowTheme(this);
            // Native title bar: colors follow the system theme; DWM immersive dark
            // mode (ApplyWindowTheme) keeps the caption dark in dark mode.
            BackColor = theme.SurfaceWindow;
            RefreshModeButtonColors();
            PopulateTagPanel(CurrentVisibleTags(), _selectedTag);
            RefreshNoteTagChipColors();
            RefreshPinnedButton();
            RefreshMoreMenuTheme();
            _notesList.Invalidate();
            bool isChecklist = _selectedNote != null && _selectedNote.NoteType == NoteType.Checklist;
            _editor.SetEditorTheme(theme.IsDarkMode, isChecklist);
            UpdateToolbarState();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if ((keyData & Keys.Control) == Keys.Control)
            {
                switch (key)
                {
                    case Keys.N:
                        NewNoteButton_Click(this, EventArgs.Empty);
                        return true;
                    case Keys.S:
                        SaveSelectedNote();
                        return true;
                    case Keys.F:
                        _searchBox.TextBox.Focus();
                        _searchBox.TextBox.SelectAll();
                        return true;
                    case Keys.B:
                        _editor.FocusEditor();
                        _editor.ToggleBold();
                        return true;
                    case Keys.I:
                        _editor.FocusEditor();
                        _editor.ToggleItalic();
                        return true;
                    case Keys.U:
                        _editor.FocusEditor();
                        _editor.ToggleUnderline();
                        return true;
                    case Keys.Z:
                        _editor.FocusEditor();
                        _editor.Undo();
                        return true;
                    case Keys.Y:
                        _editor.FocusEditor();
                        _editor.Redo();
                        return true;
                }
            }
            else if (key == Keys.Delete)
            {
                // Don't swallow Delete inside text controls or the editor
                if (ActiveControl is TextBox || ActiveControl is UITextBox || ActiveControl is HtmlEditorControl)
                    return base.ProcessCmdKey(ref msg, keyData);
                DeleteNoteButton_Click(this, EventArgs.Empty);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        #endregion

        #region Top bar actions

        private void ReloadButton_Click(object sender, EventArgs e)
        {
            RepositoryStorageService.ClearCache();
            LoadRepository();
        }

        private void SyncSettingsButton_Click(object sender, EventArgs e)
        {
            WebDavSettingsDialog dialog = new WebDavSettingsDialog { StartPosition = FormStartPosition.CenterParent };

            ISettingsService settingsService = Program.Services.GetRequiredService<ISettingsService>();
            var settings = settingsService.LoadSettingsOrDefault();
            if (settings.Credentials != null)
            {
                dialog.Prefill(
                    settings.Credentials.Url,
                    settings.Credentials.Username,
                    settings.Credentials.UnprotectedPassword,
                    settings.TransferCode,
                    settings.AutoSyncMode.ToString(),
                    settings.DataDirectory);
            }
            else
            {
                dialog.Prefill(null, null, null, settings.TransferCode, settings.AutoSyncMode.ToString(), settings.DataDirectory);
            }

            dialog.ApplyTheme(ThemeService);
            if (dialog.ShowDialog(this) == DialogResult.OK)
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
                if (!string.IsNullOrEmpty(dialog.SyncMode))
                {
                    AutoSynchronizationMode newSyncMode = (AutoSynchronizationMode)Enum.Parse(typeof(AutoSynchronizationMode), dialog.SyncMode);
                    if (settings.AutoSyncMode != newSyncMode)
                    {
                        settings.AutoSyncMode = newSyncMode;
                        settingsChanged = true;
                    }
                }

                // Handle data directory change
                string sourceDir = WindowsDataDirectoryService.GetEffectiveDirectory();
                string newDir = dialog.DataDirectory;
                string targetDir = string.IsNullOrWhiteSpace(newDir)
                    ? WindowsApplicationPaths.AppDataDirectory
                    : newDir;
                bool dirChanged = !string.Equals(sourceDir, targetDir, StringComparison.OrdinalIgnoreCase);

                if (dirChanged)
                {
                    settings.DataDirectory = string.IsNullOrWhiteSpace(newDir) ? null : newDir;
                    settingsChanged = true;

                    string repoFileName = NoteRepositoryModel.RepositoryFileName;
                    string targetRepoFile = System.IO.Path.Combine(targetDir, repoFileName);
                    bool targetHasData = System.IO.File.Exists(targetRepoFile);

                    if (targetHasData)
                    {
                        TryDeleteDirectory(sourceDir);
                    }
                    else
                    {
                        System.IO.Directory.CreateDirectory(targetDir);
                        CopyDirectoryContents(sourceDir, targetDir);
                        TryDeleteDirectory(sourceDir);
                    }

                    WindowsDataDirectoryService.WriteToRegistry(targetDir);
                }

                if (settingsChanged)
                    settingsService.TrySaveSettingsToLocalDevice(settings);

                if (dirChanged)
                {
                    RepositoryStorageService.ClearCache();
                    LoadRepository();
                }

                SetStatus(settingsChanged ? "设置已保存。" : "设置未更改。");
                _syncButton.Enabled = true;
            }
        }

        private static void CopyDirectoryContents(string sourceDir, string targetDir)
        {
            if (!System.IO.Directory.Exists(sourceDir))
                return;

            foreach (string srcFile in System.IO.Directory.GetFiles(sourceDir))
            {
                string tgtFile = System.IO.Path.Combine(targetDir, System.IO.Path.GetFileName(srcFile));
                System.IO.File.Copy(srcFile, tgtFile, true);
            }

            foreach (string srcSubDir in System.IO.Directory.GetDirectories(sourceDir))
            {
                string tgtSubDir = System.IO.Path.Combine(targetDir, System.IO.Path.GetFileName(srcSubDir));
                System.IO.Directory.CreateDirectory(tgtSubDir);
                CopyDirectoryContents(srcSubDir, tgtSubDir);
            }
        }

        private static void TryDeleteDirectory(string dirPath)
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

        private async void SyncButton_Click(object sender, EventArgs e)
        {
            if (!_syncService.HasCloudStorageConfigured)
            {
                SetStatus("请先配置同步设置。", true);
                return;
            }

            _syncButton.Enabled = false;
            _saveButton.Enabled = false;
            // Edits made during sync live in memory only and would be dropped by the
            // repository reload below; freeze the editor for the sync duration.
            _editor.SetReadOnly(true);
            SetStatus("正在同步...");

            bool success = await _syncService.SyncAsync(message =>
            {
                SafeInvoke(delegate { SetStatus(message, IsSyncErrorMessage(message)); });
            });

            SafeInvoke(delegate
            {
                _syncButton.Enabled = true;
                UpdateModeControls();
            });

            if (success)
            {
                RepositoryStorageService.ClearCache();
                SafeInvoke(delegate
                {
                    LoadRepository();
                    SetStatus("同步完成。");
                });
            }
            else
            {
                // Reload the saved content and restore the proper read-only state.
                SafeInvoke(delegate { SelectNote(_selectedNote); });
            }
        }

        private void SafeInvoke(Action action)
        {
            try
            {
                if (IsDisposed || Disposing || !IsHandleCreated)
                    return;
                BeginInvoke(action);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

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

        private async Task TryAutoSyncAsync(bool showStatus)
        {
            try
            {
                // Unsaved edits exist in memory only; syncing would reload the
                // repository file and silently drop them. Defer to the next tick.
                if (_contentDirty)
                {
                    if (showStatus)
                        SafeInvoke(delegate { SetStatus("有未保存的更改，本次自动同步已跳过。"); });
                    return;
                }

                if (showStatus)
                    SafeInvoke(delegate { SetStatus("自动同步中..."); });

                bool success = await _syncService.SyncAsync(msg =>
                {
                    if (showStatus)
                        SafeInvoke(delegate { SetStatus(msg, IsSyncErrorMessage(msg)); });
                });

                if (success)
                {
                    RepositoryStorageService.ClearCache();
                    SafeInvoke(delegate { LoadRepository(); });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Auto-sync error: " + ex.Message);
            }
        }

        private void RestoreBackupButton_Click(object sender, EventArgs e)
        {
            string location = RepositoryStorageService.GetLocation();
            string backupDir = System.IO.Path.Combine(location, "sync_backups");
            if (!System.IO.Directory.Exists(backupDir) || !System.IO.Directory.EnumerateFiles(backupDir, "*.silentnotes").Any())
            {
                SetStatus("没有找到备份文件。", true);
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择要恢复的备份文件";
                dialog.InitialDirectory = backupDir;
                dialog.Filter = "备份文件 (*.silentnotes)|*.silentnotes";

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        string targetPath = System.IO.Path.Combine(location, NoteRepositoryModel.RepositoryFileName);
                        System.IO.File.Copy(dialog.FileName, targetPath, true);
                        RepositoryStorageService.ClearCache();
                        LoadRepository();
                        SetStatus("已从备份恢复。");
                    }
                    catch (Exception ex)
                    {
                        SetStatus("恢复失败：" + ex.Message, true);
                    }
                }
            }
        }

        private void HelpButton_Click(object sender, EventArgs e)
        {
            ThemedDialogForm dialog = new ThemedDialogForm
            {
                Text = "帮助与快捷键",
                Width = 440,
                Height = 430,
            };

            UILabel helpText = new UILabel
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(24, 4, 24, 4),
                Font = UIAppFont,
                Tag = "window",
                Text =
                    "笔记操作\n" +
                    "　Ctrl+N　新建笔记\n" +
                    "　Ctrl+S　保存笔记\n" +
                    "　Ctrl+F　搜索笔记\n" +
                    "　Delete　删除笔记（移到回收站）\n" +
                    "\n格式编辑\n" +
                    "　Ctrl+B　粗体\n" +
                    "　Ctrl+I　斜体\n" +
                    "　Ctrl+U　下划线\n" +
                    "　Ctrl+Z　撤销\n" +
                    "　Ctrl+Y　重做\n" +
                    "\n清单操作\n" +
                    "　点击条目前的方框　勾选/取消清单项\n" +
                    "　Enter　　　　　　新建清单项",
            };
            UIButton closeBtn = new UIButton { Text = "关闭", Width = 78, Height = 30, Tag = "window", Font = UIAppFont, Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.Cancel };
            Panel bottom = new Panel { Dock = DockStyle.Bottom, Height = 48, Tag = "window" };
            bottom.Resize += delegate
            {
                closeBtn.Left = bottom.ClientSize.Width - closeBtn.Width - 20;
                closeBtn.Top = (bottom.ClientSize.Height - closeBtn.Height) / 2;
            };
            bottom.Controls.Add(closeBtn);

            dialog.Controls.Add(helpText);
            dialog.Controls.Add(bottom);
            dialog.CancelButton = closeBtn;
            dialog.ApplyTheme(ThemeService);
            dialog.ShowDialog(this);
        }

        private void ThemeToggleButton_Click(object sender, EventArgs e)
        {
            try
            {
                WinFormsThemeService theme = ThemeService;
                ISettingsService settingsService = Program.Services.GetRequiredService<ISettingsService>();
                var settings = settingsService.LoadSettingsOrDefault();
                ThemeMode newMode = theme.IsDarkMode ? ThemeMode.Light : ThemeMode.Dark;
                settings.ThemeMode = newMode;
                settingsService.TrySaveSettingsToLocalDevice(settings);
                theme.ApplyTheme(newMode);
                ApplyThemeToUi();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Theme toggle error: " + ex.Message);
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            SaveSelectedNote();
        }

        #endregion

        #region Note CRUD

        private void ModeToggle_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool toRecycleBin = _modeToggle.SelectedIndex == 1;
            if (_showRecycleBin == toRecycleBin)
                return;

            _showRecycleBin = toRecycleBin;
            RefreshNoteList(FindFirstNote(note => toRecycleBin ? note.InRecyclingBin : !note.InRecyclingBin));
            UpdateRepositorySummary();
            UpdateModeControls();
        }

        private NoteModel FindFirstNote(Func<NoteModel, bool> predicate)
        {
            if (_repository == null)
                return null;
            return _repository.Notes.FirstOrDefault(predicate);
        }

        private void NewNoteButton_Click(object sender, EventArgs e)
        {
            if (!HasEditableRepository())
                return;

            _showRecycleBin = false;
            _searchBox.Text = string.Empty;
            _searchText = string.Empty;

            NoteModel note = new NoteModel
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

        private void NewChecklistButton_Click(object sender, EventArgs e)
        {
            if (!HasEditableRepository())
                return;

            _showRecycleBin = false;
            _searchBox.Text = string.Empty;
            _searchText = string.Empty;

            NoteModel note = new NoteModel
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

        private bool HasEditableRepository()
        {
            return _repository != null && !Object.ReferenceEquals(_repository, NoteRepositoryModel.InvalidRepository);
        }

        private void DeleteNoteButton_Click(object sender, EventArgs e)
        {
            if (_selectedNote == null || _showRecycleBin)
                return;

            if (!ThemedConfirmDialog.Show(this, ThemeService, "移到回收站",
                "要将当前笔记移到回收站吗？", "移到回收站", false))
                return;

            _selectedNote.InRecyclingBin = true;
            _selectedNote.RefreshMetaModifiedAt();
            _repository.RefreshOrderModifiedAt();
            bool saved = RepositoryStorageService.TrySaveRepository(_repository);
            NoteModel nextNote = FindFirstNote(note => !note.InRecyclingBin);
            UpdateRepositorySummary();
            RefreshTagList();
            RefreshNoteList(nextNote);
            SetStatus(saved ? "已移到回收站。" : "移动到回收站失败。", !saved);
        }

        private void RestoreNoteButton_Click(object sender, EventArgs e)
        {
            if (_selectedNote == null || !_showRecycleBin)
                return;

            _selectedNote.InRecyclingBin = false;
            _selectedNote.RefreshMetaModifiedAt();
            _repository.RefreshOrderModifiedAt();
            bool saved = RepositoryStorageService.TrySaveRepository(_repository);
            NoteModel nextNote = FindFirstNote(note => note.InRecyclingBin);
            RefreshNoteList(nextNote);
            UpdateRepositorySummary();
            RefreshTagList();
            SetStatus(saved ? "已恢复笔记。" : "恢复笔记失败。", !saved);
        }

        private void PermanentDeleteNoteButton_Click(object sender, EventArgs e)
        {
            if (_selectedNote == null || !_showRecycleBin)
                return;

            if (!ThemedConfirmDialog.Show(this, ThemeService, "永久删除",
                "要永久删除当前笔记吗？此操作不能撤销。", "永久删除", true))
                return;

            NoteModel noteToDelete = _selectedNote;
            _repository.DeletedNotes.AddIdOrRefreshDeletedAt(noteToDelete.Id);
            _repository.Notes.Remove(noteToDelete);
            _repository.RefreshOrderModifiedAt();
            bool saved = RepositoryStorageService.TrySaveRepository(_repository);
            NoteModel nextNote = FindFirstNote(note => note.InRecyclingBin);
            RefreshNoteList(nextNote);
            UpdateRepositorySummary();
            RefreshTagList();
            SetStatus(saved ? "已永久删除笔记。" : "永久删除失败。", !saved);
        }

        private void EmptyRecycleBinButton_Click(object sender, EventArgs e)
        {
            if (_repository == null || !_showRecycleBin)
                return;

            int count = _repository.Notes.Count(note => note.InRecyclingBin);
            if (count == 0)
                return;

            if (!ThemedConfirmDialog.Show(this, ThemeService, "清空回收站",
                string.Format("要永久删除回收站中的 {0} 条笔记吗？此操作不能撤销。", count), "全部删除", true))
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

        #endregion

        #region Tags

        private void TagButton_Click(object sender, EventArgs e)
        {
            UIButton button = sender as UIButton;
            if (button == null)
                return;

            _selectedTag = string.IsNullOrEmpty(button.Name) ? null : button.Name;
            RefreshTagButtonColors();
            if (_repository != null)
            {
                RefreshNoteList(_selectedNote);
                UpdateRepositorySummary();
            }
        }

        private void SearchTextBox_TextChanged(object sender, EventArgs e)
        {
            if (_repository == null)
                return;

            _searchText = _searchBox.Text ?? string.Empty;
            UpdateRepositorySummary();
            RefreshNoteList(_selectedNote);
        }

        private void TagsTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab && _tagSuggestionList.Visible && _tagSuggestionList.Items.Count > 0)
            {
                string selected = _tagSuggestionList.Items[0] as string;
                if (!string.IsNullOrEmpty(selected))
                {
                    _tagsTextBox.Text = selected;
                    _tagsTextBox.TextBox.SelectionStart = _tagsTextBox.TextBox.TextLength;
                }
                HideTagSuggestions();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                HideTagSuggestions();
                e.Handled = true;
                e.SuppressKeyPress = true;
                TagAdded();
            }
        }

        private void TagSuggestionList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingTagSuggestions || !_tagSuggestionList.Visible)
                return;

            string selected = _tagSuggestionList.SelectedItem as string;
            if (string.IsNullOrEmpty(selected))
                return;

            _loadingTagSuggestions = true;
            try
            {
                _tagSuggestionList.SelectedIndex = -1;
                _tagsTextBox.Text = selected;
                _tagsTextBox.TextBox.SelectionStart = _tagsTextBox.TextBox.TextLength;
            }
            finally
            {
                _loadingTagSuggestions = false;
            }
            HideTagSuggestions();
            _tagsTextBox.TextBox.Focus();
        }

        private void TagSuggestionList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _tagSuggestionList.Items.Count)
                return;

            // Same contract as the notes list: cover the UIListBox default text with
            // a full-row repaint before drawing.
            WinFormsThemeService theme = ThemeService;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (SolidBrush background = new SolidBrush(selected ? theme.AccentSoft : theme.SurfacePaper))
                e.Graphics.FillRectangle(background, e.Bounds);

            string text = _tagSuggestionList.Items[e.Index] as string;
            if (string.IsNullOrEmpty(text))
                return;
            TextRenderer.DrawText(e.Graphics, text, UIAppFont,
                new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height),
                selected ? theme.Accent : theme.TextPrimary,
                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
        }

        private void HideTagSuggestions()
        {
            _loadingTagSuggestions = true;
            try
            {
                _tagSuggestionList.SelectedIndex = -1;
                _tagSuggestionList.Visible = false;
            }
            finally
            {
                _loadingTagSuggestions = false;
            }
        }

        private void TagAdded()
        {
            if (_loadingEditorMetadata || _selectedNote == null || _showRecycleBin)
                return;

            string tagToAdd = (_tagsTextBox.Text ?? string.Empty).Trim();
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
            _tagsTextBox.Text = string.Empty;
            RefreshVisibleSelectedItemTitle();
            PopulateNoteTagPanel();
            RefreshTagList();
            RefreshNoteList(_selectedNote);
            UpdateRepositorySummary();
            UpdateTagSuggestions();
            SetStatus(saved ? "已添加标签。" : "保存笔记属性失败。", !saved);
        }

        private void DeleteTagByName(string tagName)
        {
            if (_loadingEditorMetadata || _selectedNote == null || _showRecycleBin)
                return;

            if (string.IsNullOrEmpty(tagName))
                return;

            int tagIndex = _selectedNote.Tags.FindIndex(tag => string.Equals(tag, tagName, StringComparison.InvariantCultureIgnoreCase));
            if (tagIndex == -1)
            {
                SetStatus(string.Format("标签 \"{0}\" 不存在。", tagName), true);
                return;
            }

            _selectedNote.Tags.RemoveAt(tagIndex);
            _selectedNote.RefreshMetaModifiedAt();
            bool saved = RepositoryStorageService.TrySaveRepository(_repository);
            RefreshVisibleSelectedItemTitle();
            RefreshTagList();
            RefreshNoteList(_selectedNote);
            UpdateRepositorySummary();
            UpdateTagSuggestions();
            SetStatus(saved ? "已删除标签。" : "保存笔记属性失败。", !saved);
        }

        private void TagsTextBox_TextChanged(object sender, EventArgs e)
        {
            UpdateTagSuggestionPopup();
        }

        private void UpdateTagSuggestionPopup()
        {
            if (!Object.ReferenceEquals(ActiveControl, _tagsTextBox))
            {
                HideTagSuggestions();
                return;
            }

            string currentTag = (_tagsTextBox.Text ?? string.Empty).Trim();
            List<string> filtered;
            if (string.IsNullOrEmpty(currentTag))
                filtered = _tagSuggestions.ToList();
            else
                filtered = _tagSuggestions.Where(s => s.IndexOf(currentTag, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList();

            _tagSuggestionList.BeginUpdate();
            _tagSuggestionList.Items.Clear();
            foreach (string suggestion in filtered)
                _tagSuggestionList.Items.Add(suggestion);
            _tagSuggestionList.EndUpdate();
            _tagSuggestionList.Visible = filtered.Count > 0;
        }

        private void UpdateTagSuggestions()
        {
            if (_repository == null || !HasEditableRepository() || _selectedNote == null)
            {
                _tagSuggestions = new List<string>();
                return;
            }

            List<string> allTags = _repository.CollectActiveTags();
            _tagSuggestions = allTags.Where(tag => !_selectedNote.Tags.Contains(tag, StringComparer.InvariantCultureIgnoreCase)).ToList();
        }

        private List<string> CurrentVisibleTags()
        {
            if (_repository == null || !HasEditableRepository())
                return new List<string>();

            return _repository.Notes
                .Where(note => note.InRecyclingBin == _showRecycleBin)
                .SelectMany(note => note.Tags)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.InvariantCultureIgnoreCase)
                .OrderBy(tag => tag, StringComparer.InvariantCultureIgnoreCase)
                .ToList();
        }

        private void PopulateTagPanel(List<string> tags, string selectedTag)
        {
            _tagPanel.Controls.Clear();
            _selectedTag = selectedTag;

            _tagPanel.Controls.Add(CreateTagButton("全部", null, selectedTag == null));
            foreach (string tag in tags)
                _tagPanel.Controls.Add(CreateTagButton(tag, tag, string.Equals(tag, selectedTag, StringComparison.InvariantCultureIgnoreCase)));
            RefreshTagButtonColors();
        }

        private UIButton CreateTagButton(string text, string tag, bool isSelected)
        {
            // The tag name goes into Name, not Tag: Tag is read by the theme service
            // as a style token, so a note tag literally named "action" or "danger"
            // would have restyled its own filter pill.
            UIButton button = new UIButton
            {
                Text = text,
                Name = tag ?? string.Empty,
                AutoSize = false,
                Width = TextRenderer.MeasureText(text, UIAppFont).Width + 22,
                Height = 24,
                Radius = 12,
                Tag = "bare",
                Font = UIAppFont,
                Style = UIStyle.Custom,
                Margin = new Padding(2, 2, 2, 2),
            };
            button.Click += TagButton_Click;
            return button;
        }

        private void RefreshTagButtonColors()
        {
            WinFormsThemeService theme = ThemeService;
            foreach (Control child in _tagPanel.Controls)
            {
                UIButton button = child as UIButton;
                if (button == null)
                    continue;

                bool isSel = string.Equals(button.Name, _selectedTag, StringComparison.InvariantCultureIgnoreCase)
                    || (string.IsNullOrEmpty(button.Name) && _selectedTag == null);
                button.FillColor = isSel ? theme.AccentSoft : theme.SurfacePaper;
                button.RectColor = isSel ? theme.Accent : theme.BorderSubtle;
                button.ForeColor = isSel ? theme.Accent : theme.TextSecondary;
                button.FillHoverColor = theme.AccentSoft;
                button.RectHoverColor = theme.Accent;
                button.ForeHoverColor = isSel ? theme.Accent : theme.TextPrimary;
            }
        }

        private void RefreshTagList()
        {
            if (_repository == null || !HasEditableRepository())
                return;

            PopulateTagPanel(CurrentVisibleTags(), _selectedTag);
        }

        #endregion

        #region Repository and note list

        private void LoadRepository()
        {
            RepositoryStorageLoadResult loadResult = RepositoryStorageService.LoadRepositoryOrDefault(out _repository);
            if (Object.ReferenceEquals(_repository, NoteRepositoryModel.InvalidRepository))
            {
                _notesList.DataSource = null;
                _notesList.Items.Clear();
                PopulateTagPanel(new List<string>(), null);
                SelectNote(null);
                _repositorySummaryLabel.Text = "本地仓库无法读取，已停止编辑以避免覆盖原文件。";
                UpdateModeControls();
                SetStatus("仓库加载失败。", true);
                return;
            }

            RefreshTagList();
            RefreshNoteList(FindFirstNote(note => note.InRecyclingBin == _showRecycleBin));
            UpdateRepositorySummary();
            UpdateModeControls();

            SetStatus(loadResult == RepositoryStorageLoadResult.CreatedNewEmptyRepository
                ? "已创建新的本地仓库。"
                : "已加载本地仓库。");
        }

        private void RefreshNoteList(NoteModel noteToSelect)
        {
            _listItems = _repository.Notes
                .Where(note => note.InRecyclingBin == _showRecycleBin)
                .Where(MatchesTag)
                .Where(MatchesSearch)
                .OrderByDescending(note => note.IsPinned)
                .Select(note =>
                {
                    NoteListItem item = new NoteListItem(note);
                    string cachedTitle;
                    if (note.SafeId.HasValue && _safeNoteTitles.TryGetValue(note.Id, out cachedTitle))
                        item.SetCustomTitle(cachedTitle);
                    return item;
                })
                .ToList();

            _loadingSelection = true;
            try
            {
                _notesList.BeginUpdate();
                _notesList.Items.Clear();
                foreach (NoteListItem item in _listItems)
                    _notesList.Items.Add(item);
                NoteListItem selected = _listItems.FirstOrDefault(listItem => ReferenceEquals(listItem.Note, noteToSelect));
                _notesList.SelectedItem = selected;
                noteToSelect = selected != null ? selected.Note : null;
                _notesList.EndUpdate();
            }
            finally
            {
                _loadingSelection = false;
            }

            bool isEmpty = _listItems.Count == 0;
            _emptyListLabel.Text = _showRecycleBin
                ? "回收站为空。"
                : (string.IsNullOrWhiteSpace(_searchText) && string.IsNullOrEmpty(_selectedTag)
                    ? "还没有笔记，点击「新建笔记」开始。"
                    : "没有匹配的笔记。");
            _emptyListLabel.Visible = isEmpty;
            _notesList.Visible = !isEmpty;

            SelectNote(noteToSelect);
        }

        private void NotesList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingSelection)
                return;

            NoteListItem selectedItem = _notesList.SelectedItem as NoteListItem;
            SelectNote(selectedItem != null ? selectedItem.Note : null);
        }

        private void NotesList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _listItems.Count)
                return;

            // UIListBox has already painted its default background and item text here;
            // cover the FULL row (including the scrollbar gutter) first, then draw the
            // two-line note card on top.
            WinFormsThemeService theme = ThemeService;
            NoteListItem item = _listItems[e.Index];
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool hovered = (e.State & DrawItemState.HotLight) == DrawItemState.HotLight;

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (SolidBrush background = new SolidBrush(theme.SurfacePaper))
                e.Graphics.FillRectangle(background, e.Bounds);

            // Reserve a gutter for the vertical scrollbar so cards never run under it
            Rectangle rowRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 2, e.Bounds.Width - 22, e.Bounds.Height - 4);
            Color back = selected ? theme.AccentSoft : (hovered ? theme.ListHover : theme.SurfacePaper);
            using (SolidBrush background = new SolidBrush(back))
                e.Graphics.FillRectangle(background, rowRect);

            // Selection: a vertical accent bar on the card's left edge
            if (selected)
            {
                using (SolidBrush bar = new SolidBrush(theme.Accent))
                    e.Graphics.FillRectangle(bar, e.Bounds.X + 4, e.Bounds.Y + 2, 4, e.Bounds.Height - 4);
            }

            int h = e.Bounds.Height;
            int textWidth = Math.Max(40, e.Bounds.Width - 40);
            Rectangle titleRect = new Rectangle(e.Bounds.X + 14, e.Bounds.Y + 6, textWidth, 20);
            Rectangle secondaryRect = new Rectangle(e.Bounds.X + 14, e.Bounds.Y + h - 26, textWidth, 18);

            string title = item.Title ?? "无标题笔记";
            // Accent on AccentSoft is only 3.1:1 in light mode; the selected title
            // falls back to TextPrimary there (dark mode Accent passes).
            Color mainColor = selected && theme.IsDarkMode ? theme.Accent : theme.TextPrimary;
            TextRenderer.DrawText(e.Graphics, title, UIListTitleFont, titleRect, mainColor, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            string secondaryLine = item.SecondaryLine ?? string.Empty;
            if (item.IsPinned)
                secondaryLine = "★ " + secondaryLine;
            TextRenderer.DrawText(
                e.Graphics,
                secondaryLine,
                UIListBodyFont,
                secondaryRect,
                theme.TextSecondary,
                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        private void UpdateRepositorySummary()
        {
            if (_repository == null)
                return;

            int activeCount = _repository.Notes.Count(note => !note.InRecyclingBin);
            int recycleBinCount = _repository.Notes.Count(note => note.InRecyclingBin);
            int visibleCount = _repository.Notes.Count(note => note.InRecyclingBin == _showRecycleBin && MatchesTag(note) && MatchesSearch(note));
            _repositorySummaryLabel.Text = string.IsNullOrWhiteSpace(_searchText)
                ? string.Format("{0} 条活动笔记，{1} 条回收站笔记。当前视图 {2} 条。", activeCount, recycleBinCount, visibleCount)
                : string.Format("当前视图找到 {0} 条；总计 {1} 条活动笔记，{2} 条回收站笔记。", visibleCount, activeCount, recycleBinCount);
        }

        private void UpdateModeControls()
        {
            bool hasRepository = HasEditableRepository();
            _activeModeButtons.Visible = !_showRecycleBin;
            _recycleBinModeButtons.Visible = _showRecycleBin;
            _toolbar.Visible = !_showRecycleBin;
            _tagsTextBox.ReadOnly = _showRecycleBin || !hasRepository;
            _pinnedButton.Enabled = hasRepository && !_showRecycleBin;
            _pinnedButton.Visible = _selectedNote != null && !_showRecycleBin;
            _saveButton.Enabled = hasRepository && !_showRecycleBin;
            RefreshModeButtonColors();
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

        #endregion

        #region Note selection and saving

        private void SelectNote(NoteModel note)
        {
            if (!_editor.IsReady)
            {
                _pendingSelectNote = note;
                return;
            }

            _selectedNote = note;
            _contentDirty = false;

            if (note == null)
            {
                _editor.SetReadOnly(true);
                LoadEditorContent(null, false);
                LoadEditorMetadata(null);
                SetEditorTitle("编辑器");
                _editorInfoLabel.Text = _showRecycleBin ? "回收站为空，或没有匹配搜索条件的笔记。" : "请选择一条笔记。";
                return;
            }

            LoadEditorMetadata(note);
            SetEditorTitle(BuildTitle(note));
            if (_showRecycleBin)
            {
                _editor.SetReadOnly(true);
                _editorInfoLabel.Text = string.Format("回收站笔记只读。最后修改：{0:g}", note.ModifiedAt.ToLocalTime());
                LoadEditorContent(note.HtmlContent, note.NoteType == NoteType.Checklist);
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
                        _editor.SetReadOnly(!canEditSafe);
                        _editorInfoLabel.Text = canEditSafe
                            ? string.Format("安全箱笔记 · 最后修改：{0:g}", note.ModifiedAt.ToLocalTime())
                            : "安全箱笔记（只读模式）";
                        LoadEditorContent(canEditSafe ? unlockedContent : null, note.NoteType == NoteType.Checklist);
                        if (!canEditSafe)
                        {
                            _editor.SetContent("<p>" + EncodeTextToHtml(BuildPlainText(unlockedContent)) + "</p>", false);
                        }
                        string unlockedTitle = BuildPlainText(unlockedContent);
                        string displayTitle = string.IsNullOrWhiteSpace(unlockedTitle)
                            ? "无标题笔记"
                            : (unlockedTitle.Length > 80 ? unlockedTitle.Substring(0, 80) + "..." : unlockedTitle);
                        SetEditorTitle(displayTitle);
                        if (!string.IsNullOrEmpty(unlockedTitle))
                            _safeNoteTitles[note.Id] = unlockedTitle;
                        NoteListItem selectedItem = _notesList.SelectedItem as NoteListItem;
                        if (selectedItem != null)
                        {
                            selectedItem.SetCustomTitle(unlockedTitle);
                            _notesList.Invalidate();
                        }
                        return;
                    }
                }

                // Safe is not open or decryption failed - show lock message
                _editor.SetReadOnly(true);
                _editorInfoLabel.Text = "这条笔记位于安全箱中，点击上方「安全箱」按钮输入密码解锁后可编辑。";
                _editor.SetContent(
                    "<p>安全箱笔记已锁定。</p><p>请点击顶部「安全箱」按钮输入密码解锁。</p>",
                    false);
                return;
            }

            bool canEdit = _htmlCompatibilityInspector.CanEditWithoutConversion(note.HtmlContent);
            _editor.SetReadOnly(!canEdit);
            _editorInfoLabel.Text = canEdit
                ? string.Format("最后修改：{0:g}", note.ModifiedAt.ToLocalTime())
                : "这条笔记包含当前原生编辑器不支持的 HTML 内容，暂时只读以避免格式损坏。";
            LoadEditorContent(canEdit ? note.HtmlContent : null, note.NoteType == NoteType.Checklist);
            if (!canEdit)
                _editor.SetContent("<p>" + EncodeTextToHtml(BuildPlainText(note.HtmlContent)) + "</p>", false);

            _contentDirty = false;
        }

        private void LoadEditorContent(string html, bool isChecklist)
        {
            _editor.SetContent(string.IsNullOrEmpty(html) ? "<p><br></p>" : html, isChecklist);
            _editor.SetEditorTheme(ThemeService.IsDarkMode, isChecklist);
            _contentDirty = false;
            UpdateWordCount();
            UpdateToolbarState();
        }

        private static string EncodeTextToHtml(string text)
        {
            return System.Net.WebUtility.HtmlEncode(text ?? string.Empty)
                .Replace("\r\n", "<br>")
                .Replace("\n", "<br>");
        }

        private void Editor_Ready(object sender, EventArgs e)
        {
            SafeInvoke(delegate
            {
                _editor.SetEditorTheme(ThemeService.IsDarkMode, false);
                NoteModel pending = _pendingSelectNote;
                _pendingSelectNote = null;
                SelectNote(pending ?? _selectedNote);
            });
        }

        private void Editor_ContentChanged(object sender, EventArgs e)
        {
            _contentDirty = true;
            UpdateEditorTitleMarker();
            ScheduleWordCountUpdate();
        }

        private void Editor_SelectionChanged(object sender, EventArgs e)
        {
            // Coalesce the frequent selectionchange events into one toolbar refresh
            // per message-loop iteration.
            if (_toolbarStatePending || !IsHandleCreated)
                return;
            _toolbarStatePending = true;
            SafeInvoke(delegate
            {
                _toolbarStatePending = false;
                UpdateToolbarState();
            });
        }

        // GetHtml() is an MSHTML COM round trip; running it on every keystroke stutters
        // on long notes, so the word count is coalesced to once per message iteration.
        private void ScheduleWordCountUpdate()
        {
            if (_wordCountPending || !IsHandleCreated)
                return;
            _wordCountPending = true;
            SafeInvoke(delegate
            {
                _wordCountPending = false;
                UpdateWordCount();
            });
        }

        /// <summary>Sets the editor pane title; a * prefix is shown while unsaved.</summary>
        private void SetEditorTitle(string title)
        {
            _editorTitleBase = string.IsNullOrWhiteSpace(title) ? "编辑器" : title;
            UpdateEditorTitleMarker();
        }

        private void UpdateEditorTitleMarker()
        {
            _editorTitleLabel.Text = _contentDirty ? "* " + _editorTitleBase : _editorTitleBase;
        }

        /// <summary>
        /// Saves the selected note's content. Returns false only when a save was
        /// required but failed (callers may abort closing the window on false).
        /// </summary>
        private bool SaveSelectedNote(bool showStatus = true)
        {
            if (_selectedNote == null || _editor.IsReadOnly || !HasEditableRepository())
                return true;
            if (!_contentDirty)
                return true;

            // Check if it's a locked safe note
            if (_selectedNote.SafeId.HasValue && !_safeKeyService.IsSafeOpen(_selectedNote.SafeId.Value))
                return true;

            string html = _editor.GetHtml();
            if (string.IsNullOrEmpty(html))
                return true;

            // If it's a safe note, encrypt the content before storing
            if (_selectedNote.SafeId.HasValue)
            {
                string encrypted = EncryptSafeNoteContent(html);
                if (encrypted == html) // encryption failed
                {
                    SetStatus("安全箱笔记加密失败，内容未保存。", true);
                    return false;
                }
                html = encrypted;
            }

            if (html == _selectedNote.HtmlContent)
            {
                _contentDirty = false;
                UpdateEditorTitleMarker();
                return true;
            }

            _selectedNote.HtmlContent = html;
            _selectedNote.RefreshModifiedAt();
            bool saved = RepositoryStorageService.TrySaveRepository(_repository);
            if (saved)
            {
                _contentDirty = false;
                RefreshVisibleSelectedItemTitle();
                RefreshTagList();
            }
            // On failure the * marker stays so the unsaved-changes prompt keeps firing.
            UpdateEditorTitleMarker();
            UpdateRepositorySummary();
            if (showStatus)
                SafeInvoke(delegate { SetStatus(saved ? "已保存笔记。" : "保存失败。", !saved); });
            return saved;
        }

        private void SaveSelectedMetadata()
        {
            if (_loadingEditorMetadata || _selectedNote == null || _showRecycleBin)
                return;

            bool changed = false;
            if (_selectedNote.IsPinned != _pinnedState)
            {
                _selectedNote.IsPinned = _pinnedState;
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

        private void LoadEditorMetadata(NoteModel note)
        {
            _loadingEditorMetadata = true;
            try
            {
                _tagsTextBox.Text = string.Empty;
                _pinnedState = note != null && note.IsPinned;
                RefreshPinnedButton();
                _pinnedButton.Visible = note != null && !_showRecycleBin;
                PopulateNoteTagPanel();
                UpdateTagSuggestions();
                HideTagSuggestions();
            }
            finally
            {
                _loadingEditorMetadata = false;
            }
        }

        private void RefreshVisibleSelectedItemTitle()
        {
            NoteListItem selectedItem = _notesList.SelectedItem as NoteListItem;
            if (selectedItem == null)
                return;

            string cachedTitle;
            if (selectedItem.Note.SafeId.HasValue && _safeNoteTitles.TryGetValue(selectedItem.Note.Id, out cachedTitle))
                selectedItem.SetCustomTitle(cachedTitle);
            else
                selectedItem.RefreshDisplay();
            _notesList.Invalidate();
            SetEditorTitle(selectedItem.Title);
        }

        private void LinkButton_Click()
        {
            if (_editor.IsReadOnly || _selectedNote == null)
                return;
            _editor.FocusEditor();

            ThemedDialogForm dialog = new ThemedDialogForm
            {
                Text = "插入链接",
                Width = 420,
                Height = 190,
            };

            UILabel caption = new UILabel { Text = "请输入链接 URL：", AutoSize = true, Left = 20, Top = 20, Font = UIAppFont };
            UITextBox urlBox = new UITextBox { Left = 20, Top = 48, Width = 356 };
            UIButton okBtn = new UIButton { Text = "确定", Width = 78, Height = 30, Left = 218, Top = 92, Tag = "accent", Font = UIAppFont, DialogResult = DialogResult.OK };
            UIButton cancelBtn = new UIButton { Text = "取消", Width = 78, Height = 30, Left = 306, Top = 92, Tag = "window", Font = UIAppFont, DialogResult = DialogResult.Cancel };
            dialog.Controls.Add(caption);
            dialog.Controls.Add(urlBox);
            dialog.Controls.Add(okBtn);
            dialog.Controls.Add(cancelBtn);
            dialog.AcceptButton = okBtn;
            dialog.CancelButton = cancelBtn;
            dialog.ApplyTheme(ThemeService);
            urlBox.Focus();
            urlBox.TextBox.SelectAll();

            if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(urlBox.Text))
                return;

            string input = urlBox.Text.Trim();
            string url = input;
            if (!url.StartsWith("http://") && !url.StartsWith("https://") && !url.StartsWith("mailto:"))
                url = "https://" + url;

            _editor.CreateLink(url);
        }

        #endregion

        #region Safe

        private void SafeButton_Click(object sender, EventArgs e)
        {
            if (_repository == null || !HasEditableRepository())
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
                SaveSelectedNote(false);
                _logService.Info("安全箱笔记已保存，准备关闭安全箱...");
                _safeKeyService.CloseAllSafes();
                _logService.Info("安全箱已关闭，清除缓存的标题...");
                _safeNoteTitles.Clear();
                SetStatus("安全箱已关闭。");
                RefreshNoteList(_selectedNote != null && _selectedNote.SafeId.HasValue ? _selectedNote : null);
                if (_selectedNote != null && _selectedNote.SafeId.HasValue)
                {
                    _logService.Info("刷新当前安全箱笔记的显示状态...");
                    SelectNote(_selectedNote);
                }
                _logService.Info("关闭安全箱流程完成。");
            }
            catch (Exception ex)
            {
                _logService.Error(string.Format("关闭安全箱时出错: {0}", ex));
                SetStatus("关闭安全箱时发生错误，请查看日志确认详情。", true);
            }
        }

        private void ShowSafePasswordDialog(bool existingSafe)
        {
            ThemedDialogForm dialog = new ThemedDialogForm
            {
                Text = existingSafe ? "打开安全箱" : "创建安全箱",
                Width = 430,
                Height = existingSafe ? 310 : 350,
            };

            Panel content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), Tag = "window" };
            UILabel title = new UILabel { Text = existingSafe ? "打开安全箱" : "创建安全箱", AutoSize = true, Font = UIEditorTitleFont, Left = 16, Top = 10 };
            UILabel hint = new UILabel
            {
                Text = existingSafe
                    ? "输入安全箱密码以解锁受保护的笔记。"
                    : "设置一个至少 5 个字符的密码，用于保护安全箱中的笔记。",
                AutoSize = false,
                Width = 360,
                Height = 32,
                Left = 16,
                Top = 44,
                Font = UISmallFont,
                Tag = "secondary",
            };
            UILabel passwordCaption = new UILabel { Text = existingSafe ? "输入安全箱密码：" : "设置安全箱密码（至少 5 个字符）：", AutoSize = true, Left = 16, Top = 84, Font = UIAppFont };
            UITextBox passwordBox = new UITextBox { PasswordChar = '●', Width = 366, Left = 16, Top = 108, Font = UIAppFont };

            UITextBox confirmBox = null;
            UILabel confirmCaption = null;
            if (!existingSafe)
            {
                confirmCaption = new UILabel { Text = "确认密码：", AutoSize = true, Left = 16, Top = 142, Font = UIAppFont };
                confirmBox = new UITextBox { PasswordChar = '●', Width = 366, Left = 16, Top = 166, Font = UIAppFont };
            }

            UILabel errorText = new UILabel
            {
                AutoSize = false,
                Width = 366,
                Height = 34,
                Left = 16,
                Top = existingSafe ? 156 : 200,
                Font = UISmallFont,
                Tag = "danger",
            };

            UIButton lockBtn = null;
            int buttonTop = existingSafe ? 200 : 240;
            if (existingSafe)
            {
                lockBtn = new UIButton { Text = "锁定", Width = 86, Height = 30, Left = 130, Top = buttonTop, Tag = "window", Font = UIAppFont };
                lockBtn.Click += delegate
                {
                    CloseSafe();
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                };
            }

            UIButton okBtn = new UIButton { Text = existingSafe ? "解锁" : "创建", Width = 86, Height = 30, Left = 226, Top = buttonTop, Tag = "accent", Font = UIAppFont };
            UIButton cancelBtn = new UIButton { Text = "取消", Width = 86, Height = 30, Left = 34, Top = buttonTop, Tag = "window", Font = UIAppFont };
            cancelBtn.Click += delegate { dialog.DialogResult = DialogResult.Cancel; dialog.Close(); };
            dialog.CancelButton = cancelBtn;
            dialog.AcceptButton = okBtn;

            content.Controls.Add(title);
            content.Controls.Add(hint);
            content.Controls.Add(passwordCaption);
            content.Controls.Add(passwordBox);
            if (confirmCaption != null)
                content.Controls.Add(confirmCaption);
            if (confirmBox != null)
                content.Controls.Add(confirmBox);
            content.Controls.Add(errorText);
            if (lockBtn != null)
                content.Controls.Add(lockBtn);
            content.Controls.Add(okBtn);
            content.Controls.Add(cancelBtn);
            dialog.Controls.Add(content);
            dialog.ApplyTheme(ThemeService);
            errorText.ForeColor = ThemeService.Danger;
            passwordBox.Focus();

            okBtn.Click += delegate
            {
                string password = passwordBox.Text;
                if (string.IsNullOrEmpty(password) || password.Length < 5)
                {
                    errorText.Text = "密码至少需要 5 个字符。";
                    return;
                }

                if (!existingSafe)
                {
                    string confirm = confirmBox != null ? confirmBox.Text : string.Empty;
                    if (password != confirm)
                    {
                        errorText.Text = "两次输入的密码不一致。";
                        return;
                    }
                }

                SecureString securePassword = CryptoUtils.StringToSecureString(password);

                if (existingSafe)
                {
                    // Try to open all existing safes (defensively close first)
                    bool anyOpened = false;
                    foreach (SafeModel safe in _repository.Safes)
                    {
                        try
                        {
                            byte[] testEncrypted = CryptoUtils.Base64StringToBytes(safe.SerializeableKey);
                            ICryptor testCryptor = new Cryptor(SafeModel.CryptorPackageName, null);
                            bool testNeedsReEnc;
                            byte[] testDecrypted = testCryptor.Decrypt(testEncrypted, securePassword, out testNeedsReEnc);
                            _logService.Info(string.Format("直接解密安全箱成功: needsReEnc={0}", testNeedsReEnc));
                        }
                        catch (Exception testEx)
                        {
                            _logService.Error(string.Format("直接解密安全箱失败, 实际异常: {0}", testEx));
                        }

                        _safeKeyService.CloseSafe(safe.Id);
                        bool needsReEncryption;
                        if (_safeKeyService.TryOpenSafe(safe, securePassword, out needsReEncryption))
                        {
                            anyOpened = true;
                            if (needsReEncryption)
                            {
                                var settings = Program.Services.GetRequiredService<ISettingsService>().LoadSettingsOrDefault();
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
                        dialog.DialogResult = DialogResult.OK;
                        dialog.Close();
                        SetStatus("安全箱已解锁。");
                        foreach (NoteModel note in _repository.Notes)
                        {
                            if (note.SafeId.HasValue && _safeKeyService.IsSafeOpen(note.SafeId.Value) && !string.IsNullOrEmpty(note.HtmlContent))
                            {
                                string decrypted = DecryptSafeNoteContent(note);
                                if (decrypted != null)
                                {
                                    string title2 = BuildPlainText(decrypted);
                                    if (!string.IsNullOrEmpty(title2))
                                        _safeNoteTitles[note.Id] = title2;
                                }
                            }
                        }
                        RefreshNoteList(_selectedNote);
                        if (_selectedNote != null && _selectedNote.SafeId.HasValue && _safeKeyService.IsSafeOpen(_selectedNote.SafeId.Value))
                            SelectNote(_selectedNote);
                    }
                    else
                    {
                        _logService.Info(string.Format("安全箱打开失败: 仓库中有 {0} 个安全箱", _repository.Safes.Count));
                        foreach (SafeModel s in _repository.Safes)
                        {
                            _logService.Info(string.Format("  安全箱 {0}: 有密钥={1}, 密钥长度={2}", s.Id, !string.IsNullOrEmpty(s.SerializeableKey), s.SerializeableKey != null ? s.SerializeableKey.Length : 0));
                            if (!string.IsNullOrEmpty(s.SerializeableKey))
                            {
                                try
                                {
                                    byte[] raw = CryptoUtils.Base64StringToBytes(s.SerializeableKey);
                                    string header = CryptoUtils.BytesToString(raw).Substring(0, Math.Min(25, raw.Length));
                                    _logService.Info(string.Format("  密钥头内容: '{0}'", header));
                                }
                                catch (Exception ex)
                                {
                                    _logService.Error("  密钥 Base64 解析失败", ex);
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
                        var settings = Program.Services.GetRequiredService<ISettingsService>().LoadSettingsOrDefault();
                        string algorithm = settings.SelectedEncryptionAlgorithm;
                        string kdfAlgorithm = settings.SelectedKdfAlgorithm;

                        byte[] key = _cryptoRandomService.GetRandomBytes(32);
                        safe.SerializeableKey = SafeModel.EncryptKey(key, securePassword, _cryptoRandomService, algorithm, kdfAlgorithm);

                        bool dummyNeedsReEncryption;
                        if (!_safeKeyService.TryOpenSafe(safe, securePassword, out dummyNeedsReEncryption))
                        {
                            errorText.Text = "安全箱创建失败，无法验证密钥。";
                            return;
                        }

                        _repository.Safes.Add(safe);
                        RepositoryStorageService.TrySaveRepository(_repository);
                        dialog.DialogResult = DialogResult.OK;
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

            dialog.ShowDialog(this);
        }

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

        private string EncryptSafeNoteContent(string unlockedContent)
        {
            if (_selectedNote == null || !_selectedNote.SafeId.HasValue)
                return unlockedContent;

            byte[] safeKey;
            if (!_safeKeyService.TryGetKey(_selectedNote.SafeId.Value, out safeKey))
                return unlockedContent;

            try
            {
                string algorithm = Program.Services.GetRequiredService<ISettingsService>().LoadSettingsOrDefault().SelectedEncryptionAlgorithm;
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

        #endregion

        #region Status and text helpers

        private void SetStatus(string message, bool isError = false)
        {
            _statusLabel.Text = string.Format("{0}  {1:T}", message, DateTime.Now);
            _statusLabel.ForeColor = isError ? ThemeService.Danger : ThemeService.TextSecondary;
        }

        private void UpdateWordCount()
        {
            if (_selectedNote == null || _editor == null)
            {
                _wordCountLabel.Text = string.Empty;
                return;
            }

            try
            {
                string plain = BuildPlainText(_editor.GetHtml() ?? string.Empty);
                _wordCountLabel.Text = plain.Length > 0
                    ? string.Format("{0} 字", plain.Length)
                    : string.Empty;
            }
            catch
            {
                _wordCountLabel.Text = string.Empty;
            }
        }

        private static string BuildTitle(NoteModel note)
        {
            string heading = ExtractFirstHeading(note.HtmlContent);
            if (!string.IsNullOrWhiteSpace(heading))
                return heading.Length > 80 ? heading.Substring(0, 80) + "..." : heading;

            string text = BuildPlainText(note.HtmlContent);
            if (string.IsNullOrWhiteSpace(text))
                return "无标题笔记";
            return text.Length > 80 ? text.Substring(0, 80) + "..." : text;
        }

        private static string ExtractFirstHeading(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return null;

            Match match = Regex.Match(html, @"<h[123][^>]*>(.*?)</h[123]>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success)
            {
                string headingHtml = match.Groups[1].Value;
                string headingText = Regex.Replace(headingHtml, "<.*?>", " ");
                headingText = System.Net.WebUtility.HtmlDecode(headingText);
                headingText = Regex.Replace(headingText, "\\s+", " ").Trim();
                return string.IsNullOrWhiteSpace(headingText) ? null : headingText;
            }
            return null;
        }

        private static string BuildPlainText(string html)
        {
            string withoutTags = Regex.Replace(html ?? string.Empty, "<.*?>", " ");
            string decoded = System.Net.WebUtility.HtmlDecode(withoutTags);
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

        private static string BuildBodyLine(NoteModel note)
        {
            if (string.IsNullOrWhiteSpace(note.HtmlContent))
                return null;

            string withoutHeadings = Regex.Replace(note.HtmlContent, @"<h[123][^>]*>.*?</h[123]>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            string plainText = BuildPlainText(withoutHeadings);

            if (string.IsNullOrWhiteSpace(plainText))
                return null;

            return plainText.Length > 60 ? plainText.Substring(0, 60) + "..." : plainText;
        }

        #endregion

        /// <summary>Display item for the owner-drawn note list.</summary>
        private sealed class NoteListItem
        {
            public NoteListItem(NoteModel note)
            {
                Note = note;
                RefreshDisplay();
            }

            public NoteModel Note { get; private set; }

            public string Title { get; private set; }

            public string BodyLine { get; private set; }

            public string SecondaryLine { get; private set; }

            public bool IsPinned
            {
                get { return Note.IsPinned; }
            }

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

            public override string ToString()
            {
                return Title ?? string.Empty;
            }
        }
    }
}
