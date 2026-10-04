Option Strict On
Option Explicit On
Imports System
Imports System.Drawing
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.VisualBasic
Imports SilentNotes.WindowsVb.Services
Imports Sunny.UI
Imports VanillaCloudStorageClient
Imports VanillaCloudStorageClient.CloudStorageProviders

Namespace SilentNotes.WindowsVb.Controls
    ''' <summary>
    ''' WebDavSettingsDialog 的 WinForms 版：WebDAV 服务器/用户名/密码、传输码、
    ''' 同步模式与数据目录，带实时连接测试。
    ''' </summary>
    Public Class WebDavSettingsDialog
        Inherits ThemedDialogForm

        Private Shared ReadOnly UIAppFont As New Font("Microsoft YaHei UI", 9.0F)

        Private ReadOnly _serverUrlBox As UITextBox
        Private ReadOnly _usernameBox As UITextBox
        Private ReadOnly _passwordBox As UITextBox
        Private ReadOnly _transferCodeBox As UITextBox
        Private ReadOnly _syncModeBox As UIComboBox
        Private ReadOnly _dataDirectoryBox As UITextBox
        Private ReadOnly _statusText As UILabel
        Private ReadOnly _testConnectionButton As UIButton
        Private ReadOnly _saveButton As UIButton

        Private _currentDataDirectory As String

        Public Property ServerUrl As String
            Get
                Return _serverUrl
            End Get
            Private Set(value As String)
                _serverUrl = value
            End Set
        End Property
        Private _serverUrl As String

        Public Property Username As String
            Get
                Return _username
            End Get
            Private Set(value As String)
                _username = value
            End Set
        End Property
        Private _username As String

        Public Property Password As String
            Get
                Return _password
            End Get
            Private Set(value As String)
                _password = value
            End Set
        End Property
        Private _password As String

        Public Property TransferCode As String
            Get
                Return _transferCode
            End Get
            Private Set(value As String)
                _transferCode = value
            End Set
        End Property
        Private _transferCode As String

        Public Property SyncMode As String
            Get
                Return _syncMode
            End Get
            Private Set(value As String)
                _syncMode = value
            End Set
        End Property
        Private _syncMode As String

        ''' <summary>自定义数据目录路径。Nothing/空 表示默认位置。</summary>
        Public Property DataDirectory As String
            Get
                Return _dataDirectory
            End Get
            Private Set(value As String)
                _dataDirectory = value
            End Set
        End Property
        Private _dataDirectory As String

        Public Sub New()
            Text = "同步设置"
            Width = 480
            Height = 410

            ' 填充面板上的绝对布局：确定性布局，没有 TableLayoutPanel 的行增长怪癖。
            Dim content As New Panel With {.Dock = DockStyle.Fill, .Tag = "window"}

            _serverUrlBox = New UITextBox With {.Left = 112, .Top = 16, .Width = 348, .Font = UIAppFont, .Watermark = "https://…"}
            _usernameBox = New UITextBox With {.Left = 112, .Top = 54, .Width = 348, .Font = UIAppFont}
            _passwordBox = New UITextBox With {.Left = 112, .Top = 92, .Width = 348, .Font = UIAppFont, .PasswordChar = "●"c}
            _transferCodeBox = New UITextBox With {.Left = 112, .Top = 130, .Width = 348, .Font = UIAppFont}
            _syncModeBox = New UIComboBox With {.Left = 112, .Top = 168, .Width = 200, .Font = UIAppFont}
            _syncModeBox.Items.Add("每次手动同步")      ' Never
            _syncModeBox.Items.Add("有网络时自动同步")  ' CostFreeInternetOnly
            _syncModeBox.Items.Add("始终自动同步")      ' Always
            _dataDirectoryBox = New UITextBox With {.Left = 112, .Top = 206, .Width = 178, .Font = UIAppFont, .ReadOnly = True}
            Dim browseButton As New UIButton With {.Text = "浏览…", .Left = 298, .Top = 206, .Width = 76, .Height = 28, .Tag = "window", .Font = UIAppFont}
            AddHandler browseButton.Click, AddressOf BrowseDataDirButton_Click
            Dim resetDirButton As New UIButton With {.Text = "重置", .Left = 382, .Top = 206, .Width = 76, .Height = 28, .Tag = "window", .Font = UIAppFont}
            AddHandler resetDirButton.Click, Sub()
                                                 _currentDataDirectory = Nothing
                                                 _dataDirectoryBox.Text = "（默认位置）"
                                             End Sub

            _testConnectionButton = New UIButton With {.Text = "测试连接", .Left = 112, .Top = 254, .Width = 96, .Height = 30, .Tag = "accent", .Font = UIAppFont}
            AddHandler _testConnectionButton.Click, AddressOf TestConnectionButton_Click
            _testConnectionButton.Enabled = False

            _statusText = New UILabel With {
                .AutoSize = False,
                .Left = 20,
                .Top = 296,
                .Width = 440,
                .Height = 40,
                .Tag = "secondary",
                .Font = UIAppFont,
                .TextAlign = ContentAlignment.MiddleLeft
            }

            _saveButton = New UIButton With {.Text = "保存", .Left = 266, .Top = 254, .Width = 90, .Height = 30, .Tag = "accent", .Font = UIAppFont}
            AddHandler _saveButton.Click, AddressOf SaveButton_Click
            Dim cancelButton As New UIButton With {.Text = "取消", .Left = 364, .Top = 254, .Width = 90, .Height = 30, .Tag = "window", .Font = UIAppFont}
            AddHandler cancelButton.Click, Sub()
                                               DialogResult = DialogResult.Cancel
                                               Close()
                                           End Sub

            content.Controls.Add(_serverUrlBox)
            content.Controls.Add(_usernameBox)
            content.Controls.Add(_passwordBox)
            content.Controls.Add(_transferCodeBox)
            content.Controls.Add(_syncModeBox)
            content.Controls.Add(_dataDirectoryBox)
            content.Controls.Add(browseButton)
            content.Controls.Add(resetDirButton)
            content.Controls.Add(_testConnectionButton)
            content.Controls.Add(_saveButton)
            content.Controls.Add(cancelButton)
            content.Controls.Add(_statusText)
            AddCaption(content, "服务器地址", 20)
            AddCaption(content, "用户名", 58)
            AddCaption(content, "密码", 96)
            AddCaption(content, "传输码", 134)
            AddCaption(content, "自动同步", 172)
            AddCaption(content, "数据目录", 210)

            Controls.Add(content)
            AcceptButton = _saveButton
            CancelButton = cancelButton

            ' 在 UITextBox 类型的字段上订阅：SunnyUI 用 "new" 事件隐藏了 Control.TextChanged，
            ' 只有通过这个类型才会触发；Control 类型的引用会绑定到基类事件，永远不会触发。
            AddHandler _serverUrlBox.TextChanged, Sub() UpdateTestButtonState()
            AddHandler _usernameBox.TextChanged, Sub() UpdateTestButtonState()
            AddHandler _passwordBox.TextChanged, Sub() UpdateTestButtonState()
        End Sub

        Private Shared Sub AddCaption(host As Control, caption As String, top As Integer)
            host.Controls.Add(New UILabel With {
                .Text = caption,
                .Left = 20,
                .Top = top + 5,
                .AutoSize = True,
                .Font = UIAppFont
            })
        End Sub

        Private Sub UpdateTestButtonState()
            Dim hasContent As Boolean = Not String.IsNullOrWhiteSpace(_serverUrlBox.Text) _
                AndAlso Not String.IsNullOrWhiteSpace(_usernameBox.Text) _
                AndAlso Not String.IsNullOrEmpty(_passwordBox.Text)
            _testConnectionButton.Enabled = hasContent
            _statusText.Text = String.Empty
        End Sub

        ''' <summary>给表单字段设置预填值。</summary>
        Public Sub Prefill(url As String, username As String, password As String, transferCode As String, syncMode As String, dataDirectory As String)
            _serverUrlBox.Text = If(url, String.Empty)
            _usernameBox.Text = If(username, String.Empty)
            _passwordBox.Text = If(password, String.Empty)
            _transferCodeBox.Text = If(transferCode, String.Empty)
            _dataDirectoryBox.Text = If(dataDirectory, "（默认位置）")
            _currentDataDirectory = dataDirectory

            Dim modeIndex As Integer = 1
            If Not String.IsNullOrEmpty(syncMode) Then
                If String.Equals(syncMode, "Never", StringComparison.Ordinal) Then
                    modeIndex = 0
                ElseIf String.Equals(syncMode, "Always", StringComparison.Ordinal) Then
                    modeIndex = 2
                End If
            End If
            _syncModeBox.SelectedIndex = modeIndex
            UpdateTestButtonState()
        End Sub

        ' C# 版是 async void + await Task.Run；VB10 没有 async，
        ' 改为后台线程执行测试、SetStatus/BeginInvoke 回 UI 线程。
        Private Sub TestConnectionButton_Click(sender As Object, e As EventArgs)
            _testConnectionButton.Enabled = False
            _saveButton.Enabled = False
            SetStatus("正在测试连接...", False)

            Dim url As String = _serverUrlBox.Text.Trim()
            Dim username As String = _usernameBox.Text.Trim()
            Dim password As String = _passwordBox.Text

            Task.Factory.StartNew(Sub()
                                      Try
                                          Dim success As Boolean = TestWebDavConnectionSync(url, username, password)
                                          If success Then
                                              SetStatus("连接成功！WebDAV 服务器可用。", False)
                                          End If
                                      Catch ex As Exception
                                          SetStatus("发生未知错误：" & ex.Message, True)
                                      Finally
                                          BeginInvoke(New Action(Sub()
                                                                     _testConnectionButton.Enabled = True
                                                                     _saveButton.Enabled = True
                                                                 End Sub))
                                      End Try
                                  End Sub)
        End Sub

        Private Function TestWebDavConnectionSync(url As String, username As String, password As String) As Boolean
            Try
                Dim client As New WebdavCloudStorageClient(False)
                Dim credentials As New CloudStorageCredentials With {
                    .CloudStorageId = "webdav",
                    .Url = url,
                    .Username = username,
                    .UnprotectedPassword = password
                }

                ' 客户端保留 Task 签名（与 C# 版共用 DLL）；本方法运行在后台线程，Wait+解包等待，
                ' 异常还原成原始类型（AccessDeniedException 等），catch 分支语义与 C# 版一致。
                TaskUtils.WaitAndUnwrap(client.ListFileNamesAsync(credentials))
                Return True
            Catch ex As AccessDeniedException
                SetStatus("认证失败：用户名或密码错误。", True)
                Return False
            Catch ex As ConnectionFailedException
                SetStatus("连接失败：无法连接到服务器。请检查服务器地址和网络连接。" & vbCrLf & ex.Message, True)
                Return False
            Catch ex As CloudStorageException
                SetStatus("连接测试失败：" & ex.Message, True)
                Return False
            Catch ex As Exception
                SetStatus("发生未知错误：" & ex.Message, True)
                Return False
            End Try
        End Function

        Private Sub SetStatus(message As String, isError As Boolean)
            If InvokeRequired Then
                BeginInvoke(New Action(Sub() SetStatus(message, isError)))
                Return
            End If
            _statusText.Text = message
            If Theme IsNot Nothing Then
                _statusText.ForeColor = If(isError, Theme.Danger, Theme.TextSecondary)
            Else
                _statusText.ForeColor = If(isError, Color.Firebrick, Color.Gray)
            End If
        End Sub

        Private Sub BrowseDataDirButton_Click(sender As Object, e As EventArgs)
            Using dialog As New FolderBrowserDialog()
                dialog.Description = "选择本地数据文件保存目录"
                dialog.ShowNewFolderButton = True
                If Not String.IsNullOrEmpty(_currentDataDirectory) AndAlso System.IO.Directory.Exists(_currentDataDirectory) Then
                    dialog.SelectedPath = _currentDataDirectory
                End If

                If dialog.ShowDialog() = DialogResult.OK Then
                    _currentDataDirectory = dialog.SelectedPath
                    _dataDirectoryBox.Text = _currentDataDirectory
                End If
            End Using
        End Sub

        Private Sub SaveButton_Click(sender As Object, e As EventArgs)
            ServerUrl = _serverUrlBox.Text.Trim()
            Username = _usernameBox.Text.Trim()
            Password = _passwordBox.Text
            TransferCode = _transferCodeBox.Text.Replace(" ", String.Empty)
            If _syncModeBox.SelectedIndex = 0 Then
                SyncMode = "Never"
            ElseIf _syncModeBox.SelectedIndex = 2 Then
                SyncMode = "Always"
            Else
                SyncMode = "CostFreeInternetOnly"
            End If
            DataDirectory = _currentDataDirectory
            DialogResult = DialogResult.OK
        End Sub
    End Class
End Namespace
