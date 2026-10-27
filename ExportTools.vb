Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Globalization

''' <summary>
''' Builds the PDF and ZIP files that Model Shoot attaches to an email.
''' The PDF is written directly (no extra libraries): one page per screenshot,
''' US Letter, landscape or portrait to suit the image, image scaled to fit and centered.
''' </summary>
Friend Module ExportTools

    Private Const LongSide As Double = 792.0   ' US Letter, in points (1/72 inch)
    Private Const ShortSide As Double = 612.0
    Private Const PageMargin As Double = 36.0

#Region "PDF"

    Friend Sub CreatePdf(imageFiles As IList(Of String), pdfPath As String)

        If imageFiles.Count = 0 Then Throw New ArgumentException("There are no images to put in the PDF.")

        ' Convert every image to a JPEG (transparent areas become white) that can go straight into the PDF.
        Dim jpegs As New List(Of Byte())
        Dim sizes As New List(Of System.Drawing.Size)

        For Each imageFile As String In imageFiles
            Using source As System.Drawing.Bitmap = LoadCopy(imageFile)
                Using flat As New System.Drawing.Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb)
                    Using g As Graphics = Graphics.FromImage(flat)
                        g.Clear(System.Drawing.Color.White)
                        g.DrawImage(source, 0, 0, source.Width, source.Height)
                    End Using
                    jpegs.Add(ToJpegBytes(flat))
                    sizes.Add(New System.Drawing.Size(flat.Width, flat.Height))
                End Using
            End Using
        Next

        Dim pageCount As Integer = jpegs.Count
        Dim lastObject As Integer = 2 + 3 * pageCount     ' catalog + page tree + 3 objects per page
        Dim offsets(lastObject) As Long

        Using output As New System.IO.FileStream(pdfPath, System.IO.FileMode.Create, System.IO.FileAccess.Write)

            WriteText(output, "%PDF-1.4" & vbLf)

            offsets(1) = output.Position
            WriteText(output, "1 0 obj" & vbLf & "<< /Type /Catalog /Pages 2 0 R >>" & vbLf & "endobj" & vbLf)

            Dim kids As New System.Text.StringBuilder()
            For i As Integer = 0 To pageCount - 1
                kids.Append((3 + 3 * i).ToString(CultureInfo.InvariantCulture)).Append(" 0 R ")
            Next
            offsets(2) = output.Position
            WriteText(output, "2 0 obj" & vbLf & "<< /Type /Pages /Kids [ " & kids.ToString() & "] /Count " &
                      pageCount.ToString(CultureInfo.InvariantCulture) & " >>" & vbLf & "endobj" & vbLf)

            For i As Integer = 0 To pageCount - 1

                Dim pageObject As Integer = 3 + 3 * i
                Dim contentObject As Integer = 4 + 3 * i
                Dim imageObject As Integer = 5 + 3 * i

                Dim imageWidth As Integer = sizes(i).Width
                Dim imageHeight As Integer = sizes(i).Height

                ' Landscape page for wide images, portrait for tall ones.
                Dim landscape As Boolean = (imageWidth > imageHeight)
                Dim pageWidth As Double = If(landscape, LongSide, ShortSide)
                Dim pageHeight As Double = If(landscape, ShortSide, LongSide)

                Dim scale As Double = Math.Min((pageWidth - 2 * PageMargin) / imageWidth, (pageHeight - 2 * PageMargin) / imageHeight)
                Dim drawWidth As Double = imageWidth * scale
                Dim drawHeight As Double = imageHeight * scale
                Dim x As Double = (pageWidth - drawWidth) / 2
                Dim y As Double = (pageHeight - drawHeight) / 2

                Dim content As String = "q " & Num(drawWidth) & " 0 0 " & Num(drawHeight) & " " & Num(x) & " " & Num(y) & " cm /Im0 Do Q"

                offsets(pageObject) = output.Position
                WriteText(output, pageObject.ToString(CultureInfo.InvariantCulture) & " 0 obj" & vbLf &
                          "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " & Num(pageWidth) & " " & Num(pageHeight) & "] " &
                          "/Resources << /XObject << /Im0 " & imageObject.ToString(CultureInfo.InvariantCulture) & " 0 R >> >> " &
                          "/Contents " & contentObject.ToString(CultureInfo.InvariantCulture) & " 0 R >>" & vbLf & "endobj" & vbLf)

                offsets(contentObject) = output.Position
                WriteText(output, contentObject.ToString(CultureInfo.InvariantCulture) & " 0 obj" & vbLf &
                          "<< /Length " & content.Length.ToString(CultureInfo.InvariantCulture) & " >>" & vbLf &
                          "stream" & vbLf & content & vbLf & "endstream" & vbLf & "endobj" & vbLf)

                offsets(imageObject) = output.Position
                WriteText(output, imageObject.ToString(CultureInfo.InvariantCulture) & " 0 obj" & vbLf &
                          "<< /Type /XObject /Subtype /Image /Width " & imageWidth.ToString(CultureInfo.InvariantCulture) &
                          " /Height " & imageHeight.ToString(CultureInfo.InvariantCulture) &
                          " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length " &
                          jpegs(i).Length.ToString(CultureInfo.InvariantCulture) & " >>" & vbLf & "stream" & vbLf)
                output.Write(jpegs(i), 0, jpegs(i).Length)
                WriteText(output, vbLf & "endstream" & vbLf & "endobj" & vbLf)

            Next

            Dim crossReferenceStart As Long = output.Position
            WriteText(output, "xref" & vbLf & "0 " & (lastObject + 1).ToString(CultureInfo.InvariantCulture) & vbLf)
            WriteText(output, "0000000000 65535 f" & vbCrLf)
            For o As Integer = 1 To lastObject
                WriteText(output, offsets(o).ToString("D10", CultureInfo.InvariantCulture) & " 00000 n" & vbCrLf)
            Next
            WriteText(output, "trailer" & vbLf & "<< /Size " & (lastObject + 1).ToString(CultureInfo.InvariantCulture) &
                      " /Root 1 0 R >>" & vbLf & "startxref" & vbLf &
                      crossReferenceStart.ToString(CultureInfo.InvariantCulture) & vbLf & "%%EOF" & vbLf)

        End Using

    End Sub

    Private Sub WriteText(output As System.IO.Stream, text As String)
        Dim bytes As Byte() = System.Text.Encoding.ASCII.GetBytes(text)
        output.Write(bytes, 0, bytes.Length)
    End Sub

    Private Function Num(value As Double) As String
        Return value.ToString("0.####", CultureInfo.InvariantCulture)
    End Function

    Private Function ToJpegBytes(image As System.Drawing.Image) As Byte()

        Dim codec As ImageCodecInfo = ImageCodecInfo.GetImageEncoders().First(
            Function(c) c.FormatID = ImageFormat.Jpeg.Guid)

        Using parameters As New EncoderParameters(1)
            parameters.Param(0) = New EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 92L)
            Using memory As New System.IO.MemoryStream()
                image.Save(memory, codec, parameters)
                Return memory.ToArray()
            End Using
        End Using

    End Function

    ''' <summary>Reads an image file into memory so the file is not left locked.</summary>
    Friend Function LoadCopy(imageFile As String) As System.Drawing.Bitmap
        Using stream As New System.IO.FileStream(imageFile, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite)
            Using original As New System.Drawing.Bitmap(stream)
                Return New System.Drawing.Bitmap(original)
            End Using
        End Using
    End Function

#End Region

#Region "ZIP"

    Friend Sub CreateZip(files As IList(Of String), zipPath As String)

        If files.Count = 0 Then Throw New ArgumentException("There are no files to put in the ZIP.")

        If System.IO.File.Exists(zipPath) Then System.IO.File.Delete(zipPath)

        Dim usedNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        Using archive As System.IO.Compression.ZipArchive =
            System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create)

            For Each filePath As String In files

                ' Two files from different folders can share a name, so make each entry name unique.
                Dim entryName As String = System.IO.Path.GetFileName(filePath)
                Dim counter As Integer = 2
                While usedNames.Contains(entryName)
                    entryName = System.IO.Path.GetFileNameWithoutExtension(filePath) & " (" & counter.ToString(CultureInfo.InvariantCulture) & ")" &
                                System.IO.Path.GetExtension(filePath)
                    counter += 1
                End While
                usedNames.Add(entryName)

                System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(archive, filePath, entryName)

            Next
        End Using

    End Sub

#End Region

End Module
