Option Strict On
Option Explicit On
Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.IO
Imports System.Linq
Imports System.Security
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.VisualBasic
Imports SilentNotes.Crypto
Imports SilentNotes.Models
Imports SilentNotes.Services
Imports SilentNotes.WindowsVb.Controls
Imports SilentNotes.WindowsVb.Services
Imports SilentNotes.WindowsVb.Workers
Imports Sunny.UI
Imports VanillaCloudStorageClient

Namespace SilentNotes.WindowsVb.Views
    ''' <summary>
    ''' WinForms 版 WPF MainWindow 的移植：两栏工作区（左侧找笔记、右侧写笔记），
    ''' 底部状态栏非阻塞反馈，危险操作只出现在回收站模式。内容基于 SunnyUI 控件
    ''' （每个都标记 Style=Custom 并经 WinFormsThemeService 着色），窗口用系统原生
    ''' 标题栏，因此标题栏按钮与窗口动画保持完整。
    ''' </summary>
    Friend Class MainForm
        Inherits Form

        Private ReadOnly _syncService As WindowsSynchronizationService
        Private ReadOnly _internetStateService As IInternetStateService
        Private ReadOnly _safeKeyService As ISafeKeyService
        Private ReadOnly _cryptoRandomService As ICryptoRandomService
        Private ReadOnly _logService As ILogService
        Private ReadOnly _htmlCompatibilityInspector As New HtmlCompatibilityInspector()
        Private _autoSyncTimer As System.Threading.Timer

        Private _repository As NoteRepositoryModel
        Private _selectedNote As NoteModel
        Private _pendingSelectNote As NoteModel
        Private _loadingSelection As Boolean
        Private _loadingEditorMetadata As Boolean
        Private _contentDirty As Boolean
        Private _showRecycleBin As Boolean
        Private _searchText As String = String.Empty
        Private _selectedTag As String
        Private _editorTitleBase As String = "编辑器"
        Private _tagSuggestions As New List(Of String)()
        Private ReadOnly _safeNoteTitles As New Dictionary(Of Guid, String)()
        Private _listItems As New List(Of NoteListItem)()

        ' SunnyUI 控件不继承容器字体；需显式赋值。
        Private Shared ReadOnly UIAppFont As New Font("Microsoft YaHei UI", 9.0F)
        Private Shared ReadOnly UISmallFont As New Font("Microsoft YaHei UI", 8.5F)
        Private Shared ReadOnly UIEditorTitleFont As New Font("Microsoft YaHei UI", 13.5F, FontStyle.Bold)
        Private Shared ReadOnly UIListTitleFont As New Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold)
        Private Shared ReadOnly UIListBodyFont As New Font("Microsoft YaHei UI", 8.0F)
        Private ReadOnly _toolTip As New UIToolTip()

        ' 顶栏
        Private _repositorySummaryLabel As UILabel
        Private _safeButton As UIButton
        Private _syncButton As UIButton
        Private _settingsButton As UIButton
        Private _moreButton As UIButton
        Private _saveButton As UIButton
        Private _moreMenu As UIContextMenuStrip

        ' 侧栏
        Private _modeTogglePanel As Panel
        Private _activeModeButtons As Panel
        Private _recycleBinModeButtons As Panel
        Private _modeToggle As SegmentedToggle
        Private _newNoteButton As UIButton
        Private _newChecklistButton As UIButton
        Private _deleteNoteButton As UIButton
        Private _restoreButton As UIButton
        Private _permanentDeleteButton As UIButton
        Private _emptyBinButton As UIButton
        Private _searchBox As UITextBox
        Private _tagPanel As FlowLayoutPanel
        Private _emptyListLabel As UILabel
        Private _notesList As UIListBox

        ' 编辑区
        Private _editorTitleLabel As UILabel
        Private _editorInfoLabel As UILabel
        Private _pinnedButton As UISymbolButton
        Private _pinnedState As Boolean
        Private _tagsTextBox As UITextBox
        Private _noteTagPanel As FlowLayoutPanel
        Private _tagSuggestionList As UIListBox
        Private _loadingTagSuggestions As Boolean
        Private _toolbar As UIPanel
        Private _editor As HtmlEditorControl
        ' 镜像编辑器状态的格式化工具栏按钮，键为 queryCommandState 的命令
        ' （"Bold"、"InsertUnorderedList"...）或 FormatBlock 按钮的块标签
        ' （"H1"、"BLOCKQUOTE"、"PRE"）。
        Private ReadOnly _toolbarStateButtons As New Dictionary(Of String, UIButton)()
        Private _toolbarStatePending As Boolean
        Private _wordCountPending As Boolean

        ' 状态栏
        Private _statusPanel As UIPanel
        Private _statusLabel As UILabel
        Private _wordCountLabel As UILabel

        Public Sub New()
            Text = "SilentNotes"
            Font = New Font("Microsoft YaHei UI", 9.0F)
            StartPosition = FormStartPosition.CenterScreen
            MinimumSize = New Size(760, 520)
            Size = New Size(1120, 760)

            Try
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath)
            Catch
            End Try

            _syncService = New WindowsSynchronizationService(
                Program.Services.GetRequiredService(Of ISettingsService)(),
                Program.Services.GetRequiredService(Of IRepositoryStorageService)(),
                Program.Services.GetRequiredService(Of IDataProtectionService)(),
                Program.Services.GetRequiredService(Of ICryptoRandomService)(),
                Program.Services.GetRequiredService(Of ILogService)(),
                Program.Services.GetRequiredService(Of IXmlFileService)())
            _internetStateService = Program.Services.GetRequiredService(Of IInternetStateService)()
            _safeKeyService = Program.Services.GetRequiredService(Of ISafeKeyService)()
            _cryptoRandomService = Program.Services.GetRequiredService(Of ICryptoRandomService)()
            _logService = Program.Services.GetRequiredService(Of ILogService)()

            ' 周期自动同步计时器（每 30 分钟）
            _autoSyncTimer = New System.Threading.Timer(
                AddressOf OnAutoSyncTimerTick,
                Nothing,
                System.Threading.Timeout.Infinite,
                System.Threading.Timeout.Infinite)

            BuildUi()
            WireEvents()

            ' 通过在 Win32 层面把绘制设为合成（双缓冲），消除原生 ListBox 在悬停/选中时
            ' 先擦除再重绘导致的闪烁。只作用于内层列表框：外层面板也合成会让滚动变卡。
            EnableComposited(_notesList.ListBox)
        End Sub

        ' System.Threading.Timer 的回调：已在线程池线程上，直接执行自动同步。
        Private Sub OnAutoSyncTimerTick(state As Object)
            TryAutoSync(False)
        End Sub

        Private ReadOnly Property RepositoryStorageService As IRepositoryStorageService
            Get
                Return Program.Services.GetRequiredService(Of IRepositoryStorageService)()
            End Get
        End Property

        Private ReadOnly Property ThemeService As WinFormsThemeService
            Get
                Return Program.Services.GetRequiredService(Of WinFormsThemeService)()
            End Get
        End Property

