Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Windows.Forms

''' <summary>
''' The Model Shoot window: names and captures screenshots of the active Inventor view,
''' keeps a list of them with a preview, creates a PDF or ZIP from the images, and attaches
''' the selected list items to a new email.
''' This file deliberately does not use "Imports Inventor" so the Inventor types
''' File, Path, Environment, Color and so on cannot clash with the .NET ones.
''' </summary>
Friend Class ModelShootForm
    Inherits System.Windows.Forms.Form

    ''' <summary>One entry in the screenshot list. Shows just the file name.</summary>
    Private Class ShotItem
        Public ReadOnly Property FullPath As String

        Public Sub New(filePath As String)
            Me.FullPath = filePath
        End Sub

        Public Overrides Function ToString() As String
            Return System.IO.Path.GetFileName(FullPath)
        End Function
    End Class

    Private Enum BackgroundMode
        AsInInventor
        White
        Transparent
    End Enum

    Private Enum CreateKind
        Pdf
        Zip
    End Enum

    Private ReadOnly _imageExtensions As String() = {".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff"}

    Private _txtName As TextBox
    Private _txtSuffix As TextBox
    Private _numX As NumericUpDown
    Private _numY As NumericUpDown
    Private _rbJpg As RadioButton
    Private _rbPng As RadioButton
    Private _rbAsIs As RadioButton
    Private _rbWhite As RadioButton
    Private _rbTransparent As RadioButton
    Private _lstShots As ListBox
    Private _picPreview As PictureBox
    Private _lblPreviewInfo As Label
    Private _btnCapture As Button
    Private _btnAddFile As Button
    Private _btnRemove As Button
    Private _btnPaint As Button
    Private _btnFolder As Button
    Private _lblStatus As Label
    Private _tips As ToolTip

    ' True once the user has typed their own main name, so we stop overwriting it with the document name.
    Private _nameEdited As Boolean = False
    Private _settingName As Boolean = False

