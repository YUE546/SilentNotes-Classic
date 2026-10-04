Option Strict On
Option Explicit On
Imports SilentNotes.Services

Namespace SilentNotes.WindowsVb.Services
    Friend Class WindowsEnvironmentService
        Implements IEnvironmentService

        Public ReadOnly Property Os As OperatingSystem Implements IEnvironmentService.Os
            Get
                Return OperatingSystem.Windows
            End Get
        End Property

        Public ReadOnly Property InDarkMode As Boolean Implements IEnvironmentService.InDarkMode
            Get
                Return False
            End Get
        End Property

        Public ReadOnly Property KeepScreenOn As IKeepScreenOn Implements IEnvironmentService.KeepScreenOn
            Get
                Return Nothing
            End Get
        End Property

        Public ReadOnly Property Screenshots As IScreenshots Implements IEnvironmentService.Screenshots
            Get
                Return Nothing
            End Get
        End Property
    End Class
End Namespace