#Region "UI construction"

        Private Sub BuildUi()
            BuildStatusBar()
            BuildTopBar()
            BuildSplit()
            BuildMoreMenu()
        End Sub

        Private Sub BuildStatusBar()
            _statusPanel = New UIPanel With {.Dock = DockStyle.Bottom, .Height = 30, .Tag = "panel", .Radius = 0}
            _wordCountLabel = New UILabel With {
                .Dock = DockStyle.Right,
                .Width = 90,
                .Text = String.Empty,
                .TextAlign = ContentAlignment.MiddleRight,
                .Padding = New Padding(0, 0, 10, 0),
                .Font = UISmallFont,
                .Tag = "secondary"
            }
            _statusLabel = New UILabel With {
                .Dock = DockStyle.Fill,
                .Text = "就绪",
                .TextAlign = ContentAlignment.MiddleLeft,
                .Padding = New Padding(10, 0, 0, 0),
                .Font = UISmallFont,
                .Tag = "secondary"
            }
            _statusPanel.Controls.Add(_statusLabel)
            _statusPanel.Controls.Add(_wordCountLabel)
            Controls.Add(_statusPanel)
        End Sub

        Private Sub BuildTopBar()
            Dim topPanel As New UIPanel With {.Dock = DockStyle.Top, .Height = 52, .Tag = "window", .Radius = 0}

            _repositorySummaryLabel = New UILabel With {
                .Text = String.Empty,
                .AutoSize = False,
                .Width = 480,
                .Height = 34,
                .TextAlign = ContentAlignment.MiddleLeft,
                .Padding = New Padding(4, 0, 0, 0),
                .Anchor = AnchorStyles.Left Or AnchorStyles.Top Or AnchorStyles.Bottom,
                .Font = UISmallFont,
                .Tag = "secondary"
            }

            Dim topButtons As New UIPanel With {.Dock = DockStyle.Right, .Width = 264, .Radius = 0, .Tag = "window"}
            _saveButton = CreateTextButton("保存", 0, "action", 64, 32)
            _moreButton = CreateIconButton(FontAwesomeIcons.fa_ellipsis_h, "更多", "ghost", 30, 30)
            _settingsButton = CreateIconButton(FontAwesomeIcons.fa_cog, "设置", "ghost", 30, 30)
            _safeButton = CreateIconButton(FontAwesomeIcons.fa_lock, "安全箱", "ghost", 30, 30)
            _syncButton = CreateIconButton(FontAwesomeIcons.fa_refresh, "同步", "ghost", 30, 30)
            Dim visualOrderRightToLeft As UIButton() = New UIButton() {
                _saveButton, _moreButton, _settingsButton, _safeButton, _syncButton
            }
            For Each button As UIButton In visualOrderRightToLeft
                topButtons.Controls.Add(button)
            Next
            Dim relayout As EventHandler = Sub(sender As Object, e As EventArgs)
                                               Dim x As Integer = topButtons.ClientSize.Width - 8
                                               For Each button As UIButton In visualOrderRightToLeft
                                                   button.Left = x - button.Width
                                                   button.Top = Math.Max(2, (topButtons.ClientSize.Height - button.Height) \ 2)
                                                   x = button.Left - 8
                                               Next
                                           End Sub
            AddHandler topButtons.Resize, relayout
            AddHandler topButtons.HandleCreated, relayout

            _repositorySummaryLabel.Dock = DockStyle.Fill
            topPanel.Controls.Add(_repositorySummaryLabel)
            topPanel.Controls.Add(topButtons)
            Controls.Add(topPanel)
            topPanel.BringToFront()
            _statusPanel.SendToBack()
        End Sub

        Private Sub BuildMoreMenu()
            _moreMenu = New UIContextMenuStrip With {.Style = UIStyle.Custom, .Font = UIAppFont}
            AppendMenuItem(_moreMenu, "重载仓库", FontAwesomeIcons.fa_refresh, AddressOf ReloadButton_Click)
            AppendMenuItem(_moreMenu, "恢复备份", FontAwesomeIcons.fa_history, AddressOf RestoreBackupButton_Click)
            AppendMenuItem(_moreMenu, "帮助与快捷键", FontAwesomeIcons.fa_question_circle, AddressOf HelpButton_Click)
            AppendMenuItem(_moreMenu, "切换浅色/深色", FontAwesomeIcons.fa_adjust, AddressOf ThemeToggleButton_Click)
            AddHandler _moreButton.Click, Sub()
                                              _moreMenu.Show(_moreButton, New Point(_moreButton.Width - _moreMenu.Width, _moreButton.Height + 2))
                                          End Sub
        End Sub

        Private Sub AppendMenuItem(menu As UIContextMenuStrip, text As String, symbol As Integer, onClick As EventHandler)
            ' symbol 保存在 Tag 里，让 RefreshMoreMenuTheme 能在每次主题切换时
            ' 用当前调色板重建位图。
            Dim item As New ToolStripMenuItem(text) With {
                .Tag = symbol,
                .Image = FontImageHelper.CreateImage(symbol, 14, ThemeService.TextSecondary)
            }
            AddHandler item.Click, onClick
            menu.Items.Add(item)
        End Sub

        Private Sub RefreshMoreMenuTheme()
            ' 右键菜单不在窗体控件树里，主题遍历永远到不了它；主题切换后颜色和
            ' 预渲染的图标位图都会过时，除非在这里刷新。
            Dim theme As WinFormsThemeService = ThemeService
            _moreMenu.BackColor = theme.SurfacePaper
            _moreMenu.ForeColor = theme.TextPrimary
            For Each item As ToolStripItem In _moreMenu.Items
                item.ForeColor = theme.TextPrimary
                If TypeOf item.Tag Is Integer Then
                    item.Image = FontImageHelper.CreateImage(CInt(item.Tag), 14, theme.TextSecondary)
                End If
            Next
        End Sub

        Private Function CreateIconButton(symbol As Integer, tip As String, tag As String, width As Integer, height As Integer) As UIButton
            Dim button As UIButton = New UISymbolButton With {
                .Symbol = symbol,
                .SymbolSize = 16,
                .Tag = If(tag, String.Empty),
                .Radius = 6,
                .Font = UIAppFont
            }
            If Not String.IsNullOrEmpty(tip) Then
                _toolTip.SetToolTip(button, tip)
            End If
            If width > 0 Then
                button.Width = width
                button.Height = height
            End If
            Return button
        End Function

        Private Function CreateTextButton(text As String, symbol As Integer, tag As String, width As Integer, height As Integer) As UIButton
            Dim button As UIButton
            If symbol <> 0 Then
                button = New UISymbolButton With {.Symbol = symbol, .SymbolSize = 16}
            Else
                button = New UIButton()
            End If
            button.Text = text
            button.Tag = If(tag, String.Empty)
            button.Radius = 6
            button.Font = UIAppFont
            If width > 0 Then
                button.Width = width
                button.Height = height
            Else
                Dim preferred As Size = TextRenderer.MeasureText(text, UIAppFont)
                button.Width = preferred.Width + If(symbol <> 0, 40, 26)
                button.Height = If(height > 0, height, 30)
            End If
            Return button
        End Function

        Private Sub BuildSplit()
            Dim split As New SplitContainer With {
                .Dock = DockStyle.Fill,
                .SplitterWidth = 1
            }
            BuildSidebar(split.Panel1)
            BuildEditorArea(split.Panel2)
            ' Min sizes 与 SplitterDistance 在 OnLoad 里设置：此时控件还是默认大小，
            ' 现在设置会抛异常。
            Controls.Add(split)
            split.BringToFront()
            _splitContainer = split
        End Sub

        Private Sub ApplySplitLayout()
            _splitContainer.Panel1MinSize = 240
            _splitContainer.Panel2MinSize = 320
            _splitContainer.FixedPanel = FixedPanel.Panel1
            Dim maxDistance As Integer = _splitContainer.Width - _splitContainer.Panel2MinSize - _splitContainer.SplitterWidth
            If maxDistance >= _splitContainer.Panel1MinSize Then
                _splitContainer.SplitterDistance = Math.Min(300, maxDistance)
            End If
        End Sub

        Private _splitContainer As SplitContainer

        Private Sub BuildSidebar(panel As Panel)
            panel.Padding = New Padding(10, 6, 10, 6)

            ' SunnyUI UIListBox：它自己的 OnDrawItem 先画背景和条目文本，再触发
            ' DrawItem，所以我们的处理器用两行笔记卡片重绘整行矩形（覆盖默认文本）。
            _notesList = New UIListBox With {
                .Dock = DockStyle.Fill,
                .ItemHeight = 56,
                .Style = UIStyle.Custom,
                .Font = UIAppFont
            }

            _emptyListLabel = New UILabel With {
                .Dock = DockStyle.Fill,
                .TextAlign = ContentAlignment.MiddleCenter,
                .Text = "还没有笔记。",
                .Visible = False,
                .Font = UIAppFont,
                .Tag = "secondary"
            }

            ' 标签筛选药丸放在一个裁剪容器里：AutoScroll 的系统横向滚动条落在
            ' 容器外面并被隐藏（Shift+滚轮可横向滚动）。
            Dim tagPanelHost As New Panel With {.Dock = DockStyle.Top, .Height = 34, .BackColor = Color.Transparent, .Tag = "bare"}
            _tagPanel = New FlowLayoutPanel With {
                .Top = 0,
                .Left = 0,
                .Width = 300,
                .Height = 60,
                .WrapContents = False,
                .AutoScroll = True,
                .Padding = New Padding(0, 2, 0, 2),
                .BackColor = Color.Transparent,
                .Font = UIAppFont,
                .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
            }
            tagPanelHost.Controls.Add(_tagPanel)
            AddHandler tagPanelHost.Resize, Sub() _tagPanel.Width = tagPanelHost.ClientSize.Width

            _searchBox = New UITextBox With {
                .Dock = DockStyle.Top,
                .Height = 34,
                .Watermark = "搜索笔记",
                .Symbol = FontAwesomeIcons.fa_search,
                .SymbolSize = 16,
                .Font = UIAppFont
            }

            ' 模式切换器在两种模式下都保持可见，保证永远有回来的路。
            _modeTogglePanel = New Panel With {
                .Dock = DockStyle.Top,
                .Height = 38,
                .BackColor = Color.Transparent,
                .Padding = New Padding(4, 5, 4, 5)
            }
            _modeToggle = New SegmentedToggle(New String() {"活动笔记", "回收站"}) With {
                .Dock = DockStyle.Fill,
                .Font = UIAppFont
            }
            _modeTogglePanel.Controls.Add(_modeToggle)

            _activeModeButtons = New Panel With {.Dock = DockStyle.Top, .Height = 40, .BackColor = Color.Transparent}
            _recycleBinModeButtons = New Panel With {.Dock = DockStyle.Top, .Height = 40, .BackColor = Color.Transparent, .Visible = False}

            ' Dock 顺序：后加入的在最上面。
            panel.Controls.Add(_notesList)
            panel.Controls.Add(_emptyListLabel)
            panel.Controls.Add(tagPanelHost)
            panel.Controls.Add(_searchBox)
            panel.Controls.Add(_activeModeButtons)
            panel.Controls.Add(_recycleBinModeButtons)
            panel.Controls.Add(_modeTogglePanel)

            BuildActiveModeButtons()
            BuildRecycleBinModeButtons()
        End Sub

        Private Sub BuildActiveModeButtons()
            _newNoteButton = CreateTextButton("新建笔记", FontAwesomeIcons.fa_plus, "action", 0, 30)
            AddHandler _newNoteButton.Click, AddressOf NewNoteButton_Click
            _newChecklistButton = CreateTextButton("清单", FontAwesomeIcons.fa_list_ul, "window", 0, 30)
            AddHandler _newChecklistButton.Click, AddressOf NewChecklistButton_Click
            _deleteNoteButton = CreateIconButton(FontAwesomeIcons.fa_trash, "移到回收站（Delete）", "ghost", 30, 30)
            AddHandler _deleteNoteButton.Click, AddressOf DeleteNoteButton_Click
            Dim row As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .WrapContents = False,
                .BackColor = Color.Transparent,
                .Font = UIAppFont
            }
            row.Controls.Add(_newNoteButton)
            row.Controls.Add(_newChecklistButton)
            row.Controls.Add(_deleteNoteButton)
            _activeModeButtons.Controls.Add(row)
        End Sub

        Private Sub BuildRecycleBinModeButtons()
            _restoreButton = CreateTextButton("恢复", FontAwesomeIcons.fa_undo, "window", 0, 30)
            _toolTip.SetToolTip(_restoreButton, "恢复选中的笔记")
            AddHandler _restoreButton.Click, AddressOf RestoreNoteButton_Click
            _permanentDeleteButton = CreateTextButton("永久删除", FontAwesomeIcons.fa_trash, "danger", 0, 30)
            _toolTip.SetToolTip(_permanentDeleteButton, "彻底删除选中的笔记，不可恢复")
            AddHandler _permanentDeleteButton.Click, AddressOf PermanentDeleteNoteButton_Click
            _emptyBinButton = CreateTextButton("清空", FontAwesomeIcons.fa_recycle, "danger", 0, 30)
            _toolTip.SetToolTip(_emptyBinButton, "清空回收站中的全部笔记")
            AddHandler _emptyBinButton.Click, AddressOf EmptyRecycleBinButton_Click
            Dim row As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .WrapContents = False,
                .BackColor = Color.Transparent,
                .Font = UIAppFont
            }
            row.Controls.Add(_restoreButton)
            row.Controls.Add(_permanentDeleteButton)
            row.Controls.Add(_emptyBinButton)
            _recycleBinModeButtons.Controls.Add(row)
        End Sub

        Private Sub BuildEditorArea(panel As Panel)
            _editor = New HtmlEditorControl With {.Dock = DockStyle.Fill}

            ' 标题行：笔记标题 + 置顶切换
            Dim titleRow As New Panel With {.Dock = DockStyle.Top, .Height = 40}
            _pinnedButton = CType(CreateIconButton(FontAwesomeIcons.fa_star, "置顶", "ghost", 28, 28), UISymbolButton)
            AddHandler _pinnedButton.Click, Sub()
                                                If _loadingEditorMetadata OrElse _selectedNote Is Nothing OrElse _showRecycleBin Then
                                                    Return
                                                End If
                                                _pinnedState = Not _pinnedState
                                                RefreshPinnedButton()
                                                SaveSelectedMetadata()
                                            End Sub
            _editorTitleLabel = New UILabel With {
                .Dock = DockStyle.Fill,
                .Text = "编辑器",
                .TextAlign = ContentAlignment.MiddleLeft,
                .Padding = New Padding(4, 4, 0, 0),
                .Font = UIEditorTitleFont,
                .AutoEllipsis = True
            }
            _pinnedButton.Dock = DockStyle.Right
            titleRow.Controls.Add(_editorTitleLabel)
            titleRow.Controls.Add(_pinnedButton)

            _editorInfoLabel = New UILabel With {
                .Dock = DockStyle.Top,
                .Height = 22,
                .Text = "请选择一条笔记。",
                .TextAlign = ContentAlignment.MiddleLeft,
                .Padding = New Padding(4, 0, 0, 0),
                .Font = UISmallFont,
                .Tag = "secondary"
            }

            ' 标签行：说明文字、输入框、当前笔记的标签做成可移除的芯片
            Dim tagRow As New Panel With {.Dock = DockStyle.Top, .Height = 36}
            _noteTagPanel = New FlowLayoutPanel With {
                .WrapContents = False,
                .AutoScroll = False,
                .Padding = New Padding(4, 4, 4, 2),
                .BackColor = Color.Transparent,
                .Font = UIAppFont
            }
            _tagsTextBox = New UITextBox With {
                .Width = 180,
                .Watermark = "输入标签后回车",
                .Anchor = AnchorStyles.Left,
                .Font = UIAppFont
            }
            Dim tagCaption As New UILabel With {
                .Text = "标签",
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Tag = "secondary"
            }
            tagCaption.Font = New Font("Microsoft YaHei UI", 9.0F)
            tagRow.Controls.Add(_noteTagPanel)
            tagRow.Controls.Add(_tagsTextBox)
            tagRow.Controls.Add(tagCaption)
            AddHandler tagRow.Resize, Sub()
                                          tagCaption.Left = 12
                                          tagCaption.Top = (tagRow.ClientSize.Height - tagCaption.Height) \ 2
                                          _tagsTextBox.Left = tagCaption.Right + 8
                                          _tagsTextBox.Top = (tagRow.ClientSize.Height - _tagsTextBox.Height) \ 2
                                          _noteTagPanel.Left = _tagsTextBox.Right + 8
                                          _noteTagPanel.Top = 0
                                          _noteTagPanel.Width = Math.Max(0, tagRow.ClientSize.Width - _noteTagPanel.Left - 8)
                                          _noteTagPanel.Height = tagRow.ClientSize.Height
                                      End Sub

            _tagSuggestionList = New UIListBox With {
                .Dock = DockStyle.Top,
                .Height = 70,
                .ItemHeight = 28,
                .Style = UIStyle.Custom,
                .Visible = False,
                .Font = UIAppFont
            }

            ' 格式化工具栏：图标按钮按 UILine 分组分隔。镜像编辑器状态的按钮把
            ' 命令/块标签作为最后一个参数传入。
            _toolbar = New UIPanel With {.Dock = DockStyle.Top, .Height = 44, .Tag = "window", .Radius = 0}
            Dim toolbarRow As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = False, .Padding = New Padding(6, 8, 6, 6), .BackColor = Color.Transparent}
            AddToolbarTextButton(toolbarRow, "H1", "标题 1", Sub() _editor.SetHeading(1, True), "H1")
            AddToolbarTextButton(toolbarRow, "H2", "标题 2", Sub() _editor.SetHeading(2, True), "H2")
            AddToolbarTextButton(toolbarRow, "H3", "标题 3", Sub() _editor.SetHeading(3, True), "H3")
            AddToolbarSeparator(toolbarRow)
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_bold, "粗体 (Ctrl+B)", Sub() _editor.ToggleBold(), "Bold")
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_italic, "斜体 (Ctrl+I)", Sub() _editor.ToggleItalic(), "Italic")
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_underline, "下划线 (Ctrl+U)", Sub() _editor.ToggleUnderline(), "Underline")
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_strikethrough, "删除线", Sub() _editor.ToggleStrikethrough(), "StrikeThrough")
            AddToolbarSeparator(toolbarRow)
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_list_ul, "无序列表", Sub() _editor.ToggleUnorderedList(), "InsertUnorderedList")
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_list_ol, "有序列表", Sub() _editor.ToggleOrderedList(), "InsertOrderedList")
            AddToolbarSeparator(toolbarRow)
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_quote_left, "引用", Sub() _editor.SetBlockquote(True), "BLOCKQUOTE")
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_code, "代码块", Sub() _editor.SetCodeBlock(True), "PRE")
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_minus, "分割线", Sub() _editor.InsertHorizontalRule(), Nothing)
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_link, "链接", Sub() LinkButton_Click(), Nothing)
            AddToolbarSeparator(toolbarRow)
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_undo, "撤销 (Ctrl+Z)", Sub() _editor.Undo(), Nothing)
            AddToolbarIconButton(toolbarRow, FontAwesomeIcons.fa_repeat, "重做 (Ctrl+Y)", Sub() _editor.Redo(), Nothing)
            _toolbar.Controls.Add(toolbarRow)

            panel.Controls.Add(_editor)
            panel.Controls.Add(_toolbar)
            panel.Controls.Add(_tagSuggestionList)
            panel.Controls.Add(tagRow)
            panel.Controls.Add(_editorInfoLabel)
            panel.Controls.Add(titleRow)
        End Sub

        Private Sub AddToolbarIconButton(toolbar As FlowLayoutPanel, symbol As Integer, tip As String, onClick As EventHandler, stateKey As String)
            Dim button As UIButton = New UISymbolButton With {
                .Symbol = symbol,
                .SymbolSize = 16,
                .Radius = 4,
                .Width = 28,
                .Height = 28,
                .Tag = "tool",
                .Margin = New Padding(1, 0, 1, 0),
                .Font = UIAppFont
            }
            _toolTip.SetToolTip(button, tip)
            AddHandler button.Click, onClick
            RegisterToolbarStateButton(button, stateKey)
            toolbar.Controls.Add(button)
        End Sub

        Private Sub AddToolbarTextButton(toolbar As FlowLayoutPanel, text As String, tip As String, onClick As EventHandler, stateKey As String)
            Dim button As New UIButton With {
                .Text = text,
                .Radius = 4,
                .Width = 30,
                .Height = 28,
                .Tag = "tool",
                .Margin = New Padding(1, 0, 1, 0),
                .Font = UISmallFont
            }
            _toolTip.SetToolTip(button, tip)
            AddHandler button.Click, onClick
            RegisterToolbarStateButton(button, stateKey)
            toolbar.Controls.Add(button)
        End Sub

        Private Sub RegisterToolbarStateButton(button As UIButton, stateKey As String)
            If String.IsNullOrEmpty(stateKey) Then
                Return
            End If
            _toolbarStateButtons(stateKey) = button
        End Sub

        ''' <summary>刷新有状态工具栏按钮的按下外观。</summary>
        Private Sub UpdateToolbarState()
            If Not _editor.IsReady OrElse _toolbarStateButtons.Count = 0 Then
                Return
            End If

            Dim blockTag As String = _editor.GetCurrentBlockTag()
            For Each pair As KeyValuePair(Of String, UIButton) In _toolbarStateButtons
                Dim active As Boolean
                Select Case pair.Key
                    Case "H1", "H2", "H3", "BLOCKQUOTE", "PRE"
                        active = String.Equals(blockTag, pair.Key, StringComparison.OrdinalIgnoreCase)
                    Case Else
                        active = _editor.QueryState(pair.Key)
                End Select
                SetToolbarButtonActive(pair.Value, active)
            Next
        End Sub

        Private Sub SetToolbarButtonActive(button As UIButton, active As Boolean)
            Dim symbolButton As UISymbolButton = TryCast(button, UISymbolButton)
            Dim theme As WinFormsThemeService = ThemeService
            button.Style = UIStyle.Custom
            If active Then
                button.FillColor = theme.AccentSoft
                button.RectColor = theme.Accent
                button.ForeColor = theme.Accent
                button.FillHoverColor = theme.AccentSoft
                button.RectHoverColor = theme.Accent
                button.ForeHoverColor = theme.Accent
                If symbolButton IsNot Nothing Then
                    symbolButton.SymbolColor = theme.Accent
                    symbolButton.SymbolHoverColor = theme.Accent
                    symbolButton.SymbolPressColor = theme.Accent
                End If
            Else
                ' 重新跑一遍主题服务，得到安静的 "tool" 静息外观。
                theme.Apply(button)
            End If
        End Sub

        Private Shared Sub AddToolbarSeparator(toolbar As FlowLayoutPanel)
            Dim line As New UILine With {
                .Direction = UILine.LineDirection.Vertical,
                .Width = 1,
                .Height = 24,
                .Margin = New Padding(6, 0, 6, 0)
            }
            toolbar.Controls.Add(line)
        End Sub

        Private Sub WireEvents()
            AddHandler _syncButton.Click, AddressOf SyncButton_Click
            AddHandler _settingsButton.Click, AddressOf SyncSettingsButton_Click
            AddHandler _safeButton.Click, AddressOf SafeButton_Click
            AddHandler _saveButton.Click, Sub() SaveSelectedNote()

            AddHandler _modeToggle.SelectedIndexChanged, AddressOf ModeToggle_SelectedIndexChanged
            AddHandler _searchBox.TextChanged, AddressOf SearchTextBox_TextChanged
            AddHandler _notesList.SelectedIndexChanged, AddressOf NotesList_SelectedIndexChanged
            AddHandler _notesList.DrawItem, AddressOf NotesList_DrawItem

            AddHandler _tagsTextBox.TextChanged, AddressOf TagsTextBox_TextChanged

            AddHandler _editor.EditorReady, AddressOf Editor_Ready
            AddHandler _editor.ContentChanged, AddressOf Editor_ContentChanged
            AddHandler _editor.SelectionChanged, AddressOf Editor_SelectionChanged

            AddHandler _tagsTextBox.KeyDown, AddressOf TagsTextBox_KeyDown
            AddHandler _tagsTextBox.LostFocus, Sub()
                                                   HideTagSuggestions()
                                                   SaveSelectedMetadata()
                                               End Sub
            AddHandler _tagSuggestionList.SelectedIndexChanged, AddressOf TagSuggestionList_SelectedIndexChanged
            AddHandler _tagSuggestionList.DrawItem, AddressOf TagSuggestionList_DrawItem
        End Sub

        Private Sub RefreshModeButtonColors()
            _modeToggle.SelectedIndex = If(_showRecycleBin, 1, 0)
        End Sub

        Private Sub RefreshPinnedButton()
            Dim button As UISymbolButton = TryCast(_pinnedButton, UISymbolButton)
            If button Is Nothing Then
                Return
            End If

            button.Style = UIStyle.Custom
            Dim theme As WinFormsThemeService = ThemeService
            If _pinnedState Then
                button.SymbolColor = theme.Action
                button.SymbolHoverColor = theme.Action
                button.RectColor = theme.AccentSoft
                button.FillColor = theme.AccentSoft
            Else
                button.SymbolColor = theme.TextSecondary
                button.SymbolHoverColor = theme.Accent
                button.RectColor = Color.Transparent
                button.FillColor = Color.Transparent
            End If
        End Sub

        Private Sub PopulateNoteTagPanel()
            _noteTagPanel.Controls.Clear()
            If _selectedNote Is Nothing OrElse _showRecycleBin Then
                Return
            End If

            For Each tag As String In _selectedNote.Tags
                If String.IsNullOrWhiteSpace(tag) Then
                    Continue For
                End If
                _noteTagPanel.Controls.Add(CreateNoteTagChip(tag))
            Next
        End Sub

        Private Function CreateNoteTagChip(tag As String) As Control
            Dim chip As New FlowLayoutPanel With {
                .AutoSize = True,
                .WrapContents = False,
                .Margin = New Padding(0, 0, 6, 0),
                .BackColor = Color.Transparent,
                .Font = UIAppFont
            }

            Dim nameButton As New UIButton With {
                .Text = tag,
                .AutoSize = False,
                .Width = TextRenderer.MeasureText(tag, UIAppFont).Width + 18,
                .Height = 24,
                .Radius = 12,
                .Style = UIStyle.Custom,
                .Font = UIAppFont,
                .Margin = Padding.Empty
            }
            AddHandler nameButton.Click, Sub()
                                             ' 点击芯片按该标签筛选笔记列表
                                             _selectedTag = tag
                                             RefreshTagButtonColors()
                                             RefreshNoteList(_selectedNote)
                                             UpdateRepositorySummary()
                                         End Sub

            Dim removeButton As New UISymbolButton With {
                .Symbol = FontAwesomeIcons.fa_times,
                .SymbolSize = 16,
                .Width = 22,
                .Height = 24,
                .Radius = 12,
                .Style = UIStyle.Custom,
                .Margin = New Padding(0, 0, 0, 0),
                .Font = UIAppFont
            }
            _toolTip.SetToolTip(removeButton, "删除标签 """ & tag & """")
            AddHandler removeButton.Click, Sub() DeleteTagByName(tag)

            chip.Controls.Add(nameButton)
            chip.Controls.Add(removeButton)
            Return chip
        End Function

        Private Sub RefreshNoteTagChipColors()
            Dim theme As WinFormsThemeService = ThemeService
            For Each child As Control In _noteTagPanel.Controls
                Dim chip As FlowLayoutPanel = TryCast(child, FlowLayoutPanel)
                If chip Is Nothing Then
                    Continue For
                End If

                For Each part As Control In chip.Controls
                    Dim removeButton As UISymbolButton = TryCast(part, UISymbolButton)
                    If removeButton IsNot Nothing Then
                        removeButton.SymbolColor = theme.TextSecondary
                        removeButton.SymbolHoverColor = theme.Danger
                        removeButton.SymbolPressColor = theme.Danger
                        removeButton.FillColor = theme.SurfacePaper
                        removeButton.RectColor = theme.SurfacePaper
                        removeButton.FillHoverColor = theme.DangerSoft
                        removeButton.RectHoverColor = theme.DangerSoft
                        Continue For
                    End If

                    Dim nameButton As UIButton = TryCast(part, UIButton)
                    If nameButton IsNot Nothing Then
                        nameButton.FillColor = theme.SurfacePaper
                        nameButton.ForeColor = theme.TextPrimary
                        nameButton.RectColor = theme.BorderSubtle
                        nameButton.FillHoverColor = theme.AccentSoft
                        nameButton.RectHoverColor = theme.Accent
                        nameButton.ForeHoverColor = theme.Accent
                    End If
                Next
            Next
        End Sub

#End Region

#Region "Flicker suppression"

        Private Const GWL_EXSTYLE As Integer = -20
        Private Const WS_EX_COMPOSITED As Integer = &H2000000

        Private Shared Sub EnableComposited(control As Control)
            If control Is Nothing Then
                Return
            End If

            Dim onCreated As EventHandler = Nothing
            onCreated = Sub(sender As Object, e As EventArgs)
                            Try
                                Dim windowHandle As IntPtr = control.Handle
                                Dim exStyle As Integer = GetWindowLong(windowHandle, GWL_EXSTYLE)
                                SetWindowLong(windowHandle, GWL_EXSTYLE, exStyle Or WS_EX_COMPOSITED)
                            Catch
                            End Try
                            RemoveHandler control.HandleCreated, onCreated
                        End Sub
            AddHandler control.HandleCreated, onCreated
            If control.IsHandleCreated Then
                onCreated(Nothing, EventArgs.Empty)
            End If
        End Sub

        <System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint:="GetWindowLongW")>
        Private Shared Function GetWindowLong(hWnd As IntPtr, nIndex As Integer) As Integer
        End Function

        <System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint:="SetWindowLongW")>
        Private Shared Function SetWindowLong(hWnd As IntPtr, nIndex As Integer, dwNewLong As Integer) As Integer
        End Function

#End Region

#Region "Lifecycle"

        Protected Overrides Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e)
            ApplySplitLayout()

            ' 启动时校验注册表数据目录（延后执行，让窗体先显示出来）
            BeginInvoke(New Action(Sub()
                                       If Not WindowsDataDirectoryService.HasValidRegistryPath() Then
                                           Dim regPath As String = WindowsDataDirectoryService.ReadFromRegistry()
                                           If Not String.IsNullOrEmpty(regPath) Then
                                               MessageBox.Show(
                                                   String.Format("数据目录 {0} 不存在，请重新指定。", regPath),
                                                   "SilentNotes",
                                                   System.Windows.Forms.MessageBoxButtons.OK,
                                                   System.Windows.Forms.MessageBoxIcon.Warning)
                                           End If
                                           SyncSettingsButton_Click(Me, EventArgs.Empty)
                                       End If
                                   End Sub))

            ' 加载仓库前先应用主题
            Dim settingsService As ISettingsService = Program.Services.GetRequiredService(Of ISettingsService)()
            Dim settings As SettingsModel = settingsService.LoadSettingsOrDefault()
            ThemeService.ApplyTheme(settings.ThemeMode)
            ApplyThemeToUi()

            LoadRepository()

            ' 启动自动同步（不阻塞）
            If _syncService.HasCloudStorageConfigured AndAlso _syncService.HasTransferCode AndAlso ShouldAutoSync(settings.AutoSyncMode) Then
                Task.Factory.StartNew(Sub() TryAutoSync(False))
            End If

            ' 周期自动同步：计时器回调自己检查 ShouldAutoSync。
            _autoSyncTimer.Change(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30))
        End Sub

        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            If _contentDirty AndAlso _selectedNote IsNot Nothing AndAlso Not _showRecycleBin Then
                Dim choice As DialogResult = ThemedConfirmDialog.ShowSavePrompt(
                    Me, ThemeService, "未保存的更改",
                    String.Format("笔记 ""{0}"" 有未保存的更改，是否保存？", _editorTitleBase))
                If choice = DialogResult.Cancel Then
                    e.Cancel = True
                    Return
                End If
                If choice = DialogResult.Yes Then
                    Dim saved As Boolean = SaveSelectedNote(False)
                    If Not saved Then
                        SetStatus("保存失败，已取消关闭窗口。", True)
                        e.Cancel = True
                        Return
                    End If
                End If
            End If

            If _autoSyncTimer IsNot Nothing Then
                _autoSyncTimer.Dispose()
                _autoSyncTimer = Nothing
            End If
            MyBase.OnFormClosing(e)
        End Sub

        Private Sub ApplyThemeToUi()
            Dim theme As WinFormsThemeService = ThemeService
            theme.Apply(Me)
            theme.ApplyWindowTheme(Me)
            ' 原生标题栏：颜色跟随系统主题；DWM immersive dark mode
            ' （ApplyWindowTheme）让深色模式下标题栏保持深色。
            BackColor = theme.SurfaceWindow
            RefreshModeButtonColors()
            PopulateTagPanel(CurrentVisibleTags(), _selectedTag)
            RefreshNoteTagChipColors()
            RefreshPinnedButton()
            RefreshMoreMenuTheme()
            _notesList.Invalidate()
            Dim isChecklist As Boolean = _selectedNote IsNot Nothing AndAlso _selectedNote.NoteType = NoteType.Checklist
            _editor.SetEditorTheme(theme.IsDarkMode, isChecklist)
            UpdateToolbarState()
        End Sub

        Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
            Dim key As Keys = keyData And Keys.KeyCode
            If (keyData And Keys.Control) = Keys.Control Then
                Select Case key
                    Case Keys.N
                        NewNoteButton_Click(Me, EventArgs.Empty)
                        Return True
                    Case Keys.S
                        SaveSelectedNote()
                        Return True
                    Case Keys.F
                        _searchBox.TextBox.Focus()
                        _searchBox.TextBox.SelectAll()
                        Return True
                    Case Keys.B
                        _editor.FocusEditor()
                        _editor.ToggleBold()
                        Return True
                    Case Keys.I
                        _editor.FocusEditor()
                        _editor.ToggleItalic()
                        Return True
                    Case Keys.U
                        _editor.FocusEditor()
                        _editor.ToggleUnderline()
                        Return True
                    Case Keys.Z
                        _editor.FocusEditor()
                        _editor.Undo()
                        Return True
                    Case Keys.Y
                        _editor.FocusEditor()
                        _editor.Redo()
                        Return True
                End Select
            ElseIf key = Keys.Delete Then
                ' 不要吞掉文本控件或编辑器里的 Delete
                If TypeOf ActiveControl Is TextBox OrElse TypeOf ActiveControl Is UITextBox OrElse TypeOf ActiveControl Is HtmlEditorControl Then
                    Return MyBase.ProcessCmdKey(msg, keyData)
                End If
                DeleteNoteButton_Click(Me, EventArgs.Empty)
                Return True
            End If
            Return MyBase.ProcessCmdKey(msg, keyData)
        End Function

#End Region

#Region "Top bar actions"

        Private Sub ReloadButton_Click(sender As Object, e As EventArgs)
            RepositoryStorageService.ClearCache()
            LoadRepository()
        End Sub

        Private Sub SyncSettingsButton_Click(sender As Object, e As EventArgs)
            Dim dialog As New WebDavSettingsDialog With {.StartPosition = FormStartPosition.CenterParent}

            Dim settingsService As ISettingsService = Program.Services.GetRequiredService(Of ISettingsService)()
            Dim settings As SettingsModel = settingsService.LoadSettingsOrDefault()
            If settings.Credentials IsNot Nothing Then
                dialog.Prefill(
                    settings.Credentials.Url,
                    settings.Credentials.Username,
                    settings.Credentials.UnprotectedPassword,
                    settings.TransferCode,
                    settings.AutoSyncMode.ToString(),
                    settings.DataDirectory)
            Else
                dialog.Prefill(Nothing, Nothing, Nothing, settings.TransferCode, settings.AutoSyncMode.ToString(), settings.DataDirectory)
            End If

            dialog.ApplyTheme(ThemeService)
            If dialog.ShowDialog(Me) = DialogResult.OK Then
                Dim settingsChanged As Boolean = False

                ' 只在变化时保存 WebDAV 凭据
                Dim newUrl As String = If(String.IsNullOrWhiteSpace(dialog.ServerUrl), Nothing, dialog.ServerUrl)
                Dim newUsername As String = If(String.IsNullOrWhiteSpace(dialog.Username), Nothing, dialog.Username)
                Dim newPassword As String = If(String.IsNullOrWhiteSpace(dialog.Password), Nothing, dialog.Password)
                Dim credentialsExist As Boolean = settings.Credentials IsNot Nothing
                Dim newCredentialsExist As Boolean = Not String.IsNullOrEmpty(newUrl)

                If newCredentialsExist Then
                    If Not credentialsExist _
                        OrElse Not String.Equals(settings.Credentials.Url, newUrl, StringComparison.OrdinalIgnoreCase) _
                        OrElse Not String.Equals(settings.Credentials.Username, newUsername, StringComparison.Ordinal) _
                        OrElse Not String.Equals(settings.Credentials.UnprotectedPassword, newPassword, StringComparison.Ordinal) Then
                        settings.Credentials = New SerializeableCloudStorageCredentials With {
                            .CloudStorageId = "webdav",
                            .Url = newUrl,
                            .Username = newUsername,
                            .UnprotectedPassword = newPassword
                        }
                        settingsChanged = True
                    End If
                ElseIf credentialsExist Then
                    settings.Credentials = Nothing
                    settingsChanged = True
                End If

                ' 只在变化时保存传输码
                Dim newTransferCode As String = If(String.IsNullOrEmpty(dialog.TransferCode),
                    Nothing,
                    dialog.TransferCode.Replace(" ", String.Empty))
                If Not String.Equals(settings.TransferCode, newTransferCode, StringComparison.Ordinal) Then
                    settings.TransferCode = newTransferCode
                    settingsChanged = True
                End If

                ' 只在变化时保存同步模式
                If Not String.IsNullOrEmpty(dialog.SyncMode) Then
                    Dim newSyncMode As AutoSynchronizationMode = CType([Enum].Parse(GetType(AutoSynchronizationMode), dialog.SyncMode), AutoSynchronizationMode)
                    If settings.AutoSyncMode <> newSyncMode Then
                        settings.AutoSyncMode = newSyncMode
                        settingsChanged = True
                    End If
                End If

                ' 处理数据目录变更
                Dim sourceDir As String = WindowsDataDirectoryService.GetEffectiveDirectory()
                Dim newDir As String = dialog.DataDirectory
                Dim targetDir As String = If(String.IsNullOrWhiteSpace(newDir),
                    WindowsApplicationPaths.AppDataDirectory,
                    newDir)
                Dim dirChanged As Boolean = Not String.Equals(sourceDir, targetDir, StringComparison.OrdinalIgnoreCase)

                If dirChanged Then
                    settings.DataDirectory = If(String.IsNullOrWhiteSpace(newDir), Nothing, newDir)
                    settingsChanged = True

                    Dim repoFileName As String = NoteRepositoryModel.RepositoryFileName
                    Dim targetRepoFile As String = Path.Combine(targetDir, repoFileName)
                    Dim targetHasData As Boolean = File.Exists(targetRepoFile)

                    If targetHasData Then
                        TryDeleteDirectory(sourceDir)
                    Else
                        Directory.CreateDirectory(targetDir)
                        CopyDirectoryContents(sourceDir, targetDir)
                        TryDeleteDirectory(sourceDir)
                    End If

                    WindowsDataDirectoryService.WriteToRegistry(targetDir)
                End If

                If settingsChanged Then
                    settingsService.TrySaveSettingsToLocalDevice(settings)
                End If

                If dirChanged Then
                    RepositoryStorageService.ClearCache()
                    LoadRepository()
                End If

                SetStatus(If(settingsChanged, "设置已保存。", "设置未更改。"))
                _syncButton.Enabled = True
            End If
        End Sub

        Private Shared Sub CopyDirectoryContents(sourceDir As String, targetDir As String)
            If Not Directory.Exists(sourceDir) Then
                Return
            End If

            For Each srcFile As String In Directory.GetFiles(sourceDir)
                Dim tgtFile As String = Path.Combine(targetDir, Path.GetFileName(srcFile))
                File.Copy(srcFile, tgtFile, True)
            Next

            For Each srcSubDir As String In Directory.GetDirectories(sourceDir)
                Dim tgtSubDir As String = Path.Combine(targetDir, Path.GetFileName(srcSubDir))
                Directory.CreateDirectory(tgtSubDir)
                CopyDirectoryContents(srcSubDir, tgtSubDir)
            Next
        End Sub

        Private Shared Sub TryDeleteDirectory(dirPath As String)
            Try
                If Directory.Exists(dirPath) Then
                    For Each filePath As String In Directory.GetFiles(dirPath)
                        File.Delete(filePath)
                    Next
                    For Each subDirPath As String In Directory.GetDirectories(dirPath)
                        Directory.Delete(subDirPath, True)
                    Next
                    Directory.Delete(dirPath)
                End If
            Catch
            End Try
        End Sub

        Private Sub SyncButton_Click(sender As Object, e As EventArgs)
            If Not _syncService.HasCloudStorageConfigured Then
                SetStatus("请先配置同步设置。", True)
                Return
            End If

            _syncButton.Enabled = False
            _saveButton.Enabled = False
            ' 同步期间的编辑只存在于内存中，会被下面的仓库重载丢弃；
            ' 同步期间冻结编辑器。
            _editor.SetReadOnly(True)
            SetStatus("正在同步...")

            ' C# 版为 async void：VB10 无 async，同步放到后台线程上执行，
            ' UI 恢复操作经 SafeInvoke 回到 UI 线程（保留原按钮禁用/启用时序）。
            Task.Factory.StartNew(Sub()
                                      Dim progress As Action(Of String) = Sub(message)
                                                                              SafeInvoke(Sub() SetStatus(message, IsSyncErrorMessage(message)))
                                                                          End Sub
                                      Dim success As Boolean = _syncService.Sync(progress)

                                      SafeInvoke(Sub()
                                                     _syncButton.Enabled = True
                                                     UpdateModeControls()
                                                 End Sub)

                                      If success Then
                                          RepositoryStorageService.ClearCache()
                                          SafeInvoke(Sub()
                                                         LoadRepository()
                                                         SetStatus("同步完成。")
                                                     End Sub)
                                      Else
                                          ' 重新加载已保存的内容并恢复正确的只读状态。
                                          SafeInvoke(Sub() SelectNote(_selectedNote))
                                      End If
                                  End Sub)
        End Sub

        Private Sub SafeInvoke(action As Action)
            Try
                If IsDisposed OrElse Disposing OrElse Not IsHandleCreated Then
                    Return
                End If
                BeginInvoke(action)
            Catch ex As ObjectDisposedException
            Catch ex As InvalidOperationException
            End Try
        End Sub

        Private Function ShouldAutoSync(mode As AutoSynchronizationMode) As Boolean
            Select Case mode
                Case AutoSynchronizationMode.Always
                    Return True
                Case AutoSynchronizationMode.CostFreeInternetOnly
                    Return _internetStateService.IsInternetConnected()
                Case Else
                    Return False
            End Select
        End Function

        ''' <summary>
        ''' C# 版为 async Task（由线程池 Timer 回调或 OnLoad 的 fire-and-forget 调用）。
        ''' VB10 无 async：改为同步方法，调用方保证在后台线程上执行；方法内的所有
        ''' UI 触碰均经 SafeInvoke 包装（与 C# 版一致）。
        ''' </summary>
        Private Sub TryAutoSync(showStatus As Boolean)
            Try
                ' 未保存的编辑只存在于内存中；同步会重载仓库文件并静默丢弃它们。
                ' 推迟到下一次 tick。
                If _contentDirty Then
                    If showStatus Then
                        SafeInvoke(Sub() SetStatus("有未保存的更改，本次自动同步已跳过。"))
                    End If
                    Return
                End If

                If showStatus Then
                    SafeInvoke(Sub() SetStatus("自动同步中..."))
                End If

                Dim progress As Action(Of String) = Sub(msg)
                                                        If showStatus Then
                                                            SafeInvoke(Sub() SetStatus(msg, IsSyncErrorMessage(msg)))
                                                        End If
                                                    End Sub
                Dim success As Boolean = _syncService.Sync(progress)

                If success Then
                    RepositoryStorageService.ClearCache()
                    SafeInvoke(Sub() LoadRepository())
                End If
            Catch ex As Exception
                System.Diagnostics.Debug.WriteLine("Auto-sync error: " & ex.Message)
            End Try
        End Sub

        Private Sub RestoreBackupButton_Click(sender As Object, e As EventArgs)
            Dim location As String = RepositoryStorageService.GetLocation()
            Dim backupDir As String = Path.Combine(location, "sync_backups")
            If Not Directory.Exists(backupDir) OrElse Not Directory.EnumerateFiles(backupDir, "*.silentnotes").Any() Then
                SetStatus("没有找到备份文件。", True)
                Return
            End If

            Using dialog As New OpenFileDialog()
                dialog.Title = "选择要恢复的备份文件"
                dialog.InitialDirectory = backupDir
                dialog.Filter = "备份文件 (*.silentnotes)|*.silentnotes"

                If dialog.ShowDialog(Me) = DialogResult.OK Then
                    Try
                        Dim targetPath As String = Path.Combine(location, NoteRepositoryModel.RepositoryFileName)
                        File.Copy(dialog.FileName, targetPath, True)
                        RepositoryStorageService.ClearCache()
                        LoadRepository()
                        SetStatus("已从备份恢复。")
                    Catch ex As Exception
                        SetStatus("恢复失败：" & ex.Message, True)
                    End Try
                End If
            End Using
        End Sub

        Private Sub HelpButton_Click(sender As Object, e As EventArgs)
            Dim dialog As New ThemedDialogForm With {
                .Text = "帮助与快捷键",
                .Width = 440,
                .Height = 430
            }

            Dim helpText As New UILabel With {
                .Dock = DockStyle.Fill,
                .TextAlign = ContentAlignment.MiddleLeft,
                .Padding = New Padding(24, 4, 24, 4),
                .Font = UIAppFont,
                .Tag = "window",
                .Text =
                    "笔记操作" & vbLf &
                    "　Ctrl+N　新建笔记" & vbLf &
                    "　Ctrl+S　保存笔记" & vbLf &
                    "　Ctrl+F　搜索笔记" & vbLf &
                    "　Delete　删除笔记（移到回收站）" & vbLf &
                    vbLf &
                    "格式编辑" & vbLf &
                    "　Ctrl+B　粗体" & vbLf &
                    "　Ctrl+I　斜体" & vbLf &
                    "　Ctrl+U　下划线" & vbLf &
                    "　Ctrl+Z　撤销" & vbLf &
                    "　Ctrl+Y　重做" & vbLf &
                    vbLf &
                    "清单操作" & vbLf &
                    "　点击条目前的方框　勾选/取消清单项" & vbLf &
                    "　Enter　　　　　　新建清单项"
            }
            Dim closeBtn As New UIButton With {
                .Text = "关闭",
                .Width = 78,
                .Height = 30,
                .Tag = "window",
                .Font = UIAppFont,
                .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right,
                .DialogResult = DialogResult.Cancel
            }
            Dim bottom As New Panel With {.Dock = DockStyle.Bottom, .Height = 48, .Tag = "window"}
            AddHandler bottom.Resize, Sub()
                                          closeBtn.Left = bottom.ClientSize.Width - closeBtn.Width - 20
                                          closeBtn.Top = (bottom.ClientSize.Height - closeBtn.Height) \ 2
                                      End Sub
            bottom.Controls.Add(closeBtn)

            dialog.Controls.Add(helpText)
            dialog.Controls.Add(bottom)
            dialog.CancelButton = closeBtn
            dialog.ApplyTheme(ThemeService)
            dialog.ShowDialog(Me)
        End Sub

        Private Sub ThemeToggleButton_Click(sender As Object, e As EventArgs)
            Try
                Dim theme As WinFormsThemeService = ThemeService
                Dim settingsService As ISettingsService = Program.Services.GetRequiredService(Of ISettingsService)()
                Dim settings As SettingsModel = settingsService.LoadSettingsOrDefault()
                Dim newMode As ThemeMode = If(theme.IsDarkMode, ThemeMode.Light, ThemeMode.Dark)
                settings.ThemeMode = newMode
                settingsService.TrySaveSettingsToLocalDevice(settings)
                theme.ApplyTheme(newMode)
                ApplyThemeToUi()
            Catch ex As Exception
                System.Diagnostics.Debug.WriteLine("Theme toggle error: " & ex.Message)
            End Try
        End Sub

        Private Sub SaveButton_Click(sender As Object, e As EventArgs)
            SaveSelectedNote()
        End Sub