#Region "Build the window"

    Public Sub New()

        Me.Text = "Model Shoot"
        Me.Font = New Font("Segoe UI", 9.0F)
        Me.FormBorderStyle = FormBorderStyle.FixedSingle
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.ShowInTaskbar = False
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.ClientSize = New Size(706, 514)

        Try
            Me.Icon = LoadIcon("ModelShoot.ico", 32)
        Catch
            ' The window works fine without its icon.
        End Try

        _tips = New ToolTip()
        BuildControls()

        SetMainName(GetDocumentName())
        UpdateButtonStates()
        ShowPreview()

    End Sub

    Private Sub BuildControls()

        ' --- Name, suffix and resolution ---
        Me.Controls.Add(MakeLabel("Main Name", 16, 12))
        _txtName = New TextBox() With {.Location = New Point(16, 34), .Width = 330}
        AddHandler _txtName.TextChanged, AddressOf OnNameTextChanged
        Me.Controls.Add(_txtName)

        Me.Controls.Add(MakeLabel("Suffix", 358, 12))
        _txtSuffix = New TextBox() With {.Location = New Point(358, 34), .Width = 80}
        Me.Controls.Add(_txtSuffix)

        Dim grpResolution As New GroupBox() With {
            .Text = "Resolution",
            .Location = New Point(456, 6),
            .Size = New Size(234, 82)}
        grpResolution.Controls.Add(MakeLabel("X", 14, 31))
        _numX = MakeNumeric(32, 27)
        grpResolution.Controls.Add(_numX)
        grpResolution.Controls.Add(MakeLabel("Y", 122, 31))
        _numY = MakeNumeric(140, 27)
        grpResolution.Controls.Add(_numY)
        Dim hint As Label = MakeLabel("0 and 0 = same size as the window", 14, 58)
        hint.ForeColor = SystemColors.GrayText
        grpResolution.Controls.Add(hint)
        Me.Controls.Add(grpResolution)
        _tips.SetToolTip(_numX, "Width in pixels. 0 uses the current window size.")
        _tips.SetToolTip(_numY, "Height in pixels. 0 uses the current window size.")

        ' --- File type and background ---
        ' Each set of radio buttons sits in its own panel so the two sets work independently.
        Me.Controls.Add(MakeLabel("File type:", 16, 68))
        Dim pnlFileType As New Panel() With {.Location = New Point(96, 62), .Size = New Size(340, 26)}
        _rbJpg = New RadioButton() With {.Text = "JPG", .Location = New Point(0, 3), .AutoSize = True, .Checked = True}
        _rbPng = New RadioButton() With {.Text = "PNG", .Location = New Point(70, 3), .AutoSize = True}
        pnlFileType.Controls.AddRange(New Control() {_rbJpg, _rbPng})
        Me.Controls.Add(pnlFileType)

        Me.Controls.Add(MakeLabel("Background:", 16, 96))
        Dim pnlBackground As New Panel() With {.Location = New Point(96, 90), .Size = New Size(340, 26)}
        _rbAsIs = New RadioButton() With {.Text = "As in Inventor", .Location = New Point(0, 3), .AutoSize = True, .Checked = True}
        _rbWhite = New RadioButton() With {.Text = "White", .Location = New Point(124, 3), .AutoSize = True}
        _rbTransparent = New RadioButton() With {.Text = "Transparent", .Location = New Point(200, 3), .AutoSize = True}
        pnlBackground.Controls.AddRange(New Control() {_rbAsIs, _rbWhite, _rbTransparent})
        AddHandler _rbTransparent.CheckedChanged, AddressOf OnBackgroundChanged
        Me.Controls.Add(pnlBackground)

        _tips.SetToolTip(_rbJpg, "Saves screenshots as JPG files.")
        _tips.SetToolTip(_rbPng, "Saves screenshots as PNG files.")
        _tips.SetToolTip(_rbAsIs, "Uses the background shown in Inventor.")
        _tips.SetToolTip(_rbWhite, "Uses a white background. Inventor's colors are not changed.")
        _tips.SetToolTip(_rbTransparent, "Uses a transparent background. This needs the PNG file type.")

        ' --- Screenshot list and preview ---
        _lstShots = New ListBox() With {
            .Location = New Point(16, 126),
            .Size = New Size(230, 236),
            .HorizontalScrollbar = True,
            .IntegralHeight = False,
            .SelectionMode = SelectionMode.MultiExtended}
        AddHandler _lstShots.SelectedIndexChanged, AddressOf OnSelectionChanged
        AddHandler _lstShots.DoubleClick, AddressOf OnOpenSelected
        AddHandler _lstShots.KeyDown, AddressOf OnListKeyDown
        Me.Controls.Add(_lstShots)
        _tips.SetToolTip(_lstShots, "Click to select. Ctrl-click or Shift-click to select several. Double-click to open.")

        _picPreview = New PictureBox() With {
            .Location = New Point(256, 126),
            .Size = New Size(294, 210),
            .SizeMode = PictureBoxSizeMode.Zoom,
            .BorderStyle = BorderStyle.FixedSingle,
            .BackgroundImage = MakeCheckerboard(),
            .BackgroundImageLayout = ImageLayout.Tile}
        Me.Controls.Add(_picPreview)

        _lblPreviewInfo = New Label() With {
            .Location = New Point(256, 342),
            .Size = New Size(294, 20),
            .AutoEllipsis = True,
            .ForeColor = SystemColors.GrayText}
        Me.Controls.Add(_lblPreviewInfo)

        ' --- Command buttons beside the preview ---
        _btnCapture = MakeButton("Take Screenshot", 560, 126, 130, AddressOf OnCapture)
        _btnCapture.Font = New Font(Me.Font, FontStyle.Bold)
        _btnAddFile = MakeButton("Add File...", 560, 170, 130, AddressOf OnAddFile)
        _btnRemove = MakeButton("Remove from List", 560, 214, 130, AddressOf OnRemove)
        _btnPaint = MakeButton("Edit in Paint", 560, 258, 130, AddressOf OnEditInPaint)
        _btnFolder = MakeButton("Open Folder", 560, 302, 130, AddressOf OnOpenFolder)
        Me.Controls.AddRange(New Control() {_btnCapture, _btnAddFile, _btnRemove, _btnPaint, _btnFolder})

        ' --- Create and send ---
        Dim grpSend As New GroupBox() With {
            .Text = "Create and Send",
            .Location = New Point(16, 374),
            .Size = New Size(674, 106)}

        grpSend.Controls.Add(MakeLabel("Create from the files in the list:", 14, 33))
        Dim btnPdf As Button = MakeButton("PDF", 260, 26, 100, AddressOf OnCreatePdf)
        Dim btnZip As Button = MakeButton("ZIP", 370, 26, 100, AddressOf OnCreateZip)
        grpSend.Controls.AddRange(New Control() {btnPdf, btnZip})
        _tips.SetToolTip(btnPdf, "Makes one PDF with every image in the list, one per page, in the Screenshots folder. PDF and ZIP files in the list are skipped.")
        _tips.SetToolTip(btnZip, "Puts every image and PDF in the list into one ZIP file in the Screenshots folder. ZIP files in the list are skipped.")

        grpSend.Controls.Add(MakeLabel("Send the selected list items:", 14, 73))
        Dim btnEmail As Button = MakeButton("Attach to New Email", 260, 66, 210, AddressOf OnAttachToEmail)
        grpSend.Controls.Add(btnEmail)
        _tips.SetToolTip(btnEmail, "Opens a new Outlook email with the items selected in the list attached.")

        Me.Controls.Add(grpSend)

        _lblStatus = New Label() With {
            .Location = New Point(16, 488),
            .Size = New Size(674, 20),
            .AutoEllipsis = True,
            .Text = "Ready."}
        Me.Controls.Add(_lblStatus)

    End Sub

    Private Shared Function MakeLabel(text As String, x As Integer, y As Integer) As Label
        Return New Label() With {.Text = text, .Location = New Point(x, y), .AutoSize = True}
    End Function

    Private Shared Function MakeNumeric(x As Integer, y As Integer) As NumericUpDown
        Return New NumericUpDown() With {
            .Location = New Point(x, y),
            .Width = 70,
            .Minimum = 0,
            .Maximum = 20000,
            .Value = 0}
    End Function

    Private Function MakeButton(text As String, x As Integer, y As Integer, width As Integer, handler As EventHandler) As Button
        Dim b As New Button() With {
            .Text = text,
            .Location = New Point(x, y),
            .Size = New Size(width, 34),
            .UseVisualStyleBackColor = True}
        AddHandler b.Click, handler
        Return b
    End Function

    ''' <summary>A light gray checkerboard, drawn behind the preview so transparent areas can be seen.</summary>
    Private Shared Function MakeCheckerboard() As Bitmap
        Dim tile As New Bitmap(16, 16)
        Using g As Graphics = Graphics.FromImage(tile)
            g.Clear(System.Drawing.Color.FromArgb(250, 250, 250))
            Using shade As New SolidBrush(System.Drawing.Color.FromArgb(222, 222, 222))
                g.FillRectangle(shade, 0, 0, 8, 8)
                g.FillRectangle(shade, 8, 8, 8, 8)
            End Using
        End Using
        Return tile
    End Function

    Protected Overrides Sub OnActivated(e As EventArgs)
        MyBase.OnActivated(e)
        ' Refresh the preview when coming back to the window, for example after editing in Paint.
        ShowPreview()
    End Sub

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        MyBase.OnFormClosed(e)
        If _picPreview IsNot Nothing Then
            If _picPreview.Image IsNot Nothing Then _picPreview.Image.Dispose()
            _picPreview.Image = Nothing
            If _picPreview.BackgroundImage IsNot Nothing Then _picPreview.BackgroundImage.Dispose()
            _picPreview.BackgroundImage = Nothing
        End If
        If _tips IsNot Nothing Then _tips.Dispose()
    End Sub

    ''' <summary>A transparent background can only be saved as PNG, so choosing it switches to PNG and turns JPG off.</summary>
    Private Sub OnBackgroundChanged(sender As Object, e As EventArgs)
        If _rbTransparent.Checked Then
            _rbPng.Checked = True
            _rbJpg.Enabled = False
        Else
            _rbJpg.Enabled = True
        End If
    End Sub

