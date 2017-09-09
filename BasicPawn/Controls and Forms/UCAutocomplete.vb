﻿'BasicPawn
'Copyright(C) 2017 TheTimocop

'This program Is free software: you can redistribute it And/Or modify
'it under the terms Of the GNU General Public License As published by
'the Free Software Foundation, either version 3 Of the License, Or
'(at your option) any later version.

'This program Is distributed In the hope that it will be useful,
'but WITHOUT ANY WARRANTY; without even the implied warranty Of
'MERCHANTABILITY Or FITNESS FOR A PARTICULAR PURPOSE.  See the
'GNU General Public License For more details.

'You should have received a copy Of the GNU General Public License
'along with this program. If Not, see < http: //www.gnu.org/licenses/>.


Imports System.Text
Imports System.Text.RegularExpressions

Public Class UCAutocomplete
    Private g_mFormMain As FormMain

    Public g_ClassToolTip As ClassToolTip
    Public g_sLastAutocompleteText As String = ""

    Public Sub New(f As FormMain)
        g_mFormMain = f

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call. 
        Label_IntelliSense.Name &= "@SetForeColorRoyalBlue"
        Label_Autocomplete.Name &= "@SetForeColorRoyalBlue"

        g_ClassToolTip = New ClassToolTip(Me)

        'Set double buffering to avoid annonying flickers when collapsing/showing SplitContainer panels
        ClassTools.ClassForms.SetDoubleBufferingAllChilds(Me, True)
        ClassTools.ClassForms.SetDoubleBufferingUnmanagedAllChilds(Me, True)

        ListView_AutocompleteList.ListViewItemSorter = New ListViewItemComparer(Me, 2)
        ListView_AutocompleteList.Sorting = SortOrder.Ascending
    End Sub

    Class ListViewItemComparer
        Implements IComparer

        Private g_mUCAutocomplete As UCAutocomplete
        Private g_Collum As Integer

        Public Sub New(c As UCAutocomplete)
            g_mUCAutocomplete = c
            g_Collum = 0
        End Sub

        Public Sub New(c As UCAutocomplete, mCollum As Integer)
            g_mUCAutocomplete = c
            g_Collum = mCollum
        End Sub

        Private Function IComparer_Compare(x As Object, y As Object) As Integer Implements IComparer.Compare
            If (g_mUCAutocomplete.g_sLastAutocompleteText.Length > 0) Then
                Dim mItemX As ListViewItem = DirectCast(x, ListViewItem)
                Dim mItemY As ListViewItem = DirectCast(y, ListViewItem)

                Dim iItemXIndex As Integer = mItemX.SubItems(g_Collum).Text.IndexOf(g_mUCAutocomplete.g_sLastAutocompleteText)
                Dim iItemYIndex As Integer = mItemY.SubItems(g_Collum).Text.IndexOf(g_mUCAutocomplete.g_sLastAutocompleteText)

                Return iItemXIndex.CompareTo(iItemYIndex)
            Else
                Dim mItemX As ListViewItem = DirectCast(x, ListViewItem)
                Dim mItemY As ListViewItem = DirectCast(y, ListViewItem)

                Return String.Compare(mItemX.SubItems(g_Collum).Text, mItemY.SubItems(g_Collum).Text)
            End If

        End Function
    End Class


    Public Function UpdateIntelliSense() As Boolean
        Dim sTextContent As String = CStr(Me.Invoke(Function() g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor.Document.TextContent))
        Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sTextContent)

        Dim iCaretOffset As Integer = CInt(Me.Invoke(Function() g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor.ActiveTextAreaControl.TextArea.Caret.Offset))
        If (iCaretOffset - 1 < 1) Then
            Return False
        End If

        If (Not mSourceAnalysis.m_IsRange(iCaretOffset) OrElse
                        mSourceAnalysis.m_InMultiComment(iCaretOffset) OrElse
                        mSourceAnalysis.m_InSingleComment(iCaretOffset)) Then
            Return False
        End If

        'Create a valid range to read the method name and for performance. 
        Dim mStringBuilder As New StringBuilder
        Dim iLastParenthesis As Integer = mSourceAnalysis.m_GetParenthesisLevel(iCaretOffset - 1)
        Dim i As Integer
        For i = iCaretOffset - 1 To 0 Step -1
            If (mSourceAnalysis.m_GetBraceLevel(i) < 1 OrElse
                        mSourceAnalysis.m_GetParenthesisLevel(i) < iLastParenthesis - 1) Then
                Exit For
            End If

            If (mSourceAnalysis.m_InNonCode(i)) Then
                Continue For
            End If

            If (mSourceAnalysis.m_GetParenthesisLevel(i) > iLastParenthesis - 1 OrElse
                        mSourceAnalysis.m_GetBracketLevel(i) > 0) Then
                Continue For
            End If

            Dim sChar As Char
            Dim bExitFor As Boolean = False

            Me.Invoke(Sub()
                          If (i > g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor.ActiveTextAreaControl.Document.TextLength - 1) Then
                              bExitFor = True
                              Return
                          End If

                          sChar = g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor.ActiveTextAreaControl.Document.GetCharAt(i)
                      End Sub)

            If (bExitFor) Then
                Exit For
            End If

            If (sChar = "("c OrElse sChar = ")"c OrElse
                        sChar = "["c OrElse sChar = "]"c) Then
                Continue For
            End If

            mStringBuilder.Append(sChar)
        Next

        Dim sTmp As String = StrReverse(mStringBuilder.ToString).Trim
        Dim sMethodStart As String = Regex.Match(sTmp, "((\b[a-zA-Z0-9_]+\b)(\.){0,1}(\b[a-zA-Z0-9_]+\b){0,1})$").Value

        Me.BeginInvoke(Sub()
                           g_ClassToolTip.m_CurrentMethod = sMethodStart
                           g_ClassToolTip.UpdateToolTip()
                       End Sub)
        Return True
    End Function


    Public Function UpdateAutocomplete(sText As String) As Integer
        If (String.IsNullOrEmpty(sText) OrElse sText.Length < 3 OrElse Regex.IsMatch(sText, "^[0-9]+$")) Then
            ListView_AutocompleteList.Items.Clear()
            ListView_AutocompleteList.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize)
            g_sLastAutocompleteText = ""
            Return 0
        End If

        Dim bSelectedWord As Boolean = g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor.ActiveTextAreaControl.TextArea.SelectionManager.HasSomethingSelected
        Dim lListViewItemsList As New List(Of ClassListViewItemData)

        Dim sAutocompleteArray As ClassSyntaxTools.STRUC_AUTOCOMPLETE() = ClassSyntaxTools.g_lAutocompleteList.ToArray
        For i = 0 To sAutocompleteArray.Length - 1
            If (bSelectedWord) Then
                If (sAutocompleteArray(i).m_FunctionName.Equals(sText)) Then
                    Dim mListViewItemData As New ClassListViewItemData(New String() {sAutocompleteArray(i).m_File,
                                                                                    sAutocompleteArray(i).GetTypeFullNames,
                                                                                    sAutocompleteArray(i).m_FunctionName,
                                                                                    sAutocompleteArray(i).m_FullFunctionName})

                    mListViewItemData.g_mData("File") = sAutocompleteArray(i).m_File
                    mListViewItemData.g_mData("TypeFullNames") = sAutocompleteArray(i).GetTypeFullNames
                    mListViewItemData.g_mData("FunctionName") = sAutocompleteArray(i).m_FunctionName
                    mListViewItemData.g_mData("FullFunctionName") = sAutocompleteArray(i).m_FullFunctionName
                    mListViewItemData.g_mData("Info") = sAutocompleteArray(i).m_Info

                    lListViewItemsList.Add(mListViewItemData)
                End If
            Else
                If (sAutocompleteArray(i).m_File.Equals(sText, If(ClassSettings.g_iSettingsAutocompleteCaseSensitive, StringComparison.Ordinal, StringComparison.OrdinalIgnoreCase)) OrElse
                            sAutocompleteArray(i).m_FunctionName.IndexOf(sText, If(ClassSettings.g_iSettingsAutocompleteCaseSensitive, StringComparison.Ordinal, StringComparison.OrdinalIgnoreCase)) > -1) Then
                    Dim mListViewItemData As New ClassListViewItemData(New String() {sAutocompleteArray(i).m_File,
                                                                                    sAutocompleteArray(i).GetTypeFullNames,
                                                                                    sAutocompleteArray(i).m_FunctionName,
                                                                                    sAutocompleteArray(i).m_FullFunctionName})

                    mListViewItemData.g_mData("File") = sAutocompleteArray(i).m_File
                    mListViewItemData.g_mData("TypeFullNames") = sAutocompleteArray(i).GetTypeFullNames
                    mListViewItemData.g_mData("FunctionName") = sAutocompleteArray(i).m_FunctionName
                    mListViewItemData.g_mData("FullFunctionName") = sAutocompleteArray(i).m_FullFunctionName
                    mListViewItemData.g_mData("Info") = sAutocompleteArray(i).m_Info

                    lListViewItemsList.Add(mListViewItemData)
                End If
            End If
        Next

        ListView_AutocompleteList.Items.Clear()
        ListView_AutocompleteList.Items.AddRange(lListViewItemsList.ToArray)

        If (ClassSettings.g_iSettingsSwitchTabToAutocomplete AndAlso g_mFormMain.TabControl_Details.SelectedTab.TabIndex <> 0 AndAlso lListViewItemsList.Count > 0) Then
            g_mFormMain.TabControl_Details.SuspendLayout()
            g_mFormMain.TabControl_Details.Enabled = False
            g_mFormMain.TabControl_Details.SelectTab(0)
            g_mFormMain.TabControl_Details.Enabled = True
            g_mFormMain.TabControl_Details.ResumeLayout()
        End If

        'Sort ascending first then match the closest one.
        g_sLastAutocompleteText = ""
        ListView_AutocompleteList.Sort()
        g_sLastAutocompleteText = sText
        ListView_AutocompleteList.Sort()


        If (ListView_AutocompleteList.Items.Count > 0) Then
            ListView_AutocompleteList.Items(0).Selected = True
            ListView_AutocompleteList.Items(0).EnsureVisible()

            ListView_AutocompleteList.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent)
        Else
            ListView_AutocompleteList.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize)
        End If

        Return ListView_AutocompleteList.Items.Count
    End Function

    Public Function GetSelectedItem() As ClassSyntaxTools.STRUC_AUTOCOMPLETE
        If (ListView_AutocompleteList.SelectedItems.Count < 1) Then
            Return Nothing
        End If

        If (TypeOf ListView_AutocompleteList.SelectedItems(0) IsNot ClassListViewItemData) Then
            Return Nothing
        End If

        Dim mListViewItemData = DirectCast(ListView_AutocompleteList.SelectedItems(0), ClassListViewItemData)

        Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE(CStr(mListViewItemData.g_mData("Info")),
                                                                     CStr(mListViewItemData.g_mData("File")),
                                                                     ClassSyntaxTools.STRUC_AUTOCOMPLETE.ParseTypeFullNames(CStr(mListViewItemData.g_mData("TypeFullNames"))),
                                                                     CStr(mListViewItemData.g_mData("FunctionName")),
                                                                     CStr(mListViewItemData.g_mData("FullFunctionName")))

        Return mAutocomplete
    End Function

    Private Sub ListView_AutocompleteList_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ListView_AutocompleteList.SelectedIndexChanged
        g_ClassToolTip.UpdateToolTip()
    End Sub

    Public Class ClassToolTip
        Public g_AutocompleteUC As UCAutocomplete

        Private g_sCurrentMethodName As String = ""

        Public Sub New(c As UCAutocomplete)
            g_AutocompleteUC = c
        End Sub

        Public Property m_CurrentMethod As String
            Get
                Return g_sCurrentMethodName
            End Get
            Set(value As String)
                g_sCurrentMethodName = value
                ' UpdateToolTip()
            End Set
        End Property

        Public Sub UpdateToolTip()
            If (Not ClassSettings.g_iSettingsEnableToolTip) Then
                g_AutocompleteUC.SplitContainer1.Panel2Collapsed = True
                Return
            End If

            'Dim sTipTitle As String = ""
            Dim SB_TipText_IntelliSense As New Text.StringBuilder
            Dim SB_TipText_Autocomplete As New Text.StringBuilder
            Dim SB_TipText_IntelliSenseToolTip As New Text.StringBuilder
            Dim SB_TipText_AutocompleteToolTip As New Text.StringBuilder

            Dim iTabSize As Integer = 4

            If (Not String.IsNullOrEmpty(g_sCurrentMethodName)) Then
                Dim sCurrentMethodName As String = g_sCurrentMethodName

                Dim bIsMethodMapEnd As Boolean = sCurrentMethodName.StartsWith("."c)
                If (bIsMethodMapEnd) Then
                    sCurrentMethodName = sCurrentMethodName.Remove(0, 1)
                End If

                Dim bIsMethodMap As Boolean = sCurrentMethodName.Contains("."c)

                Dim lAlreadyShownList As New List(Of String)

                Dim bPrintedInfo As Boolean = False
                Dim iPrintedItems As Integer = 0
                Dim iMaxPrintedItems As Integer = 3
                Dim sAutocompleteArray As ClassSyntaxTools.STRUC_AUTOCOMPLETE() = ClassSyntaxTools.g_lAutocompleteList.ToArray
                For i = 0 To sAutocompleteArray.Length - 1
                    If ((sAutocompleteArray(i).m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE AndAlso Not bIsMethodMap) Then
                        Continue For
                    End If

                    If (bIsMethodMap) Then
                        If (Not sAutocompleteArray(i).m_FunctionName.Equals(sCurrentMethodName)) Then
                            Continue For
                        End If
                    Else
                        If (Not sAutocompleteArray(i).m_FunctionName.Contains(sCurrentMethodName) OrElse
                                    Not Regex.IsMatch(sAutocompleteArray(i).m_FunctionName, String.Format("{0}\b{1}\b", If(bIsMethodMapEnd, "(\.)", ""), Regex.Escape(sCurrentMethodName)))) Then
                            Continue For
                        End If
                    End If


                    If (ClassSettings.g_iSettingsUseWindowsToolTip AndAlso Not bPrintedInfo) Then
                        bPrintedInfo = True
                        SB_TipText_IntelliSenseToolTip.AppendLine("IntelliSense:")
                    End If

                    Dim sName As String = Regex.Replace(sAutocompleteArray(i).m_FullFunctionName.Trim, vbTab, New String(" "c, iTabSize))
                    Dim sNameToolTip As String = Regex.Replace(sAutocompleteArray(i).m_FullFunctionName.Trim, vbTab, New String(" "c, iTabSize))

                    If (lAlreadyShownList.Contains(sName)) Then
                        Continue For
                    End If

                    lAlreadyShownList.Add(sName)

                    If (ClassSettings.g_iSettingsUseWindowsToolTip) Then
                        Dim sNewlineDistance As Integer = sNameToolTip.IndexOf("("c)

                        If (sNewlineDistance > -1) Then
                            Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sNameToolTip)
                            For ii = sNameToolTip.Length - 1 To 0 Step -1
                                If (sNameToolTip(ii) <> ","c OrElse mSourceAnalysis.m_InNonCode(ii)) Then
                                    Continue For
                                End If

                                sNameToolTip = sNameToolTip.Insert(ii + 1, Environment.NewLine & New String(" "c, sNewlineDistance))
                            Next
                        End If
                    End If

                    Dim sComment As String = Regex.Replace(sAutocompleteArray(i).m_Info.Trim, "^", New String(" "c, iTabSize), RegexOptions.Multiline)

                    SB_TipText_IntelliSense.AppendLine(sName)
                    If (ClassSettings.g_iSettingsToolTipMethodComments AndAlso Not String.IsNullOrEmpty(sComment.Trim)) Then
                        SB_TipText_IntelliSense.AppendLine(sComment)
                    End If
                    SB_TipText_IntelliSense.AppendLine()

                    If (iPrintedItems < iMaxPrintedItems) Then
                        SB_TipText_IntelliSenseToolTip.AppendLine(sNameToolTip)
                        If (ClassSettings.g_iSettingsToolTipMethodComments AndAlso Not String.IsNullOrEmpty(sComment.Trim)) Then
                            SB_TipText_IntelliSenseToolTip.AppendLine(sComment)
                        End If
                        SB_TipText_IntelliSenseToolTip.AppendLine()

                    ElseIf (iPrintedItems = iMaxPrintedItems) Then
                        SB_TipText_IntelliSenseToolTip.AppendLine("...")
                    End If

                    iPrintedItems += 1
                Next
            End If

            If (g_AutocompleteUC.ListView_AutocompleteList.SelectedItems.Count > 0 AndAlso
                        TypeOf g_AutocompleteUC.ListView_AutocompleteList.SelectedItems(0) Is ClassListViewItemData) Then
                Dim mListViewItemData = DirectCast(g_AutocompleteUC.ListView_AutocompleteList.SelectedItems(0), ClassListViewItemData)

                If (ClassSettings.g_iSettingsUseWindowsToolTip) Then
                    SB_TipText_AutocompleteToolTip.AppendLine("Autocomplete:")
                End If

                Dim sName As String = Regex.Replace(CStr(mListViewItemData.g_mData("FullFunctionName")).Trim, vbTab, New String(" "c, iTabSize))
                Dim sNameToolTip As String = Regex.Replace(CStr(mListViewItemData.g_mData("FullFunctionName")).Trim, vbTab, New String(" "c, iTabSize))
                If (ClassSettings.g_iSettingsUseWindowsToolTip) Then
                    Dim sNewlineDistance As Integer = sNameToolTip.IndexOf("("c)

                    If (sNewlineDistance > -1) Then
                        Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sNameToolTip)
                        For ii = sNameToolTip.Length - 1 To 0 Step -1
                            If (sNameToolTip(ii) <> ","c OrElse mSourceAnalysis.m_InNonCode(ii)) Then
                                Continue For
                            End If

                            sNameToolTip = sNameToolTip.Insert(ii + 1, Environment.NewLine & New String(" "c, sNewlineDistance))
                        Next
                    End If
                End If

                Dim sComment As String = Regex.Replace(CStr(mListViewItemData.g_mData("Info")).Trim, "^", New String(" "c, iTabSize), RegexOptions.Multiline)

                SB_TipText_Autocomplete.AppendLine(sName)
                SB_TipText_AutocompleteToolTip.AppendLine(sNameToolTip)

                If (ClassSettings.g_iSettingsToolTipAutocompleteComments AndAlso Not String.IsNullOrEmpty(sComment.Trim)) Then
                    SB_TipText_Autocomplete.AppendLine(sComment)
                    SB_TipText_AutocompleteToolTip.AppendLine(sComment)
                End If
            End If

            If (True) Then
                'UpdateToolTipFormLocation()

                If (ClassSettings.g_iSettingsUseWindowsToolTip) Then
                    g_AutocompleteUC.g_mFormMain.g_mFormToolTip.TextEditorControl_ToolTip.Document.TextContent = SB_TipText_IntelliSenseToolTip.ToString & SB_TipText_AutocompleteToolTip.ToString
                ElseIf (g_AutocompleteUC.g_mFormMain.g_mFormToolTip.TextEditorControl_ToolTip.Document.TextLength > 0) Then
                    g_AutocompleteUC.g_mFormMain.g_mFormToolTip.TextEditorControl_ToolTip.Document.TextContent = ""
                End If

                UpdateToolTipFormLocation()
            End If

            If (True) Then
                If (SB_TipText_IntelliSense.Length > 0) Then
                    g_AutocompleteUC.SplitContainer2.Panel1Collapsed = False
                    g_AutocompleteUC.RichTextBox_IntelliSense.Text = SB_TipText_IntelliSense.ToString
                Else
                    g_AutocompleteUC.SplitContainer2.Panel1Collapsed = True
                End If

                If (SB_TipText_Autocomplete.Length > 0) Then
                    g_AutocompleteUC.SplitContainer2.Panel2Collapsed = False
                    g_AutocompleteUC.RichTextBox_Autocomplete.Text = SB_TipText_Autocomplete.ToString
                Else
                    g_AutocompleteUC.SplitContainer2.Panel2Collapsed = True
                End If

                If (SB_TipText_IntelliSense.Length <> 0 OrElse SB_TipText_Autocomplete.Length <> 0) Then
                    If (g_AutocompleteUC.SplitContainer2.Orientation = Orientation.Horizontal) Then
                        g_AutocompleteUC.SplitContainer2.SplitterDistance = CInt(g_AutocompleteUC.SplitContainer2.Height / 2)
                    Else
                        g_AutocompleteUC.SplitContainer2.SplitterDistance = CInt(g_AutocompleteUC.SplitContainer2.Width / 2)
                    End If

                    g_AutocompleteUC.SplitContainer1.Panel2Collapsed = False
                Else
                    g_AutocompleteUC.SplitContainer1.Panel2Collapsed = True
                End If
            End If
        End Sub

        Public Sub UpdateToolTipFormLocation()
            If (ClassSettings.g_iSettingsUseWindowsToolTip) Then
                Dim iEditorX As Integer = g_AutocompleteUC.g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor.ActiveTextAreaControl.PointToScreen(Point.Empty).X
                Dim iEditorY As Integer = g_AutocompleteUC.g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor.ActiveTextAreaControl.PointToScreen(Point.Empty).Y
                Dim iEditorW As Integer = g_AutocompleteUC.g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor.ActiveTextAreaControl.Width
                Dim iEditorH As Integer = g_AutocompleteUC.g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor.ActiveTextAreaControl.Height

                Dim iX As Integer = g_AutocompleteUC.g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor.ActiveTextAreaControl.Caret.ScreenPosition.X + iEditorX
                Dim iY As Integer = g_AutocompleteUC.g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor.ActiveTextAreaControl.Caret.ScreenPosition.Y + iEditorY

                Dim iFontH As Integer = CInt(g_AutocompleteUC.g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor.ActiveTextAreaControl.Font.GetHeight)
                Dim iWTabSpace As Integer = 0
                Dim iHTabSpace As Integer = 6

                Dim iNewLocation As New Point(iX + iWTabSpace, iY + iHTabSpace + iFontH)
                Dim bOutsideEditor As Boolean = False
                If (iNewLocation.X < iEditorX OrElse iNewLocation.X > (iEditorX + iEditorW)) Then
                    bOutsideEditor = True
                End If
                If (iNewLocation.Y < iEditorY OrElse iNewLocation.Y > (iEditorY + iEditorH)) Then
                    bOutsideEditor = True
                End If

                If (Not bOutsideEditor AndAlso g_AutocompleteUC.g_mFormMain.g_mFormToolTip.TextEditorControl_ToolTip.Document.TextLength > 0) Then
                    g_AutocompleteUC.g_mFormMain.g_mFormToolTip.m_Location = iNewLocation

                    g_AutocompleteUC.g_mFormMain.g_mFormToolTip.Visible = True
                    g_AutocompleteUC.g_mFormMain.g_mFormToolTip.Refresh()
                Else
                    g_AutocompleteUC.g_mFormMain.g_mFormToolTip.Visible = False
                End If
            Else
                If (g_AutocompleteUC.g_mFormMain.g_mFormToolTip.Visible) Then
                    g_AutocompleteUC.g_mFormMain.g_mFormToolTip.Visible = False
                End If
            End If
        End Sub
    End Class

    Private Sub RichTextBox_IntelliSense_TextChanged(sender As Object, e As EventArgs) Handles RichTextBox_IntelliSense.TextChanged
        If (ClassTools.ClassOperatingSystem.GetWineVersion Is Nothing) Then
            Return
        End If

        'WINE BUG: Text color keeps resetting when text changes. Re-apply color on text change.
        RichTextBox_IntelliSense.BackColor = RichTextBox_IntelliSense.BackColor
        RichTextBox_IntelliSense.ForeColor = RichTextBox_IntelliSense.ForeColor
    End Sub

    Private Sub RichTextBox_Autocomplete_TextChanged(sender As Object, e As EventArgs) Handles RichTextBox_Autocomplete.TextChanged
        If (ClassTools.ClassOperatingSystem.GetWineVersion Is Nothing) Then
            Return
        End If

        'WINE BUG: Text color keeps resetting when text changes. Re-apply color on text change.
        RichTextBox_Autocomplete.BackColor = RichTextBox_Autocomplete.BackColor
        RichTextBox_Autocomplete.ForeColor = RichTextBox_Autocomplete.ForeColor
    End Sub
End Class
