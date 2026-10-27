Imports System.Runtime.InteropServices
Imports Inventor

Public Module Globals

    Public _inventorApplication As Inventor.Application

    ''' <summary>Unique ID for this add-in.</summary>
    Public Const _simpleAddInClientId As String = "32CB0A74-E71D-4F44-B07C-816B2F33030E"
    Public Const _addInClientId As String = "{" & _simpleAddInClientId & "}"

End Module

Namespace Inventor_Model_Shoot

    <ProgIdAttribute("Inventor_Model_Shoot.StandardAddInServer"), GuidAttribute(_simpleAddInClientId)>
    Public Class StandardAddInServer
        Implements Inventor.ApplicationAddInServer

        Private WithEvents _userInterfaceEvents As UserInterfaceEvents
        Private WithEvents _modelShootButton As ButtonDefinition

        Private Const PanelName As String = "Inventor Model Shoot"
        Private Const PanelId As String = "id_InventorModelShoot_Panel"
        Private Const ButtonInternalName As String = "InventorModelShoot_Button_" & _addInClientId

        ''' <summary>
        ''' Adds the Model Shoot panel and button to the existing Tools tab
        ''' of the Part and Assembly ribbons.
        ''' </summary>
        Private Sub AddToUserInterface()

            Dim controlDefs As ControlDefinitions = _inventorApplication.CommandManager.ControlDefinitions

            ' Reuse the button definition if it already exists (e.g. after a ribbon reset).
            _modelShootButton = Nothing
            Try
                _modelShootButton = CType(controlDefs.Item(ButtonInternalName), ButtonDefinition)
            Catch
            End Try

            If _modelShootButton Is Nothing Then
                Dim smallIcon As stdole.IPictureDisp = PictureDispConverter.ToIPictureDisp(LoadIcon("ModelShoot.ico", 16))
                Dim largeIcon As stdole.IPictureDisp = PictureDispConverter.ToIPictureDisp(LoadIcon("ModelShoot.ico", 32))

                _modelShootButton = controlDefs.AddButtonDefinition(
                    "Model Shoot",
                    ButtonInternalName,
                    CommandTypesEnum.kNonShapeEditCmdType,
                    _addInClientId,
                    "Capture screenshots of the active model",
                    "Capture screenshots of the active model and send them by email.",
                    smallIcon,
                    largeIcon)
            End If

            _modelShootButton.Enabled = True

            ' Add the panel to the Tools tab in each environment.
            For Each environmentName As String In {"Part", "Assembly"}

                Dim ribbon As Ribbon = _inventorApplication.UserInterfaceManager.Ribbons.Item(environmentName)

                Dim toolsTab As RibbonTab = Nothing
                Try
                    toolsTab = ribbon.RibbonTabs.Item("id_TabTools")
                Catch
                End Try
                If toolsTab Is Nothing Then Continue For

                Dim panel As RibbonPanel = Nothing
                Try
                    panel = toolsTab.RibbonPanels.Item(PanelId)
                Catch
                End Try

                If panel Is Nothing Then
                    panel = toolsTab.RibbonPanels.Add(PanelName, PanelId, _addInClientId)
                    panel.CommandControls.AddButton(_modelShootButton, True, True)
                End If

            Next

        End Sub

        Private Sub ModelShootButton_OnExecute(Context As NameValueMap) Handles _modelShootButton.OnExecute
            ModelShootCommand.RunCommandCode()
        End Sub

#Region "ApplicationAddInServer Members"

        ''' <summary>Called by Inventor when it loads the add-in.</summary>
        Public Sub Activate(ByVal addInSiteObject As Inventor.ApplicationAddInSite, ByVal firstTime As Boolean) Implements Inventor.ApplicationAddInServer.Activate

            _inventorApplication = addInSiteObject.Application
            _userInterfaceEvents = _inventorApplication.UserInterfaceManager.UserInterfaceEvents

            Try
                AddToUserInterface()
            Catch ex As Exception
                MsgBox("Inventor Model Shoot could not add its button:" & vbCrLf & vbCrLf & ex.Message)
            End Try

        End Sub

        ''' <summary>Called by Inventor when the add-in is unloaded.</summary>
        Public Sub Deactivate() Implements Inventor.ApplicationAddInServer.Deactivate

            _modelShootButton = Nothing
            _userInterfaceEvents = Nothing
            _inventorApplication = Nothing

            System.GC.Collect()
            System.GC.WaitForPendingFinalizers()

        End Sub

        Public ReadOnly Property Automation() As Object Implements Inventor.ApplicationAddInServer.Automation
            Get
                Return Nothing
            End Get
        End Property

        ' Obsolete; commands are implemented through ControlDefinitions.
        Public Sub ExecuteCommand(ByVal commandID As Integer) Implements Inventor.ApplicationAddInServer.ExecuteCommand
        End Sub

        Private Sub UserInterfaceEvents_OnResetRibbonInterface(Context As NameValueMap) Handles _userInterfaceEvents.OnResetRibbonInterface
            ' The ribbon was reset, so add the user interface back.
            AddToUserInterface()
        End Sub

#End Region

    End Class

End Namespace
