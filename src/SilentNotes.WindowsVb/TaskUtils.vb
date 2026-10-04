Option Strict On
Option Explicit On
Imports System
Imports System.Threading.Tasks

Namespace SilentNotes.WindowsVb.Services
    ''' <summary>
    ''' net40 没有 Task.FromResult，用 TaskCompletionSource 等价实现
    ''' （Compat 先例见 SilentNotes.WindowsWinForms\SilentNotes.Core\Compat\DummyFeedbackService.net40.cs）。
    ''' 另提供 WaitAndUnwrap：底层 WebDAV 客户端保留 Task 签名（与 C# 版共用同一 DLL），
    ''' VB10 无 await，在后台线程上 Wait() 等待；StartNew 包装的异常会包成 AggregateException，
    ''' 这里解包还原成原始异常类型，让调用方的 catch（AccessDeniedException 等）语义与 C# 版一致。
    ''' </summary>
    Friend Module TaskUtils
        Friend Function TaskFromResult(Of T)(value As T) As Task(Of T)
            Dim tcs As New TaskCompletionSource(Of T)()
            tcs.SetResult(value)
            Return tcs.Task
        End Function

        ''' <summary>Task.FromResult(Of T)(Nothing) 的专用重载，绕开 Nothing 无法推断 T 的问题。</summary>
        Friend Function TaskFromResultNull(Of T As Class)() As Task(Of T)
            Dim tcs As New TaskCompletionSource(Of T)()
            tcs.SetResult(Nothing)
            Return tcs.Task
        End Function

        ''' <summary>后台线程上等待 Task 并解包 AggregateException（单内层异常时还原原始异常）。</summary>
        Friend Sub WaitAndUnwrap(task As Task)
            Try
                task.Wait()
            Catch ex As AggregateException
                If ex.InnerExceptions.Count = 1 Then
                    Throw ex.InnerException
                End If
                Throw
            End Try
        End Sub

        ''' <summary>后台线程上等待 Task(Of T) 并解包 AggregateException，返回结果。</summary>
        Friend Function WaitAndUnwrap(Of T)(task As Task(Of T)) As T
            Try
                task.Wait()
                Return task.Result
            Catch ex As AggregateException
                If ex.InnerExceptions.Count = 1 Then
                    Throw ex.InnerException
                End If
                Throw
            End Try
        End Function
    End Module
End Namespace
