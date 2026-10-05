Option Strict On
Option Explicit On
Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports SilentNotes.WindowsVb.Services

Namespace SilentNotes.WindowsVb.Controls
    ''' <summary>
    ''' 主题化二级窗口基类：原生标题栏对话框，标题栏按钮与窗口动画来自系统
    ''' （DWM immersive dark mode 跟随主题），内容控件由 WinFormsThemeService 着色。
    ''' 内容加完之后必须调用 ApplyTheme。
    ''' </summary>
    Public Class ThemedDialogForm
        Inherits Form

        ''' <summary>最近应用的一套调色板；让子类给动态元素着色。</summary>
        Friend Property Theme As WinFormsThemeService

        Public Sub New()
            FormBorderStyle = FormBorderStyle.FixedDialog
            MaximizeBox = False
            MinimizeBox = False
            ShowInTaskbar = False
            ShowIcon = False
            StartPosition = FormStartPosition.CenterParent
            Font = New Font("Microsoft YaHei UI", 9.0F)
        End Sub

        ''' <summary>把当前调色板应用到对话框及其控件。</summary>
        Friend Sub ApplyTheme(themeService As WinFormsThemeService)
            If themeService Is Nothing Then
                Return
            End If

            Theme = themeService
            BackColor = themeService.SurfaceWindow
            ForeColor = themeService.TextPrimary
            themeService.Apply(Me)
            themeService.ApplyWindowTheme(Me)
        End Sub
    End Class
End Namespace
