Option Strict On
Option Explicit On
Imports System
Imports System.Threading.Tasks
Imports Forms = System.Windows.Forms
Imports SilentNotes.Services
Imports SilentNotes.WindowsVb.Controls
Imports Sunny.UI

Namespace SilentNotes.WindowsVb.Services
    ''' <summary>
    ''' WinForms 客户端的 Core 反馈接口实现。Toast 走 SunnyUI 的瞬态 UIMessageTip；
    ''' 模态消息用 ThemedDialogForm。
    ''' </summary>
    Friend Class WinFormsFeedbackService
        Implements IFeedbackService

        Private ReadOnly _theme As WinFormsThemeService

        Public Sub New(theme As WinFormsThemeService)
            _theme = theme
        End Sub

        Public Sub ShowToast(message As String, Optional severity As FeedbackSeverity = FeedbackSeverity.Unknown) _
            Implements IFeedbackService.ShowToast

            Select Case severity
                Case FeedbackSeverity.Warning
                    UIMessageTip.ShowWarning(message, 2000)
                Case FeedbackSeverity.Error
                    UIMessageTip.ShowError(message, 2000)
                Case Else
                    UIMessageTip.Show(message)
            End Select
        End Sub

        Public Function ShowMessageAsync(
            message As String,
            title As String,
            buttons As MessageBoxButtons,
            conservativeDefault As Boolean) As Task(Of MessageBoxResult) _
            Implements IFeedbackService.ShowMessageAsync

            Dim result As MessageBoxResult
            Using dialog As New ThemedDialogForm()
                dialog.Text = If(String.IsNullOrEmpty(title), "SilentNotes", title)
                dialog.Width = 420
                dialog.Height = 190

                Dim messageLabel As New UILabel With {
                    .Text = message,
                    .AutoSize = False,
                    .Left = 18,
                    .Top = 18,
                    .Width = 370,
                    .Height = 90,
                    .TextAlign = System.Drawing.ContentAlignment.TopLeft
                }
                dialog.Controls.Add(messageLabel)

                Dim acceptButton As UIButton = Nothing
                Dim defaultButton As UIButton
                Select Case buttons
                    Case MessageBoxButtons.YesNoCancel
                        acceptButton = CreateButton("是", Forms.DialogResult.Yes, "accent")
                        Dim noButton As UIButton = CreateButton("否", Forms.DialogResult.No, "window")
                        Dim cancelButton As UIButton = CreateButton("取消", Forms.DialogResult.Cancel, "window")
                        LayoutButtonsRight(dialog, cancelButton, noButton, acceptButton)
                        dialog.AcceptButton = acceptButton
                        dialog.CancelButton = cancelButton
                        defaultButton = If(conservativeDefault, cancelButton, acceptButton)
                    Case MessageBoxButtons.ContinueCancel
                        acceptButton = CreateButton("继续", Forms.DialogResult.OK, "accent")
                        Dim cancel2Button As UIButton = CreateButton("取消", Forms.DialogResult.Cancel, "window")
                        LayoutButtonsRight(dialog, cancel2Button, acceptButton)
                        dialog.AcceptButton = acceptButton
                        dialog.CancelButton = cancel2Button
                        defaultButton = If(conservativeDefault, cancel2Button, acceptButton)
                    Case Else
                        acceptButton = CreateButton("确定", Forms.DialogResult.OK, "accent")
                        LayoutButtonsRight(dialog, acceptButton)
                        dialog.AcceptButton = acceptButton
                        dialog.CancelButton = acceptButton
                        defaultButton = acceptButton
                End Select

                dialog.ApplyTheme(_theme)
                ' Focus 在句柄创建前调用是 no-op，初始焦点会落到 Tab 序第一个按钮；
                ' Shown 之后再聚焦才能让保守默认（取消）真正生效。
                AddHandler dialog.Shown, Sub() defaultButton.Focus()
                ShowDialogOnOwner(dialog)
                result = ToResult(dialog.DialogResult, buttons)
            End Using

            Return TaskUtils.TaskFromResult(Of MessageBoxResult)(result)
        End Function

        Private Shared Function CreateButton(text As String, dialogResult As Forms.DialogResult, tag As String) As UIButton
            Return New UIButton With {
                .Text = text,
                .Width = 80,
                .Height = 30,
                .Tag = tag,
                .DialogResult = dialogResult,
                .Anchor = Forms.AnchorStyles.Bottom Or Forms.AnchorStyles.Right
            }
        End Function

        ' 按钮右对齐排布：每个按钮放到上一行末尾的左边，所以第一个参数最终在最右。
        Private Shared Sub LayoutButtonsRight(dialog As ThemedDialogForm, ParamArray buttonsLeftToRight As UIButton())
            Dim right As Integer = dialog.ClientSize.Width - 18
            For index As Integer = buttonsLeftToRight.Length - 1 To 0 Step -1
                Dim button As UIButton = buttonsLeftToRight(index)
                button.Left = right - button.Width
                button.Top = dialog.ClientSize.Height - 44
                right = button.Left - 8
                dialog.Controls.Add(button)
            Next
        End Sub

        Private Shared Sub ShowDialogOnOwner(dialog As Forms.Form)
            Dim owner As Forms.Form = Forms.Form.ActiveForm
            If owner IsNot Nothing AndAlso Not owner.IsDisposed Then
                dialog.ShowDialog(owner)
            Else
                dialog.ShowDialog()
            End If
        End Sub

        Private Shared Function ToResult(
            result As Forms.DialogResult,
            buttons As MessageBoxButtons) As MessageBoxResult

            Select Case result
                Case Forms.DialogResult.OK
                    If buttons = MessageBoxButtons.ContinueCancel Then
                        Return MessageBoxResult.Continue
                    End If
                    Return MessageBoxResult.Ok
                Case Forms.DialogResult.Yes
                    Return MessageBoxResult.Yes
                Case Forms.DialogResult.No
                    Return MessageBoxResult.No
                Case Else
                    Return MessageBoxResult.Cancel
            End Select
        End Function
    End Class
End Namespace
