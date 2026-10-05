Option Strict On
Option Explicit On
Imports System
Imports System.Drawing
Imports System.Text.RegularExpressions
Imports System.Windows.Forms

Namespace SilentNotes.WindowsVb.Controls
    ''' <summary>
    ''' 基于 MSHTML WebBrowser 控件 contentEditable 模式的富文本编辑器。
    ''' 笔记存储格式就是 HTML，内容进出不需要转换层；格式化命令与存储方言 1:1 对应。
    ''' </summary>
    Public Class HtmlEditorControl
        Inherits UserControl

        Private ReadOnly _browser As WebBrowser
        Private _ready As Boolean
        Private _readOnly As Boolean
        Private _isChecklist As Boolean
        Private _dark As Boolean

        ''' <summary>编辑文档初始化完成后触发一次。</summary>
        Public Event EditorReady As EventHandler

        ''' <summary>编辑内容变化时触发。</summary>
        Public Event ContentChanged As EventHandler

        ''' <summary>光标/选区移动时触发；用 QueryState 刷新 UI 状态。</summary>
        Public Event SelectionChanged As EventHandler

        Public Sub New()
            EnsureBrowserEmulationMode()

            _browser = New WebBrowser()
            _browser.Dock = DockStyle.Fill
            _browser.AllowNavigation = False
            _browser.AllowWebBrowserDrop = False
            _browser.IsWebBrowserContextMenuEnabled = False
            _browser.WebBrowserShortcutsEnabled = True
            _browser.ScriptErrorsSuppressed = True
            AddHandler _browser.DocumentCompleted, AddressOf OnDocumentCompleted
            Controls.Add(_browser)

            _browser.DocumentText = BuildShellHtml()
        End Sub

        Public ReadOnly Property IsReady As Boolean
            Get
                Return _ready
            End Get
        End Property

#Region "Document lifecycle"

        Private Sub OnDocumentCompleted(sender As Object, e As WebBrowserDocumentCompletedEventArgs)
            If _ready Then
                Return
            End If
            If _browser.Document Is Nothing OrElse _browser.ReadyState <> WebBrowserReadyState.Complete Then
                Return
            End If

            Dim document As HtmlDocument = _browser.Document
            document.AttachEventHandler("oninput", AddressOf OnDomEvent)
            document.AttachEventHandler("onkeyup", AddressOf OnKeyUpDomEvent)
            document.AttachEventHandler("onpaste", AddressOf OnDomEvent)
            document.AttachEventHandler("oncut", AddressOf OnDomEvent)
            document.AttachEventHandler("onselectionchange", AddressOf OnSelectionEvent)
            AddHandler document.Click, AddressOf OnDocumentClick

            _ready = True
            SetEditorTheme(_dark, _isChecklist)
            RaiseEvent EditorReady(Me, EventArgs.Empty)
        End Sub

        Private Sub OnDomEvent(sender As Object, e As EventArgs)
            RaiseEvent ContentChanged(Me, EventArgs.Empty)
        End Sub

        ' onkeyup 对所有按键触发，包括不修改内容的修饰键/导航键；只让可能改动
        ' 内容的按键标记脏，否则在编辑器里松开 Ctrl 也会点亮 * 标记。
        Private Sub OnKeyUpDomEvent(sender As Object, e As EventArgs)
            Dim keyCode As Integer = MsHtmlLateBound.GetCurrentEventKeyCode(_browser.Document.DomDocument)
            If keyCode <> 0 Then
                Select Case CType(keyCode, Keys)
                    Case Keys.ControlKey, Keys.ShiftKey, Keys.Menu, Keys.Escape, Keys.Insert,
                         Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Home, Keys.End,
                         Keys.PageUp, Keys.PageDown, Keys.PrintScreen, Keys.Pause,
                         Keys.CapsLock, Keys.NumLock, Keys.Scroll, Keys.LWin, Keys.RWin,
                         Keys.Apps, Keys.Help
                        Return
                End Select
                If keyCode >= CInt(Keys.F1) AndAlso keyCode <= CInt(Keys.F24) Then
                    Return
                End If
            End If
            RaiseEvent ContentChanged(Me, EventArgs.Empty)
        End Sub

        ' 选区变化不能把内容标记为脏；它只携带 UI 状态。
        Private Sub OnSelectionEvent(sender As Object, e As EventArgs)
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End Sub

        Private Sub OnDocumentClick(sender As Object, e As HtmlElementEventArgs)
            ' MSHTML 里文档级点击的 e.ToElement 是 null，所以元素必须从点击坐标解析。
            Dim element As HtmlElement = _browser.Document.GetElementFromPoint(e.ClientMousePosition)

            If _isChecklist Then
                ' ::before 复选框的点击也走这里——它不是 DOM 节点，按段落定位。
                Dim paragraph As HtmlElement = FindAncestorParagraph(element)
                If paragraph IsNot Nothing Then
                    ToggleChecklistItem(paragraph)
                End If
            End If

            ' MSHTML 里 <hr> 靠点击建立不了选区，Delete/退格因此删不掉它；
            ' 点击命中时用 control range 手工选中。
            SelectHorizontalRuleAt(element)
        End Sub

        Private Function FindAncestorParagraph(element As HtmlElement) As HtmlElement
            Dim current As HtmlElement = element
            While current IsNot Nothing
                If String.Equals(current.TagName, "P", StringComparison.OrdinalIgnoreCase) Then
                    Return current
                End If
                If String.Equals(current.TagName, "BODY", StringComparison.OrdinalIgnoreCase) Then
                    Return Nothing
                End If
                current = current.Parent
            End While
            Return Nothing
        End Function

        Private Sub ToggleChecklistItem(paragraph As HtmlElement)
            Dim className As String = paragraph.GetAttribute("className")
            If className Is Nothing Then
                className = String.Empty
            End If
            If className.Contains("done") Then
                paragraph.SetAttribute("className", className.Replace("done", "").Trim())
            Else
                paragraph.SetAttribute("className", (className & " done").Trim())
            End If
            OnDomEvent(Me, EventArgs.Empty)
        End Sub

        Private Sub SelectHorizontalRuleAt(element As HtmlElement)
            If _readOnly Then
                Return
            End If

            Dim current As HtmlElement = element
            While current IsNot Nothing
                If String.Equals(current.TagName, "HR", StringComparison.OrdinalIgnoreCase) Then
                    MsHtmlLateBound.SelectElementAsControlRange(_browser.Document.DomDocument, current.DomElement)
                    Return
                End If
                If String.Equals(current.TagName, "BODY", StringComparison.OrdinalIgnoreCase) Then
                    Return
                End If
                current = current.Parent
            End While
        End Sub

        ''' <summary>用给定的笔记 HTML 替换编辑器内容。</summary>
        Public Sub SetContent(html As String, isChecklist As Boolean)
            If Not _ready Then
                Return
            End If

            _isChecklist = isChecklist
            Dim body As Object = MsHtmlLateBound.GetBody(_browser.Document.DomDocument)
            MsHtmlLateBound.SetInnerHtml(body, If(String.IsNullOrEmpty(html), "<p><br></p>", html))
            SetEditorTheme(_dark, isChecklist)
        End Sub

        ''' <summary>
        ''' 返回编辑内容的 HTML。标签与属性名转为小写
        ''' （存储方言为小写标签/属性名）。
        ''' </summary>
        Public Function GetHtml() As String
            If Not _ready Then
                Return Nothing
            End If

            Dim body As Object = MsHtmlLateBound.GetBody(_browser.Document.DomDocument)
            Dim html As String = MsHtmlLateBound.GetInnerHtml(body)
            If String.IsNullOrEmpty(html) Then
                Return html
            End If

            html = Regex.Replace(html, "<(/?)([A-Za-z][A-Za-z0-9]*)",
                Function(m) "<" & m.Groups(1).Value & m.Groups(2).Value.ToLowerInvariant())
            html = Regex.Replace(html, "\s(HREF|SRC|CLASS|TARGET|ID|STYLE|NAME|TYPE|COLSPAN|ROWSPAN)=(?<q>""[^""]*"")",
                Function(m) " " & m.Groups(1).Value.ToLowerInvariant() & "=" & m.Groups("q").Value)
            Return html
        End Function

        Public Sub SetReadOnly(isReadOnly As Boolean)
            _readOnly = isReadOnly
            If Not _ready Then
                Return
            End If

            Dim body As Object = MsHtmlLateBound.GetBody(_browser.Document.DomDocument)
            MsHtmlLateBound.SetContentEditable(body, Not isReadOnly)
        End Sub

        Public ReadOnly Property IsReadOnly As Boolean
            Get
                Return _readOnly
            End Get
        End Property

        Public Sub FocusEditor()
            If _ready Then
                _browser.Document.Body.Focus()
            End If
        End Sub

        ''' <summary>切换编辑器配色表与清单渲染模式。</summary>
        Public Sub SetEditorTheme(dark As Boolean, isChecklist As Boolean)
            _dark = dark
            _isChecklist = isChecklist
            If Not _ready Then
                Return
            End If

            Dim index As Integer = If(dark, 1, 0) + If(isChecklist, 2, 0)
            Dim css As String = EditorCssCache(index)
            If css Is Nothing Then
                css = BuildEditorCss(dark, isChecklist)
                EditorCssCache(index) = css
            End If

            Try
                Dim document As Object = _browser.Document.DomDocument
                ' 每次更新都重建 style 元素：MSHTML 只认通过 styleSheet.cssText 写入的
                ' 样式表，新元素是替换样式表唯一可靠的方式。
                MsHtmlLateBound.ApplyEditorStyle(document, css)

                Dim bodyElement As Object = MsHtmlLateBound.GetBody(document)
                MsHtmlLateBound.SetBodyClassName(bodyElement, If(isChecklist, "sn-checklist", ""))
            Catch
                ' 样式刷新失败不能破坏主题切换；编辑器保留上一个样式表直到下一次 SetEditorTheme。
            End Try
        End Sub

        ''' <summary>有效组合只有 (dark, isChecklist) 四种，缓存避免每次切笔记重拼 CSS 和 Base64。</summary>
        Private Shared ReadOnly EditorCssCache(3) As String

        Private Shared Function BuildEditorCss(dark As Boolean, isChecklist As Boolean) As String
            ' MSHTML 需要字面 CSS，主题色在此以字面值硬编码；
            ' WinFormsThemeService 的 token 调整时必须同步本表。
            Dim paper As String = If(dark, "#29292D", "#FFFFFF")
            Dim text As String = If(dark, "#E4E4E7", "#1F2328")
            Dim quoteBg As String = If(dark, "#212124", "#F7F7F8")
            Dim quoteBorder As String = If(dark, "#3A3A40", "#D8D8DC")
            Dim codeBg As String = If(dark, "#212124", "#F7F7F8")
            Dim linkColor As String = If(dark, "#2DD4BF", "#0D9488")
            Dim headingColor As String = "#50A0FF"
            Dim headingLine As String = If(dark, "#3A3A40", "#D8D8DC")

            ' MSHTML（IE11 引擎）认识 scrollbar-* 属性；这是重设编辑器滚动条样式的
            ' 唯一方法（只改颜色——经典形状保留）。
            Dim sbFace As String = If(dark, "#3A3A40", "#D8D8DC")
            Dim sbTrack As String = If(dark, "#29292D", "#FFFFFF")
            Dim sbArrow As String = If(dark, "#9A9AA3", "#57575E")

            Dim css As New System.Text.StringBuilder()
            css.Append("html{background:" & paper & ";")
            css.Append("scrollbar-face-color:" & sbFace & ";scrollbar-track-color:" & sbTrack & ";")
            css.Append("scrollbar-arrow-color:" & sbArrow & ";scrollbar-highlight-color:" & sbFace & ";")
            css.Append("scrollbar-3dlight-color:" & sbFace & ";scrollbar-shadow-color:" & sbFace & ";")
            css.Append("scrollbar-darkshadow-color:" & sbFace & ";}")
            css.Append("body{font-family:'Segoe UI','Microsoft YaHei UI',sans-serif;font-size:16px;line-height:22px;")
            css.Append("color:" & text & ";background:" & paper & ";max-width:800px;margin:0 auto;padding:24px 32px;outline:none;}")
            ' 标题与正文之间更大的间距；只有 H1 带灰色虚线规则。MSHTML 里
            ' `border-bottom: dashed` 渲染成密集的 1px 线（看起来是实线），
            ' 所以虚线用重复渐变来画：2px 厚，10px 虚 / 6px 空。IE11 不支持
            ' 双位置色标——每个色标都必须写全。
            Dim dashLine As String = "border-bottom:0;background-image:repeating-linear-gradient(90deg," & headingLine & " 0," & headingLine & " 10px,transparent 10px,transparent 16px);background-size:100% 2px;background-position:0 100%;background-repeat:no-repeat;"
            css.Append("h1{color:" & headingColor & ";font-size:22px;font-weight:600;margin:26px 0 18px 0;padding-bottom:10px;" & dashLine & "}")
            css.Append("h2{color:" & headingColor & ";opacity:.85;font-size:20px;font-weight:600;margin:22px 0 14px 0;}")
            css.Append("h3{color:" & headingColor & ";opacity:.7;font-size:18px;font-weight:600;margin:18px 0 12px 0;}")
            css.Append("h1:first-child{margin-top:0;}")
            css.Append("p{margin:6px 0;}")
            css.Append("blockquote{border-left:3px solid " & quoteBorder & ";background:" & quoteBg & ";margin:8px 0;padding:4px 12px;}")
            css.Append("pre{background:" & codeBg & ";padding:8px;white-space:pre-wrap;font-family:Consolas,monospace;}")
            css.Append("code{font-family:Consolas,monospace;background:" & codeBg & ";}")
            css.Append("hr{border:0;border-top:1px solid " & quoteBorder & ";margin:12px 0;}")
            css.Append("ul,ol{padding-left:24px;margin:6px 0;}")
            css.Append("a{color:" & linkColor & ";}")
            If isChecklist Then
                Dim accent As String = If(dark, "#2DD4BF", "#0D9488")
                Dim itemBg As String = If(dark, "#242428", "#F2F2F2")
                Dim doneText As String = If(dark, "#8B8B92", "#8E8E95")
                ' 对勾是内联 SVG 背景而不是文本字形：MSHTML 的字体回退会把 U+2713
                ' 渲染成错位的缺字形方框。Base64 让 data URI 在 cssText 往返中保持安全。
                Dim checkSvg As String = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'>" &
                    "<path d='M4.5 8.5L7 11L11.5 5.5' fill='none' stroke='" & If(dark, "#12332E", "#FFFFFF") &
                    "' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'/></svg>"
                Dim checkImage As String = "data:image/svg+xml;base64," &
                    Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(checkSvg))
                ' 条目带常驻灰色卡片底色；复选框保持常驻 accent 描边。悬挂缩进
                ' （负 text-indent 把方框拉回外面）让换行与文本对齐而不是与复选框对齐。
                css.Append("body.sn-checklist p{margin:9px 0;padding:2px 6px 2px 30px;text-indent:-24px;")
                css.Append("background:" & itemBg & ";border-radius:6px;cursor:default;}")
                ' 复选框画成圆角方框（border + border-radius），不用字体字形。
                css.Append("body.sn-checklist p:before{content:'';display:inline-block;width:15px;height:15px;")
                css.Append("border:1px solid " & accent & ";border-radius:4px;margin-right:9px;vertical-align:-3px;}")
                css.Append("body.sn-checklist p.done:before{background-color:" & accent & ";border-color:" & accent & ";")
                css.Append("background-image:url(" & checkImage & ");background-repeat:no-repeat;")
                css.Append("background-position:center;background-size:14px 14px;}")
                css.Append("body.sn-checklist p.done{text-decoration:line-through;color:" & doneText & ";}")
            End If

            Return css.ToString()
        End Function