#End Region

#Region "Note CRUD"

        Private Sub ModeToggle_SelectedIndexChanged(sender As Object, e As EventArgs)
            Dim toRecycleBin As Boolean = _modeToggle.SelectedIndex = 1
            If _showRecycleBin = toRecycleBin Then
                Return
            End If

            _showRecycleBin = toRecycleBin
            RefreshNoteList(FindFirstNote(Function(note) If(toRecycleBin, note.InRecyclingBin, Not note.InRecyclingBin)))
            UpdateRepositorySummary()
            UpdateModeControls()
        End Sub

        Private Function FindFirstNote(predicate As Func(Of NoteModel, Boolean)) As NoteModel
            If _repository Is Nothing Then
                Return Nothing
            End If
            Return _repository.Notes.FirstOrDefault(predicate)
        End Function

        Private Sub NewNoteButton_Click(sender As Object, e As EventArgs)
            If Not HasEditableRepository() Then
                Return
            End If

            _showRecycleBin = False
            _searchBox.Text = String.Empty
            _searchText = String.Empty

            Dim note As New NoteModel With {
                .HtmlContent = "<p>新笔记</p>"
            }
            If Not String.IsNullOrEmpty(_selectedTag) Then
                note.Tags.Add(_selectedTag)
            End If

            note.RefreshModifiedAt()
            _repository.Notes.Insert(0, note)
            _repository.RefreshOrderModifiedAt()
            RepositoryStorageService.TrySaveRepository(_repository)
            UpdateRepositorySummary()
            RefreshTagList()
            RefreshNoteList(note)
            UpdateModeControls()
            SetStatus("已新建笔记。")
        End Sub

        Private Sub NewChecklistButton_Click(sender As Object, e As EventArgs)
            If Not HasEditableRepository() Then
                Return
            End If

            _showRecycleBin = False
            _searchBox.Text = String.Empty
            _searchText = String.Empty

            Dim note As New NoteModel With {
                .NoteType = NoteType.Checklist,
                .HtmlContent = "<p>新项目</p>"
            }
            If Not String.IsNullOrEmpty(_selectedTag) Then
                note.Tags.Add(_selectedTag)
            End If

            note.RefreshModifiedAt()
            _repository.Notes.Insert(0, note)
            _repository.RefreshOrderModifiedAt()
            RepositoryStorageService.TrySaveRepository(_repository)
            UpdateRepositorySummary()
            RefreshTagList()
            RefreshNoteList(note)
            UpdateModeControls()
            SetStatus("已新建清单。")
        End Sub

        Private Function HasEditableRepository() As Boolean
            Return _repository IsNot Nothing AndAlso Not Object.ReferenceEquals(_repository, NoteRepositoryModel.InvalidRepository)
        End Function

        Private Sub DeleteNoteButton_Click(sender As Object, e As EventArgs)
            If _selectedNote Is Nothing OrElse _showRecycleBin Then
                Return
            End If

            If Not ThemedConfirmDialog.Show(Me, ThemeService, "移到回收站",
                "要将当前笔记移到回收站吗？", "移到回收站", False) Then
                Return
            End If

            _selectedNote.InRecyclingBin = True
            _selectedNote.RefreshMetaModifiedAt()
            _repository.RefreshOrderModifiedAt()
            Dim saved As Boolean = RepositoryStorageService.TrySaveRepository(_repository)
            Dim nextNote As NoteModel = FindFirstNote(Function(note) Not note.InRecyclingBin)
            UpdateRepositorySummary()
            RefreshTagList()
            RefreshNoteList(nextNote)
            SetStatus(If(saved, "已移到回收站。", "移动到回收站失败。"), Not saved)
        End Sub

        Private Sub RestoreNoteButton_Click(sender As Object, e As EventArgs)
            If _selectedNote Is Nothing OrElse Not _showRecycleBin Then
                Return
            End If

            _selectedNote.InRecyclingBin = False
            _selectedNote.RefreshMetaModifiedAt()
            _repository.RefreshOrderModifiedAt()
            Dim saved As Boolean = RepositoryStorageService.TrySaveRepository(_repository)
            Dim nextNote As NoteModel = FindFirstNote(Function(note) note.InRecyclingBin)
            RefreshNoteList(nextNote)
            UpdateRepositorySummary()
            RefreshTagList()
            SetStatus(If(saved, "已恢复笔记。", "恢复笔记失败。"), Not saved)
        End Sub

        Private Sub PermanentDeleteNoteButton_Click(sender As Object, e As EventArgs)
            If _selectedNote Is Nothing OrElse Not _showRecycleBin Then
                Return
            End If

            If Not ThemedConfirmDialog.Show(Me, ThemeService, "永久删除",
                "要永久删除当前笔记吗？此操作不能撤销。", "永久删除", True) Then
                Return
            End If

            Dim noteToDelete As NoteModel = _selectedNote
            _repository.DeletedNotes.AddIdOrRefreshDeletedAt(noteToDelete.Id)
            _repository.Notes.Remove(noteToDelete)
            _repository.RefreshOrderModifiedAt()
            Dim saved As Boolean = RepositoryStorageService.TrySaveRepository(_repository)
            Dim nextNote As NoteModel = FindFirstNote(Function(note) note.InRecyclingBin)
            RefreshNoteList(nextNote)
            UpdateRepositorySummary()
            RefreshTagList()
            SetStatus(If(saved, "已永久删除笔记。", "永久删除失败。"), Not saved)
        End Sub

        Private Sub EmptyRecycleBinButton_Click(sender As Object, e As EventArgs)
            If _repository Is Nothing OrElse Not _showRecycleBin Then
                Return
            End If

            ' VB 陷阱：Notes.Count 有属性时会解析为对属性做索引，扩展方法须走 Where().Count()
            Dim count As Integer = _repository.Notes.Where(Function(note) note.InRecyclingBin).Count()
            If count = 0 Then
                Return
            End If

            If Not ThemedConfirmDialog.Show(Me, ThemeService, "清空回收站",
                String.Format("要永久删除回收站中的 {0} 条笔记吗？此操作不能撤销。", count), "全部删除", True) Then
                Return
            End If

            For index As Integer = _repository.Notes.Count - 1 To 0 Step -1
                Dim note As NoteModel = _repository.Notes(index)
                If Not note.InRecyclingBin Then
                    Continue For
                End If

                _repository.DeletedNotes.AddIdOrRefreshDeletedAt(note.Id)
                _repository.Notes.RemoveAt(index)
            Next
            _repository.RefreshOrderModifiedAt()
            Dim saved As Boolean = RepositoryStorageService.TrySaveRepository(_repository)
            RefreshNoteList(Nothing)
            UpdateRepositorySummary()
            RefreshTagList()
            SetStatus(If(saved, "已清空回收站。", "清空回收站失败。"), Not saved)
        End Sub

