Option Strict On
Option Explicit On
Imports System
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Namespace SilentNotes.WindowsVb.Controls
    ''' <summary>
    ''' 分段切换器（活动笔记/回收站）：凹陷的圆角轨道，选中段画成内嵌药丸。
    ''' 自绘而非两个按钮，让选中状态呈现为一个控件；双缓冲、无动画。
    ''' 颜色通过颜色属性来自 WinFormsThemeService。
    ''' </summary>
    Public Class SegmentedToggle
        Inherits Control

        Private ReadOnly _labels As String()
        Private _selectedIndex As Integer
        Private _hoverIndex As Integer = -1

        Public Sub New(labels As String())
            _labels = CType(labels.Clone(), String())
            ' 只做不透明绘制：透明 BackColor 会让 WinForms 在每次重绘时模拟父级背景，
            ' 破坏双缓冲并导致闪烁（与笔记列表同样的教训）。主题服务改为把 BackColor 设成侧栏色。
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.UserPaint Or ControlStyles.ResizeRedraw, True)
            Cursor = Cursors.Hand
        End Sub

        Public Property SelectedIndex As Integer
            Get
                Return _selectedIndex
            End Get
            Set(value As Integer)
                If value < 0 OrElse value >= _labels.Length OrElse value = _selectedIndex Then
                    Return
                End If
                _selectedIndex = value
                Invalidate()
            End Set
        End Property

        ''' <summary>用户选择分段时触发；给 SelectedIndex 赋值不会触发。</summary>
        Public Event SelectedIndexChanged As EventHandler

        Public Property TrackFill As Color
        Public Property TrackBorder As Color
        Public Property SegmentFill As Color
        Public Property SegmentText As Color
        Public Property TrackText As Color
        Public Property TrackTextHover As Color

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim index As Integer = HitTest(e.Location)
            If index <> _hoverIndex Then
                _hoverIndex = index
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            If _hoverIndex <> -1 Then
                _hoverIndex = -1
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnClick(e As EventArgs)
            MyBase.OnClick(e)
            Dim mouseArgs As MouseEventArgs = TryCast(e, MouseEventArgs)
            Dim location As Point = If(mouseArgs IsNot Nothing, mouseArgs.Location, PointToClient(Cursor.Position))
            Dim index As Integer = HitTest(location)
            Pick(index)
        End Sub

        Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
            If keyData = Keys.Left OrElse keyData = Keys.Right Then
                Return True
            End If
            Return MyBase.IsInputKey(keyData)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If e.KeyCode = Keys.Left Then
                Pick(0)
            ElseIf e.KeyCode = Keys.Right Then
                Pick(_labels.Length - 1)
            End If
        End Sub

        Private Sub Pick(index As Integer)
            If index < 0 OrElse index = _selectedIndex Then
                Return
            End If
            _selectedIndex = index
            Invalidate()
            RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
        End Sub

        Private Function HitTest(p As Point) As Integer
            If ClientSize.Width <= 0 Then
                Return -1
            End If
            Dim index As Integer = p.X * _labels.Length \ ClientSize.Width
            If index < 0 OrElse index >= _labels.Length Then
                Return -1
            End If
            Return index
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            If ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then
                Return
            End If

            Dim g As Graphics = e.Graphics
            g.SmoothingMode = SmoothingMode.AntiAlias
            Dim track As New Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1)
            Using path As GraphicsPath = RoundedRect(track, 8)
                Using brush As New SolidBrush(TrackFill)
                    g.FillPath(brush, path)
                End Using
                Using pen As New Pen(TrackBorder)
                    g.DrawPath(pen, path)
                End Using
            End Using

            For i As Integer = 0 To _labels.Length - 1
                Dim x1 As Integer = i * ClientSize.Width \ _labels.Length
                Dim x2 As Integer = (i + 1) * ClientSize.Width \ _labels.Length
                Dim cell As New Rectangle(x1, 0, x2 - x1, ClientSize.Height)
                Dim selected As Boolean = (i = _selectedIndex)
                If selected Then
                    Dim pill As New Rectangle(cell.X + 2, 2, cell.Width - 4, cell.Height - 4)
                    Using pillPath As GraphicsPath = RoundedRect(pill, 6)
                        Using brush As New SolidBrush(SegmentFill)
                            g.FillPath(brush, pillPath)
                        End Using
                    End Using
                End If
                Dim textColor As Color = If(selected, SegmentText, If(i = _hoverIndex, TrackTextHover, TrackText))
                TextRenderer.DrawText(g, _labels(i), Font, cell, textColor,
                    TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
            Next
        End Sub

        Private Shared Function RoundedRect(bounds As Rectangle, radius As Integer) As GraphicsPath
            Dim d As Integer = radius * 2
            Dim path As New GraphicsPath()
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90)
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90)
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90)
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90)
            path.CloseFigure()
            Return path
        End Function
    End Class
End Namespace
