Option Strict On
Option Explicit On
Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports SilentNotes.WindowsVb.Services
Imports Sunny.UI

Namespace SilentNotes.WindowsVb.Controls
    ''' <summary>
    ''' 确认型 MessageBox 的主题化替代：标题、说明文字和一行右对齐按钮。
    ''' 当操作不可逆（永久删除、清空回收站、恢复备份）时，确认按钮变为 danger 色。
    ''' 使用 SunnyUI 按钮/标签，让对话框与主窗口样式一致。
    ''' </summary>
    Friend Class ThemedConfirmDialog
        Inherits ThemedDialogForm

        ' SunnyUI 控件不继承容器字体（ThemedDialogForm 设的 Font 对 UILabel/UIButton
        ' 无效），不显式赋值会回退系统默认宋体。
        Private Shared ReadOnly UIFont As New Font("Microsoft YaHei UI", 9.0F)

        Private Sub New()
        End Sub

        ''' <summary>弹出模态确认框，确认时返回 True。</summary>
        Public Shared Overloads Function Show(owner As Form, themeService As WinFormsThemeService, title As String, message As String, confirmText As String, danger As Boolean) As Boolean
            Using dialog As New ThemedConfirmDialog()
                dialog.Text = title
                dialog.Width = 400
                dialog.Height = 180

                Dim messageLabel As New UILabel With {
                    .Text = message,
                    .AutoSize = False,
                    .Left = 18,
                    .Top = 18,
                    .Width = 350,
                    .Height = 70,
                    .TextAlign = ContentAlignment.TopLeft,
                    .Font = UIFont
                }

                Dim confirmButton As New UIButton With {
                    .Text = confirmText,
                    .Width = 88,
                    .Height = 30,
                    .Tag = If(danger, "danger", "accent"),
                    .DialogResult = DialogResult.OK,
                    .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right,
                    .Font = UIFont
                }
                Dim cancelButton As New UIButton With {
                    .Text = "取消",
                    .Width = 72,
                    .Height = 30,
                    .DialogResult = DialogResult.Cancel,
                    .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right,
                    .Font = UIFont
                }
                cancelButton.Left = dialog.ClientSize.Width - 18 - confirmButton.Width - 8 - cancelButton.Width
                confirmButton.Left = dialog.ClientSize.Width - 18 - confirmButton.Width
                cancelButton.Top = dialog.ClientSize.Height - 44
                confirmButton.Top = cancelButton.Top

                dialog.Controls.Add(messageLabel)
                dialog.Controls.Add(confirmButton)
                dialog.Controls.Add(cancelButton)
                dialog.AcceptButton = confirmButton
                dialog.CancelButton = cancelButton
                dialog.ApplyTheme(themeService)

                Return (dialog.ShowDialog(owner) = DialogResult.OK)
            End Using
        End Function

        ''' <summary>
        ''' 三选项关闭询问：Yes = 保存，No = 放弃，Cancel = 继续编辑。
        ''' </summary>
        Public Shared Overloads Function ShowSavePrompt(owner As Form, themeService As WinFormsThemeService, title As String, message As String) As DialogResult
            Using dialog As New ThemedConfirmDialog()
                dialog.Text = title
                dialog.Width = 420
                dialog.Height = 190

                Dim messageLabel As New UILabel With {
                    .Text = message,
                    .AutoSize = False,
                    .Left = 18,
                    .Top = 18,
                    .Width = 370,
                    .Height = 70,
                    .TextAlign = ContentAlignment.TopLeft,
                    .Font = UIFont
                }

                Dim saveButton As New UIButton With {
                    .Text = "保存",
                    .Width = 80,
                    .Height = 30,
                    .Tag = "accent",
                    .DialogResult = DialogResult.Yes,
                    .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right,
                    .Font = UIFont
                }
                Dim discardButton As New UIButton With {
                    .Text = "不保存",
                    .Width = 80,
                    .Height = 30,
                    .DialogResult = DialogResult.No,
                    .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right,
                    .Font = UIFont
                }
                Dim cancelButton As New UIButton With {
                    .Text = "取消",
                    .Width = 72,
                    .Height = 30,
                    .DialogResult = DialogResult.Cancel,
                    .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right,
                    .Font = UIFont
                }
                cancelButton.Left = dialog.ClientSize.Width - 18 - cancelButton.Width
                discardButton.Left = cancelButton.Left - 8 - discardButton.Width
                saveButton.Left = discardButton.Left - 8 - saveButton.Width
                cancelButton.Top = dialog.ClientSize.Height - 44
                discardButton.Top = cancelButton.Top
                saveButton.Top = cancelButton.Top

                dialog.Controls.Add(messageLabel)
                dialog.Controls.Add(saveButton)
                dialog.Controls.Add(discardButton)
                dialog.Controls.Add(cancelButton)
                dialog.AcceptButton = saveButton
                dialog.CancelButton = cancelButton
                dialog.ApplyTheme(themeService)

                Return dialog.ShowDialog(owner)
            End Using
        End Function
    End Class
End Namespace