#End Region

#Region "Tags"

        Private Sub TagButton_Click(sender As Object, e As EventArgs)
            Dim button As UIButton = TryCast(sender, UIButton)
            If button Is Nothing Then
                Return
            End If

            _selectedTag = If(String.IsNullOrEmpty(button.Name), Nothing, button.Name)
            RefreshTagButtonColors()
            If _repository IsNot Nothing Then
                RefreshNoteList(_selectedNote)
                UpdateRepositorySummary()
            End If
        End Sub

        Private Sub SearchTextBox_TextChanged(sender As Object, e As EventArgs)
            If _repository Is Nothing Then
                Return
            End If

            _searchText = If(_searchBox.Text, String.Empty)
            UpdateRepositorySummary()
            RefreshNoteList(_selectedNote)
        End Sub

        Private Sub TagsTextBox_KeyDown(sender As Object, e As KeyEventArgs)
            If e.KeyCode = Keys.Tab AndAlso _tagSuggestionList.Visible AndAlso _tagSuggestionList.Items.Count > 0 Then
                Dim selected As String = TryCast(_tagSuggestionList.Items(0), String)
                If Not String.IsNullOrEmpty(selected) Then
                    _tagsTextBox.Text = selected
                    _tagsTextBox.TextBox.SelectionStart = _tagsTextBox.TextBox.TextLength
                End If
                HideTagSuggestions()
                e.Handled = True
                e.SuppressKeyPress = True
            ElseIf e.KeyCode = Keys.Enter Then
                HideTagSuggestions()
                e.Handled = True
                e.SuppressKeyPress = True
                TagAdded()
            End If
        End Sub

        Private Sub TagSuggestionList_SelectedIndexChanged(sender As Object, e As EventArgs)
            If _loadingTagSuggestions OrElse Not _tagSuggestionList.Visible Then
                Return
            End If

            Dim selected As String = TryCast(_tagSuggestionList.SelectedItem, String)
            If String.IsNullOrEmpty(selected) Then
                Return
            End If

            _loadingTagSuggestions = True
            Try
                _tagSuggestionList.SelectedIndex = -1
                _tagsTextBox.Text = selected
                _tagsTextBox.TextBox.SelectionStart = _tagsTextBox.TextBox.TextLength
            Finally
                _loadingTagSuggestions = False
            End Try
            HideTagSuggestions()
            _tagsTextBox.TextBox.Focus()
        End Sub

        Private Sub TagSuggestionList_DrawItem(sender As Object, e As DrawItemEventArgs)
            If e.Index < 0 OrElse e.Index >= _tagSuggestionList.Items.Count Then
                Return
            End If

            ' 与笔记列表同样的约定：先重绘整行覆盖 UIListBox 的默认文本，再绘制。
            Dim theme As WinFormsThemeService = ThemeService
            Dim selected As Boolean = (e.State And DrawItemState.Selected) = DrawItemState.Selected
            Using background As New SolidBrush(If(selected, theme.AccentSoft, theme.SurfacePaper))
                e.Graphics.FillRectangle(background, e.Bounds)
            End Using

            Dim text As String = TryCast(_tagSuggestionList.Items(e.Index), String)
            If String.IsNullOrEmpty(text) Then
                Return
            End If
            TextRenderer.DrawText(e.Graphics, text, UIAppFont,
                New Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height),
                If(selected, theme.Accent, theme.TextPrimary),
                TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine Or TextFormatFlags.VerticalCenter)
        End Sub

        Private Sub HideTagSuggestions()
            _loadingTagSuggestions = True
            Try
                _tagSuggestionList.SelectedIndex = -1
                _tagSuggestionList.Visible = False
            Finally
                _loadingTagSuggestions = False
            End Try
        End Sub

        Private Sub TagAdded()
            If _loadingEditorMetadata OrElse _selectedNote Is Nothing OrElse _showRecycleBin Then
                Return
            End If

            Dim tagToAdd As String = If(_tagsTextBox.Text, String.Empty).Trim()
            If String.IsNullOrEmpty(tagToAdd) Then
                Return
            End If

            If _selectedNote.Tags.Contains(tagToAdd, StringComparer.InvariantCultureIgnoreCase) Then
                SetStatus(String.Format("标签 ""{0}"" 已存在。", tagToAdd), True)
                Return
            End If

            _selectedNote.Tags.Add(tagToAdd)
            _selectedNote.Tags.Sort(StringComparer.InvariantCultureIgnoreCase)
            _selectedNote.RefreshMetaModifiedAt()
            Dim saved As Boolean = RepositoryStorageService.TrySaveRepository(_repository)
            _tagsTextBox.Text = String.Empty
            RefreshVisibleSelectedItemTitle()
            PopulateNoteTagPanel()
            RefreshTagList()
            RefreshNoteList(_selectedNote)
            UpdateRepositorySummary()
            UpdateTagSuggestions()
            SetStatus(If(saved, "已添加标签。", "保存笔记属性失败。"), Not saved)
        End Sub

        Private Sub DeleteTagByName(tagName As String)
            If _loadingEditorMetadata OrElse _selectedNote Is Nothing OrElse _showRecycleBin Then
                Return
            End If

            If String.IsNullOrEmpty(tagName) Then
                Return
            End If

            Dim tagIndex As Integer = _selectedNote.Tags.FindIndex(Function(tag) String.Equals(tag, tagName, StringComparison.InvariantCultureIgnoreCase))
            If tagIndex = -1 Then
                SetStatus(String.Format("标签 ""{0}"" 不存在。", tagName), True)
                Return
            End If

            _selectedNote.Tags.RemoveAt(tagIndex)
            _selectedNote.RefreshMetaModifiedAt()
            Dim saved As Boolean = RepositoryStorageService.TrySaveRepository(_repository)
            RefreshVisibleSelectedItemTitle()
            RefreshTagList()
            RefreshNoteList(_selectedNote)
            UpdateRepositorySummary()
            UpdateTagSuggestions()
            SetStatus(If(saved, "已删除标签。", "保存笔记属性失败。"), Not saved)
        End Sub

        Private Sub TagsTextBox_TextChanged(sender As Object, e As EventArgs)
            UpdateTagSuggestionPopup()
        End Sub

        Private Sub UpdateTagSuggestionPopup()
            If Not Object.ReferenceEquals(ActiveControl, _tagsTextBox) Then
                HideTagSuggestions()
                Return
            End If

            Dim currentTag As String = If(_tagsTextBox.Text, String.Empty).Trim()
            Dim filtered As List(Of String)
            If String.IsNullOrEmpty(currentTag) Then
                filtered = _tagSuggestions.ToList()
            Else
                filtered = _tagSuggestions.Where(Function(s) s.IndexOf(currentTag, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList()
            End If

            _tagSuggestionList.BeginUpdate()
            _tagSuggestionList.Items.Clear()
            For Each suggestion As String In filtered
                _tagSuggestionList.Items.Add(suggestion)
            Next
            _tagSuggestionList.EndUpdate()
            _tagSuggestionList.Visible = filtered.Count > 0
        End Sub

        Private Sub UpdateTagSuggestions()
            If _repository Is Nothing OrElse Not HasEditableRepository() OrElse _selectedNote Is Nothing Then
                _tagSuggestions = New List(Of String)()
                Return
            End If

            Dim allTags As List(Of String) = _repository.CollectActiveTags()
            _tagSuggestions = allTags.Where(Function(tag) Not _selectedNote.Tags.Contains(tag, StringComparer.InvariantCultureIgnoreCase)).ToList()
        End Sub

        Private Function CurrentVisibleTags() As List(Of String)
            If _repository Is Nothing OrElse Not HasEditableRepository() Then
                Return New List(Of String)()
            End If

            Return _repository.Notes _
                .Where(Function(note) note.InRecyclingBin = _showRecycleBin) _
                .SelectMany(Function(note) note.Tags) _
                .Where(Function(tag) Not String.IsNullOrWhiteSpace(tag)) _
                .Distinct(StringComparer.InvariantCultureIgnoreCase) _
                .OrderBy(Function(tag) tag, StringComparer.InvariantCultureIgnoreCase) _
                .ToList()
        End Function

        Private Sub PopulateTagPanel(tags As List(Of String), selectedTag As String)
            _tagPanel.Controls.Clear()
            _selectedTag = selectedTag

            _tagPanel.Controls.Add(CreateTagButton("全部", Nothing, selectedTag Is Nothing))
            For Each tag As String In tags
                _tagPanel.Controls.Add(CreateTagButton(tag, tag, String.Equals(tag, selectedTag, StringComparison.InvariantCultureIgnoreCase)))
            Next
            RefreshTagButtonColors()
        End Sub

        Private Function CreateTagButton(text As String, tag As String, isSelected As Boolean) As UIButton
            ' 标签名放进 Name 而不是 Tag：Tag 会被主题服务当作样式 token 读取，
            ' 一条叫 "action" 或 "danger" 的标签会把自己那个筛选药丸重新着色。
            Dim button As New UIButton With {
                .Text = text,
                .Name = If(tag, String.Empty),
                .AutoSize = False,
                .Width = TextRenderer.MeasureText(text, UIAppFont).Width + 22,
                .Height = 24,
                .Radius = 12,
                .Tag = "bare",
                .Font = UIAppFont,
                .Style = UIStyle.Custom,
                .Margin = New Padding(2, 2, 2, 2)
            }
            AddHandler button.Click, AddressOf TagButton_Click
            Return button
        End Function

        Private Sub RefreshTagButtonColors()
            Dim theme As WinFormsThemeService = ThemeService
            For Each child As Control In _tagPanel.Controls
                Dim button As UIButton = TryCast(child, UIButton)
                If button Is Nothing Then
                    Continue For
                End If

                Dim isSel As Boolean = String.Equals(button.Name, _selectedTag, StringComparison.InvariantCultureIgnoreCase) _
                    OrElse (String.IsNullOrEmpty(button.Name) AndAlso _selectedTag Is Nothing)
                button.FillColor = If(isSel, theme.AccentSoft, theme.SurfacePaper)
                button.RectColor = If(isSel, theme.Accent, theme.BorderSubtle)
                button.ForeColor = If(isSel, theme.Accent, theme.TextSecondary)
                button.FillHoverColor = theme.AccentSoft
                button.RectHoverColor = theme.Accent
                button.ForeHoverColor = If(isSel, theme.Accent, theme.TextPrimary)
            Next
        End Sub

        Private Sub RefreshTagList()
            If _repository Is Nothing OrElse Not HasEditableRepository() Then
                Return
            End If

            PopulateTagPanel(CurrentVisibleTags(), _selectedTag)
        End Sub

#End Region

#Region "Repository and note list"

        Private Sub LoadRepository()
            Dim loadResult As RepositoryStorageLoadResult = RepositoryStorageService.LoadRepositoryOrDefault(_repository)
            If Object.ReferenceEquals(_repository, NoteRepositoryModel.InvalidRepository) Then
                _notesList.DataSource = Nothing
                _notesList.Items.Clear()
                PopulateTagPanel(New List(Of String)(), Nothing)
                SelectNote(Nothing)
                _repositorySummaryLabel.Text = "本地仓库无法读取，已停止编辑以避免覆盖原文件。"
                UpdateModeControls()
                SetStatus("仓库加载失败。", True)
                Return
            End If

            RefreshTagList()
            RefreshNoteList(FindFirstNote(Function(note) note.InRecyclingBin = _showRecycleBin))
            UpdateRepositorySummary()
            UpdateModeControls()

            SetStatus(If(loadResult = RepositoryStorageLoadResult.CreatedNewEmptyRepository,
                "已创建新的本地仓库。",
                "已加载本地仓库。"))
        End Sub

        Private Sub RefreshNoteList(noteToSelect As NoteModel)
            _listItems = _repository.Notes _
                .Where(Function(note) note.InRecyclingBin = _showRecycleBin) _
                .Where(Function(note) MatchesTag(note)) _
                .Where(Function(note) MatchesSearch(note)) _
                .OrderByDescending(Function(note) note.IsPinned) _
                .Select(Function(note)
                            Dim item As New NoteListItem(note)
                            Dim cachedTitle As String = Nothing
                            If note.SafeId.HasValue AndAlso _safeNoteTitles.TryGetValue(note.Id, cachedTitle) Then
                                item.SetCustomTitle(cachedTitle)
                            End If
                            Return item
                        End Function) _
                .ToList()

            _loadingSelection = True
            Try
                _notesList.BeginUpdate()
                _notesList.Items.Clear()
                For Each item As NoteListItem In _listItems
                    _notesList.Items.Add(item)
                Next
                Dim selected As NoteListItem = _listItems.FirstOrDefault(Function(listItem) ReferenceEquals(listItem.Note, noteToSelect))
                _notesList.SelectedItem = selected
                noteToSelect = If(selected IsNot Nothing, selected.Note, Nothing)
                _notesList.EndUpdate()
            Finally
                _loadingSelection = False
            End Try

            Dim isEmpty As Boolean = _listItems.Count = 0
            _emptyListLabel.Text = If(_showRecycleBin,
                "回收站为空。",
                If(String.IsNullOrWhiteSpace(_searchText) AndAlso String.IsNullOrEmpty(_selectedTag),
                    "还没有笔记，点击「新建笔记」开始。",
                    "没有匹配的笔记。"))
            _emptyListLabel.Visible = isEmpty
            _notesList.Visible = Not isEmpty

            SelectNote(noteToSelect)
        End Sub

        Private Sub NotesList_SelectedIndexChanged(sender As Object, e As EventArgs)
            If _loadingSelection Then
                Return
            End If

            Dim selectedItem As NoteListItem = TryCast(_notesList.SelectedItem, NoteListItem)
            SelectNote(If(selectedItem IsNot Nothing, selectedItem.Note, Nothing))
        End Sub

        Private Sub NotesList_DrawItem(sender As Object, e As DrawItemEventArgs)
            If e.Index < 0 OrElse e.Index >= _listItems.Count Then
                Return
            End If

            ' UIListBox 已经在这里画过默认背景和条目文本；先覆盖整行（包括滚动条
            ' 留白），再在上面绘制两行笔记卡片。
            Dim theme As WinFormsThemeService = ThemeService
            Dim item As NoteListItem = _listItems(e.Index)
            Dim selected As Boolean = (e.State And DrawItemState.Selected) = DrawItemState.Selected
            Dim hovered As Boolean = (e.State And DrawItemState.HotLight) = DrawItemState.HotLight

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            Using background As New SolidBrush(theme.SurfacePaper)
                e.Graphics.FillRectangle(background, e.Bounds)
            End Using

            ' 为纵向滚动条留出留白，卡片不会跑到它下面
            Dim rowRect As New Rectangle(e.Bounds.X + 4, e.Bounds.Y + 2, e.Bounds.Width - 22, e.Bounds.Height - 4)
            Dim back As Color = If(selected, theme.AccentSoft, If(hovered, theme.ListHover, theme.SurfacePaper))
            Using background As New SolidBrush(back)
                e.Graphics.FillRectangle(background, rowRect)
            End Using

            ' 选中态：卡片左缘的纵向强调条
            If selected Then
                Using bar As New SolidBrush(theme.Accent)
                    e.Graphics.FillRectangle(bar, e.Bounds.X + 4, e.Bounds.Y + 2, 4, e.Bounds.Height - 4)
                End Using
            End If

            Dim h As Integer = e.Bounds.Height
            Dim textWidth As Integer = Math.Max(40, e.Bounds.Width - 40)
            Dim titleRect As New Rectangle(e.Bounds.X + 14, e.Bounds.Y + 6, textWidth, 20)
            Dim secondaryRect As New Rectangle(e.Bounds.X + 14, e.Bounds.Y + h - 26, textWidth, 18)

            Dim title As String = If(item.Title, "无标题笔记")
            ' 浅色模式下 Accent 叠在 AccentSoft 上对比度只有 3.1:1；选中的标题在那里
            ' 回退为 TextPrimary（深色模式的 Accent 达标）。
            Dim mainColor As Color = If(selected AndAlso theme.IsDarkMode, theme.Accent, theme.TextPrimary)
            TextRenderer.DrawText(e.Graphics, title, UIListTitleFont, titleRect, mainColor, TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine)
            Dim secondaryLine As String = If(item.SecondaryLine, String.Empty)
            If item.IsPinned Then
                secondaryLine = "★ " & secondaryLine
            End If
            TextRenderer.DrawText(
                e.Graphics,
                secondaryLine,
                UIListBodyFont,
                secondaryRect,
                theme.TextSecondary,
                TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine)
        End Sub

        Private Sub UpdateRepositorySummary()
            If _repository Is Nothing Then
                Return
            End If

            ' VB 陷阱：Notes.Count 有属性时会解析为对属性做索引，扩展方法须走 Where().Count()
            Dim activeCount As Integer = _repository.Notes.Where(Function(note) Not note.InRecyclingBin).Count()
            Dim recycleBinCount As Integer = _repository.Notes.Where(Function(note) note.InRecyclingBin).Count()
            Dim visibleCount As Integer = _repository.Notes.Where(Function(note) note.InRecyclingBin = _showRecycleBin AndAlso MatchesTag(note) AndAlso MatchesSearch(note)).Count()
            _repositorySummaryLabel.Text = If(String.IsNullOrWhiteSpace(_searchText),
                String.Format("{0} 条活动笔记，{1} 条回收站笔记。当前视图 {2} 条。", activeCount, recycleBinCount, visibleCount),
                String.Format("当前视图找到 {0} 条；总计 {1} 条活动笔记，{2} 条回收站笔记。", visibleCount, activeCount, recycleBinCount))
        End Sub

        Private Sub UpdateModeControls()
            Dim hasRepository As Boolean = HasEditableRepository()
            _activeModeButtons.Visible = Not _showRecycleBin
            _recycleBinModeButtons.Visible = _showRecycleBin
            _toolbar.Visible = Not _showRecycleBin
            _tagsTextBox.ReadOnly = _showRecycleBin OrElse Not hasRepository
            _pinnedButton.Enabled = hasRepository AndAlso Not _showRecycleBin
            _pinnedButton.Visible = _selectedNote IsNot Nothing AndAlso Not _showRecycleBin
            _saveButton.Enabled = hasRepository AndAlso Not _showRecycleBin
            RefreshModeButtonColors()
        End Sub

        Private Function MatchesSearch(note As NoteModel) As Boolean
            If String.IsNullOrWhiteSpace(_searchText) Then
                Return True
            End If

            Dim haystack As String = BuildPlainText(note.HtmlContent)
            For Each tag As String In note.Tags
                haystack = haystack & " " & tag
            Next

            Return haystack.IndexOf(_searchText, StringComparison.InvariantCultureIgnoreCase) >= 0
        End Function

        Private Function MatchesTag(note As NoteModel) As Boolean
            If String.IsNullOrEmpty(_selectedTag) Then
                Return True
            End If

            Return note.Tags.Any(Function(tag) String.Equals(tag, _selectedTag, StringComparison.InvariantCultureIgnoreCase))
        End Function

#End Region

#Region "Note selection and saving"

        Private Sub SelectNote(note As NoteModel)
            If Not _editor.IsReady Then
                _pendingSelectNote = note
                Return
            End If

            _selectedNote = note
            _contentDirty = False

            If note Is Nothing Then
                _editor.SetReadOnly(True)
                LoadEditorContent(Nothing, False)
                LoadEditorMetadata(Nothing)
                SetEditorTitle("编辑器")
                _editorInfoLabel.Text = If(_showRecycleBin, "回收站为空，或没有匹配搜索条件的笔记。", "请选择一条笔记。")
                Return
            End If

            LoadEditorMetadata(note)
            SetEditorTitle(BuildTitle(note))
            If _showRecycleBin Then
                _editor.SetReadOnly(True)
                _editorInfoLabel.Text = String.Format("回收站笔记只读。最后修改：{0:g}", note.ModifiedAt.ToLocalTime())
                LoadEditorContent(note.HtmlContent, note.NoteType = NoteType.Checklist)
                Return
            End If

            ' 处理安全箱笔记
            If note.SafeId.HasValue Then
                If _safeKeyService.IsSafeOpen(note.SafeId.Value) Then
                    ' 安全箱已打开 - 解密内容并允许编辑
                    Dim unlockedContent As String = DecryptSafeNoteContent(note)
                    If unlockedContent IsNot Nothing Then
                        Dim canEditSafe As Boolean = _htmlCompatibilityInspector.CanEditWithoutConversion(unlockedContent)
                        _editor.SetReadOnly(Not canEditSafe)
                        _editorInfoLabel.Text = If(canEditSafe,
                            String.Format("安全箱笔记 · 最后修改：{0:g}", note.ModifiedAt.ToLocalTime()),
                            "安全箱笔记（只读模式）")
                        LoadEditorContent(If(canEditSafe, unlockedContent, Nothing), note.NoteType = NoteType.Checklist)
                        If Not canEditSafe Then
                            _editor.SetContent("<p>" & EncodeTextToHtml(BuildPlainText(unlockedContent)) & "</p>", False)
                        End If
                        Dim unlockedTitle As String = BuildPlainText(unlockedContent)
                        Dim displayTitle As String = If(String.IsNullOrWhiteSpace(unlockedTitle),
                            "无标题笔记",
                            If(unlockedTitle.Length > 80, unlockedTitle.Substring(0, 80) & "...", unlockedTitle))
                        SetEditorTitle(displayTitle)
                        If Not String.IsNullOrEmpty(unlockedTitle) Then
                            _safeNoteTitles(note.Id) = unlockedTitle
                        End If
                        Dim selectedItem As NoteListItem = TryCast(_notesList.SelectedItem, NoteListItem)
                        If selectedItem IsNot Nothing Then
                            selectedItem.SetCustomTitle(unlockedTitle)
                            _notesList.Invalidate()
                        End If
                        Return
                    End If
                End If

                ' 安全箱未打开或解密失败 - 显示锁定信息
                _editor.SetReadOnly(True)
                _editorInfoLabel.Text = "这条笔记位于安全箱中，点击上方「安全箱」按钮输入密码解锁后可编辑。"
                _editor.SetContent(
                    "<p>安全箱笔记已锁定。</p><p>请点击顶部「安全箱」按钮输入密码解锁。</p>",
                    False)
                Return
            End If

            Dim canEdit As Boolean = _htmlCompatibilityInspector.CanEditWithoutConversion(note.HtmlContent)
            _editor.SetReadOnly(Not canEdit)
            _editorInfoLabel.Text = If(canEdit,
                String.Format("最后修改：{0:g}", note.ModifiedAt.ToLocalTime()),
                "这条笔记包含当前原生编辑器不支持的 HTML 内容，暂时只读以避免格式损坏。")
            LoadEditorContent(If(canEdit, note.HtmlContent, Nothing), note.NoteType = NoteType.Checklist)
            If Not canEdit Then
                _editor.SetContent("<p>" & EncodeTextToHtml(BuildPlainText(note.HtmlContent)) & "</p>", False)
            End If

            _contentDirty = False
        End Sub

        Private Sub LoadEditorContent(html As String, isChecklist As Boolean)
            _editor.SetContent(If(String.IsNullOrEmpty(html), "<p><br></p>", html), isChecklist)
            _editor.SetEditorTheme(ThemeService.IsDarkMode, isChecklist)
            _contentDirty = False
            UpdateWordCount()
            UpdateToolbarState()
        End Sub

        Private Shared Function EncodeTextToHtml(text As String) As String
            Return System.Net.WebUtility.HtmlEncode(If(text, String.Empty)) _
                .Replace(vbCrLf, "<br>") _
                .Replace(vbLf, "<br>")
        End Function

        Private Sub Editor_Ready(sender As Object, e As EventArgs)
            SafeInvoke(Sub()
                           _editor.SetEditorTheme(ThemeService.IsDarkMode, False)
                           Dim pending As NoteModel = _pendingSelectNote
                           _pendingSelectNote = Nothing
                           SelectNote(If(pending, _selectedNote))
                       End Sub)
        End Sub

        Private Sub Editor_ContentChanged(sender As Object, e As EventArgs)
            _contentDirty = True
            UpdateEditorTitleMarker()
            ScheduleWordCountUpdate()
        End Sub

        Private Sub Editor_SelectionChanged(sender As Object, e As EventArgs)
            ' 把频繁的 selectionchange 事件合并为每次消息循环迭代一次工具栏刷新
            If _toolbarStatePending OrElse Not IsHandleCreated Then
                Return
            End If
            _toolbarStatePending = True
            SafeInvoke(Sub()
                           _toolbarStatePending = False
                           UpdateToolbarState()
                       End Sub)
        End Sub

        ' GetHtml() 是一次 MSHTML COM 往返；每次按键都跑会让长笔记卡顿，
        ' 所以字数统计合并为每次消息迭代一次。
        Private Sub ScheduleWordCountUpdate()
            If _wordCountPending OrElse Not IsHandleCreated Then
                Return
            End If
            _wordCountPending = True
            SafeInvoke(Sub()
                           _wordCountPending = False
                           UpdateWordCount()
                       End Sub)
        End Sub

        ''' <summary>设置编辑区标题；未保存时显示 * 前缀。</summary>
        Private Sub SetEditorTitle(title As String)
            _editorTitleBase = If(String.IsNullOrWhiteSpace(title), "编辑器", title)
            UpdateEditorTitleMarker()
        End Sub

        Private Sub UpdateEditorTitleMarker()
            _editorTitleLabel.Text = If(_contentDirty, "* " & _editorTitleBase, _editorTitleBase)
        End Sub

        ''' <summary>
        ''' 保存选中笔记的内容。只有当需要保存但保存失败时才返回 False
        ''' （调用方在 False 时可中止关闭窗口）。
        ''' </summary>
        Private Function SaveSelectedNote(Optional showStatus As Boolean = True) As Boolean
            If _selectedNote Is Nothing OrElse _editor.IsReadOnly OrElse Not HasEditableRepository() Then
                Return True
            End If
            If Not _contentDirty Then
                Return True
            End If

            ' 检查是否为锁定的安全箱笔记
            If _selectedNote.SafeId.HasValue AndAlso Not _safeKeyService.IsSafeOpen(_selectedNote.SafeId.Value) Then
                Return True
            End If

            Dim html As String = _editor.GetHtml()
            If String.IsNullOrEmpty(html) Then
                Return True
            End If

            ' 如果是安全箱笔记，先加密内容再存储
            If _selectedNote.SafeId.HasValue Then
                Dim encrypted As String = EncryptSafeNoteContent(html)
                If encrypted = html Then ' 加密失败
                    SetStatus("安全箱笔记加密失败，内容未保存。", True)
                    Return False
                End If
                html = encrypted
            End If

            If html = _selectedNote.HtmlContent Then
                _contentDirty = False
                UpdateEditorTitleMarker()
                Return True
            End If

            _selectedNote.HtmlContent = html
            _selectedNote.RefreshModifiedAt()
            Dim saved As Boolean = RepositoryStorageService.TrySaveRepository(_repository)
            If saved Then
                _contentDirty = False
                RefreshVisibleSelectedItemTitle()
                RefreshTagList()
            End If
            ' 失败时 * 标记保留，未保存更改询问会继续触发。
            UpdateEditorTitleMarker()
            UpdateRepositorySummary()
            If showStatus Then
                SafeInvoke(Sub() SetStatus(If(saved, "已保存笔记。", "保存失败。"), Not saved))
            End If
            Return saved
        End Function

        Private Sub SaveSelectedMetadata()
            If _loadingEditorMetadata OrElse _selectedNote Is Nothing OrElse _showRecycleBin Then
                Return
            End If

            Dim changed As Boolean = False
            If _selectedNote.IsPinned <> _pinnedState Then
                _selectedNote.IsPinned = _pinnedState
                changed = True
            End If

            If Not changed Then
                Return
            End If

            _selectedNote.RefreshMetaModifiedAt()
            Dim saved As Boolean = RepositoryStorageService.TrySaveRepository(_repository)
            RefreshVisibleSelectedItemTitle()
            RefreshTagList()
            RefreshNoteList(_selectedNote)
            UpdateRepositorySummary()
            UpdateTagSuggestions()
            SetStatus(If(saved, "已保存笔记属性。", "保存笔记属性失败。"), Not saved)
        End Sub

        Private Sub LoadEditorMetadata(note As NoteModel)
            _loadingEditorMetadata = True
            Try
                _tagsTextBox.Text = String.Empty
                _pinnedState = note IsNot Nothing AndAlso note.IsPinned
                RefreshPinnedButton()
                _pinnedButton.Visible = note IsNot Nothing AndAlso Not _showRecycleBin
                PopulateNoteTagPanel()
                UpdateTagSuggestions()
                HideTagSuggestions()
            Finally
                _loadingEditorMetadata = False
            End Try
        End Sub

        Private Sub RefreshVisibleSelectedItemTitle()
            Dim selectedItem As NoteListItem = TryCast(_notesList.SelectedItem, NoteListItem)
            If selectedItem Is Nothing Then
                Return
            End If

            Dim cachedTitle As String = Nothing
            If selectedItem.Note.SafeId.HasValue AndAlso _safeNoteTitles.TryGetValue(selectedItem.Note.Id, cachedTitle) Then
                selectedItem.SetCustomTitle(cachedTitle)
            Else
                selectedItem.RefreshDisplay()
            End If
            _notesList.Invalidate()
            SetEditorTitle(selectedItem.Title)
        End Sub

        Private Sub LinkButton_Click()
            If _editor.IsReadOnly OrElse _selectedNote Is Nothing Then
                Return
            End If
            _editor.FocusEditor()

            Dim dialog As New ThemedDialogForm With {
                .Text = "插入链接",
                .Width = 420,
                .Height = 190
            }

            Dim caption As New UILabel With {.Text = "请输入链接 URL：", .AutoSize = True, .Left = 20, .Top = 20, .Font = UIAppFont}
            Dim urlBox As New UITextBox With {.Left = 20, .Top = 48, .Width = 356}
            Dim okBtn As New UIButton With {.Text = "确定", .Width = 78, .Height = 30, .Left = 218, .Top = 92, .Tag = "accent", .Font = UIAppFont, .DialogResult = DialogResult.OK}
            Dim cancelBtn As New UIButton With {.Text = "取消", .Width = 78, .Height = 30, .Left = 306, .Top = 92, .Tag = "window", .Font = UIAppFont, .DialogResult = DialogResult.Cancel}
            dialog.Controls.Add(caption)
            dialog.Controls.Add(urlBox)
            dialog.Controls.Add(okBtn)
            dialog.Controls.Add(cancelBtn)
            dialog.AcceptButton = okBtn
            dialog.CancelButton = cancelBtn
            dialog.ApplyTheme(ThemeService)
            urlBox.Focus()
            urlBox.TextBox.SelectAll()

            If dialog.ShowDialog(Me) <> DialogResult.OK OrElse String.IsNullOrWhiteSpace(urlBox.Text) Then
                Return
            End If

            Dim input As String = urlBox.Text.Trim()
            Dim url As String = input
            If Not url.StartsWith("http://") AndAlso Not url.StartsWith("https://") AndAlso Not url.StartsWith("mailto:") Then
                url = "https://" & url
            End If

            _editor.CreateLink(url)
        End Sub

#End Region

#Region "Safe"

        Private Sub SafeButton_Click(sender As Object, e As EventArgs)
            If _repository Is Nothing OrElse Not HasEditableRepository() Then
                Return
            End If

            Dim hasAnySafe As Boolean = _repository.Safes.Count > 0
            ShowSafePasswordDialog(hasAnySafe)
        End Sub

        Private Sub CloseSafe()
            Dim hasOpenSafe As Boolean = _repository.Safes.Any(Function(s) _safeKeyService.IsSafeOpen(s.Id))
            If Not hasOpenSafe Then
                Return
            End If

            Try
                _logService.Info("开始关闭安全箱流程...")
                SaveSelectedNote(False)
                _logService.Info("安全箱笔记已保存，准备关闭安全箱...")
                _safeKeyService.CloseAllSafes()
                _logService.Info("安全箱已关闭，清除缓存的标题...")
                _safeNoteTitles.Clear()
                SetStatus("安全箱已关闭。")
                RefreshNoteList(If(_selectedNote IsNot Nothing AndAlso _selectedNote.SafeId.HasValue, _selectedNote, Nothing))
                If _selectedNote IsNot Nothing AndAlso _selectedNote.SafeId.HasValue Then
                    _logService.Info("刷新当前安全箱笔记的显示状态...")
                    SelectNote(_selectedNote)
                End If
                _logService.Info("关闭安全箱流程完成。")
            Catch ex As Exception
                _logService.[Error](String.Format("关闭安全箱时出错: {0}", ex))
                SetStatus("关闭安全箱时发生错误，请查看日志确认详情。", True)
            End Try
        End Sub

        Private Sub ShowSafePasswordDialog(existingSafe As Boolean)
            Dim dialog As New ThemedDialogForm With {
                .Text = If(existingSafe, "打开安全箱", "创建安全箱"),
                .Width = 430,
                .Height = If(existingSafe, 310, 350)
            }

            Dim content As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(16), .Tag = "window"}
            Dim title As New UILabel With {.Text = If(existingSafe, "打开安全箱", "创建安全箱"), .AutoSize = True, .Font = UIEditorTitleFont, .Left = 16, .Top = 10}
            Dim hint As New UILabel With {
                .Text = If(existingSafe,
                    "输入安全箱密码以解锁受保护的笔记。",
                    "设置一个至少 5 个字符的密码，用于保护安全箱中的笔记。"),
                .AutoSize = False,
                .Width = 360,
                .Height = 32,
                .Left = 16,
                .Top = 44,
                .Font = UISmallFont,
                .Tag = "secondary"
            }
            Dim passwordCaption As New UILabel With {.Text = If(existingSafe, "输入安全箱密码：", "设置安全箱密码（至少 5 个字符）："), .AutoSize = True, .Left = 16, .Top = 84, .Font = UIAppFont}
            Dim passwordBox As New UITextBox With {.PasswordChar = "●"c, .Width = 366, .Left = 16, .Top = 108, .Font = UIAppFont}

            Dim confirmBox As UITextBox = Nothing
            Dim confirmCaption As UILabel = Nothing
            If Not existingSafe Then
                confirmCaption = New UILabel With {.Text = "确认密码：", .AutoSize = True, .Left = 16, .Top = 142, .Font = UIAppFont}
                confirmBox = New UITextBox With {.PasswordChar = "●"c, .Width = 366, .Left = 16, .Top = 166, .Font = UIAppFont}
            End If

            Dim errorText As New UILabel With {
                .AutoSize = False,
                .Width = 366,
                .Height = 34,
                .Left = 16,
                .Top = If(existingSafe, 156, 200),
                .Font = UISmallFont,
                .Tag = "danger"
            }

            Dim lockBtn As UIButton = Nothing
            Dim buttonTop As Integer = If(existingSafe, 200, 240)
            If existingSafe Then
                lockBtn = New UIButton With {.Text = "锁定", .Width = 86, .Height = 30, .Left = 130, .Top = buttonTop, .Tag = "window", .Font = UIAppFont}
                AddHandler lockBtn.Click, Sub()
                                              CloseSafe()
                                              dialog.DialogResult = DialogResult.OK
                                              dialog.Close()
                                          End Sub
            End If

            Dim okBtn As New UIButton With {.Text = If(existingSafe, "解锁", "创建"), .Width = 86, .Height = 30, .Left = 226, .Top = buttonTop, .Tag = "accent", .Font = UIAppFont}
            Dim cancelBtn As New UIButton With {.Text = "取消", .Width = 86, .Height = 30, .Left = 34, .Top = buttonTop, .Tag = "window", .Font = UIAppFont}
            AddHandler cancelBtn.Click, Sub()
                                            dialog.DialogResult = DialogResult.Cancel
                                            dialog.Close()
                                        End Sub
            dialog.CancelButton = cancelBtn
            dialog.AcceptButton = okBtn

            content.Controls.Add(title)
            content.Controls.Add(hint)
            content.Controls.Add(passwordCaption)
            content.Controls.Add(passwordBox)
            If confirmCaption IsNot Nothing Then
                content.Controls.Add(confirmCaption)
            End If
            If confirmBox IsNot Nothing Then
                content.Controls.Add(confirmBox)
            End If
            content.Controls.Add(errorText)
            If lockBtn IsNot Nothing Then
                content.Controls.Add(lockBtn)
            End If
            content.Controls.Add(okBtn)
            content.Controls.Add(cancelBtn)
            dialog.Controls.Add(content)
            dialog.ApplyTheme(ThemeService)
            errorText.ForeColor = ThemeService.Danger
            passwordBox.Focus()

            AddHandler okBtn.Click, Sub()
                                        Dim password As String = passwordBox.Text
                                        If String.IsNullOrEmpty(password) OrElse password.Length < 5 Then
                                            errorText.Text = "密码至少需要 5 个字符。"
                                            Return
                                        End If

                                        If Not existingSafe Then
                                            Dim confirm As String = If(confirmBox IsNot Nothing, confirmBox.Text, String.Empty)
                                            If password <> confirm Then
                                                errorText.Text = "两次输入的密码不一致。"
                                                Return
                                            End If
                                        End If

                                        Dim securePassword As SecureString = CryptoUtils.StringToSecureString(password)

                                        If existingSafe Then
                                            ' 尝试打开所有已有安全箱（防御性地先关闭）
                                            Dim anyOpened As Boolean = False
                                            For Each safe As SafeModel In _repository.Safes
                                                Try
                                                    Dim testEncrypted As Byte() = CryptoUtils.Base64StringToBytes(safe.SerializeableKey)
                                                    Dim testCryptor As ICryptor = New Cryptor(SafeModel.CryptorPackageName, Nothing)
                                                    Dim testNeedsReEnc As Boolean = False
                                                    Dim testDecrypted As Byte() = testCryptor.Decrypt(testEncrypted, securePassword, testNeedsReEnc)
                                                    _logService.Info(String.Format("直接解密安全箱成功: needsReEnc={0}", testNeedsReEnc))
                                                Catch testEx As Exception
                                                    _logService.[Error](String.Format("直接解密安全箱失败, 实际异常: {0}", testEx))
                                                End Try

                                                _safeKeyService.CloseSafe(safe.Id)
                                                Dim needsReEncryption As Boolean = False
                                                If _safeKeyService.TryOpenSafe(safe, securePassword, needsReEncryption) Then
                                                    anyOpened = True
                                                    If needsReEncryption Then
                                                        Dim settings As SettingsModel = Program.Services.GetRequiredService(Of ISettingsService)().LoadSettingsOrDefault()
                                                        Dim key As Byte() = Nothing
                                                        safe.SerializeableKey = SafeModel.EncryptKey(
                                                            If(_safeKeyService.TryGetKey(safe.Id, key), key, New Byte(31) {}),
                                                            securePassword,
                                                            _cryptoRandomService,
                                                            settings.SelectedEncryptionAlgorithm,
                                                            settings.SelectedKdfAlgorithm)
                                                        safe.RefreshModifiedAt()
                                                        RepositoryStorageService.TrySaveRepository(_repository)
                                                    End If
                                                End If
                                            Next

                                            If anyOpened Then
                                                dialog.DialogResult = DialogResult.OK
                                                dialog.Close()
                                                SetStatus("安全箱已解锁。")
                                                For Each note As NoteModel In _repository.Notes
                                                    If note.SafeId.HasValue AndAlso _safeKeyService.IsSafeOpen(note.SafeId.Value) AndAlso Not String.IsNullOrEmpty(note.HtmlContent) Then
                                                        Dim decrypted As String = DecryptSafeNoteContent(note)
                                                        If decrypted IsNot Nothing Then
                                                            Dim title2 As String = BuildPlainText(decrypted)
                                                            If Not String.IsNullOrEmpty(title2) Then
                                                                _safeNoteTitles(note.Id) = title2
                                                            End If
                                                        End If
                                                    End If
                                                Next
                                                RefreshNoteList(_selectedNote)
                                                If _selectedNote IsNot Nothing AndAlso _selectedNote.SafeId.HasValue AndAlso _safeKeyService.IsSafeOpen(_selectedNote.SafeId.Value) Then
                                                    SelectNote(_selectedNote)
                                                End If
                                            Else
                                                _logService.Info(String.Format("安全箱打开失败: 仓库中有 {0} 个安全箱", _repository.Safes.Count))
                                                For Each s As SafeModel In _repository.Safes
                                                    _logService.Info(String.Format("  安全箱 {0}: 有密钥={1}, 密钥长度={2}", s.Id, Not String.IsNullOrEmpty(s.SerializeableKey), If(s.SerializeableKey IsNot Nothing, s.SerializeableKey.Length, 0)))
                                                    If Not String.IsNullOrEmpty(s.SerializeableKey) Then
                                                        Try
                                                            Dim raw As Byte() = CryptoUtils.Base64StringToBytes(s.SerializeableKey)
                                                            Dim header As String = CryptoUtils.BytesToString(raw).Substring(0, Math.Min(25, raw.Length))
                                                            _logService.Info(String.Format("  密钥头内容: '{0}'", header))
                                                        Catch ex As Exception
                                                            _logService.[Error]("  密钥 Base64 解析失败", ex)
                                                        End Try
                                                    End If
                                                Next
                                                errorText.Text = "密码错误，无法打开安全箱。请查看日志文件获取详细信息。"
                                            End If
                                        Else
                                            ' 创建新的安全箱
                                            Try
                                                Dim safe As SafeModel = New SafeModel()
                                                Dim settings As SettingsModel = Program.Services.GetRequiredService(Of ISettingsService)().LoadSettingsOrDefault()
                                                Dim algorithm As String = settings.SelectedEncryptionAlgorithm
                                                Dim kdfAlgorithm As String = settings.SelectedKdfAlgorithm

                                                Dim key As Byte() = _cryptoRandomService.GetRandomBytes(32)
                                                safe.SerializeableKey = SafeModel.EncryptKey(key, securePassword, _cryptoRandomService, algorithm, kdfAlgorithm)

                                                Dim dummyNeedsReEncryption As Boolean = False
                                                If Not _safeKeyService.TryOpenSafe(safe, securePassword, dummyNeedsReEncryption) Then
                                                    errorText.Text = "安全箱创建失败，无法验证密钥。"
                                                    Return
                                                End If

                                                _repository.Safes.Add(safe)
                                                RepositoryStorageService.TrySaveRepository(_repository)
                                                dialog.DialogResult = DialogResult.OK
                                                dialog.Close()
                                                SetStatus("安全箱已创建并解锁。")
                                            Catch ex As Exception
                                                errorText.Text = "创建安全箱失败：" & ex.Message
                                            End Try
                                        End If

                                        securePassword.Clear()
                                    End Sub

            dialog.ShowDialog(Me)
        End Sub

        Private Function DecryptSafeNoteContent(note As NoteModel) As String
            If Not note.SafeId.HasValue OrElse String.IsNullOrEmpty(note.HtmlContent) Then
                Return Nothing
            End If

            Dim safeKey As Byte() = Nothing
            If Not _safeKeyService.TryGetKey(note.SafeId.Value, safeKey) Then
                Return Nothing
            End If

            Try
                Dim cryptor As ICryptor = New Cryptor(NoteModel.CryptorPackageName, Nothing)
                Dim binaryContent As Byte() = CryptoUtils.Base64StringToBytes(note.HtmlContent)
                Dim unlockedBinary As Byte() = cryptor.Decrypt(binaryContent, safeKey)
                Return CryptoUtils.BytesToString(unlockedBinary)
            Catch
                Return Nothing
            End Try
        End Function

        Private Function EncryptSafeNoteContent(unlockedContent As String) As String
            If _selectedNote Is Nothing OrElse Not _selectedNote.SafeId.HasValue Then
                Return unlockedContent
            End If

            Dim safeKey As Byte() = Nothing
            If Not _safeKeyService.TryGetKey(_selectedNote.SafeId.Value, safeKey) Then
                Return unlockedContent
            End If

            Try
                Dim algorithm As String = Program.Services.GetRequiredService(Of ISettingsService)().LoadSettingsOrDefault().SelectedEncryptionAlgorithm
                Dim cryptor As ICryptor = New Cryptor(NoteModel.CryptorPackageName, _cryptoRandomService)
                Dim binaryContent As Byte() = CryptoUtils.StringToBytes(unlockedContent)
                Dim lockedBinary As Byte() = cryptor.Encrypt(binaryContent, safeKey, algorithm, Nothing)
                Return CryptoUtils.BytesToBase64String(lockedBinary)
            Catch
                Return unlockedContent
            End Try
        End Function

#End Region

#Region "Status and text helpers"

        Private Sub SetStatus(message As String, Optional isError As Boolean = False)
            _statusLabel.Text = String.Format("{0}  {1:T}", message, DateTime.Now)
            _statusLabel.ForeColor = If(isError, ThemeService.Danger, ThemeService.TextSecondary)
        End Sub

        Private Sub UpdateWordCount()
            If _selectedNote Is Nothing OrElse _editor Is Nothing Then
                _wordCountLabel.Text = String.Empty
                Return
            End If

            Try
                Dim plain As String = BuildPlainText(If(_editor.GetHtml(), String.Empty))
                _wordCountLabel.Text = If(plain.Length > 0,
                    String.Format("{0} 字", plain.Length),
                    String.Empty)
            Catch
                _wordCountLabel.Text = String.Empty
            End Try
        End Sub

        Private Shared Function BuildTitle(note As NoteModel) As String
            Dim heading As String = ExtractFirstHeading(note.HtmlContent)
            If Not String.IsNullOrWhiteSpace(heading) Then
                Return If(heading.Length > 80, heading.Substring(0, 80) & "...", heading)
            End If

            Dim text As String = BuildPlainText(note.HtmlContent)
            If String.IsNullOrWhiteSpace(text) Then
                Return "无标题笔记"
            End If
            Return If(text.Length > 80, text.Substring(0, 80) & "...", text)
        End Function

        Private Shared Function ExtractFirstHeading(html As String) As String
            If String.IsNullOrWhiteSpace(html) Then
                Return Nothing
            End If

            Dim match As Match = Regex.Match(html, "<h[123][^>]*>(.*?)</h[123]>", RegexOptions.IgnoreCase Or RegexOptions.Singleline)
            If match.Success Then
                Dim headingHtml As String = match.Groups(1).Value
                Dim headingText As String = Regex.Replace(headingHtml, "<.*?>", " ")
                headingText = System.Net.WebUtility.HtmlDecode(headingText)
                headingText = Regex.Replace(headingText, "\s+", " ").Trim()
                Return If(String.IsNullOrWhiteSpace(headingText), Nothing, headingText)
            End If
            Return Nothing
        End Function

        Private Shared Function BuildPlainText(html As String) As String
            Dim withoutTags As String = Regex.Replace(If(html, String.Empty), "<.*?>", " ")
            Dim decoded As String = System.Net.WebUtility.HtmlDecode(withoutTags)
            Return Regex.Replace(decoded, "\s+", " ").Trim()
        End Function

        Private Shared Function IsSyncErrorMessage(message As String) As Boolean
            If String.IsNullOrEmpty(message) Then
                Return False
            End If
            Return message.Contains("失败") _
                OrElse message.Contains("错误") _
                OrElse message.Contains("无效") _
                OrElse message.Contains("请先") _
                OrElse message.Contains("没有找到")
        End Function

        Private Shared Function BuildBodyLine(note As NoteModel) As String
            If String.IsNullOrWhiteSpace(note.HtmlContent) Then
                Return Nothing
            End If

            Dim withoutHeadings As String = Regex.Replace(note.HtmlContent, "<h[123][^>]*>.*?</h[123]>", " ", RegexOptions.IgnoreCase Or RegexOptions.Singleline)
            Dim plainText As String = BuildPlainText(withoutHeadings)

            If String.IsNullOrWhiteSpace(plainText) Then
                Return Nothing
            End If

            Return If(plainText.Length > 60, plainText.Substring(0, 60) & "...", plainText)
        End Function

#End Region

        ''' <summary>自绘笔记列表的显示条目。</summary>
        Private NotInheritable Class NoteListItem

            Private ReadOnly _note As NoteModel
            Private _title As String
            Private _bodyLine As String
            Private _secondaryLine As String

            Public Sub New(note As NoteModel)
                _note = note
                RefreshDisplay()
            End Sub

            Public ReadOnly Property Note As NoteModel
                Get
                    Return _note
                End Get
            End Property

            Public ReadOnly Property Title As String
                Get
                    Return _title
                End Get
            End Property

            Public ReadOnly Property BodyLine As String
                Get
                    Return _bodyLine
                End Get
            End Property

            Public ReadOnly Property SecondaryLine As String
                Get
                    Return _secondaryLine
                End Get
            End Property

            Public ReadOnly Property IsPinned As Boolean
                Get
                    Return _note.IsPinned
                End Get
            End Property

            Public Sub RefreshDisplay()
                _title = BuildTitle(_note)
                _bodyLine = BuildBodyLine(_note)
                Dim parts As New List(Of String)()
                If _note.NoteType = NoteType.Checklist Then
                    parts.Add("清单")
                End If
                If _note.IsPinned Then
                    parts.Add("置顶")
                End If
                If _note.Tags.Count > 0 Then
                    parts.Add(String.Join(" · ", _note.Tags))
                End If
                parts.Add(_note.ModifiedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
                _secondaryLine = String.Join("  ·  ", parts)
            End Sub

            Public Sub SetCustomTitle(customTitle As String)
                _title = If(String.IsNullOrWhiteSpace(customTitle), "无标题笔记",
                    If(customTitle.Length > 80, customTitle.Substring(0, 80) & "...", customTitle))
            End Sub

            Public Overrides Function ToString() As String
                Return If(_title, String.Empty)
            End Function
        End Class
    End Class
End Namespace
