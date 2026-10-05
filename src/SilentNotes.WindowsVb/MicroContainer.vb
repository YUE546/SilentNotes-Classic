Option Strict On
Option Explicit On
Imports System
Imports System.Collections.Generic
Imports System.Reflection

Namespace SilentNotes.WindowsVb
    ''' <summary>
    ''' 极简服务容器，替代 net40 上不可用的 Microsoft.Extensions.DependencyInjection。
    ''' 只支持单例注册（类型或工厂）与构造函数注入，够 Program.vb 的 17 个注册使用。
    ''' </summary>
    Public Class ServiceCollection
        Private ReadOnly _descriptors As New List(Of ServiceDescriptor)()

        ''' <summary>把 TService 注册为单例，实现类就是 TService 本身。</summary>
        Public Function AddSingleton(Of TService As Class)() As ServiceCollection
            _descriptors.Add(ServiceDescriptor.ForType(GetType(TService), GetType(TService)))
            Return Me
        End Function

        ''' <summary>把 TService 注册为单例，实现类为 TImplementation。</summary>
        Public Function AddSingleton(Of TService As Class, TImplementation As Class)() As ServiceCollection
            _descriptors.Add(ServiceDescriptor.ForType(GetType(TService), GetType(TImplementation)))
            Return Me
        End Function

        ''' <summary>用工厂创建单例。</summary>
        Public Function AddSingleton(Of TService As Class)(factory As Func(Of IServiceProvider, TService)) As ServiceCollection
            _descriptors.Add(ServiceDescriptor.ForFactory(GetType(TService), factory))
            Return Me
        End Function

        Public Function BuildServiceProvider() As ServiceProvider
            Return New ServiceProvider(_descriptors)
        End Function
    End Class

    Friend Class ServiceDescriptor
        Public ServiceType As Type
        Public ImplementationType As Type
        Public Factory As Func(Of IServiceProvider, Object)

        Public Shared Function ForType(serviceType As Type, implementationType As Type) As ServiceDescriptor
            Dim d As New ServiceDescriptor()
            d.ServiceType = serviceType
            d.ImplementationType = implementationType
            Return d
        End Function

        Public Shared Function ForFactory(serviceType As Type, factory As Func(Of IServiceProvider, Object)) As ServiceDescriptor
            Dim d As New ServiceDescriptor()
            d.ServiceType = serviceType
            d.Factory = factory
            Return d
        End Function
    End Class

    Public Class ServiceProvider
        Implements IServiceProvider

        Private ReadOnly _descriptors As New Dictionary(Of Type, ServiceDescriptor)()
        Private ReadOnly _instances As New Dictionary(Of Type, Object)()
        Private ReadOnly _buildLock As New Object()

        Friend Sub New(descriptors As IEnumerable(Of ServiceDescriptor))
            For Each d As ServiceDescriptor In descriptors
                _descriptors(d.ServiceType) = d
            Next
        End Sub

        Public Function GetService(serviceType As Type) As Object Implements IServiceProvider.GetService
            SyncLock _buildLock
                Return Resolve(serviceType)
            End SyncLock
        End Function

        ''' <summary>未注册时抛 InvalidOperationException。</summary>
        Public Function GetRequiredService(Of T As {Class})() As T
            Return CType(GetService(GetType(T)), T)
        End Function

        Private Function Resolve(serviceType As Type) As Object
            Dim cached As Object = Nothing
            If _instances.TryGetValue(serviceType, cached) Then
                Return cached
            End If

            Dim d As ServiceDescriptor = Nothing
            If Not _descriptors.TryGetValue(serviceType, d) Then
                Throw New InvalidOperationException("服务未注册: " & serviceType.FullName)
            End If

            Dim instance As Object
            If d.Factory IsNot Nothing Then
                instance = d.Factory(Me)
            Else
                instance = CreateInstance(d.ImplementationType)
            End If
            _instances(serviceType) = instance
            Return instance
        End Function

        ''' <summary>按"参数最多的公共构造函数"做反射构造注入。</summary>
        Private Function CreateInstance(implementationType As Type) As Object
            Dim constructors As ConstructorInfo() = implementationType.GetConstructors()
            If constructors.Length = 0 Then
                Throw New InvalidOperationException("实现类没有公共构造函数: " & implementationType.FullName)
            End If

            Dim chosen As ConstructorInfo = constructors(0)
            For Each c As ConstructorInfo In constructors
                If c.GetParameters().Length > chosen.GetParameters().Length Then
                    chosen = c
                End If
            Next

            Dim parameters As ParameterInfo() = chosen.GetParameters()
            Dim args(parameters.Length - 1) As Object
            For i As Integer = 0 To parameters.Length - 1
                args(i) = Resolve(parameters(i).ParameterType)
            Next
            ' 注意：不能用 chosen.Invoke(Nothing, args)（MethodBase.Invoke 两参重载）——
            ' 本机 .NET Framework 运行时对该形式调构造函数会抛 TargetException
            ' （"非静态方法需要一个目标"，StringBuilder 等系统类型同样复现）；
            ' ConstructorInfo.Invoke(Object[]) 单参重载与 Activator.CreateInstance 均正常。
            Return chosen.Invoke(args)
        End Function
    End Class
End Namespace
