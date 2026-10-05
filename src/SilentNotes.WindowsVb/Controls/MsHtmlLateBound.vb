Option Strict Off
Option Explicit On
Imports System

Namespace SilentNotes.WindowsVb.Controls
    ''' <summary>
    ''' MSHTML DOM 后期绑定工具：全项目唯一 Option Strict Off 的文件。
    ''' VB 后期绑定访问 DomDocument；MSHTML 的 null 经 COM 封送是 DBNull 而非 Nothing，
    ''' 所有取值必须同时排查两者。这里集中封装全部后期绑定调用，其余代码保持 Option Strict On。
    ''' </summary>
    Friend Module MsHtmlLateBound

        ''' <summary>取 body 元素（后期绑定）。</summary>
        Friend Function GetBody(domDocument As Object) As Object
            Return domDocument.body
        End Function

        Friend Sub SetInnerHtml(body As Object, html As String)
            body.innerHTML = html
        End Sub

        ''' <summary>读 body.innerHTML；经 COM 封送可能得到 DBNull，统一转成 Nothing。</summary>
        Friend Function GetInnerHtml(body As Object) As String
            Dim value As Object = body.innerHTML
            If value Is Nothing OrElse TypeOf value Is DBNull Then
                Return Nothing
            End If
            Return CStr(value)
        End Function

        Friend Sub SetContentEditable(body As Object, editable As Boolean)
            body.contentEditable = If(editable, "true", "false")
        End Sub

        Friend Sub SetBodyClassName(body As Object, className As String)
            body.className = className
        End Sub

        ''' <summary>重建 sn-editor-style 样式表：移除旧元素、新建 &lt;style&gt;、挂到 head、写入 cssText。</summary>
        Friend Sub ApplyEditorStyle(domDocument As Object, css As String)
            Dim head As Object = domDocument.head
            If head Is Nothing OrElse TypeOf head Is DBNull Then
                head = domDocument.getElementsByTagName("head").item(0)
            End If

            Dim old As Object = domDocument.getElementById("sn-editor-style")
            ' MSHTML 的 null 经 COM 封送回来是 DBNull，不是 Nothing
            If old IsNot Nothing AndAlso Not TypeOf old Is DBNull Then
                old.removeNode(True)
            End If

            Dim styleElement As Object = domDocument.createElement("style")
            styleElement.id = "sn-editor-style"
            head.appendChild(styleElement)
            styleElement.styleSheet.cssText = css
        End Sub

        Friend Function QueryCommandState(domDocument As Object, command As String) As Boolean
            Return CBool(domDocument.queryCommandState(command))
        End Function

        ''' <summary>取当前选区的父元素；选区为空/经封送为 DBNull 时返回 Nothing。</summary>
        Friend Function GetSelectionParentElement(domDocument As Object) As Object
            Dim range As Object = domDocument.selection.createRange()
            If range Is Nothing OrElse TypeOf range Is DBNull Then
                Return Nothing
            End If
            Return range.parentElement()
        End Function

        ''' <summary>取当前 DOM 事件（window.event）的 keyCode；事件不可用或非键盘事件时返回 0。</summary>
        Friend Function GetCurrentEventKeyCode(domDocument As Object) As Integer
            Dim ev As Object = domDocument.parentWindow.event
            If ev Is Nothing OrElse TypeOf ev Is DBNull Then
                Return 0
            End If
            Dim code As Object = ev.keyCode
            If code Is Nothing OrElse TypeOf code Is DBNull Then
                Return 0
            End If
            Return CInt(code)
        End Function

        Friend Function GetTagName(element As Object) As String
            Dim value As Object = element.tagName
            If value Is Nothing OrElse TypeOf value Is DBNull Then
                Return Nothing
            End If
            Return CStr(value)
        End Function

        Friend Function GetParentElement(element As Object) As Object
            Return element.parentElement
        End Function

        ''' <summary>
        ''' 把 hr 等控件元素放进 control range 并选中：MSHTML 不给这类元素常规
        ''' 文本选区，control range 选中后 Delete/退格才能删掉它。
        ''' </summary>
        Friend Sub SelectElementAsControlRange(domDocument As Object, element As Object)
            If element Is Nothing OrElse TypeOf element Is DBNull Then
                Return
            End If
            Dim controlRange As Object = domDocument.body.createControlRange()
            controlRange.add(element)
            controlRange.select()
        End Sub
    End Module
End Namespace