#End Region

#Region "Names and folders"

    ''' <summary>The active document's file name without its extension (or "Untitled" if it was never saved).</summary>
    Private Shared Function GetDocumentName() As String

        If _inventorApplication Is Nothing Then Return ""

        Dim doc As Inventor.Document = _inventorApplication.ActiveDocument
        If doc Is Nothing Then Return ""

        Dim fullName As String = doc.FullFileName
        If Not String.IsNullOrEmpty(fullName) Then
            Return System.IO.Path.GetFileNameWithoutExtension(fullName)
        End If

        ' Document.DisplayName was renamed in Inventor 2027, so it is not used here.
        Return "Untitled"

    End Function

    Private Sub SetMainName(newName As String)
        _settingName = True
        _txtName.Text = newName
        _settingName = False
    End Sub

    Private Sub OnNameTextChanged(sender As Object, e As EventArgs)
        If _settingName Then Return
        ' Typing a name means "keep mine"; clearing the box goes back to following the document.
        _nameEdited = (_txtName.Text.Length > 0)
    End Sub

    ''' <summary>Removes characters Windows does not allow in file names.</summary>
    Private Shared Function CleanFileName(name As String) As String
        Dim result As String = name
        For Each c As Char In System.IO.Path.GetInvalidFileNameChars()
            result = result.Replace(c.ToString(), "")
        Next
        Return result
    End Function

    ''' <summary>The name (without extension) used for screenshots and for the PDF and ZIP files.</summary>
    Private Function CurrentBaseName() As String
        Return CleanFileName((_txtName.Text & _txtSuffix.Text))
    End Function

    ''' <summary>The "Screenshots" folder inside the Inventor project workspace. Created if it does not exist.</summary>
    Private Shared Function GetScreenshotsFolder() As String

        Dim baseFolder As String = ""

        Try
            baseFolder = _inventorApplication.DesignProjectManager.ActiveDesignProject.WorkspacePath
        Catch
        End Try

        ' Fall back to the active document's folder, then to My Pictures.
        If String.IsNullOrWhiteSpace(baseFolder) OrElse Not System.IO.Directory.Exists(baseFolder) Then
            baseFolder = ""
            Try
                Dim doc As Inventor.Document = _inventorApplication.ActiveDocument
                If doc IsNot Nothing AndAlso Not String.IsNullOrEmpty(doc.FullFileName) Then
                    baseFolder = System.IO.Path.GetDirectoryName(doc.FullFileName)
                End If
            Catch
            End Try
        End If

        If String.IsNullOrWhiteSpace(baseFolder) OrElse Not System.IO.Directory.Exists(baseFolder) Then
            baseFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures)
        End If

        Dim folder As String = System.IO.Path.Combine(baseFolder, "Screenshots")
        System.IO.Directory.CreateDirectory(folder)
        Return folder

    End Function

    Private Shared Function IsZipFile(filePath As String) As Boolean
        Return String.Equals(System.IO.Path.GetExtension(filePath), ".zip", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function IsImageFile(filePath As String) As Boolean
        Dim extension As String = System.IO.Path.GetExtension(filePath)
        Return _imageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
    End Function

#End Region

#Region "Taking the screenshot"

    Private Function CurrentBackground() As BackgroundMode
        If _rbTransparent.Checked Then Return BackgroundMode.Transparent
        If _rbWhite.Checked Then Return BackgroundMode.White
        Return BackgroundMode.AsInInventor
    End Function

    ''' <summary>The file extension chosen with the File type buttons. A transparent background always means PNG.</summary>
    Private Function CurrentExtension() As String
        If _rbTransparent.Checked OrElse _rbPng.Checked Then Return ".png"
        Return ".jpg"
    End Function

    Private Sub OnCapture(sender As Object, e As EventArgs)

        Try
            If _inventorApplication Is Nothing OrElse _inventorApplication.ActiveDocument Is Nothing Then
                MessageBox.Show(Me, "Open a part or assembly first.", "Model Shoot")
                Return
            End If

            ' Follow the active document unless the user typed their own name.
            If Not _nameEdited Then SetMainName(GetDocumentName())

            Dim baseName As String = CurrentBaseName()
            If baseName.Trim().Length = 0 Then
                MessageBox.Show(Me, "Enter a name for the screenshot.", "Model Shoot")
                Return
            End If

            Dim fileName As String = baseName & CurrentExtension()
            Dim folder As String = GetScreenshotsFolder()
            Dim target As String = System.IO.Path.Combine(folder, fileName)

            If Not ConfirmReplace(target) Then Return

            SaveView(target, CInt(_numX.Value), CInt(_numY.Value), CurrentBackground())
            AddToList(target)
            ShowPreview()
            SetStatus("Saved " & fileName)

        Catch ex As Exception
            MessageBox.Show(Me, "The screenshot could not be created:" & vbCrLf & vbCrLf & ex.Message, "Model Shoot",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try

    End Sub

    ''' <summary>If the file already exists, asks whether to replace it. Returns True if it is OK to write the file.</summary>
    Private Function ConfirmReplace(target As String) As Boolean

        If Not System.IO.File.Exists(target) Then Return True

        Dim answer As DialogResult = MessageBox.Show(Me,
            """" & System.IO.Path.GetFileName(target) & """ already exists." & vbCrLf & vbCrLf &
            "Replace it? (Choose No and enter a new suffix to keep both.)",
            "Model Shoot", MessageBoxButtons.YesNo, MessageBoxIcon.Question)

        Return (answer = DialogResult.Yes)

    End Function

    ''' <summary>
    ''' Saves the active view. The file type (JPG or PNG) comes from the extension of the target file.
    ''' Width and height of 0 mean "same size as the window".
    ''' As in Inventor and Transparent save straight from Inventor. For White the view is captured with a
    ''' transparent background and flattened onto white in memory, so Inventor's own color scheme is never changed.
    ''' </summary>
    Private Shared Sub SaveView(target As String, width As Integer, height As Integer, mode As BackgroundMode)

        Dim view As Inventor.View = _inventorApplication.ActiveView
        If view Is Nothing Then Throw New InvalidOperationException("There is no active view to capture.")

        Select Case mode

            Case BackgroundMode.AsInInventor
                view.SaveAsBitmap(target, width, height)

            Case BackgroundMode.Transparent
                Dim options As Inventor.NameValueMap = _inventorApplication.TransientObjects.CreateNameValueMap()
                options.Add("TransparentBackground", True)
                view.SaveAsBitmapWithOptions(target, width, height, options)

            Case BackgroundMode.White
                Dim isPng As Boolean = target.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                Dim tempFile As String = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), "ModelShoot_" & Guid.NewGuid().ToString("N") & ".png")
                Try
                    Dim options As Inventor.NameValueMap = _inventorApplication.TransientObjects.CreateNameValueMap()
                    options.Add("TransparentBackground", True)
                    view.SaveAsBitmapWithOptions(tempFile, width, height, options)

                    Using source As New System.Drawing.Bitmap(tempFile)
                        Using flat As New System.Drawing.Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb)
                            Using g As Graphics = Graphics.FromImage(flat)
                                g.Clear(System.Drawing.Color.White)
                                g.DrawImage(source, 0, 0, source.Width, source.Height)
                            End Using
                            If isPng Then
                                flat.Save(target, ImageFormat.Png)
                            Else
                                SaveJpeg(flat, target)
                            End If
                        End Using
                    End Using
                Finally
                    Try
                        System.IO.File.Delete(tempFile)
                    Catch
                    End Try
                End Try

        End Select

    End Sub

    Private Shared Sub SaveJpeg(image As System.Drawing.Image, target As String)

        Dim codec As ImageCodecInfo = ImageCodecInfo.GetImageEncoders().First(
            Function(c) c.FormatID = ImageFormat.Jpeg.Guid)

        Using parameters As New EncoderParameters(1)
            parameters.Param(0) = New EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 95L)
            image.Save(target, codec, parameters)
        End Using

    End Sub

