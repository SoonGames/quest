﻿Public Class Editor

    Private WithEvents m_controller As EditorController
    Private m_elementEditors As Dictionary(Of String, ElementEditor)
    Private m_currentEditor As ElementEditor
    Private m_menu As AxeSoftware.Quest.Controls.Menu
    Private m_filename As String
    Private m_currentElement As String

    Public Event AddToRecent(filename As String, name As String)
    Public Event Close()
    Public Event Play(filename As String)

    Public Sub Initialise(ByRef filename As String)
        m_filename = filename
        m_controller = New EditorController()
        InitialiseEditorControlsList()
        m_controller.Initialise(filename)
        SetUpTree()
        SetUpToolbar()
        SetUpEditors()
        RaiseEvent AddToRecent(filename, m_controller.GameName)
    End Sub

    Private Sub InitialiseEditorControlsList()
        For Each t As Type In AxeSoftware.Utility.Classes.GetImplementations(System.Reflection.Assembly.GetExecutingAssembly(), GetType(IElementEditorControl))
            Dim controlType As ControlTypeAttribute = DirectCast(Attribute.GetCustomAttribute(t, GetType(ControlTypeAttribute)), ControlTypeAttribute)
            If Not controlType Is Nothing Then
                m_controller.AddControlType(controlType.ControlType, t)
            End If
        Next
    End Sub

    Public Sub SetMenu(menu As AxeSoftware.Quest.Controls.Menu)
        m_menu = menu
        menu.AddMenuClickHandler("save", AddressOf Save)
        menu.AddMenuClickHandler("saveas", AddressOf SaveAs)
        menu.AddMenuClickHandler("undo", AddressOf Undo)
        menu.AddMenuClickHandler("redo", AddressOf Redo)
        menu.AddMenuClickHandler("addobject", AddressOf AddNewObject)
        menu.AddMenuClickHandler("addroom", AddressOf AddNewRoom)
        menu.AddMenuClickHandler("addexit", AddressOf AddNewExit)
        menu.AddMenuClickHandler("play", AddressOf PlayGame)
        menu.AddMenuClickHandler("close", AddressOf CloseEditor)
    End Sub

    Private Sub SetUpToolbar()
        ctlToolbar.ResetToolbar()
        ctlToolbar.AddButtonHandler("save", AddressOf Save)
        ctlToolbar.AddButtonHandler("undo", AddressOf Undo)
        ctlToolbar.AddButtonHandler("redo", AddressOf Redo)
        ctlToolbar.AddButtonHandler("addobject", AddressOf AddNewObject)
        ctlToolbar.AddButtonHandler("addroom", AddressOf AddNewRoom)
        ctlToolbar.AddButtonHandler("play", AddressOf PlayGame)
    End Sub

    Private Sub SetUpTree()
        ctlTree.SetAvailableFilters(m_controller.AvailableFilters)
        ctlTree.SetCanDragDelegate(AddressOf m_controller.CanMoveElement)
        ctlTree.SetDoDragDelegate(AddressOf m_controller.MoveElement)
        ctlTree.CollapseAdvancedNode()

        ctlTree.AddMenuClickHandler("addobject", AddressOf AddNewObject)
        ctlTree.AddMenuClickHandler("addroom", AddressOf AddNewRoom)
        ctlTree.AddMenuClickHandler("addexit", AddressOf AddNewExit)
    End Sub

    Private Sub SetUpEditors()
        m_elementEditors = New Dictionary(Of String, ElementEditor)

        For Each editor As String In m_controller.GetAllEditorNames()
            AddEditor(editor)
        Next
    End Sub

    Private Sub AddEditor(name As String)
        ' Get an EditorDefinition from the EditorController, then pass it in to the ElementEditor so it can initialise its
        ' tabs and subcontrols.
        Dim editor As ElementEditor
        editor = New ElementEditor
        editor.Initialise(m_controller, m_controller.GetEditorDefinition(name))
        editor.Visible = False
        editor.Parent = pnlContent
        editor.Dock = DockStyle.Fill
        AddHandler editor.Dirty, AddressOf Editor_Dirty
        m_elementEditors.Add(name, editor)
    End Sub

    Private Sub Editor_Dirty(sender As Object, args As DataModifiedEventArgs)
        ctlToolbar.EnableUndo()
        ' TO DO: Set status saying game not saved
    End Sub

    Private Sub m_controller_AddedNode(key As String, text As String, parent As String, foreColor As System.Drawing.Color?, backColor As System.Drawing.Color?) Handles m_controller.AddedNode
        ctlTree.AddNode(key, text, parent, foreColor, backColor)
    End Sub

    Private Sub m_controller_RemovedNode(key As String) Handles m_controller.RemovedNode
        ctlTree.RemoveNode(key)
    End Sub

    Private Sub m_controller_RenamedNode(oldName As String, newName As String) Handles m_controller.RenamedNode
        If m_currentElement = oldName Then
            m_currentElement = newName
        End If
        ctlTree.RenameNode(oldName, newName)
        ctlToolbar.RenameHistory(oldName, newName)
    End Sub

    Private Sub m_controller_RetitledNode(key As String, newTitle As String) Handles m_controller.RetitledNode
        If (m_currentElement = key) Then
            lblHeader.Text = newTitle
        End If
        ctlTree.RetitleNode(key, newTitle)
        ctlToolbar.RetitleHistory(key, newTitle)
    End Sub

    Private Sub m_controller_BeginTreeUpdate() Handles m_controller.BeginTreeUpdate
        ctlTree.BeginUpdate()
    End Sub

    Private Sub m_controller_ClearTree() Handles m_controller.ClearTree
        ctlTree.Clear()
    End Sub

    Private Sub m_controller_ElementUpdated(sender As Object, e As EditorController.ElementUpdatedEventArgs) Handles m_controller.ElementUpdated
        If e.Element = m_currentElement Then
            m_currentEditor.UpdateField(e.Attribute, e.NewValue, e.IsUndo)
        End If
    End Sub

    Private Sub m_controller_ElementRefreshed(sender As Object, e As EditorController.ElementRefreshedEventArgs) Handles m_controller.ElementRefreshed
        If e.Element = m_currentElement Then
            m_currentEditor.Populate(m_controller.GetEditorData(e.Element))
        End If
    End Sub

    Private Sub m_controller_EndTreeUpdate() Handles m_controller.EndTreeUpdate
        ctlTree.EndUpdate()
    End Sub

    Private Sub m_controller_UndoListUpdated(sender As Object, e As EditorController.UpdateUndoListEventArgs) Handles m_controller.UndoListUpdated
        ctlToolbar.UpdateUndoMenu(e.UndoList)
    End Sub

    Private Sub m_controller_RedoListUpdated(sender As Object, e As EditorController.UpdateUndoListEventArgs) Handles m_controller.RedoListUpdated
        ctlToolbar.UpdateRedoMenu(e.UndoList)
    End Sub

    Private Sub m_controller_ShowMessage(message As String) Handles m_controller.ShowMessage
        System.Windows.Forms.MessageBox.Show(message)
    End Sub

    Private Sub ctlTree_FiltersUpdated() Handles ctlTree.FiltersUpdated
        m_controller.UpdateFilterOptions(ctlTree.FilterSettings)
    End Sub

    Private Sub ctlTree_SelectionChanged(key As String) Handles ctlTree.SelectionChanged
        ' TO DO: Need to add the tree text as the second parameter so we get friendly name for "Verbs" etc. instead of the key
        ctlToolbar.AddHistory(key, m_controller.GetDisplayName(key))
        ShowEditor(key)
    End Sub

    Private Sub ShowEditor(key As String)

        Dim editorName As String = m_controller.GetElementEditorName(key)
        Dim nextEditor As ElementEditor = m_elementEditors(editorName)

        nextEditor.Visible = True

        If Not m_currentEditor Is Nothing Then
            If Not m_currentEditor.Equals(nextEditor) Then
                m_currentEditor.Visible = False
            End If
        End If

        m_currentEditor = nextEditor

        m_currentElement = key
        m_currentEditor.Populate(m_controller.GetEditorData(key))
        lblHeader.Text = m_controller.GetDisplayName(key)
    End Sub

    Private Sub Save()
        ' TO DO: Save the currently selected control first

        If (m_filename.Length = 0) Then
            SaveAs()
        Else
            Save(m_filename)
        End If
    End Sub

    Private Sub SaveAs()
        ctlSaveFile.FileName = m_filename
        If ctlSaveFile.ShowDialog() = DialogResult.OK Then
            m_filename = ctlSaveFile.FileName
            Save(m_filename)
        End If
    End Sub

    Private Sub Save(filename As String)
        Try
            If Not m_currentEditor Is Nothing Then
                m_currentEditor.SaveData()
            End If
            System.IO.File.WriteAllText(filename, m_controller.Save())
        Catch ex As Exception
            MsgBox("Unable to save the file due to the following error:" + Environment.NewLine + Environment.NewLine + ex.Message, MsgBoxStyle.Critical)
        End Try
    End Sub

    Private Sub ctlToolbar_HistoryClicked(Key As String) Handles ctlToolbar.HistoryClicked
        ctlTree.SetSelectedItemNoEvent(Key)
        ShowEditor(Key)
    End Sub

    Private Sub Undo()
        If Not m_currentEditor Is Nothing Then
            m_currentEditor.SaveData()
            m_controller.Undo()
        End If
    End Sub

    Private Sub Redo()
        If Not m_currentEditor Is Nothing Then
            m_currentEditor.SaveData()
            m_controller.Redo()
        End If
    End Sub

    Private Sub ctlToolbar_SaveCurrentEditor() Handles ctlToolbar.SaveCurrentEditor
        If Not m_currentEditor Is Nothing Then
            m_currentEditor.SaveData()
        End If
    End Sub

    Private Sub ctlToolbar_UndoClicked(level As Integer) Handles ctlToolbar.UndoClicked
        m_controller.Undo(level)
    End Sub

    Private Sub ctlToolbar_RedoClicked(level As Integer) Handles ctlToolbar.RedoClicked
        m_controller.Redo(level)
    End Sub

    Private Sub ctlToolbar_UndoEnabled(enabled As Boolean) Handles ctlToolbar.UndoEnabled
        m_menu.MenuEnabled("undo") = enabled
    End Sub

    Private Sub ctlToolbar_RedoEnabled(enabled As Boolean) Handles ctlToolbar.RedoEnabled
        m_menu.MenuEnabled("redo") = enabled
    End Sub

    Private Sub AddNewObject()

        Dim possibleParents = m_controller.GetPossibleNewObjectParentsForCurrentSelection(ctlTree.SelectedItem)
        Const prompt As String = "Please enter a name for the new object"
        Const noParent As String = "(none)"

        If possibleParents Is Nothing Then
            Dim result = PopupEditors.EditString(prompt, "")
            If result.Cancelled Then Return
            If Not ValidateInput(result.Result) Then Return

            m_controller.CreateNewObject(result.Result, Nothing)
            ctlTree.SetSelectedItem(result.Result)
        Else
            Dim parentOptions As New List(Of String)
            parentOptions.Add(noParent)
            parentOptions.AddRange(possibleParents)

            Dim result = PopupEditors.EditStringWithDropdown(prompt, "", "Parent", parentOptions, ctlTree.SelectedItem)
            If result.Cancelled Then Return
            If Not ValidateInput(result.Result) Then Return

            Dim parent = result.ListResult
            If parent = noParent Then parent = Nothing

            m_controller.CreateNewObject(result.Result, parent)
            ctlTree.SetSelectedItem(result.Result)
        End If

    End Sub

    Private Sub AddNewRoom()
        Dim result = PopupEditors.EditString("Please enter a name for the new room", "")
        If result.Cancelled Then Return
        If Not ValidateInput(result.Result) Then Return

        m_controller.CreateNewRoom(result.Result, Nothing)
        ctlTree.SetSelectedItem(result.Result)
    End Sub

    Private Sub AddNewExit()
        Dim parent As String
        If m_controller.GetElementType(ctlTree.SelectedItem) = "object" AndAlso m_controller.GetObjectType(ctlTree.SelectedItem) = "object" Then
            parent = ctlTree.SelectedItem
        Else
            parent = Nothing
        End If

        Dim newExit = m_controller.CreateNewExit(parent)
        ctlTree.SetSelectedItem(newExit)
    End Sub

    Private Function ValidateInput(input As String) As Boolean
        Dim result = m_controller.CanAdd(input)
        If result.Valid Then Return True

        MsgBox(PopupEditors.GetError(result.Message, input), MsgBoxStyle.Exclamation, "Unable to add element")
        Return False
    End Function

    Public Function CreateNewGame() As String
        Dim templates As Dictionary(Of String, String) = GetAvailableTemplates()
        Dim newGameWindow As New NewGameWindow
        newGameWindow.SetAvailableTemplates(templates)
        newGameWindow.ShowDialog()

        If newGameWindow.Cancelled Then Return Nothing

        Dim filename = newGameWindow.txtFilename.Text
        Dim folder = System.IO.Path.GetDirectoryName(filename)
        If Not System.IO.Directory.Exists(folder) Then
            System.IO.Directory.CreateDirectory(folder)
        End If

        Dim templateText = System.IO.File.ReadAllText(templates(newGameWindow.lstTemplate.Text))
        Dim initialFileText = templateText.Replace("$NAME$", newGameWindow.txtGameName.Text)

        System.IO.File.WriteAllText(filename, initialFileText)

        Return filename
    End Function

    Public Function GetAvailableTemplates() As Dictionary(Of String, String)
        Dim templates As New Dictionary(Of String, String)

        Dim folder As String = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().CodeBase)
        folder = folder.Substring(6) ' remove initial "file://"

        For Each file In System.IO.Directory.GetFiles(folder, "*.template")
            templates.Add(System.IO.Path.GetFileNameWithoutExtension(file), file)
        Next

        Return templates
    End Function

    Private Sub PlayGame()
        ' TO DO: Current game must be saved and up to date i.e. non-dirty
        RaiseEvent Play(m_filename)
    End Sub

    Private Sub CloseEditor()
        RaiseEvent Close()
    End Sub

End Class
