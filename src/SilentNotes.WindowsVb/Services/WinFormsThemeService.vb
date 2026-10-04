Option Strict On
Option Explicit On
Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports SilentNotes.Models
Imports SilentNotes.WindowsVb.Controls
Imports Sunny.UI

Namespace SilentNotes.WindowsVb.Services
    ''' <summary>
    ''' WinForms 客户端的应用配色，源自"Minimalism & Swiss Style"设计（teal 焦点色 + 单一 orange 动作色）。
    ''' 浅深两套均基于 token；控件样式靠遍历控件树下发。控件用 Tag 指令控制外观：
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

        Public Property SurfaceWindow As Color
            Get
                Return _surfaceWindow
            End Get
            Private Set(value As Color)
                _surfaceWindow = value
            End Set
        End Property
        Private _surfaceWindow As Color

        Public Property SurfacePanel As Color
            Get
                Return _surfacePanel
            End Get
            Private Set(value As Color)
                _surfacePanel = value
            End Set
        End Property
        Private _surfacePanel As Color

        Public Property SurfacePaper As Color
            Get
                Return _surfacePaper
            End Get
            Private Set(value As Color)
                _surfacePaper = value
            End Set
        End Property
        Private _surfacePaper As Color

        Public Property BorderSubtle As Color
            Get
                Return _borderSubtle
            End Get
            Private Set(value As Color)
                _borderSubtle = value
            End Set
        End Property
        Private _borderSubtle As Color

        Public Property TextPrimary As Color
            Get
                Return _textPrimary
            End Get
            Private Set(value As Color)
                _textPrimary = value
            End Set
        End Property
        Private _textPrimary As Color

        Public Property TextSecondary As Color
            Get
                Return _textSecondary
            End Get
            Private Set(value As Color)
                _textSecondary = value
            End Set
        End Property
        Private _textSecondary As Color

        Public Property Accent As Color
            Get
                Return _accent
            End Get
            Private Set(value As Color)
                _accent = value
            End Set
        End Property
        Private _accent As Color

        Public Property AccentHover As Color
            Get
                Return _accentHover
            End Get
            Private Set(value As Color)
                _accentHover = value
            End Set
        End Property
        Private _accentHover As Color

        Public Property AccentSoft As Color
            Get
                Return _accentSoft
            End Get
            Private Set(value As Color)
                _accentSoft = value
            End Set
        End Property
        Private _accentSoft As Color

        Public Property Action As Color
            Get
                Return _action
            End Get
            Private Set(value As Color)
                _action = value
            End Set
        End Property
        Private _action As Color

        Public Property ActionHover As Color
            Get
                Return _actionHover
            End Get
            Private Set(value As Color)
                _actionHover = value
            End Set
        End Property
        Private _actionHover As Color

        Public Property Danger As Color
            Get
                Return _danger
            End Get
            Private Set(value As Color)
                _danger = value
            End Set
        End Property
        Private _danger As Color

        Public Property DangerSoft As Color
            Get
                Return _dangerSoft
            End Get
            Private Set(value As Color)
                _dangerSoft = value
            End Set
        End Property
        Private _dangerSoft As Color

        Public Property ListHover As Color
            Get
                Return _listHover
            End Get
            Private Set(value As Color)
                _listHover = value
            End Set
        End Property
        Private _listHover As Color

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
            SetPalette(False)
        End Sub

        Public Sub ApplyTheme(dark As Boolean)
            If IsDarkMode = dark Then
                Return
            End If
            _isDarkMode = dark
            SetPalette(dark)
        End Sub

        Private Sub SetPalette(dark As Boolean)
            If dark Then
                _surfaceWindow = FromHex("#17211F")
                _surfacePanel = FromHex("#1D2927")
                _surfacePaper = FromHex("#223030")
                _borderSubtle = FromHex("#33443F")
                _textPrimary = FromHex("#D9E6E2")
                _textSecondary = FromHex("#8FA8A0")
                _accent = FromHex("#2DD4BF")
                _accentHover = FromHex("#14B8A6")
                _accentSoft = FromHex("#1E3D38")
                ' 深色的 Action 保持深橙：浅色的橙在深色表面上看着像霓虹（只有 保存/新建笔记 用它）。
                _action = FromHex("#C2410C")
                _actionHover = FromHex("#EA580C")
                _danger = FromHex("#F87171")
                _dangerSoft = FromHex("#3B2222")
                _listHover = FromHex("#263532")
            Else
                _surfaceWindow = FromHex("#F0F7F5")
                _surfacePanel = FromHex("#E6F0EC")
                _surfacePaper = FromHex("#FFFFFF")
                _borderSubtle = FromHex("#CFE0DA")
                _textPrimary = FromHex("#12433E")
                ' 从 #5B776E 加深：在侧栏面板上 4.18:1 未达 WCAG 4.5:1（8.5F 次要文本：状态栏、信息行、标签标题）。
                _textSecondary = FromHex("#4E6A61")
                _accent = FromHex("#0D9488")
                _accentHover = FromHex("#0A7E73")
                _accentSoft = FromHex("#CFF2EB")
                ' 从 #EA580C 加深：白色 9F 文本只有 3.58:1；#C94D0B 达到 4.6:1。
                _action = FromHex("#C94D0B")
                _actionHover = FromHex("#B24409")
                _danger = FromHex("#DC2626")
                _dangerSoft = FromHex("#FEE2E2")
                _listHover = FromHex("#EDF5F2")
            End If
        End Sub

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

        Private Sub ApplyControl(control As Control)
            ' 窗体（包括 SunnyUI 的 UIForm）通过 ApplyThemeToUi 自行着色
            If TypeOf control Is Form Then
                Return
            End If

            Dim tag As String = If(TryCast(control.Tag, String), String.Empty)
            Dim hasAccent As Boolean = tag.IndexOf("accent", StringComparison.OrdinalIgnoreCase) >= 0
            Dim hasAction As Boolean = tag.IndexOf("action", StringComparison.OrdinalIgnoreCase) >= 0
            Dim hasDanger As Boolean = tag.IndexOf("danger", StringComparison.OrdinalIgnoreCase) >= 0
            Dim hasSecondary As Boolean = tag.IndexOf("secondary", StringComparison.OrdinalIgnoreCase) >= 0
            Dim hasPaper As Boolean = tag.IndexOf("paper", StringComparison.OrdinalIgnoreCase) >= 0
            Dim hasWindow As Boolean = tag.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0
            Dim hasTool As Boolean = tag.IndexOf("tool", StringComparison.OrdinalIgnoreCase) >= 0
            Dim bare As Boolean = tag.IndexOf("bare", StringComparison.OrdinalIgnoreCase) >= 0

            ' 自绘 SegmentedToggle 直接用这些 token 色；必须在通用回退之前匹配，
            ' 否则通用回退会用面板填充色盖掉它的透明背景。
            Dim segmentedToggle As SegmentedToggle = TryCast(control, SegmentedToggle)
            If segmentedToggle IsNot Nothing Then
                ' 凹槽轨道（window 色）+ 凸起的 paper 色药丸——经典分段控件配色。
                ' 不透明底色与侧栏一致；轨道画在其上。
                segmentedToggle.BackColor = SurfacePanel
                segmentedToggle.TrackFill = SurfaceWindow
                segmentedToggle.TrackBorder = BorderSubtle
                segmentedToggle.SegmentFill = SurfacePaper
                segmentedToggle.SegmentText = Accent
                segmentedToggle.TrackText = TextSecondary
                segmentedToggle.TrackTextHover = TextPrimary
                Return
            End If

            ' SunnyUI 控件必须先于其标准基类匹配；
            ' UIButton/UISymbolButton/UICheckBox 派生自 UIControl，不是 Button。
            Dim symbolButton As UISymbolButton = TryCast(control, UISymbolButton)
            If symbolButton IsNot Nothing Then
                StyleSymbolButton(symbolButton, hasAccent, hasAction, hasDanger, hasPaper, hasWindow, hasTool, bare)
                Return
            End If

            Dim uiButton As UIButton = TryCast(control, UIButton)
            If uiButton IsNot Nothing Then
                StyleButton(uiButton, hasAccent, hasAction, hasDanger, hasPaper, hasWindow, hasTool, bare)
                Return
            End If

            Dim uiLabel As UILabel = TryCast(control, UILabel)
            If uiLabel IsNot Nothing Then
                uiLabel.Style = UIStyle.Custom
                uiLabel.ForeColor = If(hasDanger, Danger, If(hasSecondary, TextSecondary, TextPrimary))
                Return
            End If

            Dim uiTextBox As UITextBox = TryCast(control, UITextBox)
            If uiTextBox IsNot Nothing Then
                uiTextBox.Style = UIStyle.Custom
                uiTextBox.FillColor = SurfacePaper
                uiTextBox.ForeColor = TextPrimary
                uiTextBox.RectColor = BorderSubtle
                uiTextBox.WatermarkColor = TextSecondary
                uiTextBox.WatermarkActiveColor = TextSecondary
                uiTextBox.SymbolColor = TextSecondary
                ' 只读 UITextBox（数据目录）切换到专用只读调色板，
                ' 其近白默认色在深色模式下会发亮。
                uiTextBox.FillReadOnlyColor = SurfacePaper
                uiTextBox.RectReadOnlyColor = BorderSubtle
                uiTextBox.ForeReadOnlyColor = TextSecondary
                Return
            End If

            Dim uiListBox As UIListBox = TryCast(control, UIListBox)
            If uiListBox IsNot Nothing Then
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
                Return
            End If

            Dim uiComboBox As UIComboBox = TryCast(control, UIComboBox)
            If uiComboBox IsNot Nothing Then
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
                Return
            End If

            Dim uiPanel As UIPanel = TryCast(control, UIPanel)
            If uiPanel IsNot Nothing Then
                uiPanel.Style = UIStyle.Custom
                Dim panelFill As Color = If(hasWindow, SurfaceWindow, If(hasPaper, SurfacePaper, SurfacePanel))
                uiPanel.FillColor = panelFill
                uiPanel.RectColor = panelFill
                Return
            End If

            Dim uiLine As UILine = TryCast(control, UILine)
            If uiLine IsNot Nothing Then
                uiLine.Style = UIStyle.Custom
                uiLine.LineColor = BorderSubtle
                Return
            End If

            Dim uiCheckBox As UICheckBox = TryCast(control, UICheckBox)
            If uiCheckBox IsNot Nothing Then
                uiCheckBox.Style = UIStyle.Custom
                uiCheckBox.ForeColor = TextPrimary
                Return
            End If

            Dim button As Button = TryCast(control, Button)
            If button IsNot Nothing Then
                button.FlatStyle = FlatStyle.Flat
                button.FlatAppearance.BorderSize = 1
                If hasAccent Then
                    button.BackColor = Accent
                    button.ForeColor = Color.White
                    button.FlatAppearance.BorderColor = Accent
                    button.FlatAppearance.MouseOverBackColor = AccentHover
                ElseIf hasDanger Then
                    button.BackColor = DangerSoft
                    button.ForeColor = Danger
                    button.FlatAppearance.BorderColor = Danger
                    button.FlatAppearance.MouseOverBackColor = DangerSoft
                Else
                    button.BackColor = If(hasPaper, SurfacePaper, SurfaceWindow)
                    button.ForeColor = If(hasDanger, Danger, TextPrimary)
                    button.FlatAppearance.BorderColor = BorderSubtle
                    button.FlatAppearance.MouseOverBackColor = AccentSoft
                End If
                Return
            End If

            Dim textBox As TextBox = TryCast(control, TextBox)
            If textBox IsNot Nothing Then
                textBox.BackColor = SurfacePaper
                textBox.ForeColor = TextPrimary
                ' SunnyUI 包装器（UITextBox）自绘边框；给它们的内部 TextBox 强加边框
                ' 会画出多余的括号。
                If Not (TypeOf textBox.Parent Is UITextBox) AndAlso Not (TypeOf textBox.Parent Is UIComboBox) Then
                    textBox.BorderStyle = BorderStyle.FixedSingle
                End If
                Return
            End If

            Dim comboBox As ComboBox = TryCast(control, ComboBox)
            If comboBox IsNot Nothing Then
                comboBox.FlatStyle = FlatStyle.Flat
                comboBox.BackColor = SurfacePaper
                comboBox.ForeColor = TextPrimary
                Return
            End If

            Dim listBox As ListBox = TryCast(control, ListBox)
            If listBox IsNot Nothing Then
                If TypeOf listBox.Parent Is UIListBox Then
                    ' SunnyUI UIListBox 的内层 listbox：SunnyUI 自绘边框且条目在 DrawItem 里重绘；
                    ' 在这里强加边框会画出多余边框，可见的 ForeColor 会导致闪烁。
                    listBox.BackColor = SurfacePaper
                    listBox.ForeColor = SurfacePaper
                    listBox.BorderStyle = BorderStyle.None
                    Return
                End If

                listBox.BackColor = SurfacePaper
                listBox.ForeColor = TextPrimary
                listBox.BorderStyle = BorderStyle.FixedSingle
                Return
            End If

            Dim label As Label = TryCast(control, Label)
            If label IsNot Nothing Then
                If Not bare Then
                    label.BackColor = Color.Transparent
                End If
                label.ForeColor = If(hasDanger, Danger, If(hasSecondary, TextSecondary, TextPrimary))
                Return
            End If

            Dim checkBox As CheckBox = TryCast(control, CheckBox)
            If checkBox IsNot Nothing Then
                If Not bare Then
                    checkBox.BackColor = If(control.Parent IsNot Nothing, control.Parent.BackColor, SurfacePanel)
                End If
                checkBox.ForeColor = TextPrimary
                Return
            End If

            Dim toolStrip As ToolStrip = TryCast(control, ToolStrip)
            If toolStrip IsNot Nothing Then
                toolStrip.BackColor = SurfacePanel
                toolStrip.ForeColor = TextPrimary
                Return
            End If

            Dim statusStrip As StatusStrip = TryCast(control, StatusStrip)
            If statusStrip IsNot Nothing Then
                statusStrip.BackColor = SurfacePanel
                statusStrip.ForeColor = TextSecondary
                Return
            End If

            Dim splitContainer As SplitContainer = TryCast(control, SplitContainer)
            If splitContainer IsNot Nothing Then
                splitContainer.BackColor = BorderSubtle
                Return
            End If

            If Not bare Then
                control.BackColor = If(hasPaper, SurfacePaper, SurfacePanel)
            End If

            If hasSecondary Then
                control.ForeColor = TextSecondary
            End If
        End Sub

        Private Sub StyleButton(button As UIButton, hasAccent As Boolean, hasAction As Boolean, hasDanger As Boolean, hasPaper As Boolean, hasWindow As Boolean, hasTool As Boolean, bare As Boolean)
            ' SunnyUI 会把全局样式级联到每个 Style 仍为 "Inherited" 的控件上；
            ' 把控件标记为 Custom 可以让它退出级联。
            button.Style = UIStyle.Custom
            If hasAction Then
                button.FillColor = Action
                button.ForeColor = Color.White
                button.RectColor = Action
                button.FillHoverColor = ActionHover
                button.RectHoverColor = ActionHover
                button.ForeHoverColor = Color.White
                button.FillPressColor = ActionHover
                button.RectPressColor = ActionHover
                Return
            End If

            If hasAccent Then
                button.FillColor = Accent
                button.ForeColor = Color.White
                button.RectColor = Accent
                button.FillHoverColor = AccentHover
                button.RectHoverColor = AccentHover
                button.ForeHoverColor = Color.White
                button.FillPressColor = AccentHover
                button.RectPressColor = AccentHover
                Return
            End If

            If hasDanger Then
                button.FillColor = DangerSoft
                button.ForeColor = Danger
                button.RectColor = Danger
                button.FillHoverColor = DangerSoft
                button.RectHoverColor = Danger
                button.ForeHoverColor = Danger
                Return
            End If

            Dim fill As Color = If(hasWindow, SurfaceWindow, If(hasPaper, SurfacePaper, SurfacePanel))
            button.FillColor = fill
            button.ForeColor = TextPrimary
            button.RectColor = If(bare, fill, BorderSubtle)

            ' 工具栏（"tool"）按钮保持安静的悬停效果：一排 16 个按钮都用完整的
            ' AccentSoft/Accent 高亮会盖过上方的 ghost 按钮。
            If hasTool Then
                button.FillHoverColor = ListHover
                button.RectHoverColor = ListHover
                button.FillPressColor = ListHover
                button.RectPressColor = ListHover
                button.ForeHoverColor = TextPrimary
                Return
            End If

            button.FillHoverColor = AccentSoft
            button.RectHoverColor = Accent
            button.ForeHoverColor = TextPrimary
        End Sub

        Private Sub StyleSymbolButton(button As UISymbolButton, hasAccent As Boolean, hasAction As Boolean, hasDanger As Boolean, hasPaper As Boolean, hasWindow As Boolean, hasTool As Boolean, bare As Boolean)
            StyleButton(button, hasAccent, hasAction, hasDanger, hasPaper, hasWindow, hasTool, bare)
            If hasAction Then
                button.SymbolColor = Color.White
                button.SymbolHoverColor = Color.White
                button.SymbolPressColor = Color.White
                Return
            End If

            If hasAccent Then
                button.SymbolColor = Color.White
                button.SymbolHoverColor = Color.White
                button.SymbolPressColor = Color.White
                Return
            End If

            If hasDanger Then
                button.SymbolColor = Danger
                button.SymbolHoverColor = Danger
                button.SymbolPressColor = Danger
                Return
            End If

            ' Ghost 图标按钮：安静的符号，悬停时点亮
            button.SymbolColor = TextSecondary
            button.SymbolHoverColor = Accent
            button.SymbolPressColor = AccentHover
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

                Dim attribute As Integer = 20
                Dim value As Integer = If(dark, 1, 0)
                DwmSetWindowAttribute(handle, attribute, value, 4)
            Catch
            End Try
        End Sub

        <System.Runtime.InteropServices.DllImport("dwmapi.dll")>
        Private Shared Function DwmSetWindowAttribute(hwnd As IntPtr, dwAttribute As Integer, ByRef pvAttribute As Integer, cbAttribute As Integer) As Integer
        End Function

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