#End Region

#Region "The screenshot list and preview"

    ''' <summary>Adds a file to the list (once) and makes it the only selected item.</summary>
    Private Sub AddToList(filePath As String)

        For Each existing As Object In _lstShots.Items
            Dim item As ShotItem = DirectCast(existing, ShotItem)
            If String.Equals(item.FullPath, filePath, StringComparison.OrdinalIgnoreCase) Then
                _lstShots.ClearSelected()
                _lstShots.SetSelected(_lstShots.Items.IndexOf(existing), True)
                Return
            End If
        Next

        Dim newItem As New ShotItem(filePath)
        _lstShots.Items.Add(newItem)
        _lstShots.ClearSelected()
        _lstShots.SetSelected(_lstShots.Items.IndexOf(newItem), True)

    End Sub

    ''' <summary>The first selected item, which is the one shown in the preview.</summary>
    Private Function SelectedPath() As String
        If _lstShots.SelectedItems.Count = 0 Then Return Nothing
        Return DirectCast(_lstShots.SelectedItems(0), ShotItem).FullPath
    End Function

    Private Function SelectedPaths() As List(Of String)
        Dim paths As New List(Of String)
        For Each selected As Object In _lstShots.SelectedItems
            paths.Add(DirectCast(selected, ShotItem).FullPath)
        Next
        Return paths
    End Function

    Private Function AllPaths() As List(Of String)
        Dim paths As New List(Of String)
        For Each existing As Object In _lstShots.Items
            paths.Add(DirectCast(existing, ShotItem).FullPath)
        Next
        Return paths
    End Function

    Private Sub UpdateButtonStates()
        Dim firstSelected As String = SelectedPath()
        _btnRemove.Enabled = (firstSelected IsNot Nothing)
        ' Paint can only open pictures, not PDF or ZIP files.
        _btnPaint.Enabled = (firstSelected IsNot Nothing AndAlso IsImageFile(firstSelected))
    End Sub

    Private Sub OnSelectionChanged(sender As Object, e As EventArgs)
        UpdateButtonStates()
        ShowPreview()
    End Sub

    ''' <summary>Shows the first selected item in the preview box.</summary>
    Private Sub ShowPreview()

        If _picPreview Is Nothing Then Return

        Dim oldImage As System.Drawing.Image = _picPreview.Image
        _picPreview.Image = Nothing
        If oldImage IsNot Nothing Then oldImage.Dispose()

        Dim filePath As String = SelectedPath()
        If filePath Is Nothing Then
            _lblPreviewInfo.Text = "Select a screenshot to preview it."
            Return
        End If

        If Not IsImageFile(filePath) Then
            _lblPreviewInfo.Text = "No preview for " & System.IO.Path.GetExtension(filePath).TrimStart("."c).ToUpperInvariant() & " files. Double-click to open it."
            Return
        End If

        Try
            Dim picture As System.Drawing.Bitmap = ExportTools.LoadCopy(filePath)
            _picPreview.Image = picture
            Dim sizeKb As Long = (New System.IO.FileInfo(filePath)).Length \ 1024
            _lblPreviewInfo.Text = picture.Width.ToString() & " x " & picture.Height.ToString() & " px   (" & sizeKb.ToString() & " KB)"
        Catch
            _lblPreviewInfo.Text = "Preview not available for this file."
        End Try

    End Sub

    Private Sub OnAddFile(sender As Object, e As EventArgs)

        Using dialog As New OpenFileDialog()
            dialog.Title = "Add files"
            dialog.Filter = "Images, PDF and ZIP files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.pdf;*.zip|All files (*.*)|*.*"
            dialog.Multiselect = True
            Try
                dialog.InitialDirectory = GetScreenshotsFolder()
            Catch
            End Try

            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return

            For Each fileName As String In dialog.FileNames
                AddToList(fileName)
            Next
            SetStatus("Added " & dialog.FileNames.Length.ToString() & " file(s).")
        End Using

    End Sub

    ''' <summary>Takes the selected items off the list. The files themselves are not deleted.</summary>
    Private Sub OnRemove(sender As Object, e As EventArgs)

        Dim indices As List(Of Integer) = _lstShots.SelectedIndices.Cast(Of Integer)().OrderByDescending(Function(i) i).ToList()
        If indices.Count = 0 Then Return

        Dim lowest As Integer = indices.Last()

        _lstShots.BeginUpdate()
        For Each index As Integer In indices
            _lstShots.Items.RemoveAt(index)
        Next
        _lstShots.EndUpdate()

        If _lstShots.Items.Count > 0 Then
            _lstShots.SetSelected(Math.Min(lowest, _lstShots.Items.Count - 1), True)
        End If
        UpdateButtonStates()
        ShowPreview()
        SetStatus("Removed from the list. The files were not deleted.")

    End Sub

    Private Sub OnListKeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.Delete Then OnRemove(sender, EventArgs.Empty)
    End Sub

    Private Sub OnEditInPaint(sender As Object, e As EventArgs)

        Dim filePath As String = SelectedPath()
        If filePath Is Nothing OrElse Not IsImageFile(filePath) Then Return

        Try
            System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo("mspaint.exe", """" & filePath & """") With {.UseShellExecute = True})
        Catch ex As Exception
            MessageBox.Show(Me, "Paint could not be opened:" & vbCrLf & vbCrLf & ex.Message, "Model Shoot")
        End Try

    End Sub

    ''' <summary>Double-clicking an item opens it in its default program (image viewer, PDF reader or ZIP tool).</summary>
    Private Sub OnOpenSelected(sender As Object, e As EventArgs)

        Dim filePath As String = SelectedPath()
        If filePath Is Nothing Then Return

        Try
            System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo(filePath) With {.UseShellExecute = True})
        Catch ex As Exception
            MessageBox.Show(Me, "The file could not be opened:" & vbCrLf & vbCrLf & ex.Message, "Model Shoot")
        End Try

    End Sub

    Private Sub OnOpenFolder(sender As Object, e As EventArgs)

        Try
            Dim folder As String = GetScreenshotsFolder()
            System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo("explorer.exe", """" & folder & """") With {.UseShellExecute = True})
        Catch ex As Exception
            MessageBox.Show(Me, "The folder could not be opened:" & vbCrLf & vbCrLf & ex.Message, "Model Shoot")
        End Try

    End Sub

#End Region

#Region "Create and send"

    Private Sub OnCreatePdf(sender As Object, e As EventArgs)
        CreateFromList(CreateKind.Pdf)
    End Sub

    Private Sub OnCreateZip(sender As Object, e As EventArgs)
        CreateFromList(CreateKind.Zip)
    End Sub

    ''' <summary>
    ''' Creates a PDF or ZIP from the list. A PDF uses every image in the list (other files are skipped).
    ''' A ZIP uses every file in the list except other ZIP files, so images and PDFs can both go in a ZIP.
    ''' The new file goes in the Screenshots folder, next to the screenshots, and is added to the list.
    ''' </summary>
    Private Sub CreateFromList(kind As CreateKind)

        Dim kindName As String = If(kind = CreateKind.Pdf, "PDF", "ZIP")

        ' A PDF can only hold images. A ZIP can hold anything except another ZIP.
        Dim sources As List(Of String)
        If kind = CreateKind.Pdf Then
            sources = AllPaths().Where(Function(p) IsImageFile(p)).ToList()
        Else
            sources = AllPaths().Where(Function(p) Not IsZipFile(p)).ToList()
        End If

        If sources.Count = 0 Then
            If kind = CreateKind.Pdf Then
                MessageBox.Show(Me, "There are no images in the list to put in a PDF." & vbCrLf & vbCrLf &
                                "Take a screenshot or add image files first. Other files in the list are skipped.", "Model Shoot")
            Else
                MessageBox.Show(Me, "There are no files in the list to put in a ZIP." & vbCrLf & vbCrLf &
                                "Take a screenshot or add files first. ZIP files in the list are skipped.", "Model Shoot")
            End If
            Return
        End If

        Dim missing As List(Of String) = sources.Where(Function(p) Not System.IO.File.Exists(p)).ToList()
        If missing.Count > 0 Then
            MessageBox.Show(Me, "These files no longer exist:" & vbCrLf & vbCrLf &
                            String.Join(vbCrLf, missing.Select(Function(p) System.IO.Path.GetFileName(p))) & vbCrLf & vbCrLf &
                            "Remove them from the list and try again.", "Model Shoot")
            Return
        End If

        Dim baseName As String = CurrentBaseName().Trim()
        If baseName.Length = 0 Then baseName = "Screenshots"

        Dim extension As String = If(kind = CreateKind.Pdf, ".pdf", ".zip")
        Dim target As String = System.IO.Path.Combine(GetScreenshotsFolder(), baseName & extension)

        If Not ConfirmReplace(target) Then Return

        Dim previousCursor As Cursor = Me.Cursor
        Me.Cursor = Cursors.WaitCursor

        Try
            SetStatus("Creating the " & kindName & "...")

            If kind = CreateKind.Pdf Then
                ExportTools.CreatePdf(sources, target)
            Else
                ExportTools.CreateZip(sources, target)
            End If

            AddToList(target)
            ShowPreview()
            SetStatus("Created " & System.IO.Path.GetFileName(target) & " from " & sources.Count.ToString() & " file(s).")

        Catch ex As Exception
            SetStatus("The " & kindName & " could not be created.")
            MessageBox.Show(Me, "The " & kindName & " could not be created:" & vbCrLf & vbCrLf & ex.Message & vbCrLf & vbCrLf &
                            "If the file is open in another program, close it and try again.", "Model Shoot",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Finally
            Me.Cursor = previousCursor
        End Try

    End Sub

    ''' <summary>Opens a new Outlook email with the items selected in the list attached.</summary>
    Private Sub OnAttachToEmail(sender As Object, e As EventArgs)

        Dim attachments As List(Of String) = SelectedPaths()
        If attachments.Count = 0 Then
            MessageBox.Show(Me, "Select one or more items in the list first." & vbCrLf & vbCrLf &
                            "Use Ctrl-click or Shift-click to select several.", "Model Shoot")
            Return
        End If

        Dim missing As List(Of String) = attachments.Where(Function(p) Not System.IO.File.Exists(p)).ToList()
        If missing.Count > 0 Then
            MessageBox.Show(Me, "These files no longer exist:" & vbCrLf & vbCrLf &
                            String.Join(vbCrLf, missing.Select(Function(p) System.IO.Path.GetFileName(p))) & vbCrLf & vbCrLf &
                            "Remove them from the list and try again.", "Model Shoot")
            Return
        End If

        Dim baseName As String = CurrentBaseName().Trim()
        If baseName.Length = 0 Then baseName = "Screenshots"

        Dim previousCursor As Cursor = Me.Cursor
        Me.Cursor = Cursors.WaitCursor

        Try
            Dim problem As String = Nothing
            If OutlookMail.TryCreate("Screenshots: " & baseName, attachments, problem) Then
                SetStatus("Email created with " & attachments.Count.ToString() & " attachment(s).")
            Else
                SetStatus("The email could not be created.")
                MessageBox.Show(Me, "The email could not be created:" & vbCrLf & vbCrLf & problem, "Model Shoot",
                                MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        Catch ex As Exception
            SetStatus("Something went wrong.")
            MessageBox.Show(Me, "Could not finish:" & vbCrLf & vbCrLf & ex.Message, "Model Shoot",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Finally
            Me.Cursor = previousCursor
        End Try

    End Sub

#End Region

    Private Sub SetStatus(message As String)
        _lblStatus.Text = message
        _lblStatus.Refresh()
    End Sub

End Class
