' Outlook is controlled through late binding, so this file needs Option Strict Off.
Option Strict Off

''' <summary>Creates a new Outlook email with attachments, without needing any Outlook files at build time.</summary>
Friend Module OutlookMail

    ''' <summary>
    ''' Creates a new message with the attachments and shows it, so the person can add recipients and send.
    ''' Returns False (and explains why in <paramref name="problem"/>) if Outlook cannot be used.
    ''' </summary>
    Friend Function TryCreate(subject As String, attachments As IEnumerable(Of String), ByRef problem As String) As Boolean

        Try
            Dim outlookType As Type = Type.GetTypeFromProgID("Outlook.Application")
            If outlookType Is Nothing Then
                problem = "Classic Microsoft Outlook was not found. (The newer Outlook app cannot be controlled by other programs.)"
                Return False
            End If

            Dim outlook As Object = Activator.CreateInstance(outlookType)
            Dim mail As Object = outlook.CreateItem(0)   ' 0 = a mail message

            mail.Subject = subject
            For Each attachmentPath As String In attachments
                mail.Attachments.Add(attachmentPath)
            Next
            mail.Display()

            Return True

        Catch ex As Exception
            problem = ex.Message
            Return False
        End Try

    End Function

End Module
