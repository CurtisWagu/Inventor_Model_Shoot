Imports System.Windows.Forms

Module ModelShootCommand

    Private _form As ModelShootForm

    ''' <summary>Runs when the Model Shoot button is clicked. Opens the window, or brings it forward if it is already open.</summary>
    Sub RunCommandCode()

        Try
            If _form IsNot Nothing AndAlso Not _form.IsDisposed Then
                If _form.WindowState = FormWindowState.Minimized Then _form.WindowState = FormWindowState.Normal
                _form.Activate()
                Return
            End If

            Try
                System.Windows.Forms.Application.EnableVisualStyles()
            Catch
            End Try

            _form = New ModelShootForm()
            AddHandler _form.FormClosed, AddressOf OnFormClosed

            ' Parent it to Inventor's main window so it stays with Inventor but does not block it.
            Dim owner As New WindowWrapper.WindowWrapper(New IntPtr(CLng(_inventorApplication.MainFrameHWND)))
            _form.Show(owner)

        Catch ex As Exception
            MessageBox.Show("Model Shoot could not open:" & vbCrLf & vbCrLf & ex.Message, "Model Shoot")
            _form = Nothing
        End Try

    End Sub

    Private Sub OnFormClosed(sender As Object, e As FormClosedEventArgs)
        _form = Nothing
    End Sub

End Module
