Option Strict On
Option Explicit On
Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Windows.Forms
Imports SilentNotes.Models
Imports SilentNotes.WindowsVb.Controls
Imports Sunny.UI

Namespace SilentNotes.WindowsVb.Services
    ''' <summary>
    ''' WinForms 客户端的应用配色：表面/文字/边框用中性灰，色相只出现在强调 token
    ''' （Accent teal、Action 橙、Danger 红）。浅深两套均基于 token；控件样式靠遍历
    ''' 控件树下发。控件用 Tag 指令控制外观：
    ''' "accent"(teal)、"action"(orange CTA)、"danger"、"ghost"(纯图标按钮)、"secondary"、
    ''' "paper"、"panel"、"window"、"bare"(无背景)。
    ''' </summary>
    Friend Class WinFormsThemeService

        Public Property IsDarkMode As Boolean
            Get
                Return _isDarkMode
            End Get
            Private Set(value As Boolean)
                _isDarkMode = value
            End Set
        End Property
        Private _isDarkMode As Boolean

        Private Structure Palette
            Public SurfaceWindow, SurfacePanel, SurfacePaper, BorderSubtle, TextPrimary, TextSecondary As Color
            Public Accent, AccentHover, AccentSoft, Action, ActionHover, Danger, DangerSoft, ListHover As Color
        End Structure

        Private Shared ReadOnly DarkPalette As Palette = BuildPalette(True)
        Private Shared ReadOnly LightPalette As Palette = BuildPalette(False)
        Private _palette As Palette

        Public ReadOnly Property SurfaceWindow As Color
            Get
                Return _palette.SurfaceWindow
            End Get
        End Property

        Public ReadOnly Property SurfacePanel As Color
            Get
                Return _palette.SurfacePanel
            End Get
        End Property

        Public ReadOnly Property SurfacePaper As Color
            Get
                Return _palette.SurfacePaper
            End Get
        End Property

        Public ReadOnly Property BorderSubtle As Color
            Get
                Return _palette.BorderSubtle
            End Get
        End Property

        Public ReadOnly Property TextPrimary As Color
            Get
                Return _palette.TextPrimary
            End Get
        End Property

        Public ReadOnly Property TextSecondary As Color
            Get
                Return _palette.TextSecondary
            End Get
        End Property

        Public ReadOnly Property Accent As Color
            Get
                Return _palette.Accent
            End Get
        End Property

        Public ReadOnly Property AccentHover As Color
            Get
                Return _palette.AccentHover
            End Get
        End Property

        Public ReadOnly Property AccentSoft As Color
            Get
                Return _palette.AccentSoft
            End Get
        End Property

        Public ReadOnly Property Action As Color
            Get
                Return _palette.Action
            End Get
        End Property

        Public ReadOnly Property ActionHover As Color
            Get
                Return _palette.ActionHover
            End Get
        End Property

        Public ReadOnly Property Danger As Color
            Get
                Return _palette.Danger
            End Get
        End Property

        Public ReadOnly Property DangerSoft As Color
            Get
                Return _palette.DangerSoft
            End Get
        End Property

        Public ReadOnly Property ListHover As Color
            Get
                Return _palette.ListHover
            End Get
        End Property

        Public Sub ApplyTheme(mode As ThemeMode)
            Dim shouldBeDark As Boolean
            If mode = ThemeMode.Dark Then
                shouldBeDark = True
            ElseIf mode = ThemeMode.Light Then
                shouldBeDark = False
            Else
                shouldBeDark = IsSystemDarkMode()
            End If
            ApplyTheme(shouldBeDark)
        End Sub

        Public Sub New()
            ' 调色板必须先填充：当请求的模式与初始 dark 标志一致时 ApplyTheme 会提前返回，
            ' 而编辑器在第一次 ApplyTheme 之前就依赖 IsDarkMode。
            _isDarkMode = False
            _palette = LightPalette
            RegisterStylers()
        End Sub

        Public Sub ApplyTheme(dark As Boolean)
            If IsDarkMode = dark Then
                Return
            End If
            _isDarkMode = dark
            _palette = If(dark, DarkPalette, LightPalette)
        End Sub

        Private Shared Function BuildPalette(dark As Boolean) As Palette
            Dim p As New Palette()
            If dark Then
                ' 深色表面/文字/边框/悬停为中性炭灰（无色相）；色相只出现在 accent/action/danger token。
                p.SurfaceWindow = FromHex("#1A1A1C")
                p.SurfacePanel = FromHex("#212124")
                p.SurfacePaper = FromHex("#29292D")
                p.BorderSubtle = FromHex("#3A3A40")
                p.TextPrimary = FromHex("#E4E4E7")
                p.TextSecondary = FromHex("#9A9AA3")
                p.Accent = FromHex("#2DD4BF")
                p.AccentHover = FromHex("#14B8A6")
                p.AccentSoft = FromHex("#1E3D38")
                ' Action 仅用于 保存/新建笔记 按钮。
                p.Action = FromHex("#C2410C")
                p.ActionHover = FromHex("#EA580C")
                p.Danger = FromHex("#F87171")
                p.DangerSoft = FromHex("#3B2222")
                p.ListHover = FromHex("#2E2E33")
            Else
                p.SurfaceWindow = FromHex("#F7F7F8")
                p.SurfacePanel = FromHex("#EEEEF0")
                p.SurfacePaper = FromHex("#FFFFFF")
                p.BorderSubtle = FromHex("#D8D8DC")
                p.TextPrimary = FromHex("#1F2328")
                ' 次要文本对比度 ≥ 4.5:1（8.5F 文本用于状态栏、信息行、标签标题）。
                p.TextSecondary = FromHex("#57575E")
                p.Accent = FromHex("#0D9488")
                p.AccentHover = FromHex("#0A7E73")
                p.AccentSoft = FromHex("#CFF2EB")
                ' Action 底色上的白色 9F 文本对比度 ≥ 4.5:1。
                p.Action = FromHex("#C94D0B")
                p.ActionHover = FromHex("#B24409")
                p.Danger = FromHex("#DC2626")
                p.DangerSoft = FromHex("#FEE2E2")
                p.ListHover = FromHex("#EFEFF1")
            End If
            Return p
        End Function

        ''' <summary>把当前调色板递归应用到控件树。</summary>
        Public Sub Apply(root As Control)
            If root Is Nothing Then
                Return
            End If
            ApplyControl(root)
            For Each child As Control In root.Controls
                Apply(child)
            Next
        End Sub

        ' 类型 → 样式处理器：ApplyControl 沿控件实际类型的继承链向上取最近的注册项，
        ' SunnyUI 类型因此天然先于同名 WinForms 基类匹配。注册项为 Nothing 表示跳过着色。
        ' Tag 指令：accent / action / danger / secondary / paper / window / tool / bare。
        Private ReadOnly _stylers As New Dictionary(Of Type, Action(Of Control, ThemeTags))()

        Private Sub RegisterStylers()
            RegisterStyler(Of Form)(Nothing)
            RegisterStyler(Of UIBreadcrumb)(AddressOf StyleBreadcrumb)
            RegisterStyler(Of UISymbolButton)(AddressOf StyleUISymbolButton)
            RegisterStyler(Of UIButton)(AddressOf StyleButtonCore)
            RegisterStyler(Of UILabel)(AddressOf StyleUILabel)
            RegisterStyler(Of UITextBox)(AddressOf StyleUITextBox)
            RegisterStyler(Of UIListBox)(AddressOf StyleUIListBox)
            RegisterStyler(Of UIComboBox)(AddressOf StyleUIComboBox)
            RegisterStyler(Of UIPanel)(AddressOf StyleUIPanel)
            RegisterStyler(Of UILine)(AddressOf StyleUILine)
            RegisterStyler(Of UICheckBox)(AddressOf StyleUICheckBox)
            RegisterStyler(Of Button)(AddressOf StyleWinButton)
            RegisterStyler(Of TextBox)(AddressOf StyleWinTextBox)
            RegisterStyler(Of ComboBox)(AddressOf StyleWinComboBox)
            RegisterStyler(Of ListBox)(AddressOf StyleWinListBox)
            RegisterStyler(Of Label)(AddressOf StyleWinLabel)
            RegisterStyler(Of CheckBox)(AddressOf StyleWinCheckBox)
            RegisterStyler(Of ToolStrip)(AddressOf StyleToolStrip)
            RegisterStyler(Of StatusStrip)(AddressOf StyleStatusStrip)
            RegisterStyler(Of SplitContainer)(AddressOf StyleSplitContainer)
        End Sub

        Private Sub RegisterStyler(Of T As Control)(handler As Action(Of T, ThemeTags))
            ' Nothing 哨兵必须原样存入字典：包装成 lambda 后外层永远非 Nothing，
            ' ApplyControl 的判空就拦不住内层调用，会在运行时抛 NRE。
            If handler Is Nothing Then
                _stylers(GetType(T)) = Nothing
            Else
                _stylers(GetType(T)) = Sub(control, tags) handler(DirectCast(control, T), tags)
            End If
        End Sub

        Private Structure ThemeTags
            ' accent / action / danger / secondary / paper / window / tool / bare
            Public ReadOnly IsAccent, IsAction, IsDanger, IsSecondary, IsPaper, IsWindow, IsTool, IsBare As Boolean

            Public Sub New(tag As Object)
                Dim text As String = If(TryCast(tag, String), String.Empty)
                IsAccent = HasToken(text, "accent")
                IsAction = HasToken(text, "action")
                IsDanger = HasToken(text, "danger")
                IsSecondary = HasToken(text, "secondary")
                IsPaper = HasToken(text, "paper")
                IsWindow = HasToken(text, "window")
                IsTool = HasToken(text, "tool")
                IsBare = HasToken(text, "bare")
            End Sub

            Private Shared Function HasToken(text As String, token As String) As Boolean
                ' 按 token 全词比对：子串匹配会把 "dangerous" 误判成 danger。
                For Each part As String In text.Split(" "c)
                    If String.Equals(part, token, StringComparison.OrdinalIgnoreCase) Then
                        Return True
                    End If
                Next
                Return False
            End Function
        End Structure

        Private Sub ApplyControl(control As Control)
            Dim tags As New ThemeTags(control.Tag)
            Dim current As Type = control.GetType()
            While current IsNot Nothing AndAlso current IsNot GetType(Object)
                Dim styler As Action(Of Control, ThemeTags) = Nothing
                If _stylers.TryGetValue(current, styler) Then
                    If styler IsNot Nothing Then
                        styler(control, tags)
                    End If
                    Return
                End If
                current = current.BaseType
            End While
            ApplyFallbackStyle(control, tags)
        End Sub

        ''' <summary>无专属样式的普通控件：paper 决定背景，secondary 决定前景。</summary>
        Private Sub ApplyFallbackStyle(control As Control, tags As ThemeTags)
            If Not tags.IsBare Then
                control.BackColor = If(tags.IsPaper, SurfacePaper, SurfacePanel)
            End If
            If tags.IsSecondary Then
                control.ForeColor = TextSecondary
            End If
        End Sub

        Private Sub StyleBreadcrumb(breadcrumb As UIBreadcrumb, tags As ThemeTags)
            ' 文字颜色按 index <= ItemIndex 累积取色、无法逐节点指定，两节点统一
            ' 用 TextPrimary；填充色由 RefreshModeButtonColors 经 SetItemColor 逐节点
            ' 覆盖（选中 AccentSoft、未选 SurfaceWindow）。BackColor 必须不透明：
            ' 透明背景在重绘时模拟父级背景会导致闪烁。
            breadcrumb.BackColor = SurfacePanel
            breadcrumb.SelectedColor = AccentSoft
            breadcrumb.ForeColor = TextPrimary
            breadcrumb.UnSelectedColor = SurfaceWindow
            breadcrumb.UnSelectedForeColor = TextPrimary
        End Sub

        Private Sub StyleUISymbolButton(button As UISymbolButton, tags As ThemeTags)
            StyleButtonCore(button, tags)
            If tags.IsAction Then
                button.SymbolColor = Color.White
                button.SymbolHoverColor = Color.White
                button.SymbolPressColor = Color.White
            ElseIf tags.IsAccent Then
                button.SymbolColor = Color.White
                button.SymbolHoverColor = Color.White
                button.SymbolPressColor = Color.White
            ElseIf tags.IsDanger Then
                button.SymbolColor = Danger
                button.SymbolHoverColor = Danger
                button.SymbolPressColor = Danger
            Else
                ' Ghost 图标按钮：SymbolColor=TextSecondary，悬停 Accent，按下 AccentHover。
                button.SymbolColor = TextSecondary
                button.SymbolHoverColor = Accent
                button.SymbolPressColor = AccentHover
            End If
        End Sub

        Private Sub StyleUILabel(label As UILabel, tags As ThemeTags)
            label.Style = UIStyle.Custom
            label.ForeColor = If(tags.IsDanger, Danger, If(tags.IsSecondary, TextSecondary, TextPrimary))
        End Sub

        ''' <summary>已挂水印面板监听的 UITextBox 内层编辑框：主题每次重刷都走 StyleUITextBox，防 AddHandler 累积。</summary>
        Private ReadOnly _watermarkWatched As New HashSet(Of Control)()

        Private Sub StyleUITextBox(uiTextBox As UITextBox, tags As ThemeTags)
            uiTextBox.Style = UIStyle.Custom
            uiTextBox.FillColor = SurfacePaper
            uiTextBox.RectColor = BorderSubtle
            uiTextBox.ForeColor = TextPrimary
            uiTextBox.WatermarkColor = TextSecondary
            uiTextBox.WatermarkActiveColor = TextSecondary
            uiTextBox.SymbolColor = TextSecondary
            ' 内层编辑框（UIEdit）构造为 FixedSingle 边框，会在圆角框内再画出
            ' 一圈灰色矩形，置为无边框。
            uiTextBox.TextBox.BorderStyle = BorderStyle.None
            ' UIEdit 空文本时会把水印面板盖在编辑区上，其默认灰底会把输入框
            ' 整块盖住；打 paper 标记并填输入框底色，含清空文本后重建的情形。
            For Each wmChild As Control In uiTextBox.TextBox.Controls
                wmChild.Tag = "paper"
                wmChild.BackColor = SurfacePaper
            Next
            If _watermarkWatched.Add(uiTextBox.TextBox) Then
                AddHandler uiTextBox.TextBox.ControlAdded,
                    Sub(s, ea)
                        ea.Control.Tag = "paper"
                        ea.Control.BackColor = SurfacePaper
                    End Sub
            End If
            ' 只读 UITextBox（数据目录）切换到专用只读调色板。
            uiTextBox.FillReadOnlyColor = SurfacePaper
            uiTextBox.RectReadOnlyColor = BorderSubtle
            uiTextBox.ForeReadOnlyColor = TextSecondary
        End Sub

        Private Sub StyleUIListBox(uiListBox As UIListBox, tags As ThemeTags)
            uiListBox.Style = UIStyle.Custom
            uiListBox.FillColor = SurfacePaper
            uiListBox.RectColor = SurfacePaper
            uiListBox.ListBox.BackColor = SurfacePaper
            uiListBox.ItemSelectBackColor = AccentSoft
            uiListBox.ItemSelectForeColor = Accent
            uiListBox.HoverColor = ListHover
            uiListBox.ScrollBarBackColor = SurfacePaper
            uiListBox.ScrollBarColor = BorderSubtle
            ' 列表自身的默认条目文本保持不可见：条目在 DrawItem 处理器里重绘，
            ' 可见的默认文本会在悬停时闪烁。
            uiListBox.ForeColor = SurfacePaper
        End Sub

        Private Sub StyleUIComboBox(uiComboBox As UIComboBox, tags As ThemeTags)
            ' 下拉箭头用 RectColor 画在 FillColor 上；保持默认值会让箭头消失在背景里。
            uiComboBox.Style = UIStyle.Custom
            uiComboBox.FillColor = SurfacePaper
            uiComboBox.RectColor = BorderSubtle
            uiComboBox.ForeColor = TextPrimary
            uiComboBox.ItemFillColor = SurfacePaper
            uiComboBox.ItemForeColor = TextPrimary
            uiComboBox.ItemSelectBackColor = AccentSoft
            uiComboBox.ItemSelectForeColor = TextPrimary
            uiComboBox.ItemHoverColor = ListHover
        End Sub

        Private Sub StyleUIPanel(uiPanel As UIPanel, tags As ThemeTags)
            uiPanel.Style = UIStyle.Custom
            Dim fill As Color = If(tags.IsWindow, SurfaceWindow, If(tags.IsPaper, SurfacePaper, SurfacePanel))
            uiPanel.FillColor = fill
            uiPanel.RectColor = fill
        End Sub

        Private Sub StyleUILine(uiLine As UILine, tags As ThemeTags)
            uiLine.Style = UIStyle.Custom
            uiLine.LineColor = BorderSubtle
        End Sub

        Private Sub StyleUICheckBox(uiCheckBox As UICheckBox, tags As ThemeTags)
            uiCheckBox.Style = UIStyle.Custom
            uiCheckBox.ForeColor = TextPrimary
        End Sub

        Private Sub StyleWinButton(button As Button, tags As ThemeTags)
            button.FlatStyle = FlatStyle.Flat
            button.FlatAppearance.BorderSize = 1
            If tags.IsAccent Then
                button.BackColor = Accent
                button.ForeColor = Color.White
                button.FlatAppearance.BorderColor = Accent
                button.FlatAppearance.MouseOverBackColor = AccentHover
            ElseIf tags.IsDanger Then
                button.BackColor = DangerSoft
                button.ForeColor = Danger
                button.FlatAppearance.BorderColor = Danger
                button.FlatAppearance.MouseOverBackColor = DangerSoft
            Else
                button.BackColor = If(tags.IsPaper, SurfacePaper, SurfaceWindow)
                button.ForeColor = TextPrimary
                button.FlatAppearance.BorderColor = BorderSubtle
                button.FlatAppearance.MouseOverBackColor = AccentSoft
            End If
        End Sub

        Private Sub StyleWinTextBox(textBox As TextBox, tags As ThemeTags)
            textBox.BackColor = SurfacePaper
            textBox.ForeColor = TextPrimary
            ' SunnyUI 包装器（UITextBox/UIComboBox）自绘边框；给它们的内部
            ' TextBox 强加边框会画出多余的括号。
            If Not (TypeOf textBox.Parent Is UITextBox) AndAlso Not (TypeOf textBox.Parent Is UIComboBox) Then
                textBox.BorderStyle = BorderStyle.FixedSingle
            End If
        End Sub

        Private Sub StyleWinComboBox(comboBox As ComboBox, tags As ThemeTags)
            comboBox.FlatStyle = FlatStyle.Flat
            comboBox.BackColor = SurfacePaper
            comboBox.ForeColor = TextPrimary
        End Sub

        Private Sub StyleWinListBox(listBox As ListBox, tags As ThemeTags)
            If TypeOf listBox.Parent Is UIListBox Then
                ' UIListBox 的内层 listbox：SunnyUI 自绘边框且条目在 DrawItem 里重绘；
                ' 强加边框会画出多余边框，可见的 ForeColor 会导致闪烁。
                listBox.BackColor = SurfacePaper
                listBox.ForeColor = SurfacePaper
                listBox.BorderStyle = BorderStyle.None
                Return
            End If
            listBox.BackColor = SurfacePaper
            listBox.ForeColor = TextPrimary
            listBox.BorderStyle = BorderStyle.FixedSingle
        End Sub

        Private Sub StyleWinLabel(label As Label, tags As ThemeTags)
            If Not tags.IsBare Then
                label.BackColor = Color.Transparent
            End If
            label.ForeColor = If(tags.IsDanger, Danger, If(tags.IsSecondary, TextSecondary, TextPrimary))
        End Sub

        Private Sub StyleWinCheckBox(checkBox As CheckBox, tags As ThemeTags)
            If Not tags.IsBare Then
                checkBox.BackColor = If(checkBox.Parent IsNot Nothing, checkBox.Parent.BackColor, SurfacePanel)
            End If
            checkBox.ForeColor = TextPrimary
        End Sub

        Private Sub StyleToolStrip(toolStrip As ToolStrip, tags As ThemeTags)
            toolStrip.BackColor = SurfacePanel
            toolStrip.ForeColor = TextPrimary
        End Sub

        Private Sub StyleStatusStrip(statusStrip As StatusStrip, tags As ThemeTags)
            statusStrip.BackColor = SurfacePanel
            statusStrip.ForeColor = TextSecondary
        End Sub

        Private Sub StyleSplitContainer(splitContainer As SplitContainer, tags As ThemeTags)
            splitContainer.BackColor = BorderSubtle
        End Sub

        Private Sub StyleButtonCore(button As UIButton, tags As ThemeTags)
            ' SunnyUI 会把全局样式级联到每个 Style 仍为 "Inherited" 的控件上；
            ' 把控件标记为 Custom 可以让它退出级联。
            button.Style = UIStyle.Custom
            If tags.IsAction Then
                button.FillColor = Action
                button.ForeColor = Color.White
                button.RectColor = Action
                button.FillHoverColor = ActionHover
                button.RectHoverColor = ActionHover
                button.ForeHoverColor = Color.White
                button.FillPressColor = ActionHover
                button.RectPressColor = ActionHover
            ElseIf tags.IsAccent Then
                button.FillColor = Accent
                button.ForeColor = Color.White
                button.RectColor = Accent
                button.FillHoverColor = AccentHover
                button.RectHoverColor = AccentHover
                button.ForeHoverColor = Color.White
                button.FillPressColor = AccentHover
                button.RectPressColor = AccentHover
            ElseIf tags.IsDanger Then
                button.FillColor = DangerSoft
                button.ForeColor = Danger
                button.RectColor = Danger
                button.FillHoverColor = DangerSoft
                button.RectHoverColor = Danger
                button.ForeHoverColor = Danger
            Else
                Dim fill As Color = If(tags.IsWindow, SurfaceWindow, If(tags.IsPaper, SurfacePaper, SurfacePanel))
                button.FillColor = fill
                button.ForeColor = TextPrimary
                button.RectColor = If(tags.IsBare, fill, BorderSubtle)
                ' 工具栏（"tool"）按钮悬停/按下使用 ListHover，不使用 AccentSoft/Accent。
                If tags.IsTool Then
                    button.FillHoverColor = ListHover
                    button.RectHoverColor = ListHover
                    button.FillPressColor = ListHover
                    button.RectPressColor = ListHover
                    button.ForeHoverColor = TextPrimary
                Else
                    button.FillHoverColor = AccentSoft
                    button.RectHoverColor = Accent
                    button.ForeHoverColor = TextPrimary
                End If
            End If
        End Sub

        Public Sub ApplyWindowTheme(form As Form)
            If form Is Nothing Then
                Return
            End If
            TrySetImmersiveDarkMode(form, IsDarkMode)
        End Sub

        Private Shared Function FromHex(hex As String) As Color
            Return ColorTranslator.FromHtml(hex)
        End Function

        Private Shared Sub TrySetImmersiveDarkMode(form As Form, dark As Boolean)
            Try
                Dim handle As IntPtr = form.Handle
                If handle = IntPtr.Zero Then
                    Return
                End If

                Dim value As Integer = If(dark, 1, 0)
                DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, value, 4)
            Catch
            End Try
        End Sub

        <System.Runtime.InteropServices.DllImport("dwmapi.dll")>
        Private Shared Function DwmSetWindowAttribute(hwnd As IntPtr, dwAttribute As Integer, ByRef pvAttribute As Integer, cbAttribute As Integer) As Integer
        End Function

        Private Const DWMWA_USE_IMMERSIVE_DARK_MODE As Integer = 20

        Private Shared Function IsSystemDarkMode() As Boolean
            Try
                Using key As Microsoft.Win32.RegistryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")
                    Dim value As Object = Nothing
                    If key IsNot Nothing Then
                        value = key.GetValue("AppsUseLightTheme")
                    End If
                    If TypeOf value Is Integer Then
                        Return (DirectCast(value, Integer) = 0)
                    End If
                End Using
            Catch
            End Try
            Return False
        End Function
    End Class
End Namespace