#End Region

#Region "Formatting commands"

        Private Sub Exec(command As String, value As Object)
            If Not _ready OrElse _readOnly Then
                Return
            End If
            _browser.Document.ExecCommand(command, False, value)
        End Sub

        Public Sub ToggleBold()
            Exec("Bold", Nothing)
        End Sub

        Public Sub ToggleItalic()
            Exec("Italic", Nothing)
        End Sub

        Public Sub ToggleUnderline()
            Exec("Underline", Nothing)
        End Sub

        Public Sub ToggleStrikethrough()
            Exec("StrikeThrough", Nothing)
        End Sub

        Public Sub ToggleUnorderedList()
            Exec("InsertUnorderedList", Nothing)
        End Sub

        Public Sub ToggleOrderedList()
            Exec("InsertOrderedList", Nothing)
        End Sub

        Public Sub Undo()
            Exec("Undo", Nothing)
        End Sub

        Public Sub Redo()
            Exec("Redo", Nothing)
        End Sub

        Public Sub InsertHorizontalRule()
            Exec("InsertHorizontalRule", Nothing)
        End Sub

        Public Sub RemoveLink()
            Exec("UnLink", Nothing)
        End Sub

        Public Sub CreateLink(url As String)
            If String.IsNullOrEmpty(url) Then
                Return
            End If
            Exec("CreateLink", url)
        End Sub

        ''' <summary>把当前块设为/切换为标题。level：1-3，0 = 段落。</summary>
        Public Sub SetHeading(level As Integer, toggleOffIfActive As Boolean)
            Dim tag As String = "H" & level.ToString()
            If toggleOffIfActive AndAlso String.Equals(GetCurrentBlockTag(), tag, StringComparison.OrdinalIgnoreCase) Then
                tag = "P"
            End If
            Exec("FormatBlock", "<" & tag & ">")
        End Sub

        Public Sub SetBlockquote(toggle As Boolean)
            Dim tag As String = "BLOCKQUOTE"
            If toggle AndAlso String.Equals(GetCurrentBlockTag(), tag, StringComparison.OrdinalIgnoreCase) Then
                tag = "P"
            End If
            Exec("FormatBlock", "<" & tag & ">")
        End Sub

        Public Sub SetCodeBlock(toggle As Boolean)
            Dim tag As String = "PRE"
            If toggle AndAlso String.Equals(GetCurrentBlockTag(), tag, StringComparison.OrdinalIgnoreCase) Then
                tag = "P"
            End If
            Exec("FormatBlock", "<" & tag & ">")
        End Sub

        Public Function QueryState(command As String) As Boolean
            If Not _ready Then
                Return False
            End If
            Try
                Dim document As Object = _browser.Document.DomDocument
                Return MsHtmlLateBound.QueryCommandState(document, command)
            Catch
                Return False
            End Try
        End Function

        Public Function GetCurrentBlockTag() As String
            If Not _ready Then
                Return Nothing
            End If
            Try
                Dim document As Object = _browser.Document.DomDocument
                Dim parent As Object = MsHtmlLateBound.GetSelectionParentElement(document)
                While parent IsNot Nothing AndAlso Not TypeOf parent Is DBNull
                    Dim tagName As String = MsHtmlLateBound.GetTagName(parent)
                    If tagName Is Nothing Then
                        Return Nothing
                    End If
                    tagName = tagName.ToUpperInvariant()
                    If tagName = "P" OrElse tagName = "H1" OrElse tagName = "H2" OrElse tagName = "H3" OrElse
                        tagName = "BLOCKQUOTE" OrElse tagName = "PRE" OrElse tagName = "LI" OrElse tagName = "DIV" Then
                        Return tagName
                    End If
                    parent = MsHtmlLateBound.GetParentElement(parent)
                End While
            Catch
            End Try
            Return Nothing
        End Function

#End Region

#Region "Shell document and browser mode"

        ''' <summary>
        ''' 判断一个原生窗口句柄是否在编辑器 WebBrowser 的窗口树内。MSHTML 的
        ''' 内部原生窗口（Internet Explorer_Server 等）是 WebBrowser 控件的
        ''' 子孙窗口，键盘消息不会经过 WinForms 的焦点体系，快捷键过滤器靠
        ''' 本方法判定按键来源。
        ''' </summary>
        Public Function ContainsNativeWindow(hwnd As IntPtr) As Boolean
            If hwnd = IntPtr.Zero OrElse Not _browser.IsHandleCreated Then
                Return False
            End If

            Dim browserHandle As IntPtr = _browser.Handle
            Dim current As IntPtr = hwnd
            While current <> IntPtr.Zero
                If current = browserHandle Then
                    Return True
                End If
                current = GetParent(current)
            End While
            Return False
        End Function

        <System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint:="GetParent")>
        Private Shared Function GetParent(hWnd As IntPtr) As IntPtr
        End Function

        Private Shared Function BuildShellHtml() As String
            Return "<!DOCTYPE html><html><head><meta http-equiv=""X-UA-Compatible"" content=""IE=11"">" &
                "</head><body contenteditable=""true""><p><br></p></body></html>"
        End Function

        ''' <summary>
        ''' 强制内嵌 MSHTML 引擎进入 IE11 模式，否则控件以 IE7 仿真运行，
        ''' contentEditable/formatBlock 行为会不一样。
        ''' </summary>
        Private Shared Sub EnsureBrowserEmulationMode()
            Try
                Const keyPath As String = "Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"
                Using key As Microsoft.Win32.RegistryKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(keyPath)
                    Dim exeName As String = AppDomain.CurrentDomain.FriendlyName
                    key.SetValue(exeName, 11001, Microsoft.Win32.RegistryValueKind.DWord)
                End Using
            Catch
            End Try
        End Sub

#End Region
    End Class
End Namespace
