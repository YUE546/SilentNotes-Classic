Option Strict On
Option Explicit On
Imports System

Namespace SilentNotes.WindowsVb.Workers
    ''' <summary>检查 HTML 内容能否不经转换直接在原生 MSHTML 编辑器中编辑。</summary>
    Friend NotInheritable Class HtmlCompatibilityInspector

        Public Function CanEditWithoutConversion(html As String) As Boolean
            If String.IsNullOrWhiteSpace(html) Then
                Return True
            End If

            Return html.IndexOf("<script", StringComparison.OrdinalIgnoreCase) < 0 _
                AndAlso html.IndexOf("<iframe", StringComparison.OrdinalIgnoreCase) < 0 _
                AndAlso html.IndexOf("<object", StringComparison.OrdinalIgnoreCase) < 0
        End Function
    End Class
End Namespace
