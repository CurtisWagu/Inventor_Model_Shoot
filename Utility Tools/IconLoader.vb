Module IconLoader

    ''' <summary>
    ''' Loads one size out of an .ico embedded in the Resources folder.
    ''' Example: LoadIcon("ModelShoot.ico", 32)
    ''' </summary>
    Friend Function LoadIcon(fileName As String, size As Integer) As System.Drawing.Icon

        Dim asm As System.Reflection.Assembly = System.Reflection.Assembly.GetExecutingAssembly()
        Dim names As String() = asm.GetManifestResourceNames()
        Dim resourceName As String = names.FirstOrDefault(
            Function(n) n.EndsWith("." & fileName, StringComparison.OrdinalIgnoreCase))

        If resourceName Is Nothing Then
            Throw New System.IO.FileNotFoundException(
                "Embedded icon not found: " & fileName & ". Embedded resources: " & String.Join(", ", names))
        End If

        Using stream As System.IO.Stream = asm.GetManifestResourceStream(resourceName)
            Return New System.Drawing.Icon(stream, size, size)
        End Using

    End Function

End Module
