﻿'BasicPawn
'Copyright(C) 2018 TheTimocop

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

#Const SEARCH_EVERYWHERE = False
#Const PROFILE_AUTOCOMPLETE = False

Imports System.Text
Imports System.Text.RegularExpressions

Public Class ClassAutocompleteUpdater
    Private g_mFormMain As FormMain
    Private g_mAutocompleteUpdaterThread As Threading.Thread
    Private _lock As New Object

    Public g_lFullAutocompleteTabRequests As New ClassSyncList(Of String)

    Public Event OnAutocompleteUpdateStarted(iUpdateType As ENUM_AUTOCOMPLETE_UPDATE_TYPE_FLAGS)
    Public Event OnAutocompleteUpdateEnd()
    Public Event OnAutocompleteUpdateAbort()

    Public Sub New(f As FormMain)
        g_mFormMain = f
    End Sub

    Enum ENUM_AUTOCOMPLETE_UPDATE_TYPE_FLAGS
        ALL = -1
        FULL_AUTOCOMPLETE = (1 << 0)
        VARIABLES_AUTOCOMPLETE = (1 << 1)
    End Enum

    ''' <summary>
    ''' Starts the autocomplete update thread
    ''' </summary>
    ''' <param name="iUpdateType"></param>
    ''' <param name="sTabIdentifier">The tab to request an update. |Nothing| for current active tab.</param>
    ''' <returns></returns>
    Public Function StartUpdate(iUpdateType As ENUM_AUTOCOMPLETE_UPDATE_TYPE_FLAGS, sTabIdentifier As String) As Boolean
        If (String.IsNullOrEmpty(sTabIdentifier)) Then
            sTabIdentifier = g_mFormMain.g_ClassTabControl.m_ActiveTab.m_Identifier
        End If

        If (Not ClassThread.IsValid(g_mAutocompleteUpdaterThread)) Then
            g_mAutocompleteUpdaterThread = New Threading.Thread(Sub()
                                                                    SyncLock _lock
                                                                        If ((iUpdateType And ENUM_AUTOCOMPLETE_UPDATE_TYPE_FLAGS.FULL_AUTOCOMPLETE) = ENUM_AUTOCOMPLETE_UPDATE_TYPE_FLAGS.FULL_AUTOCOMPLETE) Then
                                                                            RaiseEvent OnAutocompleteUpdateStarted(iUpdateType)
                                                                            FullAutocompleteUpdate_Thread(sTabIdentifier)
                                                                        End If

                                                                        If ((iUpdateType And ENUM_AUTOCOMPLETE_UPDATE_TYPE_FLAGS.VARIABLES_AUTOCOMPLETE) = ENUM_AUTOCOMPLETE_UPDATE_TYPE_FLAGS.VARIABLES_AUTOCOMPLETE) Then
                                                                            RaiseEvent OnAutocompleteUpdateStarted(iUpdateType)
                                                                            VariableAutocompleteUpdate_Thread(sTabIdentifier)
                                                                        End If
                                                                    End SyncLock

                                                                    RaiseEvent OnAutocompleteUpdateEnd()
                                                                End Sub) With {
                .Priority = Threading.ThreadPriority.Lowest,
                .IsBackground = True
            }
            g_mAutocompleteUpdaterThread.Start()

            If ((iUpdateType And ENUM_AUTOCOMPLETE_UPDATE_TYPE_FLAGS.FULL_AUTOCOMPLETE) = ENUM_AUTOCOMPLETE_UPDATE_TYPE_FLAGS.FULL_AUTOCOMPLETE) Then
                g_lFullAutocompleteTabRequests.Remove(sTabIdentifier)
            End If

            Return True
        Else
            'If (g_lFullAutocompleteTabRequests.Count < 1) Then
            '    g_mFormMain.PrintInformation("[INFO]", "Could not start autocomplete update thread, it's already running!", False, False)
            'End If

            If ((iUpdateType And ENUM_AUTOCOMPLETE_UPDATE_TYPE_FLAGS.FULL_AUTOCOMPLETE) = ENUM_AUTOCOMPLETE_UPDATE_TYPE_FLAGS.FULL_AUTOCOMPLETE) Then
                If (Not g_lFullAutocompleteTabRequests.Contains(sTabIdentifier)) Then
                    g_lFullAutocompleteTabRequests.Add(sTabIdentifier)
                End If
            End If

            Return False
        End If
    End Function

    ''' <summary>
    ''' Stops the autocomplete update thread
    ''' </summary>
    Public Sub StopUpdate()
        RaiseEvent OnAutocompleteUpdateAbort()

        ClassThread.Abort(g_mAutocompleteUpdaterThread)
    End Sub

    Private Sub FullAutocompleteUpdate_Thread(sTabIdentifier As String)
        Try
            'g_mFormMain.PrintInformation("[INFO]", "Autocomplete update started...")

            Dim sActiveTabIdentifier As String = ClassThread.ExecEx(Of String)(g_mFormMain, Function() g_mFormMain.g_ClassTabControl.m_ActiveTab.m_Identifier)
            Dim mTabs As ClassTabControl.SourceTabPage() = ClassThread.ExecEx(Of ClassTabControl.SourceTabPage())(g_mFormMain, Function() g_mFormMain.g_ClassTabControl.GetAllTabs())
            Dim mRequestTab As ClassTabControl.SourceTabPage = ClassThread.ExecEx(Of ClassTabControl.SourceTabPage)(g_mFormMain, Function() g_mFormMain.g_ClassTabControl.GetTabByIdentifier(sTabIdentifier))
            If (mRequestTab Is Nothing) Then
                ClassThread.ExecAsync(g_mFormMain, Sub()
                                                       g_mFormMain.ToolStripProgressBar_Autocomplete.ToolTipText = ""
                                                       g_mFormMain.ToolStripProgressBar_Autocomplete.Value = 100
                                                       g_mFormMain.ToolStripProgressBar_Autocomplete.Visible = False
                                                   End Sub)

                g_mFormMain.PrintInformation("[WARN]", "Autocomplete update failed! Could not get tab!", False, False)
                Return
            End If

            Dim sRequestedSourceFile As String = mRequestTab.m_File
            Dim iRequestedLangauge As ClassSyntaxTools.ENUM_LANGUAGE_TYPE = mRequestTab.m_Language
            Dim sRequestedSource As String = ClassThread.ExecEx(Of String)(mRequestTab, Function() mRequestTab.m_TextEditor.Document.TextContent)
            Dim mRequestedConfig As ClassConfigs.STRUC_CONFIG_ITEM = ClassThread.ExecEx(Of ClassConfigs.STRUC_CONFIG_ITEM)(mRequestTab, Function() mRequestTab.m_ActiveConfig)

            If (String.IsNullOrEmpty(sRequestedSourceFile) OrElse Not IO.File.Exists(sRequestedSourceFile)) Then
                ClassThread.ExecAsync(g_mFormMain, Sub()
                                                       g_mFormMain.ToolStripProgressBar_Autocomplete.ToolTipText = ""
                                                       g_mFormMain.ToolStripProgressBar_Autocomplete.Value = 100
                                                       g_mFormMain.ToolStripProgressBar_Autocomplete.Visible = False
                                                   End Sub)

                g_mFormMain.PrintInformation("[ERRO]", "Autocomplete update failed! Could not get current source file!", False, False)
                Return
            End If

            ClassThread.ExecAsync(g_mFormMain, Sub()
                                                   g_mFormMain.ToolStripProgressBar_Autocomplete.ToolTipText = IO.Path.GetFileName(sRequestedSourceFile)
                                                   g_mFormMain.ToolStripProgressBar_Autocomplete.Value = 0
                                                   g_mFormMain.ToolStripProgressBar_Autocomplete.Visible = True
                                               End Sub)

            Dim lNewAutocompleteList As New ClassSyncList(Of ClassSyntaxTools.STRUC_AUTOCOMPLETE)

            'Add debugger placeholder variables and methods
            lNewAutocompleteList.AddRange((New ClassDebuggerParser(g_mFormMain)).GetDebuggerAutocomplete)

            Dim lIncludeFiles As New List(Of DictionaryEntry)
            Dim lIncludeFilesFull As New List(Of DictionaryEntry)

            For Each sInclude In GetIncludeFiles(mRequestedConfig, sRequestedSource, sRequestedSourceFile, sRequestedSourceFile)
                lIncludeFiles.Add(New DictionaryEntry(sTabIdentifier, sInclude))
            Next
            For Each sInclude In GetIncludeFiles(mRequestedConfig, sRequestedSource, sRequestedSourceFile, sRequestedSourceFile, True)
                lIncludeFilesFull.Add(New DictionaryEntry(sTabIdentifier, sInclude))
            Next

            'Find main tab of include tabs
            If (True) Then
                Dim bRefIncludeAdded As Boolean = False

                Dim i As Integer
                For i = 0 To mTabs.Length - 1
                    If (mTabs(i).m_IsUnsaved) Then
                        Continue For
                    End If

                    If (mTabs(i).m_File.ToLower = sRequestedSourceFile.ToLower) Then
                        Continue For
                    End If

                    If (mTabs(i).m_IncludeFiles.Count < 1) Then
                        Continue For
                    End If

                    Dim sOtherTabIdentifier As String = mTabs(i).m_Identifier
                    Dim mIncludes As DictionaryEntry() = mTabs(i).m_IncludeFiles.ToArray
                    Dim bIsMain As Boolean = False

                    Dim j As Integer
                    For j = 0 To mIncludes.Length - 1
                        'Only check orginal includes, skip other ones
                        If (CStr(mIncludes(j).Key) <> sOtherTabIdentifier) Then
                            Continue For
                        End If

                        If (CStr(mIncludes(j).Value).ToLower <> sRequestedSourceFile.ToLower) Then
                            Continue For
                        End If

                        bIsMain = True
                        Exit For
                    Next

                    If (Not bIsMain) Then
                        Continue For
                    End If

                    For j = 0 To mIncludes.Length - 1
                        'Only check orginal includes, skip other ones
                        If (CStr(mIncludes(j).Key) <> sOtherTabIdentifier) Then
                            Continue For
                        End If

                        If (Not lIncludeFiles.Exists(Function(x As DictionaryEntry) CStr(x.Value).ToLower = CStr(mIncludes(j).Value).ToLower)) Then
                            lIncludeFiles.Add(New DictionaryEntry(sOtherTabIdentifier, mIncludes(j).Value))
                            bRefIncludeAdded = True
                        End If

                        If (Not lIncludeFilesFull.Exists(Function(x As DictionaryEntry) CStr(x.Value).ToLower = CStr(mIncludes(j).Value).ToLower)) Then
                            lIncludeFilesFull.Add(New DictionaryEntry(sOtherTabIdentifier, mIncludes(j).Value))
                            bRefIncludeAdded = True
                        End If
                    Next
                Next

                ''mRequestTab.m_HasReferenceIncludes' Calls UI elements, invoke it.
                ClassThread.ExecAsync(mRequestTab, Sub()
                                                       mRequestTab.m_HasReferenceIncludes = bRefIncludeAdded
                                                   End Sub)
            End If

            'Save includes first, they wont be modified below this anyways
            mRequestTab.m_IncludeFiles.DoSync(
                Sub()
                    mRequestTab.m_IncludeFiles.Clear()
                    mRequestTab.m_IncludeFiles.AddRange(lIncludeFiles.ToArray)
                End Sub)

            mRequestTab.m_IncludeFilesFull.DoSync(
                Sub()
                    mRequestTab.m_IncludeFilesFull.Clear()
                    mRequestTab.m_IncludeFilesFull.AddRange(lIncludeFilesFull.ToArray)
                End Sub)

            'Add preprocessor stuff
            lNewAutocompleteList.AddRange(GetPreprocessorKeywords(lIncludeFilesFull.ToArray))

            'Detect current mod type...
            If (mRequestedConfig.g_iLanguage = ClassConfigs.STRUC_CONFIG_ITEM.ENUM_LANGUAGE_DETECT_TYPE.AUTO_DETECT) Then
                Dim iLanguage As ClassSyntaxTools.ENUM_LANGUAGE_TYPE = CType(-1, ClassSyntaxTools.ENUM_LANGUAGE_TYPE)

                For i = 0 To lIncludeFiles.Count - 1
                    '... by includes
                    Select Case (IO.Path.GetFileName(CStr(lIncludeFiles(i).Value)).ToLower)
                        Case "sourcemod.inc"
                            iLanguage = ClassSyntaxTools.ENUM_LANGUAGE_TYPE.SOURCEPAWN
                            Exit For

                        Case "amxmodx.inc"
                            iLanguage = ClassSyntaxTools.ENUM_LANGUAGE_TYPE.AMXMODX
                            Exit For
                    End Select

                    '... by extension
                    Select Case (IO.Path.GetExtension(CStr(lIncludeFiles(i).Value)).ToLower)
                        Case ".sp"
                            iLanguage = ClassSyntaxTools.ENUM_LANGUAGE_TYPE.SOURCEPAWN
                            Exit For

                        Case ".sma"
                            iLanguage = ClassSyntaxTools.ENUM_LANGUAGE_TYPE.AMXMODX
                            Exit For

                        Case ".p", ".pwn"
                            iLanguage = ClassSyntaxTools.ENUM_LANGUAGE_TYPE.PAWN
                            Exit For
                    End Select
                Next

                If (iRequestedLangauge <> iLanguage) Then
                    Select Case (iLanguage)
                        Case ClassSyntaxTools.ENUM_LANGUAGE_TYPE.SOURCEPAWN
                            g_mFormMain.PrintInformation("[INFO]", String.Format("Auto-Detected language: SourcePawn ({0})", IO.Path.GetFileName(sRequestedSourceFile)))

                        Case ClassSyntaxTools.ENUM_LANGUAGE_TYPE.AMXMODX
                            g_mFormMain.PrintInformation("[INFO]", String.Format("Auto-Detected language: AMX Mod X ({0})", IO.Path.GetFileName(sRequestedSourceFile)))

                        Case ClassSyntaxTools.ENUM_LANGUAGE_TYPE.PAWN
                            g_mFormMain.PrintInformation("[INFO]", String.Format("Auto-Detected language: Pawn ({0})", IO.Path.GetFileName(sRequestedSourceFile)))

                        Case Else
                            g_mFormMain.PrintInformation("[WARN]", String.Format("Auto-Detected language: Unknown ({0})", IO.Path.GetFileName(sRequestedSourceFile)))
                    End Select
                End If

                If (iLanguage > -1) Then
                    iRequestedLangauge = iLanguage
                End If
            Else
                Select Case (mRequestedConfig.g_iLanguage)
                    Case ClassConfigs.STRUC_CONFIG_ITEM.ENUM_LANGUAGE_DETECT_TYPE.SOURCEPAWN
                        iRequestedLangauge = ClassSyntaxTools.ENUM_LANGUAGE_TYPE.SOURCEPAWN

                    Case ClassConfigs.STRUC_CONFIG_ITEM.ENUM_LANGUAGE_DETECT_TYPE.AMXMODX
                        iRequestedLangauge = ClassSyntaxTools.ENUM_LANGUAGE_TYPE.AMXMODX
                End Select
            End If

            'Set mod type
            mRequestTab.m_Language = iRequestedLangauge

            Dim mPreWatch As New Stopwatch
            Dim mPostWatch As New Stopwatch
            Dim mFinalizeWatch As New Stopwatch
            Dim mApplyWatch As New Stopwatch

            'Parse everything. Methods etc.
            If (True) Then
                Dim sSourceList As New ClassSyncList(Of String())

                mPreWatch.Start()
                Dim i As Integer
                For i = 0 To lIncludeFiles.Count - 1
                    ParseAutocomplete_Pre(sRequestedSource, sRequestedSourceFile, CStr(lIncludeFiles(i).Value), sSourceList, lNewAutocompleteList, iRequestedLangauge)

                    ClassThread.ExecAsync(g_mFormMain, Sub()
                                                           Dim iVal = Math.Min(CInt(Math.Floor(((i / lIncludeFiles.Count) * 100) * 0.5)), 100)

                                                           If (iVal > g_mFormMain.ToolStripProgressBar_Autocomplete.Value) Then
                                                               g_mFormMain.ToolStripProgressBar_Autocomplete.Value = iVal
                                                           End If
                                                       End Sub)
                Next
                mPreWatch.Stop()

                mPostWatch.Start()
                For i = 0 To sSourceList.Count - 1
                    ParseAutocomplete_Post(sRequestedSource, sRequestedSourceFile, sSourceList(i)(0), sSourceList(i)(1), lNewAutocompleteList, iRequestedLangauge)

                    ClassThread.ExecAsync(g_mFormMain, Sub()
                                                           Dim iVal = Math.Min(CInt(Math.Floor(((i / sSourceList.Count) * 100) * 0.5) + 50), 100)

                                                           If (iVal > g_mFormMain.ToolStripProgressBar_Autocomplete.Value) Then
                                                               g_mFormMain.ToolStripProgressBar_Autocomplete.Value = iVal
                                                           End If
                                                       End Sub)
                Next
                mPostWatch.Stop()
            End If

            'Finalize Methodmaps
            mFinalizeWatch.Start()
            FinalizeAutocompleteMethodmap(lNewAutocompleteList)
            mFinalizeWatch.Stop()

            'Save everything and update syntax 
            mApplyWatch.Start()
            mRequestTab.m_AutocompleteItems.DoSync(
                Sub()
                    mRequestTab.m_AutocompleteItems.RemoveAll(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) <> ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE)
                    mRequestTab.m_AutocompleteItems.AddRange(lNewAutocompleteList.ToArray)
                End Sub)
            mApplyWatch.Stop()

            'Dont spam the user with UI updates, only on active tabs
            If (sActiveTabIdentifier = sTabIdentifier) Then
                ClassThread.ExecAsync(g_mFormMain, Sub()
                                                       'Dont move this outside of invoke! Results in "File is already in use!" when aborting the thread... for some reason...
                                                       g_mFormMain.g_ClassSyntaxTools.g_ClassSyntaxHighlighting.UpdateSyntax(ClassSyntaxTools.ENUM_SYNTAX_UPDATE_TYPE.AUTOCOMPLETE)
                                                       g_mFormMain.g_ClassSyntaxTools.g_ClassSyntaxHighlighting.UpdateTextEditorSyntax()
                                                   End Sub)

                ClassThread.ExecAsync(g_mFormMain.g_mUCObjectBrowser, Sub()
                                                                          g_mFormMain.g_mUCObjectBrowser.StartUpdate()
                                                                      End Sub)
            End If


            ClassThread.ExecAsync(g_mFormMain, Sub()
                                                   g_mFormMain.ToolStripProgressBar_Autocomplete.ToolTipText = ""
                                                   g_mFormMain.ToolStripProgressBar_Autocomplete.Value = 100
                                                   g_mFormMain.ToolStripProgressBar_Autocomplete.Visible = False
                                               End Sub)

            lNewAutocompleteList = Nothing

#If DEBUG AndAlso PROFILE_AUTOCOMPLETE Then
            g_mFormMain.PrintInformation("[DEBG]", "Autocomplete update finished!")
            g_mFormMain.PrintInformation("[DEBG]", vbTab & "Times:")
            g_mFormMain.PrintInformation("[DEBG]", vbTab & "Pre: " & mPreWatch.Elapsed.ToString)
            g_mFormMain.PrintInformation("[DEBG]", vbTab & "Post: " & mPostWatch.Elapsed.ToString)
            g_mFormMain.PrintInformation("[DEBG]", vbTab & "Finalize: " & mFinalizeWatch.Elapsed.ToString)
            g_mFormMain.PrintInformation("[DEBG]", vbTab & "Apply: " & mApplyWatch.Elapsed.ToString)
#End If
            'g_mFormMain.PrintInformation("[INFO]", "Autocomplete update finished!")
        Catch ex As Threading.ThreadAbortException
            Throw
        Catch ex As Exception
            ClassThread.ExecAsync(g_mFormMain, Sub()
                                                   g_mFormMain.ToolStripProgressBar_Autocomplete.ToolTipText = ""
                                                   g_mFormMain.ToolStripProgressBar_Autocomplete.Value = 100
                                                   g_mFormMain.ToolStripProgressBar_Autocomplete.Visible = False
                                               End Sub)

            g_mFormMain.PrintInformation("[ERRO]", "Autocomplete update failed! " & ex.Message, False, False)
            ClassExceptionLog.WriteToLog(ex)
        End Try
    End Sub

    Private Function GetPreprocessorKeywords(mIncludes As DictionaryEntry()) As ClassSyntaxTools.STRUC_AUTOCOMPLETE()
        Dim lTmpAutoList As New List(Of ClassSyntaxTools.STRUC_AUTOCOMPLETE)

        Dim mDic As New Dictionary(Of String, String)
        Dim mDicOp As New Dictionary(Of String, String)

        'Pawn
        mDic("assert") = "#assert"
        mDic("define") = "#define <name>"
        mDic("emit") = "#emit <opcode>"
        mDic("endinput") = "#endinput"
        mDic("endscript") = "#endscript"
        mDic("error") = "#error <message>"
        mDic("warning") = "#warning <message>"
        mDic("file") = "#file <filepath>"
        mDic("line") = "#line <value>"
        mDic("section") = "#section <name>"
        mDic("include") = "#include <filename>"
        mDic("tryinclude") = "#tryinclude <filename>"
        mDic("undef") = "#undef <name>"

        mDic("pragma") = "#pragma <keyvalue> <value>"
        mDic("pragma align") = "#pragma align"
        mDic("pragma amxram") = "#pragma amxram <value>"
        mDic("pragma amxlimit") = "#pragma amxlimit <value>"
        mDic("pragma codepage") = "#pragma codepage <name/value>"
        mDic("pragma compress") = "#pragma compress <1/0>"
        mDic("pragma ctrlchar") = "#pragma ctrlchar <value>"
        mDic("pragma deprecated") = "#pragma deprecated <value>"
        mDic("pragma dynamic") = "#pragma dynamic <value>"
        mDic("pragma pack") = "#pragma pack <1/0>"
        mDic("pragma rational") = "#pragma rational <tagname(value)>"
        mDic("pragma semicolon") = "#pragma semicolon <1/0>"
        mDic("pragma tabsize") = "#pragma tabsize <value>"
        mDic("pragma unused") = "#pragma unused <symbol,...>"

        'AMX Mod X
        mDic("pragma library") = "#pragma library <library>"
        mDic("pragma reqlib") = "#pragma reqlib <library>"
        mDic("pragma loadlib") = "#pragma loadlib <library>"
        mDic("pragma reqclass") = "#pragma reqclass <class>"
        mDic("pragma defclasslib") = "#pragma defclasslib <class> <library>"
        mDic("pragma explib") = "#pragma explib <symbol> <symbol>" 'TODO: Wtf are those and what arguments? Never been used anywhere, only found in the compiler source.
        mDic("pragma expclass") = "#pragma expclass <symbol> <symbol>" 'TODO: Wtf are those and what arguments? Never been used anywhere, only found in the compiler source.

        'SourcePawn
        'NOTE: SourcePawn has alot of removed pragmas
        mDic("pragma newdecls") = "#pragma newdecls <required/optional>"
        mDic("pragma newdecls required") = "#pragma newdecls <required/optional>"
        mDic("pragma newdecls optional") = "#pragma newdecls <required/optional>"

        For i = 0 To mIncludes.Length - 1
            Dim sKey As String = String.Format("include <{0}>", IO.Path.GetFileNameWithoutExtension(CStr(mIncludes(i).Value)))
            Dim sValue As String = String.Format("#include <{0}>", IO.Path.GetFileNameWithoutExtension(CStr(mIncludes(i).Value)))
            mDic(sKey) = sValue
        Next

        'Operators
        mDicOp("char") = "<exp> char"
        mDicOp("defined") = "defined <symbol>"
        mDicOp("sizeof") = "sizeof <symbol>"
        mDicOp("state") = "state <symbol>"
        mDicOp("tagof") = "tagof <symbol>"

        For Each mItem In mDic
            lTmpAutoList.Add(New ClassSyntaxTools.STRUC_AUTOCOMPLETE("", "compiler.exe", "", ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.PREPROCESSOR, mItem.Key, mItem.Key, mItem.Value))
        Next

        For Each mItem In mDicOp
            lTmpAutoList.Add(New ClassSyntaxTools.STRUC_AUTOCOMPLETE("", "compiler.exe", "", ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.OPERATOR, mItem.Key, mItem.Key, mItem.Value))
        Next

        Return lTmpAutoList.ToArray
    End Function

    ''' <summary>
    ''' Merges all methods etc. methodmaps with parent methodmaps
    ''' </summary>
    ''' <param name="lTmpAutoList"></param>
    Private Sub FinalizeAutocompleteMethodmap(lTmpAutoList As ClassSyncList(Of ClassSyntaxTools.STRUC_AUTOCOMPLETE))
        'Merging:
        '     methodmap Test1 : Handle {...}
        '     methodmap Test2 : Test1 {...}     (All Test1 methods etc. will be added to Test2, because its a child of Test1)
        '   to
        '       Test1.Close
        '       Test2.Close
        If (True) Then
            Dim lTmpAutoAddList As New List(Of ClassSyntaxTools.STRUC_AUTOCOMPLETE)

            For i = lTmpAutoList.Count - 1 To 0 Step -1
                If ((lTmpAutoList(i).m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP) <> ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP OrElse
                            Not lTmpAutoList(i).m_FunctionString.Contains("."c)) Then
                    Continue For
                End If

                Dim sMethodmapName As String = CStr(lTmpAutoList(i).m_Data("MethodmapName"))
                Dim sMethodmapParentName As String = CStr(lTmpAutoList(i).m_Data("MethodmapParentName"))
                If (String.IsNullOrEmpty(sMethodmapName) OrElse String.IsNullOrEmpty(sMethodmapParentName)) Then
                    Continue For
                End If

                Dim iOldNextParent As String = sMethodmapParentName

                While (Not String.IsNullOrEmpty(iOldNextParent))
                    Dim sNextParent As String = ""

                    For ii = 0 To lTmpAutoList.Count - 1
                        If ((lTmpAutoList(ii).m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP) <> ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP OrElse
                                    Not lTmpAutoList(ii).m_FunctionString.Contains("."c)) Then
                            Continue For
                        End If

                        'TODO: Dont use yet, make methodmap parsing more efficent first
                        'If ((lTmpAutoList(ii).m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.STATIC) <> ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.STATIC) Then
                        '    Continue For
                        'End If

                        Dim sParentMethodmapName As String = CStr(lTmpAutoList(ii).m_Data("MethodmapName"))
                        Dim sParentMethodmapParentName As String = CStr(lTmpAutoList(ii).m_Data("MethodmapParentName"))
                        Dim sParentMethodmapMethodName As String = CStr(lTmpAutoList(ii).m_Data("MethodmapMethodName"))
                        If (String.IsNullOrEmpty(sParentMethodmapName) OrElse String.IsNullOrEmpty(sParentMethodmapMethodName)) Then
                            Continue For
                        End If

                        If (iOldNextParent <> sParentMethodmapName) Then
                            Continue For
                        End If

                        sNextParent = sParentMethodmapParentName

                        Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE(lTmpAutoList(ii).m_Info,
                                                                                    lTmpAutoList(ii).m_Filename,
                                                                                    lTmpAutoList(ii).m_Path,
                                                                                    lTmpAutoList(ii).m_Type,
                                                                                    sParentMethodmapMethodName,
                                                                                    String.Format("{0}.{1}", sMethodmapName, sParentMethodmapMethodName),
                                                                                    lTmpAutoList(ii).m_FullFunctionString)

                        For Each mData In lTmpAutoList(ii).m_Data
                            mAutocomplete.m_Data(mData.Key) = mData.Value
                        Next

                        mAutocomplete.m_Data("VariableMethodmapName") = sParentMethodmapName
                        mAutocomplete.m_Data("VariableMethodmapMethod") = sParentMethodmapMethodName

#If DEBUG Then
                        mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Merges all methods to methodmaps"
#End If

                        If (Not lTmpAutoList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString) AndAlso
                                Not lTmpAutoAddList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                            lTmpAutoAddList.Add(mAutocomplete)
                        End If
                    Next

                    iOldNextParent = sNextParent
                End While
            Next

            lTmpAutoList.AddRange(lTmpAutoAddList.ToArray)
        End If

        'Merging:
        '       methodmap Test1 : Handle = Test2 Method() {...}
        '   to
        '       Method.Close
        If (True) Then
            Dim lTmpAutoAddList As New List(Of ClassSyntaxTools.STRUC_AUTOCOMPLETE)

            For i = lTmpAutoList.Count - 1 To 0 Step -1
                If ((lTmpAutoList(i).m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP) <> ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP OrElse
                            Not lTmpAutoList(i).m_FunctionString.Contains("."c)) Then
                    Continue For
                End If

                Dim sMethodmapMethodName As String = CStr(lTmpAutoList(i).m_Data("MethodmapMethodName"))
                Dim sMethodmapMethodTag As String = CStr(lTmpAutoList(i).m_Data("MethodmapMethodTag"))
                If (String.IsNullOrEmpty(sMethodmapMethodName) OrElse String.IsNullOrEmpty(sMethodmapMethodTag)) Then
                    Continue For
                End If

                Dim iOldNextParent As String = sMethodmapMethodTag

                While (Not String.IsNullOrEmpty(iOldNextParent))
                    Dim sNextParent As String = ""

                    For ii = 0 To lTmpAutoList.Count - 1
                        If ((lTmpAutoList(ii).m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP) <> ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP OrElse
                                    Not lTmpAutoList(ii).m_FunctionString.Contains("."c)) Then
                            Continue For
                        End If

                        'TODO: Dont use yet, make methodmap parsing more efficent first
                        'If ((lTmpAutoList(ii).m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.STATIC) <> ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.STATIC) Then
                        '    Continue For
                        'End If

                        Dim sParentMethodmapName As String = CStr(lTmpAutoList(ii).m_Data("MethodmapName"))
                        Dim sParentMethodmapParentName As String = CStr(lTmpAutoList(ii).m_Data("MethodmapParentName"))
                        Dim sParentMethodmapMethodName As String = CStr(lTmpAutoList(ii).m_Data("MethodmapMethodName"))
                        If (String.IsNullOrEmpty(sParentMethodmapName) OrElse String.IsNullOrEmpty(sParentMethodmapMethodName)) Then
                            Continue For
                        End If

                        If (iOldNextParent <> sParentMethodmapName) Then
                            Continue For
                        End If

                        sNextParent = sParentMethodmapParentName

                        Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE(lTmpAutoList(ii).m_Info,
                                                                                    lTmpAutoList(ii).m_Filename,
                                                                                    lTmpAutoList(ii).m_Path,
                                                                                    lTmpAutoList(ii).m_Type,
                                                                                    sParentMethodmapMethodName,
                                                                                    String.Format("{0}.{1}", sMethodmapMethodName, sParentMethodmapMethodName),
                                                                                    lTmpAutoList(ii).m_FullFunctionString)

                        For Each mData In lTmpAutoList(ii).m_Data
                            mAutocomplete.m_Data(mData.Key) = mData.Value
                        Next

                        mAutocomplete.m_Data("VariableMethodmapName") = sMethodmapMethodName
                        mAutocomplete.m_Data("VariableMethodmapMethod") = sParentMethodmapMethodName

#If DEBUG Then
                        mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Merges all methodmaps with parent methodmaps"
#End If

                        If (Not lTmpAutoList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString) AndAlso
                                Not lTmpAutoAddList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                            lTmpAutoAddList.Add(mAutocomplete)
                        End If
                    Next

                    iOldNextParent = sNextParent
                End While
            Next

            lTmpAutoList.AddRange(lTmpAutoAddList.ToArray)
        End If
    End Sub

    Private Function GetTypeNamesToPattern(lTmpAutoList As ClassSyncList(Of ClassSyntaxTools.STRUC_AUTOCOMPLETE)) As String
        Return String.Format("(\b{0}\b)", String.Join("\b|\b", GetTypeNames(lTmpAutoList)))
    End Function

    Private Function GetTypeNames(lTmpAutoList As ClassSyncList(Of ClassSyntaxTools.STRUC_AUTOCOMPLETE)) As String()
        Dim lNames As New List(Of String) From {
            "void", 'For >1.7
            "int",
            "bool",
            "float",
            "char",
            "function",
            "object",
            "null_t",
            "nullfunc_t",
            "__nullable__",
 _
            "_", 'For <1.6
            "any",
            "bool",
            "Float",
            "String",
            "Function"
        }

        lTmpAutoList.ForEach(Sub(j As ClassSyntaxTools.STRUC_AUTOCOMPLETE)
                                 Dim sName As String = Nothing

                                 Select Case (True)
                                     Case ((j.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.ENUM) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.ENUM)
                                         sName = CStr(j.m_Data("EnumName"))
                                     Case ((j.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.STRUCT) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.STRUCT)
                                         sName = CStr(j.m_Data("StructName"))
                                     Case ((j.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCENUM) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCENUM)
                                         sName = CStr(j.m_Data("FuncenumName"))
                                     Case ((j.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCTAG) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCTAG)
                                         sName = CStr(j.m_Data("FunctagName"))
                                     Case ((j.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPEDEF) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPEDEF)
                                         sName = CStr(j.m_Data("TypedefName"))
                                     Case ((j.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPESET) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPESET)
                                         sName = CStr(j.m_Data("TypesetName"))
                                     Case Else
                                         Return
                                 End Select

                                 If (String.IsNullOrEmpty(sName)) Then
                                     Return
                                 End If

                                 If (sName.Contains("."c)) Then
                                     Return
                                 End If

                                 If (lNames.Contains(sName)) Then
                                     Return
                                 End If

                                 lNames.Add(sName)
                             End Sub)

        Return lNames.ToArray
    End Function

    Public Sub CleanUpNewLinesSource(ByRef sSource As String, iLanguage As ClassSyntaxTools.ENUM_LANGUAGE_TYPE)
        'Remove new lines e.g: MyStuff \
        '                      MyStuff2
        sSource = Regex.Replace(sSource, "\\\s*\n\s*", "")

        If (True) Then
            'From:
            '   public
            '       static
            '           bla()
            'To:
            '   public static bla()
            Dim sTypes As String() = {"enum", "funcenum", "functag", "stock", "static", "const", "public", "native", "forward", "typeset", "methodmap", "typedef"}
            Dim mRegMatchCol As MatchCollection = Regex.Matches(sSource, String.Format("^\s*(\b{0}\b)(?<Space>\s*\n\s*)", String.Join("\b|\b", sTypes)), RegexOptions.Multiline)
            Dim mSourceBuilder As New StringBuilder(sSource)

            For i = mRegMatchCol.Count - 1 To 0 Step -1
                Dim mMatch As Match = mRegMatchCol(i)
                If (Not mMatch.Groups("Space").Success) Then
                    Continue For
                End If

                mSourceBuilder = mSourceBuilder.Remove(mMatch.Groups("Space").Index, mMatch.Groups("Space").Length)
                mSourceBuilder = mSourceBuilder.Insert(mMatch.Groups("Space").Index, " "c)
            Next

            sSource = mSourceBuilder.ToString
        End If

        If (True) Then
            Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sSource, iLanguage)
            Dim mSourceBuilder As New StringBuilder(sSource)

            'Filter new lines in statements with parenthesis e.g: MyStuff(MyArg1,
            '                                                               MyArg2)
            For i = mSourceBuilder.Length - 1 To 0 Step -1
                Select Case (mSourceBuilder(i))
                    Case vbLf(0)
                        If (mSourceAnalysis.m_InNonCode(i)) Then
                            Exit Select
                        End If

                        If (mSourceAnalysis.GetParenthesisLevel(i, Nothing) > 0) Then
                            Select Case (True)
                                Case i > 1 AndAlso mSourceBuilder(i - 1) = vbCr
                                    mSourceBuilder = mSourceBuilder.Remove(i - 1, 2)
                                    mSourceBuilder = mSourceBuilder.Insert(i - 1, New String(" "c, 2))
                                Case Else
                                    mSourceBuilder = mSourceBuilder.Remove(i, 1)
                                    mSourceBuilder = mSourceBuilder.Insert(i, New String(" "c, 1))
                            End Select
                        End If

                    Case Else
                        'If we collapse statements etc., we need to remove single line comments!
                        'if(
                        '       //This my cause problems!
                        '   )
                        'Results in:
                        'if(//This my cause problems!)
                        'Just remove the comments
                        If (mSourceAnalysis.GetParenthesisLevel(i, Nothing) > 0 AndAlso mSourceAnalysis.m_InSingleComment(i)) Then
                            mSourceBuilder = mSourceBuilder.Remove(i, 1)
                            mSourceBuilder = mSourceBuilder.Insert(i, " "c)
                        End If

                End Select
            Next

            sSource = mSourceBuilder.ToString
        End If
    End Sub

    Private Sub ParseAutocomplete_Pre(sActiveSource As String,
                                      sActiveSourceFile As String,
                                      sFile As String,
                                      ByRef sSourceList As ClassSyncList(Of String()),
                                      ByRef lNewAutocompleteList As ClassSyncList(Of ClassSyntaxTools.STRUC_AUTOCOMPLETE),
                                      iLanguage As ClassSyntaxTools.ENUM_LANGUAGE_TYPE)
        Dim sSource As String
        If (sActiveSourceFile.ToLower = sFile.ToLower) Then
            sSource = sActiveSource
        Else
            sSource = IO.File.ReadAllText(sFile)
        End If

        CleanUpNewLinesSource(sSource, iLanguage)

        Dim lBraceLocList As New List(Of Integer())

        'Fix function spaces
        If (True) Then
            Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sSource, iLanguage)
            Dim mSourceBuilder As New StringBuilder(sSource)

            Dim iParentStart As Integer = 0
            Dim iParentLen As Integer = 0
            For i = 0 To mSourceBuilder.Length - 1
                Dim iParentRange As ClassSyntaxTools.ClassSyntaxSourceAnalysis.ENUM_STATE_RANGE
                Dim iParentLevel As Integer = mSourceAnalysis.GetParenthesisLevel(i, iParentRange)
                Select Case (iParentRange)
                    Case ClassSyntaxTools.ClassSyntaxSourceAnalysis.ENUM_STATE_RANGE.START
                        If (iParentLevel - 1 = 0) Then
                            iParentStart = i
                        End If

                    Case ClassSyntaxTools.ClassSyntaxSourceAnalysis.ENUM_STATE_RANGE.END
                        If (iParentLevel - 1 = 0) Then
                            iParentLen = i - iParentStart + 1
                            lBraceLocList.Add(New Integer() {iParentStart, iParentLen})
                        End If

                End Select
            Next

            For i = lBraceLocList.Count - 1 To 0 Step -1
                iParentStart = lBraceLocList(i)(0)
                iParentLen = lBraceLocList(i)(1)

                Dim sBraceString As String = mSourceBuilder.ToString(iParentStart, iParentLen)
                Dim mRegMatchCol As MatchCollection = Regex.Matches(sBraceString, "\s+")

                For ii = mRegMatchCol.Count - 1 To 0 Step -1
                    Dim mMatch As Match = mRegMatchCol(ii)
                    If (Not mMatch.Success) Then
                        Continue For
                    End If

                    Dim iOffset As Integer = iParentStart + mMatch.Index
                    Dim bContinue As Boolean = False
                    If (mSourceAnalysis.m_InNonCode(iOffset)) Then
                        Continue For
                    End If

                    sBraceString = sBraceString.Remove(mMatch.Index, mMatch.Length)
                    sBraceString = sBraceString.Insert(mMatch.Index, " "c)
                Next

                mSourceBuilder = mSourceBuilder.Remove(iParentStart, iParentLen)
                mSourceBuilder = mSourceBuilder.Insert(iParentStart, sBraceString)
            Next

            sSource = mSourceBuilder.ToString
        End If


        'Get strucs (names only)
        If (sSource.Contains("struct")) Then
            Dim mSourceBuilder As New StringBuilder(sSource.Length)

            If (True) Then
                Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sSource, iLanguage)

                For i = 0 To sSource.Length - 1
                    mSourceBuilder.Append(If(mSourceAnalysis.m_InNonCode(i), " "c, sSource(i)))
                Next
            End If

            Dim mPossibleStructMatches As MatchCollection = Regex.Matches(mSourceBuilder.ToString, "^\s*\b(struct)\b\s+(?<Name>\b[a-zA-Z0-9_]+\b)", RegexOptions.Multiline)

            Dim mMatch As Match

            For i = 0 To mPossibleStructMatches.Count - 1
                mMatch = mPossibleStructMatches(i)

                If (Not mMatch.Success) Then
                    Continue For
                End If

                Dim sStructName As String = mMatch.Groups("Name").Value

                Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE("",
                                                                            IO.Path.GetFileName(sFile),
                                                                            sFile,
                                                                            ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.STRUCT,
                                                                            sStructName,
                                                                            sStructName,
                                                                            "struct " & sStructName)

                mAutocomplete.m_Data("StructName") = sStructName

#If DEBUG Then
                mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get strucs (names only)"
#End If

                If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                    lNewAutocompleteList.Add(mAutocomplete)
                End If
            Next
        End If


        'Get methodmap enums
        If ((ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_7 OrElse ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_MIX) AndAlso sSource.Contains("methodmap")) Then
            Dim mSourceBuilder As New StringBuilder(sSource.Length)

            If (True) Then
                Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sSource, iLanguage)

                For i = 0 To sSource.Length - 1
                    mSourceBuilder.Append(If(mSourceAnalysis.m_InNonCode(i), " "c, sSource(i)))
                Next
            End If

            Dim mPossibleEnumMatches As MatchCollection = Regex.Matches(mSourceBuilder.ToString, "^\s*\b(methodmap)\b\s+(?<Name>\b[a-zA-Z0-9_]+\b)", RegexOptions.Multiline)

            Dim mMatch As Match

            For i = 0 To mPossibleEnumMatches.Count - 1
                mMatch = mPossibleEnumMatches(i)

                If (Not mMatch.Success) Then
                    Continue For
                End If

                Dim sEnumName As String = mMatch.Groups("Name").Value

                Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE("",
                                                                            IO.Path.GetFileName(sFile),
                                                                            sFile,
                                                                            ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.ENUM,
                                                                            sEnumName,
                                                                            sEnumName,
                                                                            "enum " & sEnumName)

                mAutocomplete.m_Data("EnumName") = sEnumName
                mAutocomplete.m_Data("EnumIsMethodmap") = True
                mAutocomplete.m_Data("EnumHidden") = True

#If DEBUG Then
                mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get methodmap enums"
#End If

                If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                    lNewAutocompleteList.Add(mAutocomplete)
                End If
            Next
        End If


        'Get typeset enums
        If ((ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_7 OrElse ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_MIX) AndAlso sSource.Contains("typeset")) Then
            Dim mSourceBuilder As New StringBuilder(sSource.Length)

            If (True) Then
                Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sSource, iLanguage)

                For i = 0 To sSource.Length - 1
                    mSourceBuilder.Append(If(mSourceAnalysis.m_InNonCode(i), " "c, sSource(i)))
                Next
            End If

            Dim mPossibleEnumMatches As MatchCollection = Regex.Matches(mSourceBuilder.ToString, "^\s*\b(typeset)\b\s+(?<Name>\b[a-zA-Z0-9_]+\b)", RegexOptions.Multiline)

            Dim mMatch As Match

            For i = 0 To mPossibleEnumMatches.Count - 1
                mMatch = mPossibleEnumMatches(i)

                If (Not mMatch.Success) Then
                    Continue For
                End If

                Dim sEnumName As String = mMatch.Groups("Name").Value

                Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE("",
                                                                            IO.Path.GetFileName(sFile),
                                                                            sFile,
                                                                            ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.ENUM,
                                                                            sEnumName,
                                                                            sEnumName,
                                                                            "enum " & sEnumName)

                mAutocomplete.m_Data("EnumName") = sEnumName
                mAutocomplete.m_Data("EnumIsTypeset") = True
                mAutocomplete.m_Data("EnumHidden") = True

#If DEBUG Then
                mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get typeset enums"
#End If

                If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                    lNewAutocompleteList.Add(mAutocomplete)
                End If
            Next
        End If

        'Get typedef enums
        If ((ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_7 OrElse ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_MIX) AndAlso sSource.Contains("typedef")) Then
            Dim mSourceBuilder As New StringBuilder(sSource.Length)

            If (True) Then
                Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sSource, iLanguage)

                For i = 0 To sSource.Length - 1
                    mSourceBuilder.Append(If(mSourceAnalysis.m_InNonCode(i), " "c, sSource(i)))
                Next
            End If

            Dim mPossibleEnumMatches As MatchCollection = Regex.Matches(mSourceBuilder.ToString, "^\s*\b(typedef)\b\s+(?<Name>\b[a-zA-Z0-9_]+\b)", RegexOptions.Multiline)

            Dim mMatch As Match

            For i = 0 To mPossibleEnumMatches.Count - 1
                mMatch = mPossibleEnumMatches(i)

                If (Not mMatch.Success) Then
                    Continue For
                End If

                Dim sEnumName As String = mMatch.Groups("Name").Value

                Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE("",
                                                                            IO.Path.GetFileName(sFile),
                                                                            sFile,
                                                                            ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.ENUM,
                                                                            sEnumName,
                                                                            sEnumName,
                                                                            "enum " & sEnumName)

                mAutocomplete.m_Data("EnumName") = sEnumName
                mAutocomplete.m_Data("EnumIsTypedef") = True
                mAutocomplete.m_Data("EnumHidden") = True

#If DEBUG Then
                mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get typedef enums"
#End If

                If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                    lNewAutocompleteList.Add(mAutocomplete)
                End If
            Next
        End If

        'Get functag enums
        If ((ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_6 OrElse ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_MIX) AndAlso sSource.Contains("functag")) Then
            Dim mSourceBuilder As New StringBuilder(sSource.Length)

            If (True) Then
                Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sSource, iLanguage)

                For i = 0 To sSource.Length - 1
                    mSourceBuilder.Append(If(mSourceAnalysis.m_InNonCode(i), " "c, sSource(i)))
                Next
            End If

            Dim mPossibleEnumMatches As MatchCollection = Regex.Matches(mSourceBuilder.ToString, "^\s*\b(functag)\b\s+\b(public)\b\s+(?<Tag>\b[a-zA-Z0-9_]+\b\s+|\b[a-zA-Z0-9_]+\b\:\s*|)(?<Name>\b[a-zA-Z0-9_]+\b)\s*\(", RegexOptions.Multiline)

            Dim mMatch As Match

            For i = 0 To mPossibleEnumMatches.Count - 1
                mMatch = mPossibleEnumMatches(i)

                If (Not mMatch.Success) Then
                    Continue For
                End If

                Dim sEnumName As String = mMatch.Groups("Name").Value

                Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE("",
                                                                            IO.Path.GetFileName(sFile),
                                                                            sFile,
                                                                            ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.ENUM,
                                                                            sEnumName,
                                                                            sEnumName,
                                                                            "enum " & sEnumName)

                mAutocomplete.m_Data("EnumName") = sEnumName
                mAutocomplete.m_Data("EnumIsFunctag") = True
                mAutocomplete.m_Data("EnumHidden") = True

#If DEBUG Then
                mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get functag enums"
#End If

                If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                    lNewAutocompleteList.Add(mAutocomplete)
                End If
            Next
        End If

        'Get funcenum enums
        If ((ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_6 OrElse ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_MIX) AndAlso sSource.Contains("funcenum")) Then
            Dim mSourceBuilder As New StringBuilder(sSource.Length)

            If (True) Then
                Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sSource, iLanguage)

                For i = 0 To sSource.Length - 1
                    mSourceBuilder.Append(If(mSourceAnalysis.m_InNonCode(i), " "c, sSource(i)))
                Next
            End If

            Dim mPossibleEnumMatches As MatchCollection = Regex.Matches(mSourceBuilder.ToString, "^\s*\b(funcenum)\b\s+(?<Name>\b[a-zA-Z0-9_]+\b)", RegexOptions.Multiline)

            Dim mMatch As Match

            For i = 0 To mPossibleEnumMatches.Count - 1
                mMatch = mPossibleEnumMatches(i)

                If (Not mMatch.Success) Then
                    Continue For
                End If

                Dim sEnumName As String = mMatch.Groups("Name").Value

                Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE("",
                                                                            IO.Path.GetFileName(sFile),
                                                                            sFile,
                                                                            ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.ENUM,
                                                                            sEnumName,
                                                                            sEnumName,
                                                                            "enum " & sEnumName)

                mAutocomplete.m_Data("EnumName") = sEnumName
                mAutocomplete.m_Data("EnumIsFuncenum") = True
                mAutocomplete.m_Data("EnumHidden") = True

#If DEBUG Then
                mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get funcenum enums"
#End If

                If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                    lNewAutocompleteList.Add(mAutocomplete)
                End If
            Next
        End If

        'Get enums
        If (sSource.Contains("enum")) Then
            Dim mSourceBuilder As New StringBuilder(sSource.Length)

            If (True) Then
                Dim mSourceAnalysis2 As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sSource, iLanguage)

                For i = 0 To sSource.Length - 1
                    mSourceBuilder.Append(If(mSourceAnalysis2.m_InNonCode(i), " "c, sSource(i)))
                Next
            End If

            Dim mPossibleEnumMatches As MatchCollection = Regex.Matches(mSourceBuilder.ToString, "^\s*\b(enum)\b\s*((?<Tag>\b[a-zA-Z0-9_]+\b)(\:)(?<Name>\b[a-zA-Z0-9_]+\b)\s*(\(.*?\)){0,1}|(?<Name>\b[a-zA-Z0-9_]+\b)(\:){0,1}\s*(\(.*?\)){0,1}|\(.*?\)|)\s*(?<BraceStart>\{)", RegexOptions.Multiline)
            Dim iBraceList As Integer()() = g_mFormMain.g_ClassSyntaxTools.GetExpressionBetweenCharacters(mSourceBuilder.ToString, "{"c, "}"c, 1, iLanguage, True)


            Dim mMatch As Match
            Dim mMatch2 As Match

            Dim sEnumName As String

            Dim bIsValid As Boolean
            Dim sEnumSource As String
            Dim iBraceIndex As Integer

            Dim mSourceAnalysis As ClassSyntaxTools.ClassSyntaxSourceAnalysis

            Dim mEnumBuilder As StringBuilder
            Dim lEnumSplitList As List(Of String)

            Dim sLine As String

            Dim iTargetEnumSplitListIndex As Integer


            For i = 0 To mPossibleEnumMatches.Count - 1
                mMatch = mPossibleEnumMatches(i)
                If (Not mMatch.Success) Then
                    Continue For
                End If

                sEnumName = mMatch.Groups("Name").Value
                If (String.IsNullOrEmpty(sEnumName.Trim)) Then
                    'g_mFormMain.PrintInformation("[WARN]", String.Format("Failed to read name from enum because it has no name: Renamed to 'Enum' ({0})", IO.Path.GetFileName(sFile)), False, False)
                    sEnumName = "Enum"
                End If

                bIsValid = False
                sEnumSource = ""
                iBraceIndex = mMatch.Groups("BraceStart").Index
                For ii = 0 To iBraceList.Length - 1
                    If (iBraceIndex = iBraceList(ii)(0)) Then
                        sEnumSource = mSourceBuilder.ToString.Substring(iBraceList(ii)(0) + 1, iBraceList(ii)(1) - iBraceList(ii)(0) - 1)
                        bIsValid = True
                        Exit For
                    End If
                Next
                If (Not bIsValid) Then
                    Continue For
                End If

                mSourceAnalysis = New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sEnumSource, iLanguage)

                mEnumBuilder = New StringBuilder
                lEnumSplitList = New List(Of String)

                For ii = 0 To sEnumSource.Length - 1
                    Select Case (sEnumSource(ii))
                        Case ","c
                            If (mSourceAnalysis.GetParenthesisLevel(ii, Nothing) > 0 OrElse mSourceAnalysis.GetBracketLevel(ii, Nothing) > 0 OrElse mSourceAnalysis.GetBraceLevel(ii, Nothing) > 0 OrElse mSourceAnalysis.m_InNonCode(ii)) Then
                                Exit Select
                            End If

                            If (mEnumBuilder.Length < 1) Then
                                Exit Select
                            End If

                            sLine = mEnumBuilder.ToString
                            sLine = Regex.Replace(sLine.Trim, "\s+", " ")

                            lEnumSplitList.Add(sLine)
                            mEnumBuilder = New StringBuilder
                        Case Else
                            If (mSourceAnalysis.m_InNonCode(ii)) Then
                                Exit Select
                            End If

                            mEnumBuilder.Append(sEnumSource(ii))
                    End Select
                Next

                If (mEnumBuilder.Length > 0) Then
                    sLine = mEnumBuilder.ToString
                    sLine = Regex.Replace(sLine.Trim, "\s+", " ")

                    If (Not String.IsNullOrEmpty(sLine)) Then
                        lEnumSplitList.Add(sLine)
                        mEnumBuilder = New StringBuilder
                    End If
                End If


                Dim lEnumCommentArray As String() = New String(lEnumSplitList.Count - 1) {}
                For j = 0 To lEnumCommentArray.Length - 1
                    lEnumCommentArray(j) = ""
                Next

                Dim sEnumSourceLines As String() = sEnumSource.Split(New String() {vbNewLine, vbLf}, 0)
                For ii = 0 To sEnumSourceLines.Length - 1
                    iTargetEnumSplitListIndex = -1
                    For iii = 0 To lEnumSplitList.Count - 1
                        If (sEnumSourceLines(ii).Contains(lEnumSplitList(iii)) AndAlso Regex.IsMatch(sEnumSourceLines(ii), String.Format("\b{0}\b", Regex.Escape(lEnumSplitList(iii))))) Then
                            iTargetEnumSplitListIndex = iii
                            Exit For
                        End If
                    Next
                    If (iTargetEnumSplitListIndex < 0) Then
                        Continue For
                    End If

                    While True
                        mMatch2 = Regex.Match(sEnumSourceLines(ii), "/\*(.*?)\*/\s*$")
                        If (mMatch2.Success) Then
                            lEnumCommentArray(iTargetEnumSplitListIndex) = mMatch2.Value
                            Exit While
                        End If
                        If (ii > 1) Then
                            mMatch2 = Regex.Match(sEnumSourceLines(ii - 1), "^\s*(?<Comment>//(.*?)$)")
                            If (mMatch2.Success) Then
                                lEnumCommentArray(iTargetEnumSplitListIndex) = mMatch2.Groups("Comment").Value
                                Exit While
                            End If
                        End If

                        lEnumCommentArray(iTargetEnumSplitListIndex) = ""
                        Exit While
                    End While
                Next

                If (True) Then
                    Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE("",
                                                                                IO.Path.GetFileName(sFile),
                                                                                sFile,
                                                                                ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.ENUM,
                                                                                sEnumName,
                                                                                sEnumName,
                                                                                "enum " & sEnumName)

                    mAutocomplete.m_Data("EnumName") = sEnumName

#If DEBUG Then
                    mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get enums"
#End If

                    If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                        lNewAutocompleteList.Add(mAutocomplete)
                    End If
                End If

                For ii = 0 To lEnumSplitList.Count - 1
                    Dim sEnumFull As String = lEnumSplitList(ii)
                    Dim sEnumComment As String = lEnumCommentArray(ii)

                    mMatch2 = Regex.Match(sEnumFull, "^\s*(?<Tag>\b[a-zA-Z0-9_]+\b:)*(?<Name>\b[a-zA-Z0-9_]+\b)")
                    If (Not mMatch2.Groups("Name").Success) Then
                        g_mFormMain.PrintInformation("[WARN]", String.Format("Failed to resolve type 'Enum': enum {0} {1}", sEnumName, sEnumFull), False, False)
                        Continue For
                    End If

                    Dim sEnumVarName As String = mMatch2.Groups("Name").Value

                    Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE(sEnumComment,
                                                                                IO.Path.GetFileName(sFile),
                                                                                sFile,
                                                                                ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.ENUM,
                                                                                sEnumVarName,
                                                                                String.Format("{0}.{1}", sEnumName, sEnumVarName),
                                                                                String.Format("enum {0} {1}", sEnumName, sEnumFull))

                    mAutocomplete.m_Data("EnumName") = sEnumName
                    mAutocomplete.m_Data("EnumIsChild") = True
                    mAutocomplete.m_Data("EnumChildName") = sEnumVarName

#If DEBUG Then
                    mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get enums"
#End If

                    If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                        lNewAutocompleteList.Add(mAutocomplete)
                    End If
                Next
            Next
        End If

        sSourceList.Add(New String() {sFile, sSource})
    End Sub

    Private Sub ParseAutocomplete_Post(sActiveSource As String,
                                       sActiveSourceFile As String,
                                       ByRef sFile As String,
                                       ByRef sSource As String,
                                       ByRef lNewAutocompleteList As ClassSyncList(Of ClassSyntaxTools.STRUC_AUTOCOMPLETE),
                                       iLanguage As ClassSyntaxTools.ENUM_LANGUAGE_TYPE)
        'Get Defines
        If (sSource.Contains("#define")) Then
            Dim sLine As String

            Dim mMatch As Match

            Dim sFullDefine As String
            Dim sFullName As String
            Dim sName As String

            Dim iBraceList As Integer()()

            Dim sBraceText As String

            Using mSR As New IO.StringReader(sSource)
                While True
                    sLine = mSR.ReadLine
                    If (sLine Is Nothing) Then
                        Exit While
                    End If

                    If (Not sLine.Contains("#define")) Then
                        Continue While
                    End If

                    mMatch = Regex.Match(sLine, "(?<FullDefine>^\s*#define\s+(?<Name>\b[a-zA-Z0-9_]+\b)\s*)(?<Arguments>\()*")
                    If (Not mMatch.Success) Then
                        Continue While
                    End If

                    sFullName = ""
                    sName = mMatch.Groups("Name").Value
                    sFullDefine = mMatch.Groups("FullDefine").Value

                    If (mMatch.Groups("Arguments").Success) Then
                        iBraceList = g_mFormMain.g_ClassSyntaxTools.GetExpressionBetweenCharacters(sLine, "("c, ")"c, 1, iLanguage, True)
                        If (iBraceList.Length < 1) Then
                            Continue While
                        End If

                        sBraceText = sLine.Substring(iBraceList(0)(0), iBraceList(0)(1) - iBraceList(0)(0) + 1)

                        sFullDefine = Regex.Match(sLine, String.Format("{0}{1}(.*?)$", Regex.Escape(sFullDefine), Regex.Escape(sBraceText))).Value
                        sFullName = sFullDefine
                    Else
                        sBraceText = ""

                        sFullDefine = Regex.Match(sLine, String.Format("{0}(.*?)$", Regex.Escape(sFullDefine))).Value
                        sFullName = sFullDefine
                    End If

                    sFullName = sFullName.Replace(vbTab, " ")

                    Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE("",
                                                                                IO.Path.GetFileName(sFile),
                                                                                sFile,
                                                                                ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.DEFINE,
                                                                                sName,
                                                                                sName,
                                                                                sFullName)

                    mAutocomplete.m_Data("DefineName") = sName
                    mAutocomplete.m_Data("DefineArguments") = sBraceText

#If DEBUG Then
                    mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get Defines"
#End If

                    If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                        lNewAutocompleteList.Add(mAutocomplete)
                    End If
                End While
            End Using
        End If

        'Get public global variables
        If (sSource.Contains("public")) Then
            'Dim mSourceBuilder As New StringBuilder(sSource.Length)

            'If (True) Then
            '    Dim iSynCR_Source As New ClassSyntaxTools.ClassSyntaxCharReader(sSource)

            '    For i = 0 To sSource.Length - 1
            '        mSourceBuilder.Append(If(iSynCR_Source.InNonCode(i), " "c, sSource(i)))
            '    Next
            'End If

            Dim sRegExTypePattern As String = GetTypeNamesToPattern(lNewAutocompleteList)

            Dim mMatch As Match

            Dim iBracketList As Integer()()
            Dim sFullName As String
            Dim sName As String
            Dim sComment As String
            Dim sTypes As String
            Dim sTag As String
            Dim sOther As String
            Dim IsFunction As Boolean
            Dim IsInvalidName As Boolean
            Dim IsArray As Boolean

            Dim sLines As String() = sSource.Split(New String() {vbNewLine, vbLf}, 0)
            Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sSource, iLanguage)
            For i = 0 To sLines.Length - 1
                If (Not sLines(i).Contains("public")) Then
                    Continue For
                End If

                Dim iIndex = mSourceAnalysis.GetIndexFromLine(i)
                If (iIndex < 0 OrElse mSourceAnalysis.GetBraceLevel(iIndex, Nothing) > 0 OrElse mSourceAnalysis.m_InNonCode(iIndex)) Then
                    Continue For
                End If

                'SP 1.7 +Tags
                mMatch = Regex.Match(sLines(i), String.Format("^\s*(?<Types>public(\b[a-zA-Z0-9_ ]+\b)*)\s+(?<Tag>\b{0}\b(\[\s*\])*\s)\s*(?<Name>\b[a-zA-Z0-9_]+\b)(?<Other>(.*?))$", sRegExTypePattern))
                If (Not mMatch.Success) Then
                    If (ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_7) Then
                        Continue For
                    End If

                    'SP 1.6 +Tags
                    mMatch = Regex.Match(sLines(i), String.Format("^\s*(?<Types>public(\b[a-zA-Z0-9_ ]+\b)*)\s+(?<Tag>\b{0}\b\:)(?<Name>\b[a-zA-Z0-9_]+\b)(?<Other>(.*?))$", sRegExTypePattern, "{0}"))
                    If (Not mMatch.Success) Then
                        'SP 1.6 -Tags
                        mMatch = Regex.Match(sLines(i), String.Format("^\s*(?<Types>public(\b[a-zA-Z0-9_ ]+\b)*)\s+(?<!<Tag>\b{0}\b\:)(?<Name>\b[a-zA-Z0-9_]+\b)(?<Other>(.*?))$", sRegExTypePattern, "{0}"))
                        If (Not mMatch.Success) Then
                            Continue For
                        End If
                    End If
                Else
                    If (ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_6) Then
                        Continue For
                    End If
                End If

                sFullName = ""
                sName = mMatch.Groups("Name").Value
                sComment = Regex.Match(mMatch.Groups("Other").Value, "/\*(.*?)\*/\s*$").Value
                sTypes = mMatch.Groups("Types").Value
                sTag = mMatch.Groups("Tag").Value
                sOther = mMatch.Groups("Other").Value.Trim
                IsFunction = sOther.StartsWith("(")
                IsArray = sOther.StartsWith("[")
                IsInvalidName = sOther.StartsWith(":")

                If (IsFunction OrElse IsInvalidName) Then
                    Continue For
                End If

                Dim sArrayDim As String = ""
                If (IsArray) Then
                    iBracketList = g_mFormMain.g_ClassSyntaxTools.GetExpressionBetweenCharacters(sOther, "["c, "]"c, 1, iLanguage, True)
                    For j = 0 To iBracketList.Length - 1
                        sArrayDim &= sOther.Substring(iBracketList(j)(0), iBracketList(j)(1) - iBracketList(j)(0) + 1)
                    Next
                End If

                sFullName = String.Format("{0} {1}{2}{3}", sTypes, sTag, sName, sArrayDim)
                sFullName = sFullName.Replace(vbTab, " "c)

                Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE(sComment,
                                                                                IO.Path.GetFileName(sFile),
                                                                                sFile,
                                                                                ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.PUBLICVAR,
                                                                                sName,
                                                                                sName,
                                                                                sFullName)

                mAutocomplete.m_Data("PublicvarName") = sName
                mAutocomplete.m_Data("PublicvarTypes") = sTypes
                mAutocomplete.m_Data("PublicvarTag") = sTag
                mAutocomplete.m_Data("PublicvarArrayDim") = sArrayDim

#If DEBUG Then
                mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get public global variables"
#End If

                If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                    lNewAutocompleteList.Add(mAutocomplete)
                End If
            Next
        End If

        'Get funcenums
        If ((ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_6 OrElse ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_MIX) AndAlso sSource.Contains("funcenum")) Then
            Dim mPossibleEnumMatches As MatchCollection = Regex.Matches(sSource, "^\s*\b(funcenum)\b\s+(?<Name>\b[a-zA-Z0-9_]+\b)\s*(?<BraceStart>\{)", RegexOptions.Multiline)
            Dim iBraceList As Integer()() = g_mFormMain.g_ClassSyntaxTools.GetExpressionBetweenCharacters(sSource, "{"c, "}"c, 1, iLanguage, True)

            Dim mMatch As Match

            Dim sEnumName As String

            Dim bIsValid As Boolean
            Dim sEnumSource As String
            Dim iBraceIndex As Integer

            Dim mEnumBuilder As StringBuilder
            Dim lEnumSplitList As List(Of String)

            Dim mSourceAnalysis As ClassSyntaxTools.ClassSyntaxSourceAnalysis

            Dim sLine As String
            Dim iInvalidLen As Integer

            Dim sComment As String
            Dim lEnumCommentArray As String()
            Dim sEnumSourceLines As String()

            Dim iLine As Integer
            Dim bCommentStart As Boolean
            Dim mRegMatch2 As Match

            For i = 0 To mPossibleEnumMatches.Count - 1
                mMatch = mPossibleEnumMatches(i)
                If (Not mMatch.Success) Then
                    Continue For
                End If

                sEnumName = mMatch.Groups("Name").Value

                bIsValid = False
                sEnumSource = ""
                iBraceIndex = mMatch.Groups("BraceStart").Index
                For ii = 0 To iBraceList.Length - 1
                    If (iBraceIndex = iBraceList(ii)(0)) Then
                        sEnumSource = sSource.Substring(iBraceList(ii)(0) + 1, iBraceList(ii)(1) - iBraceList(ii)(0) - 1)
                        bIsValid = True
                        Exit For
                    End If
                Next
                If (Not bIsValid) Then
                    Continue For
                End If

                mEnumBuilder = New StringBuilder
                lEnumSplitList = New List(Of String)

                mSourceAnalysis = New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sEnumSource, iLanguage)

                For ii = 0 To sEnumSource.Length - 1
                    Select Case (sEnumSource(ii))
                        Case ","c
                            If (mSourceAnalysis.GetParenthesisLevel(ii, Nothing) > 0 OrElse mSourceAnalysis.GetBracketLevel(ii, Nothing) > 0 OrElse mSourceAnalysis.GetBraceLevel(ii, Nothing) > 0 OrElse mSourceAnalysis.m_InNonCode(ii)) Then
                                Exit Select
                            End If

                            If (mEnumBuilder.Length < 1) Then
                                Exit Select
                            End If

                            sLine = mEnumBuilder.ToString
                            sLine = Regex.Replace(sLine.Trim, "\s+", " ")

                            iInvalidLen = Regex.Match(sLine, "^(.*?)(?<End>\b[a-zA-Z0-9_]+\b)").Groups("End").Index
                            If (iInvalidLen > 0) Then
                                sLine = sLine.Remove(0, iInvalidLen)
                            End If

                            lEnumSplitList.Add(sLine)
                            mEnumBuilder = New StringBuilder
                    End Select

                    If (Not mSourceAnalysis.m_InSingleComment(ii) AndAlso Not mSourceAnalysis.m_InMultiComment(ii)) Then
                        mEnumBuilder.Append(sEnumSource(ii))
                    End If
                Next

                If (mEnumBuilder.Length > 0) Then
                    sLine = mEnumBuilder.ToString
                    sLine = Regex.Replace(sLine.Trim, "\s+", " ")

                    iInvalidLen = Regex.Match(sLine, "^(.*?)(?<End>\b[a-zA-Z0-9_]+\b)").Groups("End").Index
                    If (iInvalidLen > 0) Then
                        sLine = sLine.Remove(0, iInvalidLen)

                        If (Not String.IsNullOrEmpty(sLine)) Then
                            lEnumSplitList.Add(sLine)
                            mEnumBuilder = New StringBuilder
                        End If
                    End If
                End If

                sComment = ""
                lEnumCommentArray = New String(lEnumSplitList.Count - 1) {}
                For j = 0 To lEnumCommentArray.Length - 1
                    lEnumCommentArray(j) = ""
                Next

                sEnumSourceLines = sEnumSource.Split(New String() {vbNewLine, vbLf}, 0)

                For ii = 0 To lEnumSplitList.Count - 1
                    iLine = 0
                    For iii = 0 To sEnumSourceLines.Length - 1
                        If (sEnumSourceLines(iii).Contains(lEnumSplitList(ii))) Then
                            iLine = iii
                            Exit For
                        End If
                    Next

                    bCommentStart = False
                    sComment = ""
                    For iii = iLine - 1 To 0 Step -1
                        mRegMatch2 = Regex.Match(sEnumSourceLines(iii), "^\s*(?<Start>//(.*?))$")
                        If (Not bCommentStart AndAlso mRegMatch2.Success) Then
                            sComment = mRegMatch2.Groups("Start").Value & Environment.NewLine & sComment
                            Continue For
                        End If

                        mRegMatch2 = Regex.Match(sEnumSourceLines(iii), "(?<Start>(/\*+))|(?<End>\*+/|(?<Pragma>)^\s*#pragma)")

                        If (Not bCommentStart AndAlso Not mRegMatch2.Groups("End").Success) Then
                            Exit For
                        End If

                        If (Not mRegMatch2.Groups("Pragma").Success) Then
                            bCommentStart = True
                        End If

                        Select Case (True)
                            Case mRegMatch2.Groups("End").Success AndAlso Not mRegMatch2.Groups("Pragma").Success
                                sComment = sEnumSourceLines(iii).Substring(0, mRegMatch2.Groups("End").Index + 2) & Environment.NewLine & sComment
                            Case mRegMatch2.Groups("Start").Success
                                sComment = sEnumSourceLines(iii).Substring(mRegMatch2.Groups("Start").Index, sEnumSourceLines(iii).Length - mRegMatch2.Groups("Start").Index) & Environment.NewLine & sComment
                            Case Else
                                sComment = sEnumSourceLines(iii) & Environment.NewLine & sComment
                        End Select

                        If (bCommentStart AndAlso mRegMatch2.Groups("Start").Success) Then
                            Exit For
                        End If
                    Next

                    lEnumCommentArray(ii) = sComment
                Next

                For ii = 0 To lEnumSplitList.Count - 1
                    Dim sEnumFull As String = lEnumSplitList(ii)
                    Dim sEnumComment As String = lEnumCommentArray(ii)

                    sEnumComment = New Regex("^\s+", RegexOptions.Multiline).Replace(sEnumComment, "")

                    Dim regMatch As Match = Regex.Match(sEnumFull, "^\s*(?<Tag>\b[a-zA-Z0-9_]+\b:)*(?<Name>\b[a-zA-Z0-9_]+\b)")
                    If (Not regMatch.Groups("Name").Success) Then
                        g_mFormMain.PrintInformation("[WARN]", String.Format("Failed to resolve type 'Enum': enum {0} {1}", sEnumName, sEnumFull), False, False)
                        Continue For
                    End If

                    Dim sEnumVarName As String = regMatch.Groups("Name").Value

                    Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE(sEnumComment,
                                                                                IO.Path.GetFileName(sFile),
                                                                                sFile,
                                                                                ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCENUM,
                                                                                sEnumName,
                                                                                "public " & (New Regex("\s*\b(public)\b\s*").Replace(sEnumFull, sEnumName, 1)),
                                                                                String.Format("funcenum {0} {1}", sEnumName, sEnumFull))

                    mAutocomplete.m_Data("FuncenumName") = sEnumName
                    mAutocomplete.m_Data("FuncenumVarName") = sEnumVarName
                    mAutocomplete.m_Data("FuncenumFull") = sEnumFull

#If DEBUG Then
                    mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get funcenums"
#End If

                    If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                        lNewAutocompleteList.Add(mAutocomplete)
                    End If
                Next
            Next
        End If

        'Get typesets
        If ((ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_7 OrElse ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_MIX) AndAlso sSource.Contains("typeset")) Then
            Dim sRegExTypePattern As String = GetTypeNamesToPattern(lNewAutocompleteList)

            Dim mPossibleMethodmapMatches As MatchCollection = Regex.Matches(sSource, "^\s*\b(typeset)\b\s+(?<Name>\b[a-zA-Z0-9_]+\b)\s*(?<BraceStart>\{)", RegexOptions.Multiline)
            Dim iBraceList As Integer()() = g_mFormMain.g_ClassSyntaxTools.GetExpressionBetweenCharacters(sSource, "{"c, "}"c, 1, iLanguage, True)

            Dim mMatch As Match

            Dim bIsValid As Boolean
            Dim sMethodmapSource As String
            Dim iBraceIndex As Integer

            Dim sName As String

            For i = 0 To mPossibleMethodmapMatches.Count - 1
                mMatch = mPossibleMethodmapMatches(i)
                If (Not mMatch.Success) Then
                    Continue For
                End If

                bIsValid = False
                sMethodmapSource = ""
                iBraceIndex = mMatch.Groups("BraceStart").Index
                For ii = 0 To iBraceList.Length - 1
                    If (iBraceIndex = iBraceList(ii)(0)) Then
                        sMethodmapSource = sSource.Substring(iBraceList(ii)(0) + 1, iBraceList(ii)(1) - iBraceList(ii)(0) - 1)
                        bIsValid = True
                        Exit For
                    End If
                Next
                If (Not bIsValid) Then
                    Continue For
                End If

                sName = mMatch.Groups("Name").Value

                Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sMethodmapSource, iLanguage)
                Dim iMethodmapBraceList As Integer()() = g_mFormMain.g_ClassSyntaxTools.GetExpressionBetweenCharacters(sMethodmapSource, "("c, ")"c, 1, iLanguage, True)

                Dim mMethodMatches As MatchCollection = Regex.Matches(sMethodmapSource, String.Format("^\s*(?<Type>\b(function)\b)\s+(?<Tag>\b({0})\b)\s*(?<BraceStart>\()", sRegExTypePattern), RegexOptions.Multiline)

                Dim SB As StringBuilder

                For ii = 0 To mMethodMatches.Count - 1
                    If (mSourceAnalysis.m_InNonCode(mMethodMatches(ii).Groups("Type").Index)) Then
                        Continue For
                    End If

                    SB = New StringBuilder
                    For iii = mMethodMatches(ii).Groups("Type").Index - 1 To 0 Step -1
                        Select Case (sMethodmapSource(iii))
                            Case " "c, vbTab(0), vbLf(0), vbCr(0)
                                SB.Append(sMethodmapSource(iii))
                            Case Else
                                If (Not mSourceAnalysis.m_InMultiComment(iii) AndAlso Not mSourceAnalysis.m_InSingleComment(iii) AndAlso Not mSourceAnalysis.m_InPreprocessor(iii)) Then
                                    Exit For
                                End If

                                SB.Append(sMethodmapSource(iii))
                        End Select
                    Next

                    Dim sComment As String = StrReverse(SB.ToString)
                    sComment = Regex.Replace(sComment, "^\s+", "", RegexOptions.Multiline)
                    sComment = Regex.Replace(sComment, "\s+$", "", RegexOptions.Multiline)
                    Dim sType As String = mMethodMatches(ii).Groups("Type").Value
                    Dim sTag As String = mMethodMatches(ii).Groups("Tag").Value.Trim
                    Dim iBraceStart As Integer = mMethodMatches(ii).Groups("BraceStart").Index
                    Dim sBraceString As String = Nothing

                    For iii = 0 To iMethodmapBraceList.Length - 1
                        If (iBraceStart = iMethodmapBraceList(iii)(0)) Then
                            sBraceString = sMethodmapSource.Substring(iMethodmapBraceList(iii)(0), iMethodmapBraceList(iii)(1) - iMethodmapBraceList(iii)(0) + 1)
                            Exit For
                        End If
                    Next

                    If (String.IsNullOrEmpty(sBraceString)) Then
                        Continue For
                    End If

                    Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE(sComment,
                                                                                IO.Path.GetFileName(sFile),
                                                                                sFile,
                                                                                ClassSyntaxTools.STRUC_AUTOCOMPLETE.ParseTypeFullNames(sType) Or ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPESET,
                                                                                sName,
                                                                                String.Format("public {0} {1}{2}", sTag, sName, sBraceString),
                                                                                String.Format("typeset {0} {1} {2}{3}", sName, sType, sTag, sBraceString))

                    mAutocomplete.m_Data("TypesetType") = sType
                    mAutocomplete.m_Data("TypesetTag") = sTag
                    mAutocomplete.m_Data("TypesetName") = sName
                    mAutocomplete.m_Data("TypesetArguments") = sBraceString

#If DEBUG Then
                    mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get typesets"
#End If

                    If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                        lNewAutocompleteList.Add(mAutocomplete)
                    End If
                Next
            Next
        End If

        'Get typedefs
        If ((ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_7 OrElse ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_MIX) AndAlso sSource.Contains("typedef")) Then
            Dim sRegExTypePattern As String = GetTypeNamesToPattern(lNewAutocompleteList)

            Dim mPossibleMethodmapMatches As MatchCollection = Regex.Matches(sSource, String.Format("^\s*\b(typedef)\b\s+(?<Name>\b[a-zA-Z0-9_]+\b)\s+=\s+\b(function)\b\s+(?<Tag>\b({0})\b)\s*(?<BraceStart>\()", sRegExTypePattern), RegexOptions.Multiline)
            Dim iBraceList As Integer()() = g_mFormMain.g_ClassSyntaxTools.GetExpressionBetweenCharacters(sSource, "("c, ")"c, 1, iLanguage, True)

            Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sSource, iLanguage)

            For i = 0 To mPossibleMethodmapMatches.Count - 1
                Dim mMatch As Match = mPossibleMethodmapMatches(i)
                If (Not mMatch.Success) Then
                    Continue For
                End If

                If (mSourceAnalysis.m_InNonCode(mMatch.Index)) Then
                    Continue For
                End If

                Dim bIsValid As Boolean = False
                Dim sBraceString As String = ""
                Dim iBraceIndex As Integer = mMatch.Groups("BraceStart").Index
                For ii = 0 To iBraceList.Length - 1
                    If (iBraceIndex = iBraceList(ii)(0)) Then
                        sBraceString = sSource.Substring(iBraceList(ii)(0) + 1, iBraceList(ii)(1) - iBraceList(ii)(0) - 1)
                        bIsValid = True
                        Exit For
                    End If
                Next
                If (Not bIsValid) Then
                    Continue For
                End If

                If (String.IsNullOrEmpty(sBraceString)) Then
                    Continue For
                End If

                sBraceString = sBraceString.Trim

                Dim sName As String = mMatch.Groups("Name").Value
                Dim sTag As String = mMatch.Groups("Tag").Value

                Dim SB As New StringBuilder
                For iii = mMatch.Index - 1 To 0 Step -1
                    Select Case (sSource(iii))
                        Case " "c, vbTab(0), vbLf(0), vbCr(0)
                            SB.Append(sSource(iii))
                        Case Else
                            If (Not mSourceAnalysis.m_InMultiComment(iii) AndAlso Not mSourceAnalysis.m_InSingleComment(iii) AndAlso Not mSourceAnalysis.m_InPreprocessor(iii)) Then
                                Exit For
                            End If

                            SB.Append(sSource(iii))
                    End Select
                Next

                If (True) Then
                    Dim sComment As String = StrReverse(SB.ToString)
                    sComment = Regex.Replace(sComment, "^\s+", "", RegexOptions.Multiline)
                    sComment = Regex.Replace(sComment, "\s+$", "", RegexOptions.Multiline)

                    Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE(sComment,
                                                                                IO.Path.GetFileName(sFile),
                                                                                sFile,
                                                                                ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPEDEF,
                                                                                sName,
                                                                                String.Format("public {0} {1}({2})", sTag, sName, sBraceString),
                                                                                String.Format("typedef {0} = function {1} ({2})", sName, sTag, sBraceString))

                    mAutocomplete.m_Data("TypedefTag") = sTag
                    mAutocomplete.m_Data("TypedefName") = sName
                    mAutocomplete.m_Data("TypedefArguments") = String.Format("({0})", sBraceString)

#If DEBUG Then
                    mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get typedefs"
#End If

                    If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                        lNewAutocompleteList.Add(mAutocomplete)
                    End If
                End If
            Next
        End If

        'Get methods/functags
        If (True) Then
            Dim sRegExTypePattern As String = GetTypeNamesToPattern(lNewAutocompleteList)

            Dim iBraceList As Integer()()
            Dim sBraceText As String
            Dim mMatch As Match

            Dim sTypes As String()
            Dim sTag As String
            Dim sName As String
            Dim sFullname As String
            Dim sComment As String
            Dim iTypes As ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS

            Dim mFuncTagMatch As Match

            Dim bCommentStart As Boolean
            Dim mRegMatch2 As Match

            Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sSource, iLanguage)
            Dim sLines As String() = sSource.Split(New String() {vbNewLine, vbLf}, 0)

            If (mSourceAnalysis.m_MaxLength - 1 > 0) Then
                Dim iLeftBraceRange As ClassSyntaxTools.ClassSyntaxSourceAnalysis.ENUM_STATE_RANGE
                Dim iLastBraceLevel As Integer = mSourceAnalysis.GetBraceLevel(mSourceAnalysis.m_MaxLength - 1, iLeftBraceRange)
                If (iLastBraceLevel > 0 AndAlso iLeftBraceRange <> ClassSyntaxTools.ClassSyntaxSourceAnalysis.ENUM_STATE_RANGE.END) Then
                    g_mFormMain.PrintInformation("[ERRO]", String.Format("Uneven brace level! May lead to syntax parser failures! [LV:{0}] ({1})", iLastBraceLevel, IO.Path.GetFileName(sFile)), False, False)
                End If
            End If

            For i = 0 To sLines.Length - 1
                If ((ClassTools.ClassStrings.WordCount(sLines(i), "("c) + ClassTools.ClassStrings.WordCount(sLines(i), ")"c)) Mod 2 <> 0) Then
                    Continue For
                End If

                Dim iIndex = mSourceAnalysis.GetIndexFromLine(i)
                If (iIndex < 0 OrElse mSourceAnalysis.GetBraceLevel(iIndex, Nothing) > 0 OrElse mSourceAnalysis.m_InNonCode(iIndex)) Then
                    Continue For
                End If

                iBraceList = g_mFormMain.g_ClassSyntaxTools.GetExpressionBetweenCharacters(sLines(i), "("c, ")"c, 1, iLanguage, True)
                If (iBraceList.Length < 1) Then
                    Continue For
                End If

                sBraceText = sLines(i).Substring(iBraceList(0)(0), iBraceList(0)(1) - iBraceList(0)(0) + 1)

                'SP 1.7 +Tags
                mMatch = Regex.Match(sLines(i), String.Format("^\s*(?<Types>[a-zA-Z0-9_ ]*)(?<Tag>\b{0}\b\s)\s*(?<Name>\b[a-zA-Z0-9_]+\b)\s*{1}(?<IsFunc>\s*;){2}", sRegExTypePattern, Regex.Escape(sBraceText), "{0,1}"))
                If (Not mMatch.Success) Then
                    If (ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_7) Then
                        Continue For
                    End If

                    'SP 1.6 +Tags
                    mMatch = Regex.Match(sLines(i), String.Format("^\s*(?<Types>[a-zA-Z0-9_ ]*)(?<Tag>\b{0}\b\:)(?<Name>\b[a-zA-Z0-9_]+\b)\s*{1}(?<IsFunc>\s*;){2}", sRegExTypePattern, Regex.Escape(sBraceText), "{0,1}"))
                    If (Not mMatch.Success) Then
                        'SP 1.6 -Tags
                        mMatch = Regex.Match(sLines(i), String.Format("^\s*(?<Types>[a-zA-Z0-9_ ]*)(?<!<Tag>\b{0}\b\:)(?<Name>\b[a-zA-Z0-9_]+\b)\s*{1}(?<IsFunc>\s*;){2}", sRegExTypePattern, Regex.Escape(sBraceText), "{0,1}"))
                        If (Not mMatch.Success) Then
                            Continue For
                        End If
                    End If
                Else
                    If (ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_6) Then
                        Continue For
                    End If
                End If

                sTypes = mMatch.Groups("Types").Value.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
                sTag = mMatch.Groups("Tag").Value
                sName = mMatch.Groups("Name").Value
                sFullname = mMatch.Groups("Types").Value & sTag & sName & sBraceText
                sComment = ""
                iTypes = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ParseTypeNames(sTypes)

                If (Regex.IsMatch(sName, String.Format("(\b{0}\b)", String.Join("\b|\b", ClassSyntaxTools.g_sStatementsArray)))) Then
                    Continue For
                End If

                If (sTypes.Length < 1 AndAlso mMatch.Groups("IsFunc").Success) Then
                    Continue For
                End If

                Dim bIsFunctag As Boolean = False

                If ((iTypes And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCTAG) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCTAG) Then
                    While True
                        mFuncTagMatch = Regex.Match(sFullname, "(?<Name>\b[a-zA-Z0-9_]+\b)\s+(?<Tag>\b[a-zA-Z0-9_]+\:\b)*\b(public)\b\s*\(")
                        If (mFuncTagMatch.Success) Then
                            sTypes = New String() {"functag"}
                            sTag = mFuncTagMatch.Groups("Tag").Value
                            sName = mFuncTagMatch.Groups("Name").Value
                            sFullname = sTag & sName & sBraceText

                            bIsFunctag = True
                            Exit While
                        End If

                        mFuncTagMatch = Regex.Match(sFullname, "\b(public)\b\s+(?<Tag>\b[a-zA-Z0-9_]+\:\b)*(?<Name>\b[a-zA-Z0-9_]+\b)\s*\(")
                        If (mFuncTagMatch.Success) Then
                            sTypes = New String() {"functag"}
                            sTag = mFuncTagMatch.Groups("Tag").Value
                            sName = mFuncTagMatch.Groups("Name").Value
                            sFullname = sTag & sName & sBraceText

                            bIsFunctag = True
                            Exit While
                        End If

                        Continue For
                    End While
                End If

                bCommentStart = False
                For ii = i - 1 To 0 Step -1
                    mRegMatch2 = Regex.Match(sLines(ii), "(?<Start>(/\*+))|(?<End>\*+/|(?<Pragma>)^\s*#pragma)")

                    If (Not bCommentStart AndAlso Not mRegMatch2.Groups("End").Success) Then
                        Exit For
                    End If

                    If (Not mRegMatch2.Groups("Pragma").Success) Then
                        bCommentStart = True
                    End If

                    Select Case (True)
                        Case mRegMatch2.Groups("End").Success AndAlso Not mRegMatch2.Groups("Pragma").Success
                            sComment = sLines(ii).Substring(0, mRegMatch2.Groups("End").Index + 2) & Environment.NewLine & sComment
                        Case mRegMatch2.Groups("Start").Success
                            sComment = sLines(ii).Substring(mRegMatch2.Groups("Start").Index, sLines(ii).Length - mRegMatch2.Groups("Start").Index) & Environment.NewLine & sComment
                        Case Else
                            sComment = sLines(ii) & Environment.NewLine & sComment
                    End Select

                    If (bCommentStart AndAlso mRegMatch2.Groups("Start").Success) Then
                        Exit For
                    End If
                Next

                sComment = New Regex("^\s+", RegexOptions.Multiline).Replace(sComment, "")

                If (bIsFunctag) Then
                    Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE(sComment,
                                                                                IO.Path.GetFileName(sFile),
                                                                                sFile,
                                                                                ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCTAG,
                                                                                sName,
                                                                                String.Format("public {0}", sFullname),
                                                                                String.Format("functag {0}", sFullname))

                    mAutocomplete.m_Data("FunctagName") = sName
                    mAutocomplete.m_Data("FunctagTag") = sTag.Trim
                    mAutocomplete.m_Data("FunctagArguments") = sBraceText

#If DEBUG Then
                    mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get methods/functags"
#End If

                    If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                        lNewAutocompleteList.Add(mAutocomplete)
                    End If
                Else
                    'Filter out non-valid method types that 'ParseTypeNames()' may could parse
                    If (iTypes <> ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.NONE) Then
                        If ((iTypes And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.PUBLIC) <> ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.PUBLIC AndAlso
                            (iTypes And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.NATIVE) <> ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.NATIVE AndAlso
                            (iTypes And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.STOCK) <> ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.STOCK AndAlso
                            (iTypes And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.STATIC) <> ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.STATIC AndAlso
                            (iTypes And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FORWARD) <> ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FORWARD) Then
                            Continue For
                        End If
                    End If

                    Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE(sComment,
                                                                                IO.Path.GetFileName(sFile),
                                                                                sFile,
                                                                                ClassSyntaxTools.STRUC_AUTOCOMPLETE.ParseTypeNames(sTypes) Or ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHOD,
                                                                                sName,
                                                                                sName,
                                                                                sFullname)

                    mAutocomplete.m_Data("MethodName") = sName
                    mAutocomplete.m_Data("MethodType") = String.Join(" ", sTypes)
                    mAutocomplete.m_Data("MethodTag") = sTag.Trim
                    mAutocomplete.m_Data("MethodArguments") = sBraceText

#If DEBUG Then
                    mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get methods/functags"
#End If

                    If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                        lNewAutocompleteList.Add(mAutocomplete)
                    End If
                End If
            Next
        End If

        'Get methodmaps
        If ((ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_7 OrElse ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_MIX) AndAlso sSource.Contains("methodmap")) Then
            Dim sRegExTypePattern As String = GetTypeNamesToPattern(lNewAutocompleteList)

            Dim mPossibleMethodmapMatches As MatchCollection = Regex.Matches(sSource, "^\s*\b(methodmap)\b\s+(?<Name>\b[a-zA-Z0-9_]+\b)(?<ParentingName>\s+\b[a-zA-Z0-9_]+\b){0,1}(?<FullParent>\s*\<\s*(?<Parent>\b[a-zA-Z0-9_]+\b)){0,1}\s*(?<BraceStart>\{)", RegexOptions.Multiline)
            Dim iBraceList As Integer()() = g_mFormMain.g_ClassSyntaxTools.GetExpressionBetweenCharacters(sSource, "{"c, "}"c, 1, iLanguage, True)

            Dim mMatch As Match

            Dim bIsValid As Boolean
            Dim sMethodmapSource As String
            Dim iBraceIndex As Integer

            Dim sMethodMapName As String
            Dim sMethodMapHasParent As Boolean
            Dim sMethodMapParentName As String
            Dim sMethodMapFullParentName As String
            Dim sMethodMapParentingName As String

            For i = 0 To mPossibleMethodmapMatches.Count - 1
                mMatch = mPossibleMethodmapMatches(i)
                If (Not mMatch.Success) Then
                    Continue For
                End If

                bIsValid = False
                sMethodmapSource = ""
                iBraceIndex = mMatch.Groups("BraceStart").Index
                For ii = 0 To iBraceList.Length - 1
                    If (iBraceIndex = iBraceList(ii)(0)) Then
                        sMethodmapSource = sSource.Substring(iBraceList(ii)(0) + 1, iBraceList(ii)(1) - iBraceList(ii)(0) - 1)
                        bIsValid = True
                        Exit For
                    End If
                Next
                If (Not bIsValid) Then
                    Continue For
                End If

                sMethodMapName = mMatch.Groups("Name").Value
                sMethodMapHasParent = mMatch.Groups("Parent").Success
                sMethodMapParentName = mMatch.Groups("Parent").Value
                sMethodMapFullParentName = mMatch.Groups("FullParent").Value
                sMethodMapParentingName = mMatch.Groups("ParentingName").Value

                If (True) Then
                    Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE("",
                                                                                IO.Path.GetFileName(sFile),
                                                                                sFile,
                                                                                ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP,
                                                                                sMethodMapName,
                                                                                sMethodMapName,
                                                                                "methodmap " & sMethodMapName & sMethodMapFullParentName)

                    mAutocomplete.m_Data("MethodmapName") = sMethodMapName
                    mAutocomplete.m_Data("MethodmapParentName") = sMethodMapParentName
                    mAutocomplete.m_Data("MethodmapParentingName") = sMethodMapParentingName

                    mAutocomplete.m_Data("MethodmapMethodType") = ""
                    mAutocomplete.m_Data("MethodmapMethodTag") = ""
                    mAutocomplete.m_Data("MethodmapMethodName") = ""
                    mAutocomplete.m_Data("MethodmapMethodArguments") = ""
                    mAutocomplete.m_Data("MethodmapMethodIsConstructor") = False

#If DEBUG Then
                    mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get methodmaps"
#End If

                    If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                        lNewAutocompleteList.Add(mAutocomplete)
                    End If
                End If

                Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sMethodmapSource, iLanguage)
                Dim iMethodmapBraceList As Integer()() = g_mFormMain.g_ClassSyntaxTools.GetExpressionBetweenCharacters(sMethodmapSource, "("c, ")"c, 1, iLanguage, True)

                Dim mMethodMatches As MatchCollection = Regex.Matches(sMethodmapSource,
                                                                      String.Format("^\s*(?<Type>\b(property|public\s+(static\s*){2}(native\s*){4}|public)\b)\s+((?<Tag>\b({0})\b\s)\s*(?<Name>\b[a-zA-Z0-9_]+\b)|(?<Constructor>\b{1}\b)|(?<Name>\b[a-zA-Z0-9_]+\b))\s*(?<BraceStart>\(){3}", sRegExTypePattern, sMethodMapName, "{0,1}", "{0,1}", "{0,1}"),
                                                                      RegexOptions.Multiline)

                Dim SB As StringBuilder

                For ii = 0 To mMethodMatches.Count - 1
                    If (mSourceAnalysis.m_InNonCode(mMethodMatches(ii).Groups("Type").Index)) Then
                        Continue For
                    End If

                    SB = New StringBuilder
                    For iii = mMethodMatches(ii).Groups("Type").Index - 1 To 0 Step -1
                        Select Case (sMethodmapSource(iii))
                            Case " "c, vbTab(0), vbLf(0), vbCr(0)
                                SB.Append(sMethodmapSource(iii))
                            Case Else
                                If (Not mSourceAnalysis.m_InMultiComment(iii) AndAlso Not mSourceAnalysis.m_InSingleComment(iii) AndAlso Not mSourceAnalysis.m_InPreprocessor(iii)) Then
                                    Exit For
                                End If

                                SB.Append(sMethodmapSource(iii))
                        End Select
                    Next

                    Dim sComment As String = StrReverse(SB.ToString)
                    sComment = Regex.Replace(sComment, "^\s+", "", RegexOptions.Multiline)
                    sComment = Regex.Replace(sComment, "\s+$", "", RegexOptions.Multiline)
                    Dim sType As String = mMethodMatches(ii).Groups("Type").Value
                    Dim sTag As String = mMethodMatches(ii).Groups("Tag").Value.Trim
                    Dim sName As String = mMethodMatches(ii).Groups("Name").Value

                    If (sName = "get" OrElse sName = "set") Then
                        Continue For
                    End If

                    If (sType = "property") Then
                        Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE(sComment,
                                                                                    IO.Path.GetFileName(sFile),
                                                                                    sFile,
                                                                                    ClassSyntaxTools.STRUC_AUTOCOMPLETE.ParseTypeFullNames(sType) Or ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP,
                                                                                    sName,
                                                                                    String.Format("{0}.{1}", sMethodMapName, sName),
                                                                                    String.Format("methodmap {0} {1}{2}{3} = {4} {5}", sType, sMethodMapName, sMethodMapParentingName, If(sMethodMapHasParent, " < " & sMethodMapParentName, ""), sTag, sName))

                        mAutocomplete.m_Data("MethodmapName") = sMethodMapName
                        mAutocomplete.m_Data("MethodmapParentName") = sMethodMapParentName
                        mAutocomplete.m_Data("MethodmapParentingName") = sMethodMapParentingName

                        mAutocomplete.m_Data("MethodmapMethodType") = sType
                        mAutocomplete.m_Data("MethodmapMethodTag") = sTag
                        mAutocomplete.m_Data("MethodmapMethodName") = sName
                        mAutocomplete.m_Data("MethodmapMethodArguments") = ""
                        mAutocomplete.m_Data("MethodmapMethodIsConstructor") = False

#If DEBUG Then
                        mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get methodmaps"
#End If

                        If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                            lNewAutocompleteList.Add(mAutocomplete)
                        End If
                    Else
                        Dim bIsConstructor As Boolean = mMethodMatches(ii).Groups("Constructor").Success
                        Dim iBraceStart As Integer = mMethodMatches(ii).Groups("BraceStart").Index
                        Dim sBraceString As String = Nothing

                        For iii = 0 To iMethodmapBraceList.Length - 1
                            If (iBraceStart = iMethodmapBraceList(iii)(0)) Then
                                sBraceString = sMethodmapSource.Substring(iMethodmapBraceList(iii)(0), iMethodmapBraceList(iii)(1) - iMethodmapBraceList(iii)(0) + 1)
                                Exit For
                            End If
                        Next

                        If (String.IsNullOrEmpty(sBraceString)) Then
                            Continue For
                        End If

                        If (bIsConstructor) Then
                            Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE(sComment,
                                                                                        IO.Path.GetFileName(sFile),
                                                                                        sFile,
                                                                                        ClassSyntaxTools.STRUC_AUTOCOMPLETE.ParseTypeFullNames(sType) Or ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP,
                                                                                        sMethodMapName,
                                                                                        sMethodMapName,
                                                                                        String.Format("methodmap {0}{1} = {2}{3}", sMethodMapName, If(sMethodMapHasParent, " < " & sMethodMapParentName, ""), sMethodMapName, sBraceString))

                            mAutocomplete.m_Data("MethodmapName") = sMethodMapName
                            mAutocomplete.m_Data("MethodmapParentName") = sMethodMapParentName
                            mAutocomplete.m_Data("MethodmapParentingName") = sMethodMapParentingName

                            mAutocomplete.m_Data("MethodmapMethodType") = sType
                            mAutocomplete.m_Data("MethodmapMethodTag") = sTag
                            mAutocomplete.m_Data("MethodmapMethodName") = sName
                            mAutocomplete.m_Data("MethodmapMethodArguments") = sBraceString
                            mAutocomplete.m_Data("MethodmapMethodIsConstructor") = True

#If DEBUG Then
                            mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get methodmaps"
#End If

                            'Remove all single methodmaps and replace them with the constructor, the enum version needs to stay for autocompletion.
                            lNewAutocompleteList.RemoveAll(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)

                            If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                                lNewAutocompleteList.Add(mAutocomplete)
                            End If
                        Else
                            Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE(sComment,
                                                                                        IO.Path.GetFileName(sFile),
                                                                                        sFile,
                                                                                        ClassSyntaxTools.STRUC_AUTOCOMPLETE.ParseTypeFullNames(sType) Or ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP,
                                                                                        sName,
                                                                                        String.Format("{0}.{1}", sMethodMapName, sName),
                                                                                        String.Format("methodmap {0} {1}{2} = {3} {4}{5}", sType, sMethodMapName, If(sMethodMapHasParent, " < " & sMethodMapParentName, ""), sTag, sName, sBraceString))

                            mAutocomplete.m_Data("MethodmapName") = sMethodMapName
                            mAutocomplete.m_Data("MethodmapParentName") = sMethodMapParentName
                            mAutocomplete.m_Data("MethodmapParentingName") = sMethodMapParentingName

                            mAutocomplete.m_Data("MethodmapMethodType") = sType
                            mAutocomplete.m_Data("MethodmapMethodTag") = sTag
                            mAutocomplete.m_Data("MethodmapMethodName") = sName
                            mAutocomplete.m_Data("MethodmapMethodArguments") = sBraceString
                            mAutocomplete.m_Data("MethodmapMethodIsConstructor") = False

#If DEBUG Then
                            mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Get methodmaps"
#End If

                            If (Not lNewAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                                lNewAutocompleteList.Add(mAutocomplete)
                            End If
                        End If
                    End If
                Next
            Next
        End If
    End Sub

    Private Sub VariableAutocompleteUpdate_Thread(sTabIdentifier As String)
        Try
            Dim mRequestTab As ClassTabControl.SourceTabPage = ClassThread.ExecEx(Of ClassTabControl.SourceTabPage)(g_mFormMain, Function() g_mFormMain.g_ClassTabControl.GetTabByIdentifier(sTabIdentifier))
            If (mRequestTab Is Nothing) Then
                'g_mFormMain.PrintInformation("[WARN]", "Variable autocomplete update failed! Could not get tab!", False, False)
                Return
            End If

            Dim sRequestedSourceFile As String = mRequestTab.m_File
            Dim iRequestedLangauge As ClassSyntaxTools.ENUM_LANGUAGE_TYPE = mRequestTab.m_Language
            Dim sRequestedSource As String = ClassThread.ExecEx(Of String)(mRequestTab, Function() mRequestTab.m_TextEditor.Document.TextContent)

            'g_mFormMain.PrintInformation("[INFO]", "Variable autocomplete update started...")
            If (String.IsNullOrEmpty(sRequestedSourceFile) OrElse Not IO.File.Exists(sRequestedSourceFile)) Then
                'g_mFormMain.PrintInformation("[ERRO]", "Variable autocomplete update failed! Could not get current source file!")
                Return
            End If

            Dim lOldVarAutocompleteList As New ClassSyncList(Of ClassSyntaxTools.STRUC_AUTOCOMPLETE)
            lOldVarAutocompleteList.AddRange(mRequestTab.m_AutocompleteItems.ToArray)

            'No autocomplete entries?
            If (lOldVarAutocompleteList.Count < 1) Then
                Return
            End If

            Dim mPreWatch As New Stopwatch
            Dim mPostWatch As New Stopwatch
            Dim mApplyWatch As New Stopwatch

            Dim lNewVarAutocompleteList As New ClassSyncList(Of ClassSyntaxTools.STRUC_AUTOCOMPLETE)

            'Parse variables and create methodmaps for variables
            If (True) Then
                mPreWatch.Start()
                If (ClassSettings.g_iSettingsVarAutocompleteCurrentSourceOnly) Then
                    ParseVariables_Pre(sRequestedSource, sRequestedSourceFile, sRequestedSourceFile, lNewVarAutocompleteList, lOldVarAutocompleteList, iRequestedLangauge)
                Else
                    Dim mIncludeFiles = mRequestTab.m_IncludeFiles.ToArray
                    For i = 0 To mIncludeFiles.Length - 1
                        ParseVariables_Pre(sRequestedSource, sRequestedSourceFile, CStr(mIncludeFiles(i).Value), lNewVarAutocompleteList, lOldVarAutocompleteList, iRequestedLangauge)
                    Next
                End If
                mPreWatch.Stop()

                mPostWatch.Start()
                ParseVariables_Post(sRequestedSource, sRequestedSourceFile, lNewVarAutocompleteList, lOldVarAutocompleteList, iRequestedLangauge)
                mPostWatch.Stop()
            End If

            mApplyWatch.Start()
            mRequestTab.m_AutocompleteItems.DoSync(
                Sub()
                    mRequestTab.m_AutocompleteItems.RemoveAll(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE)
                    mRequestTab.m_AutocompleteItems.AddRange(lNewVarAutocompleteList.ToArray)
                End Sub)
            mApplyWatch.Stop()

            lNewVarAutocompleteList = Nothing

#If DEBUG AndAlso PROFILE_AUTOCOMPLETE Then
            g_mFormMain.PrintInformation("[DEBG]", "Variable Autocomplete update finished!")
            g_mFormMain.PrintInformation("[DEBG]", vbTab & "Times:")
            g_mFormMain.PrintInformation("[DEBG]", vbTab & "Pre: " & mPreWatch.Elapsed.ToString)
            g_mFormMain.PrintInformation("[DEBG]", vbTab & "Post: " & mPostWatch.Elapsed.ToString)
            g_mFormMain.PrintInformation("[DEBG]", vbTab & "Apply: " & mApplyWatch.Elapsed.ToString)
#End If
            'g_mFormMain.PrintInformation("[INFO]", "Variable autocomplete update finished!")
        Catch ex As Threading.ThreadAbortException
            Throw
        Catch ex As Exception
            g_mFormMain.PrintInformation("[ERRO]", "Variable autocomplete update failed! " & ex.Message, False, False)
            ClassExceptionLog.WriteToLog(ex)
        End Try
    End Sub

    Private Structure STRUC_PARSE_ARGUMENT_ITEM
        Dim sArgument As String
        Dim sFile As String
    End Structure

    Private Sub ParseVariables_Pre(sActiveSource As String,
                                   sActiveSourceFile As String,
                                   ByRef sFile As String,
                                   ByRef lNewVarAutocompleteList As ClassSyncList(Of ClassSyntaxTools.STRUC_AUTOCOMPLETE),
                                   ByRef lOldVarAutocompleteList As ClassSyncList(Of ClassSyntaxTools.STRUC_AUTOCOMPLETE),
                                   iLanguage As ClassSyntaxTools.ENUM_LANGUAGE_TYPE)
        Dim sSource As String
        If (sActiveSourceFile.ToLower = sFile.ToLower) Then
            sSource = sActiveSource
        Else
            sSource = IO.File.ReadAllText(sFile)
        End If

        CleanUpNewLinesSource(sSource, iLanguage)

        'Parse variables
        If (True) Then
            Dim sRegExTypePattern As String = GetTypeNamesToPattern(lOldVarAutocompleteList)

            Dim sStatementsVar As String = "\b(for)\b"
            Dim sInitTypesPattern As String = "\b(new|decl|static|const)\b" '"public" is already taken care off
            Dim sOldStyleVarPattern As String = String.Format("(?<Init>{0}\s+)(?<IsConst>\b(const)\b\s+){2}((?<Tag>{1})\:\s*(?<Var>\b[a-zA-Z0-9_]+\b)|(?<Var>\b[a-zA-Z0-9_]+\b))($|\W)", sInitTypesPattern, sRegExTypePattern, "{0,1}")
            Dim sNewStyleVarPattern As String = String.Format("(?<IsConst>\b(const)\b\s+){1}(?<Tag>{0})\s+(?<Var>\b[a-zA-Z0-9_]+\b)($|\W)", sRegExTypePattern, "{0,1}")

            Dim mSourceList As New List(Of String) From {
                sSource
            }

            'Get variables from statements
            If (True) Then
                Dim mStatementMatches = Regex.Matches(sSource, String.Format("(?<Name>{0})\s*(?<Start>\()", sStatementsVar))
                Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sSource, iLanguage)
                Dim mSourceBuilder As StringBuilder

                For Each mMatch As Match In mStatementMatches
                    mSourceBuilder = New StringBuilder

                    Dim iStartIndex As Integer = mMatch.Groups("Start").Index
                    Dim iStartLevel As Integer = mSourceAnalysis.GetParenthesisLevel(iStartIndex, Nothing)
                    Dim bFinished As Boolean = False

                    For i = iStartIndex To sSource.Length - 1
                        If (mSourceAnalysis.m_InNonCode(i)) Then
                            Continue For
                        End If

                        Dim iParentRange As ClassSyntaxTools.ClassSyntaxSourceAnalysis.ENUM_STATE_RANGE
                        Dim iParentLevel = mSourceAnalysis.GetParenthesisLevel(i, iParentRange)

                        If (iParentLevel < iStartLevel) Then
                            bFinished = True
                            Exit For
                        End If

                        If (iParentLevel = iStartLevel) Then
                            If (iParentRange <> ClassSyntaxTools.ClassSyntaxSourceAnalysis.ENUM_STATE_RANGE.NONE) Then
                                Continue For
                            End If
                        End If

                        If (iParentLevel > iStartLevel) Then
                            Select Case (iParentRange)
                                Case ClassSyntaxTools.ClassSyntaxSourceAnalysis.ENUM_STATE_RANGE.START
                                    mSourceBuilder.Append("(")

                                Case ClassSyntaxTools.ClassSyntaxSourceAnalysis.ENUM_STATE_RANGE.END
                                    mSourceBuilder.Append(")")
                            End Select

                            Continue For
                        End If

                        mSourceBuilder.Append(sSource(i))
                    Next

                    If (bFinished) Then
                        If (mSourceBuilder.Length > 0) Then
                            mSourceList.Add(mSourceBuilder.ToString)
                        End If
                    End If
                Next
            End If

            Dim lCommaLinesList As New List(Of String)

            For j = 0 To mSourceList.Count - 1
                Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(mSourceList(j), iLanguage)
                Dim mCodeBuilder As New StringBuilder

                'This is the easiest and yet stupiest idea to resolve multi-decls i've ever come up with...
                For i = 0 To mSourceList(j).Length - 1
                    If (i <> mSourceList(j).Length - 1) Then
                        If (mSourceAnalysis.m_InNonCode(i)) Then
                            Continue For
                        End If

                        If (mSourceAnalysis.GetBracketLevel(i, Nothing) > 0) Then
                            Continue For
                        End If

                        Dim iParentRange As ClassSyntaxTools.ClassSyntaxSourceAnalysis.ENUM_STATE_RANGE
                        If (mSourceAnalysis.GetParenthesisLevel(i, iParentRange) > 0) Then
                            Select Case (iParentRange)
                                Case ClassSyntaxTools.ClassSyntaxSourceAnalysis.ENUM_STATE_RANGE.START
                                    mCodeBuilder.Append("(")

                                Case ClassSyntaxTools.ClassSyntaxSourceAnalysis.ENUM_STATE_RANGE.END
                                    mCodeBuilder.Append(")")
                            End Select

                            Continue For
                        End If
                    End If

                    If (mSourceList(j)(i) = ","c OrElse mSourceList(j)(i) = "="c OrElse i = mSourceList(j).Length - 1) Then
                        Dim sLine As String = mCodeBuilder.ToString.Trim

                        If (Not String.IsNullOrEmpty(sLine)) Then
                            lCommaLinesList.Add(sLine)
                        End If

                        mCodeBuilder = New StringBuilder

                        'To make sure the assignment doesnt get parsed as variable
                        If (mSourceList(j)(i) = "="c) Then
                            mCodeBuilder.Append(mSourceList(j)(i))
                        End If

                        Continue For
                    End If

                    mCodeBuilder.Append(mSourceList(j)(i))
                Next
            Next

            '0: Not yet found
            '1: Old style
            '2: New style
            Dim iLastInitStyle As Byte = 0
            Dim iLastInitIndex As Integer = 0
            Dim sLastTag As String = Nothing

            For Each sLine As String In lCommaLinesList
                Select Case (iLastInitStyle)
                    Case 1 'Old Style
                        If (ClassSettings.g_iSettingsAutocompleteSyntax <> ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_MIX AndAlso ClassSettings.g_iSettingsAutocompleteSyntax <> ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_6) Then
                            Exit Select
                        End If

                        Dim mMatch As Match = Regex.Match(sLine, String.Format("^((?<Tag>{0})\:\s*)*(?<Var>\b[a-zA-Z_][a-zA-Z0-9_]*\b)((?<End>$)|(?<IsFunc>\()|(?<IsMethodmap>\.)|(?<IsTag>\:)|(?<More>\W))", sRegExTypePattern))
                        Dim sTag As String = mMatch.Groups("Tag").Value.Trim
                        Dim sVar As String = mMatch.Groups("Var").Value.Trim

                        If (mMatch.Groups("IsFunc").Success OrElse mMatch.Groups("IsMethodmap").Success OrElse mMatch.Groups("IsTag").Success OrElse Not mMatch.Groups("Var").Success) Then
                            Exit Select
                        End If

                        If (g_mFormMain.g_ClassSyntaxTools.IsForbiddenVariableName(sVar)) Then
                            Exit Select
                        End If

                        If (Regex.IsMatch(sVar, sRegExTypePattern)) Then
                            Exit Select
                        End If

                        If (lOldVarAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE)
                                                               If ((x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FORWARD) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FORWARD OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCENUM) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCENUM OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCTAG) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCTAG OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPEDEF) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPEDEF OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPESET) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPESET) Then
                                                                   Return False
                                                               End If

                                                               Return Regex.IsMatch(x.m_FunctionString, String.Format("\b{0}\b", Regex.Escape(sVar)))
                                                           End Function)) Then
                            Exit Select
                        End If

                        If (String.IsNullOrEmpty(sTag)) Then
                            sTag = "int"
                        End If

                        Dim sTmpFile As String = IO.Path.GetFileName(sFile)
                        Dim mItem As ClassSyntaxTools.STRUC_AUTOCOMPLETE = lNewVarAutocompleteList.Find(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Filename.ToLower = sTmpFile.ToLower AndAlso x.m_FunctionString = sVar AndAlso (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE)
                        If (mItem Is Nothing) Then
                            Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE("",
                                                                                        IO.Path.GetFileName(sFile),
                                                                                        sFile,
                                                                                        ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE,
                                                                                        sVar,
                                                                                        sVar,
                                                                                        String.Format("{0} > {1}", sVar, sTag))

                            mAutocomplete.m_Data("VariableName") = sVar
                            mAutocomplete.m_Data("VariableTags") = New String() {sTag}

#If DEBUG Then
                            mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Parse variables"
#End If

                            lNewVarAutocompleteList.Add(mAutocomplete)
                        Else
                            If (Not Regex.IsMatch(mItem.m_FullFunctionString, String.Format("\b{0}\b", Regex.Escape(sTag)))) Then
                                mItem.m_FullFunctionString = String.Format("{0}|{1}", mItem.m_FullFunctionString, sTag)
                            End If

                            If (mItem.m_Data.ContainsKey("VariableTags")) Then
                                Dim sItemTags As String() = CType(mItem.m_Data("VariableTags"), String())

                                If (Array.IndexOf(sItemTags, sTag) = -1) Then
                                    With New List(Of String)
                                        .AddRange(sItemTags)
                                        .Add(sTag)
                                        mItem.m_Data("VariableTags") = .ToArray
                                    End With
                                End If
                            Else
                                Throw New ArgumentException("VariableTags invalid")
                            End If
                        End If

                    Case 2 'New Style
                        If (ClassSettings.g_iSettingsAutocompleteSyntax <> ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_MIX AndAlso ClassSettings.g_iSettingsAutocompleteSyntax <> ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_7) Then
                            Exit Select
                        End If

                        Dim mMatch As Match = Regex.Match(sLine, String.Format("^(?<Tag>{0}\s+)*(?<Var>\b[a-zA-Z_][a-zA-Z0-9_]*\b)((?<End>$)|(?<IsFunc>\()|(?<IsMethodmap>\.)|(?<IsTag>\:)|(?<More>\W))", sRegExTypePattern))
                        Dim sTag As String = mMatch.Groups("Tag").Value.Trim
                        Dim sVar As String = mMatch.Groups("Var").Value.Trim

                        If (mMatch.Groups("IsFunc").Success OrElse mMatch.Groups("IsMethodmap").Success OrElse mMatch.Groups("IsTag").Success OrElse Not mMatch.Groups("Var").Success) Then
                            Exit Select
                        End If

                        If (g_mFormMain.g_ClassSyntaxTools.IsForbiddenVariableName(sVar)) Then
                            Exit Select
                        End If

                        If (Regex.IsMatch(sVar, sRegExTypePattern)) Then
                            Exit Select
                        End If

                        If (lOldVarAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE)
                                                               If ((x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FORWARD) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FORWARD OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCENUM) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCENUM OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCTAG) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCTAG OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPEDEF) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPEDEF OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPESET) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPESET) Then
                                                                   Return False
                                                               End If

                                                               Return Regex.IsMatch(x.m_FunctionString, String.Format("\b{0}\b", Regex.Escape(sVar)))
                                                           End Function)) Then
                            Exit Select
                        End If

                        If (String.IsNullOrEmpty(sTag)) Then
                            If (String.IsNullOrEmpty(sLastTag)) Then
                                sTag = "int"
                            Else
                                sTag = sLastTag
                            End If
                        End If

                        Dim sTmpFile As String = IO.Path.GetFileName(sFile)
                        Dim mItem As ClassSyntaxTools.STRUC_AUTOCOMPLETE = lNewVarAutocompleteList.Find(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Filename.ToLower = sTmpFile.ToLower AndAlso x.m_FunctionString = sVar AndAlso (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE)
                        If (mItem Is Nothing) Then
                            Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE("",
                                                                                        IO.Path.GetFileName(sFile),
                                                                                        sFile,
                                                                                        ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE,
                                                                                        sVar,
                                                                                        sVar,
                                                                                        String.Format("{0} > {1}", sVar, sTag))

                            mAutocomplete.m_Data("VariableName") = sVar
                            mAutocomplete.m_Data("VariableTags") = New String() {sTag}

#If DEBUG Then
                            mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Parse variables"
#End If

                            lNewVarAutocompleteList.Add(mAutocomplete)
                        Else
                            If (Not Regex.IsMatch(mItem.m_FullFunctionString, String.Format("\b{0}\b", Regex.Escape(sTag)))) Then
                                mItem.m_FullFunctionString = String.Format("{0}|{1}", mItem.m_FullFunctionString, sTag)
                            End If

                            If (mItem.m_Data.ContainsKey("VariableTags")) Then
                                Dim sItemTags As String() = CType(mItem.m_Data("VariableTags"), String())

                                If (Array.IndexOf(sItemTags, sTag) = -1) Then
                                    With New List(Of String)
                                        .AddRange(sItemTags)
                                        .Add(sTag)
                                        mItem.m_Data("VariableTags") = .ToArray
                                    End With
                                End If
                            Else
                                Throw New ArgumentException("VariableTags invalid")
                            End If
                        End If

                End Select

                iLastInitIndex = 0
                If (sLine.Contains(";"c)) Then
                    iLastInitStyle = 0
                End If


                If (ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_MIX OrElse ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_6) Then
                    For Each mMatch As Match In Regex.Matches(sLine, sOldStyleVarPattern)
                        Dim iIndex As Integer = mMatch.Groups("Var").Index
                        Dim sInit As String = mMatch.Groups("Init").Value.Trim
                        Dim sTag As String = mMatch.Groups("Tag").Value.Trim
                        Dim sVar As String = mMatch.Groups("Var").Value.Trim
                        Dim bIsConst As Boolean = mMatch.Groups("IsConst").Success

                        If (iLastInitIndex < iIndex) Then
                            iLastInitIndex = iIndex
                            iLastInitStyle = 1 'Old style
                        End If

                        sLastTag = Nothing

                        If (g_mFormMain.g_ClassSyntaxTools.IsForbiddenVariableName(sVar)) Then
                            Continue For
                        End If

                        If (Regex.IsMatch(sVar, sRegExTypePattern)) Then
                            Continue For
                        End If

                        If (lOldVarAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE)
                                                               If ((x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FORWARD) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FORWARD OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCENUM) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCENUM OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCTAG) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCTAG OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPEDEF) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPEDEF OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPESET) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPESET) Then
                                                                   Return False
                                                               End If

                                                               Return Regex.IsMatch(x.m_FunctionString, String.Format("\b{0}\b", Regex.Escape(sVar)))
                                                           End Function)) Then
                            Continue For
                        End If

                        If (String.IsNullOrEmpty(sTag)) Then
                            sTag = "int"
                        End If

                        Dim sTmpFile As String = IO.Path.GetFileName(sFile)
                        Dim mItem As ClassSyntaxTools.STRUC_AUTOCOMPLETE = lNewVarAutocompleteList.Find(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Filename.ToLower = sTmpFile.ToLower AndAlso x.m_FunctionString = sVar AndAlso (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE)
                        If (mItem Is Nothing) Then
                            Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE("",
                                                                                        IO.Path.GetFileName(sFile),
                                                                                        sFile,
                                                                                        ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE,
                                                                                        sVar,
                                                                                        sVar,
                                                                                        String.Format("{0} > {1}", sVar, sTag)) 'String.Format("{0} > {1}", sVar, If(bIsConst, "const:", "") & sTag)

                            mAutocomplete.m_Data("VariableName") = sVar
                            mAutocomplete.m_Data("VariableTags") = New String() {sTag}

#If DEBUG Then
                            mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Parse variables"
#End If

                            lNewVarAutocompleteList.Add(mAutocomplete)
                        Else
                            If (Not Regex.IsMatch(mItem.m_FullFunctionString, String.Format("\b{0}\b", Regex.Escape(sTag)))) Then
                                mItem.m_FullFunctionString = String.Format("{0}|{1}", mItem.m_FullFunctionString, sTag)
                            End If
                            'If (bIsConst AndAlso Not Regex.IsMatch(mItem.m_FullFunctionName, String.Format("\b{0}\b", "const"))) Then
                            '    mItem.m_FullFunctionName = String.Format("{0}:{1}", "const", mItem.m_FullFunctionName)
                            'End If

                            If (mItem.m_Data.ContainsKey("VariableTags")) Then
                                Dim sItemTags As String() = CType(mItem.m_Data("VariableTags"), String())

                                If (Array.IndexOf(sItemTags, sTag) = -1) Then
                                    With New List(Of String)
                                        .AddRange(sItemTags)
                                        .Add(sTag)
                                        mItem.m_Data("VariableTags") = .ToArray
                                    End With
                                End If
                            Else
                                Throw New ArgumentException("VariableTags invalid")
                            End If
                        End If
                    Next
                End If

                If (ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_MIX OrElse ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_7) Then
                    For Each mMatch As Match In Regex.Matches(sLine, sNewStyleVarPattern)
                        Dim iIndex As Integer = mMatch.Groups("Var").Index
                        Dim sInit As String = mMatch.Groups("Init").Value.Trim
                        Dim sTag As String = mMatch.Groups("Tag").Value.Trim
                        Dim sVar As String = mMatch.Groups("Var").Value.Trim
                        Dim bIsConst As Boolean = mMatch.Groups("IsConst").Success

                        If (iLastInitIndex < iIndex) Then
                            iLastInitIndex = iIndex
                            iLastInitStyle = 2 'New style
                        End If

                        sLastTag = Nothing

                        If (String.IsNullOrEmpty(sTag)) Then
                            Continue For
                        End If

                        If (g_mFormMain.g_ClassSyntaxTools.IsForbiddenVariableName(sVar)) Then
                            Continue For
                        End If

                        If (Regex.IsMatch(sVar, sRegExTypePattern)) Then
                            Continue For
                        End If

                        If (lOldVarAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE)
                                                               If ((x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FORWARD) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FORWARD OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCENUM) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCENUM OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCTAG) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.FUNCTAG OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPEDEF) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPEDEF OrElse
                                                                    (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPESET) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.TYPESET) Then
                                                                   Return False
                                                               End If

                                                               Return Regex.IsMatch(x.m_FunctionString, String.Format("\b{0}\b", Regex.Escape(sVar)))
                                                           End Function)) Then
                            Continue For
                        End If

                        sLastTag = sTag

                        Dim sTmpFile As String = IO.Path.GetFileName(sFile)
                        Dim mItem As ClassSyntaxTools.STRUC_AUTOCOMPLETE = lNewVarAutocompleteList.Find(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Filename.ToLower = sTmpFile.ToLower AndAlso x.m_FunctionString = sVar AndAlso (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE)
                        If (mItem Is Nothing) Then
                            Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE("",
                                                                                        IO.Path.GetFileName(sFile),
                                                                                        sFile,
                                                                                        ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE,
                                                                                        sVar,
                                                                                        sVar,
                                                                                        String.Format("{0} > {1}", sVar, sTag)) 'String.Format("{0} > {1}", sVar, If(bIsConst, "const:", "") & sTag)

                            mAutocomplete.m_Data("VariableName") = sVar
                            mAutocomplete.m_Data("VariableTags") = New String() {sTag}

#If DEBUG Then
                            mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Parse variables"
#End If

                            lNewVarAutocompleteList.Add(mAutocomplete)
                        Else
                            If (Not Regex.IsMatch(mItem.m_FullFunctionString, String.Format("\b{0}\b", Regex.Escape(sTag)))) Then
                                mItem.m_FullFunctionString = String.Format("{0}|{1}", mItem.m_FullFunctionString, sTag)
                            End If
                            'If (bIsConst AndAlso Not Regex.IsMatch(mItem.m_FullFunctionName, String.Format("\b{0}\b", "const"))) Then
                            '    mItem.m_FullFunctionName = String.Format("{0}:{1}", "const", mItem.m_FullFunctionName)
                            'End If

                            If (mItem.m_Data.ContainsKey("VariableTags")) Then
                                Dim sItemTags As String() = CType(mItem.m_Data("VariableTags"), String())

                                If (Array.IndexOf(sItemTags, sTag) = -1) Then
                                    With New List(Of String)
                                        .AddRange(sItemTags)
                                        .Add(sTag)
                                        mItem.m_Data("VariableTags") = .ToArray
                                    End With
                                End If
                            Else
                                Throw New ArgumentException("VariableTags invalid")
                            End If
                        End If
                    Next
                End If
            Next
        End If

    End Sub

    Private Sub ParseVariables_Post(sActiveSource As String,
                                    sActiveSourceFile As String,
                                    ByRef lNewVarAutocompleteList As ClassSyncList(Of ClassSyntaxTools.STRUC_AUTOCOMPLETE),
                                    ByRef lOldVarAutocompleteList As ClassSyncList(Of ClassSyntaxTools.STRUC_AUTOCOMPLETE),
                                    iLanguage As ClassSyntaxTools.ENUM_LANGUAGE_TYPE)
        'Parse function argument variables
        If (True) Then
            Dim sRegExTypePattern As String = GetTypeNamesToPattern(lOldVarAutocompleteList)

            Dim lArgList As New List(Of STRUC_PARSE_ARGUMENT_ITEM)
            Dim mCodeBuilder As New StringBuilder

            For Each mItem In lOldVarAutocompleteList
                If ((mItem.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) Then
                    Continue For
                End If

                If (ClassSettings.g_iSettingsVarAutocompleteCurrentSourceOnly) Then
                    If (Not String.IsNullOrEmpty(sActiveSourceFile) AndAlso sActiveSourceFile.ToLower <> mItem.m_Path.ToLower) Then
                        Continue For
                    End If
                End If

                Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(mItem.m_FullFunctionString, iLanguage)
                mCodeBuilder = New StringBuilder

                Dim bNeedSave As Boolean = False
                'This is the easiest and yet stupiest idea to resolve multi-decls i've ever come up with...
                For i = 0 To mItem.m_FullFunctionString.Length - 1
                    If (bNeedSave OrElse i = mItem.m_FullFunctionString.Length - 1) Then
                        bNeedSave = False

                        Dim sLine As String = mCodeBuilder.ToString.Trim
                        sLine = Regex.Replace(sLine, "(\=)+(.*$)", "")
                        sLine = Regex.Replace(sLine, Regex.Escape("&"), "")
                        sLine = Regex.Replace(sLine, Regex.Escape("@"), "")

                        If (Not String.IsNullOrEmpty(sLine)) Then
                            lArgList.Add(New STRUC_PARSE_ARGUMENT_ITEM With {.sArgument = sLine, .sFile = mItem.m_Filename})
                        End If

                        mCodeBuilder = New StringBuilder
                    End If

                    If (mSourceAnalysis.m_InNonCode(i)) Then
                        Continue For
                    End If

                    If (mSourceAnalysis.GetBracketLevel(i, Nothing) > 0 OrElse
                                mSourceAnalysis.GetBraceLevel(i, Nothing) > 0) Then
                        Continue For
                    End If

                    Dim iParentRange As ClassSyntaxTools.ClassSyntaxSourceAnalysis.ENUM_STATE_RANGE
                    If (mSourceAnalysis.GetParenthesisLevel(i, iParentRange) < 1 OrElse
                                iParentRange <> ClassSyntaxTools.ClassSyntaxSourceAnalysis.ENUM_STATE_RANGE.NONE) Then
                        Continue For
                    End If

                    If (mItem.m_FullFunctionString(i) = ","c) Then
                        bNeedSave = True
                        Continue For
                    Else
                        mCodeBuilder.Append(mItem.m_FullFunctionString(i))
                    End If
                Next
            Next

            For Each mArg As STRUC_PARSE_ARGUMENT_ITEM In lArgList
                'Old style
                If (ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_MIX OrElse ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_6) Then
                    Dim mMatch As Match = Regex.Match(mArg.sArgument, String.Format("(?<OneSevenTag>\b{0}\b\s+)*((?<Tag>\b{1}\b)\:\s*)*(?<Var>\b[a-zA-Z_][a-zA-Z0-9_]*\b)$", sRegExTypePattern, sRegExTypePattern))
                    Dim sTag As String = If(String.IsNullOrEmpty(mMatch.Groups("Tag").Value), "int", mMatch.Groups("Tag").Value).Trim
                    Dim sVar As String = mMatch.Groups("Var").Value.Trim
                    Dim bIsOneSeven As Boolean = mMatch.Groups("OneSevenTag").Success

                    If (Not bIsOneSeven AndAlso mMatch.Success AndAlso mMatch.Groups("Var").Success AndAlso Not g_mFormMain.g_ClassSyntaxTools.IsForbiddenVariableName(sVar)) Then
                        Dim sTmpFile As String = IO.Path.GetFileName(mArg.sFile)
                        Dim mItem As ClassSyntaxTools.STRUC_AUTOCOMPLETE = lNewVarAutocompleteList.Find(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Filename.ToLower = sTmpFile.ToLower AndAlso x.m_FunctionString = sVar AndAlso (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE)
                        If (mItem Is Nothing) Then
                            Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE("",
                                                                                        IO.Path.GetFileName(mArg.sFile),
                                                                                        mArg.sFile,
                                                                                        ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE,
                                                                                        sVar,
                                                                                        sVar,
                                                                                        String.Format("{0} > {1}", sVar, sTag))

                            mAutocomplete.m_Data("VariableName") = sVar
                            mAutocomplete.m_Data("VariableTags") = New String() {sTag}

#If DEBUG Then
                            mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Parse function argument variables"
#End If

                            lNewVarAutocompleteList.Add(mAutocomplete)
                        Else
                            If (Not Regex.IsMatch(mItem.m_FullFunctionString, String.Format("\b{0}\b", Regex.Escape(sTag)))) Then
                                mItem.m_FullFunctionString = String.Format("{0}|{1}", mItem.m_FullFunctionString, sTag)
                            End If

                            If (mItem.m_Data.ContainsKey("VariableTags")) Then
                                Dim sItemTags As String() = CType(mItem.m_Data("VariableTags"), String())

                                If (Array.IndexOf(sItemTags, sTag) = -1) Then
                                    With New List(Of String)
                                        .AddRange(sItemTags)
                                        .Add(sTag)
                                        mItem.m_Data("VariableTags") = .ToArray
                                    End With
                                End If
                            Else
                                Throw New ArgumentException("VariableTags invalid")
                            End If
                        End If
                    End If
                End If

                'New style
                If (ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_MIX OrElse ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_7) Then
                    Dim mMatch As Match = Regex.Match(mArg.sArgument, String.Format("(?<Tag>\b{0}\b)\s+(?<Var>\b[a-zA-Z_][a-zA-Z0-9_]*\b)$", sRegExTypePattern))
                    Dim sTag As String = If(String.IsNullOrEmpty(mMatch.Groups("Tag").Value), "int", mMatch.Groups("Tag").Value).Trim
                    Dim sVar As String = mMatch.Groups("Var").Value.Trim

                    If (mMatch.Success AndAlso mMatch.Groups("Var").Success AndAlso Not g_mFormMain.g_ClassSyntaxTools.IsForbiddenVariableName(sVar)) Then
                        Dim sTmpFile As String = IO.Path.GetFileName(mArg.sFile)
                        Dim mItem As ClassSyntaxTools.STRUC_AUTOCOMPLETE = lNewVarAutocompleteList.Find(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Filename.ToLower = sTmpFile.ToLower AndAlso x.m_FunctionString = sVar AndAlso (x.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE)
                        If (mItem Is Nothing) Then
                            Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE("",
                                                                                        IO.Path.GetFileName(mArg.sFile),
                                                                                        mArg.sFile,
                                                                                        ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE,
                                                                                        sVar,
                                                                                        sVar,
                                                                                        String.Format("{0} > {1}", sVar, sTag))

                            mAutocomplete.m_Data("VariableName") = sVar
                            mAutocomplete.m_Data("VariableTags") = New String() {sTag}

#If DEBUG Then
                            mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Parse function argument variables"
#End If

                            lNewVarAutocompleteList.Add(mAutocomplete)
                        Else
                            If (Not Regex.IsMatch(mItem.m_FullFunctionString, String.Format("\b{0}\b", Regex.Escape(sTag)))) Then
                                mItem.m_FullFunctionString = String.Format("{0}|{1}", mItem.m_FullFunctionString, sTag)
                            End If

                            If (mItem.m_Data.ContainsKey("VariableTags")) Then
                                Dim sItemTags As String() = CType(mItem.m_Data("VariableTags"), String())

                                If (Array.IndexOf(sItemTags, sTag) = -1) Then
                                    With New List(Of String)
                                        .AddRange(sItemTags)
                                        .Add(sTag)
                                        mItem.m_Data("VariableTags") = .ToArray
                                    End With
                                End If
                            Else
                                Throw New ArgumentException("VariableTags invalid")
                            End If
                        End If
                    End If
                End If
            Next
        End If

        'Make methodmaps using variables
        If (ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_MIX OrElse ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_7) Then
            Dim lVarMethodmapList As New List(Of ClassSyntaxTools.STRUC_AUTOCOMPLETE)

            For Each mVariableItem In lNewVarAutocompleteList
                If ((mVariableItem.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) <> ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) Then
                    Continue For
                End If

                Dim sVariableName As String = CStr(mVariableItem.m_Data("VariableName"))
                Dim sVariableTags As String() = CType(mVariableItem.m_Data("VariableTags"), String())
                If (String.IsNullOrEmpty(sVariableName)) Then
                    Continue For
                End If

                For Each mMethodmapItem In lOldVarAutocompleteList
                    If ((mMethodmapItem.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP) <> ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP OrElse
                                Not mMethodmapItem.m_FunctionString.Contains("."c)) Then
                        Continue For
                    End If

                    'TODO: Dont use yet, make methodmap parsing more efficent first
                    'If ((mMethodmapItem.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.STATIC) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.STATIC) Then
                    '    Continue For
                    'End If

                    Dim sMethodmapName As String = CStr(mMethodmapItem.m_Data("MethodmapName"))
                    Dim sMethodmapMethodName As String = CStr(mMethodmapItem.m_Data("MethodmapMethodName"))
                    If (String.IsNullOrEmpty(sMethodmapName) OrElse String.IsNullOrEmpty(sMethodmapMethodName)) Then
                        Continue For
                    End If

                    If (Array.IndexOf(sVariableTags, sMethodmapName) = -1) Then
                        Continue For
                    End If

                    Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE(mMethodmapItem.m_Info,
                                                                                mVariableItem.m_Filename,
                                                                                mVariableItem.m_Path,
                                                                                (ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE Or mMethodmapItem.m_Type) And Not ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP,
                                                                                sMethodmapMethodName,
                                                                                String.Format("{0}.{1}", sVariableName, sMethodmapMethodName),
                                                                                mMethodmapItem.m_FullFunctionString)

                    mAutocomplete.m_Data("VariableName") = sVariableName
                    mAutocomplete.m_Data("VariableTags") = sVariableTags

                    For Each mData In mMethodmapItem.m_Data
                        mAutocomplete.m_Data(mData.Key) = mData.Value
                    Next

                    mAutocomplete.m_Data("VariableMethodmapName") = sVariableName
                    mAutocomplete.m_Data("VariableMethodmapMethod") = sMethodmapMethodName

#If DEBUG Then
                    mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Make methodmaps using variables"
#End If

                    If (Not lNewVarAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString) AndAlso
                                Not lVarMethodmapList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                        lVarMethodmapList.Add(mAutocomplete)
                    End If
                Next
            Next

            lNewVarAutocompleteList.AddRange(lVarMethodmapList.ToArray)
        End If

        'Make methodmaps using methods
        If (ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_MIX OrElse ClassSettings.g_iSettingsAutocompleteSyntax = ClassSettings.ENUM_AUTOCOMPLETE_SYNTAX.SP_1_7) Then
            Dim lVarMethodmapList As New List(Of ClassSyntaxTools.STRUC_AUTOCOMPLETE)

            For Each mVariableItem In lOldVarAutocompleteList
                If ((mVariableItem.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE) Then
                    Continue For
                End If

                If ((mVariableItem.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHOD) <> ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHOD) Then
                    Continue For
                End If

                Dim sVariableName As String = CStr(mVariableItem.m_Data("MethodName"))
                Dim sVariableTag As String = CStr(mVariableItem.m_Data("MethodTag"))
                If (String.IsNullOrEmpty(sVariableName)) Then
                    Continue For
                End If

                For Each mMethodmapItem In lOldVarAutocompleteList
                    If ((mMethodmapItem.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP) <> ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP OrElse
                                Not mMethodmapItem.m_FunctionString.Contains("."c)) Then
                        Continue For
                    End If

                    'TODO: Dont use yet, make methodmap parsing more efficent first
                    'If ((mMethodmapItem.m_Type And ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.STATIC) = ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.STATIC) Then
                    '    Continue For
                    'End If

                    Dim sMethodmapName As String = CStr(mMethodmapItem.m_Data("MethodmapName"))
                    Dim sMethodmapMethodName As String = CStr(mMethodmapItem.m_Data("MethodmapMethodName"))
                    If (String.IsNullOrEmpty(sMethodmapName) OrElse String.IsNullOrEmpty(sMethodmapMethodName)) Then
                        Continue For
                    End If

                    If (sVariableTag <> sMethodmapName) Then
                        Continue For
                    End If

                    Dim mAutocomplete As New ClassSyntaxTools.STRUC_AUTOCOMPLETE(mMethodmapItem.m_Info,
                                                                                mVariableItem.m_Filename,
                                                                                mVariableItem.m_Path,
                                                                                (ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.VARIABLE Or mMethodmapItem.m_Type) And Not ClassSyntaxTools.STRUC_AUTOCOMPLETE.ENUM_TYPE_FLAGS.METHODMAP,
                                                                                sMethodmapMethodName,
                                                                                String.Format("{0}.{1}", sVariableName, sMethodmapMethodName),
                                                                                mMethodmapItem.m_FullFunctionString)

                    mAutocomplete.m_Data("VariableName") = sVariableName
                    mAutocomplete.m_Data("VariableTags") = New String() {sVariableTag}

                    For Each mData In mMethodmapItem.m_Data
                        mAutocomplete.m_Data(mData.Key) = mData.Value
                    Next

                    mAutocomplete.m_Data("VariableMethodmapName") = sVariableName
                    mAutocomplete.m_Data("VariableMethodmapMethod") = sMethodmapMethodName

#If DEBUG Then
                    mAutocomplete.m_Data("DataSet-" & ClassExceptionLog.GetDebugStackTrace("")) = "Make methodmaps using methods"
#End If

                    If (Not lNewVarAutocompleteList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString) AndAlso
                                Not lVarMethodmapList.Exists(Function(x As ClassSyntaxTools.STRUC_AUTOCOMPLETE) x.m_Type = mAutocomplete.m_Type AndAlso x.m_FunctionString = mAutocomplete.m_FunctionString)) Then
                        lVarMethodmapList.Add(mAutocomplete)
                    End If
                Next
            Next

            lNewVarAutocompleteList.AddRange(lVarMethodmapList.ToArray)
        End If
    End Sub

    ''' <summary>
    ''' Gets all include files from a file
    ''' </summary>
    ''' <param name="sPath"></param>
    ''' <returns>Array if include file paths</returns>
    Public Function GetIncludeFiles(mConfig As ClassConfigs.STRUC_CONFIG_ITEM, sActiveSource As String, sActiveSourceFile As String, sPath As String, Optional bFindAll As Boolean = False, Optional iMaxDirectoryDepth As Integer = 10) As String()
        Dim lList As New List(Of String)
        Dim lLoadedIncludes As New Dictionary(Of String, Boolean)

        GetIncludeFilesRecursive(mConfig, sActiveSource, sActiveSourceFile, sPath, lList, lLoadedIncludes)

        If (bFindAll) Then
            While True
#If SEARCH_EVERYWHERE Then
                If (String.IsNullOrEmpty(sActiveSourceFile) OrElse Not IO.File.Exists(sActiveSourceFile)) Then
                    g_mFormMain.PrintInformation("[ERRO]", "Could not read includes! Could not get current source file!")
                    Exit While
                End If
#End If

                'Check includes
                Dim sIncludePaths As String
                If (mConfig.g_iCompilingType = ClassSettings.ENUM_COMPILING_TYPE.AUTOMATIC) Then
                    If (String.IsNullOrEmpty(sActiveSourceFile) OrElse Not IO.File.Exists(sActiveSourceFile)) Then
                        g_mFormMain.PrintInformation("[ERRO]", "Could not read includes! Could not get current source file!", False, False)
                        Exit While
                    End If
                    sIncludePaths = IO.Path.Combine(IO.Path.GetDirectoryName(sActiveSourceFile), "include")
                Else
                    sIncludePaths = mConfig.g_sIncludeFolders
                End If

#If SEARCH_EVERYWHERE Then
                'Check compiler
                Dim sCompilerPath As String
                If (ClassSettings.g_iConfigCompilingType = ClassSettings.ENUM_COMPILING_TYPE.AUTOMATIC) Then
                    If (String.IsNullOrEmpty(sActiveSourceFile) OrElse Not IO.File.Exists(sActiveSourceFile)) Then
                        g_mFormMain.PrintInformation("[ERRO]", "Could not read includes! Could not get current source file!", False, False, 1)
                        Exit While
                    End If
                    sCompilerPath = IO.Path.GetDirectoryName(sActiveSourceFile)
                ElseIf (Not String.IsNullOrEmpty(ClassSettings.g_sConfigCompilerPath) AndAlso IO.File.Exists(ClassSettings.g_sConfigCompilerPath)) Then
                    sCompilerPath = IO.Path.GetDirectoryName(ClassSettings.g_sConfigCompilerPath)
                Else
                    sCompilerPath = ""
                End If
#End If

#If SEARCH_EVERYWHERE Then
                GetIncludeFilesRecursiveAll(IO.Path.GetDirectoryName(sActiveSourceFile), lList, lLoadedIncludes, iMaxDirectoryDepth)
#End If

                For Each sInclude As String In sIncludePaths.Split(";"c)
                    If (Not IO.Directory.Exists(sInclude)) Then
                        Continue For
                    End If

                    GetIncludeFilesRecursiveAll(mConfig, sInclude, lList, lLoadedIncludes, iMaxDirectoryDepth)
                Next

#If SEARCH_EVERYWHERE Then
                GetIncludeFilesRecursiveAll(sCompilerPath, lList, lLoadedIncludes, iMaxDirectoryDepth)
#End If

                Exit While
            End While
        End If

        For Each i In lLoadedIncludes
            If (i.Value) Then
                Continue For
            End If

            g_mFormMain.PrintInformation("[ERRO]", String.Format("Could not read include: {0}", i.Key), False, False)
        Next

        Return lList.ToArray
    End Function

    Private Sub GetIncludeFilesRecursiveAll(mConfig As ClassConfigs.STRUC_CONFIG_ITEM, sInclude As String, ByRef lList As List(Of String), lLoadedIncludes As Dictionary(Of String, Boolean), iMaxDirectoryDepth As Integer)
        Dim sFiles As String()
        Dim sDirectories As String()

        sFiles = IO.Directory.GetFiles(sInclude)
        sDirectories = IO.Directory.GetDirectories(sInclude)

        For Each i As String In sFiles
            Dim sFileName As String = IO.Path.GetFileNameWithoutExtension(i)

            If (Not IO.File.Exists(i)) Then
                Continue For
            End If

            If (lList.Contains(i.ToLower)) Then
                lLoadedIncludes(sFileName.ToLower) = True
                Continue For
            End If

            Select Case (IO.Path.GetExtension(i).ToLower)
                Case ".sp", ".sma", ".p", ".pwn", ".inc"
                    lList.Add(i.ToLower)
                    lLoadedIncludes(sFileName.ToLower) = True
            End Select
        Next

        If (iMaxDirectoryDepth < 1) Then
            g_mFormMain.PrintInformation("[ERRO]", "Max recursive directory search depth reached!", False, False)
            Return
        End If

        For Each i As String In sDirectories
            GetIncludeFilesRecursiveAll(mConfig, i, lList, lLoadedIncludes, iMaxDirectoryDepth - 1)
        Next
    End Sub

    Private Sub GetIncludeFilesRecursive(mConfig As ClassConfigs.STRUC_CONFIG_ITEM, sActiveSource As String, sActiveSourceFile As String, sPath As String, ByRef lList As List(Of String), lLoadedIncludes As Dictionary(Of String, Boolean))
        Dim sSource As String

        Dim sFileName As String = IO.Path.GetFileNameWithoutExtension(sPath)

        If (sActiveSourceFile.ToLower = sPath.ToLower) Then
            If (lList.Contains(sPath.ToLower)) Then
                lLoadedIncludes(sFileName.ToLower) = True
                Return
            End If

            lList.Add(sPath.ToLower)
            lLoadedIncludes(sFileName.ToLower) = True

            sSource = sActiveSource
        Else
            If (Not IO.File.Exists(sPath)) Then
                If (Not lLoadedIncludes.ContainsKey(sFileName.ToLower)) Then
                    lLoadedIncludes(sFileName.ToLower) = False
                End If

                Return
            End If

            If (lList.Contains(sPath.ToLower)) Then
                lLoadedIncludes(sFileName.ToLower) = True
                Return
            End If

            lList.Add(sPath.ToLower)
            lLoadedIncludes(sFileName.ToLower) = True

            sSource = IO.File.ReadAllText(sPath)
        End If

        Dim lPathList As New List(Of String)
        Dim sCurrentDir As String = IO.Path.GetDirectoryName(sPath)

        Dim sCorrectPath As String
        Dim sMatchValue As String

        While True
            'Check includes
            Dim sIncludePaths As String
            If (mConfig.g_iCompilingType = ClassSettings.ENUM_COMPILING_TYPE.AUTOMATIC) Then
                If (String.IsNullOrEmpty(sActiveSourceFile) OrElse Not IO.File.Exists(sActiveSourceFile)) Then
                    g_mFormMain.PrintInformation("[ERRO]", "Could not read includes! Could not get current source file!", False, False)
                    Exit While
                End If
                sIncludePaths = IO.Path.Combine(IO.Path.GetDirectoryName(sActiveSourceFile), "include")
            Else
                sIncludePaths = mConfig.g_sIncludeFolders
            End If

            'Check compiler
            Dim sCompilerPath As String
            If (mConfig.g_iCompilingType = ClassSettings.ENUM_COMPILING_TYPE.AUTOMATIC) Then
                If (String.IsNullOrEmpty(sActiveSourceFile) OrElse Not IO.File.Exists(sActiveSourceFile)) Then
                    g_mFormMain.PrintInformation("[ERRO]", "Could not read includes! Could not get current source file!", False, False)
                    Exit While
                End If
                sCompilerPath = IO.Path.GetDirectoryName(sActiveSourceFile)
            ElseIf (Not String.IsNullOrEmpty(mConfig.g_sCompilerPath) AndAlso IO.File.Exists(mConfig.g_sCompilerPath)) Then
                sCompilerPath = IO.Path.GetDirectoryName(mConfig.g_sCompilerPath)
            Else
                sCompilerPath = ""
            End If

            For Each sInclude As String In sIncludePaths.Split(";"c)
                If (ClassSettings.g_iSettingsAlwaysLoadDefaultIncludes) Then
                    If (sActiveSourceFile.ToLower = sPath.ToLower) Then
                        For Each sDefaultInc As String In New String() {"sourcemod"}
                            Select Case (True)
                                Case IO.File.Exists(IO.Path.Combine(sInclude, sDefaultInc))
                                    sCorrectPath = IO.Path.GetFullPath(IO.Path.Combine(sInclude, sDefaultInc))
                                Case IO.File.Exists(IO.Path.Combine(sCurrentDir, sDefaultInc))
                                    sCorrectPath = IO.Path.GetFullPath(IO.Path.Combine(sCurrentDir, sDefaultInc))
                                Case IO.File.Exists(IO.Path.Combine(sCompilerPath, sDefaultInc))
                                    sCorrectPath = IO.Path.GetFullPath(IO.Path.Combine(sCompilerPath, sDefaultInc))

                                Case IO.File.Exists(String.Format("{0}.inc", IO.Path.Combine(sInclude, sDefaultInc)))
                                    sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.inc", IO.Path.Combine(sInclude, sDefaultInc)))

                                Case IO.File.Exists(String.Format("{0}.inc", IO.Path.Combine(sCurrentDir, sDefaultInc)))
                                    sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.inc", IO.Path.Combine(sCurrentDir, sDefaultInc)))

                                Case IO.File.Exists(String.Format("{0}.inc", IO.Path.Combine(sCompilerPath, sDefaultInc)))
                                    sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.inc", IO.Path.Combine(sCompilerPath, sDefaultInc)))

                                Case Else
                                    Continue For
                            End Select

                            If (Not IO.File.Exists(sCorrectPath)) Then
                                Continue For
                            End If

                            If (lPathList.Contains(sCorrectPath.ToLower)) Then
                                Continue For
                            End If

                            lPathList.Add(sCorrectPath.ToLower)
                        Next
                    End If
                End If

                Using mSR As New IO.StringReader(sSource)
                    While True
                        Dim sLine As String = mSR.ReadLine()
                        If (sLine Is Nothing) Then
                            Exit While
                        End If

                        If (Not sLine.Contains("#include") AndAlso Not sLine.Contains("#tryinclude")) Then
                            Continue While
                        End If

                        Dim mMatch As Match = Regex.Match(sLine, "^\s*#(include|tryinclude)\s+(\<(?<PathInc>.*?)\>|""(?<PathFull>.*?)"")\s*$")
                        If (Not mMatch.Success) Then
                            Continue While
                        End If

                        Select Case (True)
                            Case mMatch.Groups("PathInc").Success
                                sMatchValue = mMatch.Groups("PathInc").Value.Replace("/"c, "\"c)

                                Select Case (True)
                                    Case IO.File.Exists(IO.Path.Combine(sInclude, sMatchValue))
                                        sCorrectPath = IO.Path.GetFullPath(IO.Path.Combine(sInclude, sMatchValue))
                                    Case IO.File.Exists(IO.Path.Combine(sCompilerPath, sMatchValue))
                                        sCorrectPath = IO.Path.GetFullPath(IO.Path.Combine(sCompilerPath, sMatchValue))

                                    'Case IO.File.Exists(String.Format("{0}.sp", IO.Path.Combine(sInclude, sMatchValue)))
                                    '    sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.sp", IO.Path.Combine(sInclude, sMatchValue)))
                                    'Case IO.File.Exists(String.Format("{0}.sma", IO.Path.Combine(sInclude, sMatchValue)))
                                    '    sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.sma", IO.Path.Combine(sInclude, sMatchValue)))
                                    'Case IO.File.Exists(String.Format("{0}.p", IO.Path.Combine(sInclude, sMatchValue)))
                                    '    sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.p", IO.Path.Combine(sInclude, sMatchValue)))
                                    'Case IO.File.Exists(String.Format("{0}.pwn", IO.Path.Combine(sInclude, sMatchValue)))
                                    '    sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.pwn", IO.Path.Combine(sInclude, sMatchValue)))
                                    Case IO.File.Exists(String.Format("{0}.inc", IO.Path.Combine(sInclude, sMatchValue)))
                                        sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.inc", IO.Path.Combine(sInclude, sMatchValue)))

                                    'Case IO.File.Exists(String.Format("{0}.sp", IO.Path.Combine(sCompilerPath, sMatchValue)))
                                    '    sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.sp", IO.Path.Combine(sCompilerPath, sMatchValue)))
                                    'Case IO.File.Exists(String.Format("{0}.sma", IO.Path.Combine(sCompilerPath, sMatchValue)))
                                    '    sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.sma", IO.Path.Combine(sCompilerPath, sMatchValue)))
                                    'Case IO.File.Exists(String.Format("{0}.p", IO.Path.Combine(sCompilerPath, sMatchValue)))
                                    '    sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.p", IO.Path.Combine(sCompilerPath, sMatchValue)))
                                    'Case IO.File.Exists(String.Format("{0}.pwn", IO.Path.Combine(sCompilerPath, sMatchValue)))
                                    '    sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.pwn", IO.Path.Combine(sCompilerPath, sMatchValue)))
                                    Case IO.File.Exists(String.Format("{0}.inc", IO.Path.Combine(sCompilerPath, sMatchValue)))
                                        sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.inc", IO.Path.Combine(sCompilerPath, sMatchValue)))

                                    Case Else
                                        If (Not lLoadedIncludes.ContainsKey(sMatchValue.ToLower)) Then
                                            lLoadedIncludes(sMatchValue.ToLower) = False
                                        End If
                                        Continue While
                                End Select

                            Case mMatch.Groups("PathFull").Success
                                sMatchValue = mMatch.Groups("PathFull").Value.Replace("/"c, "\"c)

                                Select Case (True)
                                    Case sMatchValue.Length > 1 AndAlso sMatchValue(1) = ":"c AndAlso IO.File.Exists(sMatchValue)
                                        sCorrectPath = IO.Path.GetFullPath(sMatchValue)

                                    Case IO.File.Exists(IO.Path.Combine(sInclude, sMatchValue))
                                        sCorrectPath = IO.Path.GetFullPath(IO.Path.Combine(sInclude, sMatchValue))
                                    Case IO.File.Exists(IO.Path.Combine(sCurrentDir, sMatchValue))
                                        sCorrectPath = IO.Path.GetFullPath(IO.Path.Combine(sCurrentDir, sMatchValue))
                                    Case IO.File.Exists(IO.Path.Combine(sCompilerPath, sMatchValue))
                                        sCorrectPath = IO.Path.GetFullPath(IO.Path.Combine(sCompilerPath, sMatchValue))

                                    Case IO.File.Exists(String.Format("{0}.sp", IO.Path.Combine(sInclude, sMatchValue)))
                                        sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.sp", IO.Path.Combine(sInclude, sMatchValue)))
                                    Case IO.File.Exists(String.Format("{0}.sma", IO.Path.Combine(sInclude, sMatchValue)))
                                        sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.sma", IO.Path.Combine(sInclude, sMatchValue)))
                                    Case IO.File.Exists(String.Format("{0}.p", IO.Path.Combine(sInclude, sMatchValue)))
                                        sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.p", IO.Path.Combine(sInclude, sMatchValue)))
                                    Case IO.File.Exists(String.Format("{0}.pwn", IO.Path.Combine(sInclude, sMatchValue)))
                                        sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.pwn", IO.Path.Combine(sInclude, sMatchValue)))
                                    Case IO.File.Exists(String.Format("{0}.inc", IO.Path.Combine(sInclude, sMatchValue)))
                                        sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.inc", IO.Path.Combine(sInclude, sMatchValue)))

                                    Case IO.File.Exists(String.Format("{0}.sp", IO.Path.Combine(sCurrentDir, sMatchValue)))
                                        sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.sp", IO.Path.Combine(sCurrentDir, sMatchValue)))
                                    Case IO.File.Exists(String.Format("{0}.sma", IO.Path.Combine(sCurrentDir, sMatchValue)))
                                        sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.sma", IO.Path.Combine(sCurrentDir, sMatchValue)))
                                    Case IO.File.Exists(String.Format("{0}.p", IO.Path.Combine(sCurrentDir, sMatchValue)))
                                        sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.p", IO.Path.Combine(sCurrentDir, sMatchValue)))
                                    Case IO.File.Exists(String.Format("{0}.pwn", IO.Path.Combine(sCurrentDir, sMatchValue)))
                                        sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.pwn", IO.Path.Combine(sCurrentDir, sMatchValue)))
                                    Case IO.File.Exists(String.Format("{0}.inc", IO.Path.Combine(sCurrentDir, sMatchValue)))
                                        sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.inc", IO.Path.Combine(sCurrentDir, sMatchValue)))

                                    Case IO.File.Exists(String.Format("{0}.sp", IO.Path.Combine(sCompilerPath, sMatchValue)))
                                        sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.sp", IO.Path.Combine(sCompilerPath, sMatchValue)))
                                    Case IO.File.Exists(String.Format("{0}.sma", IO.Path.Combine(sCompilerPath, sMatchValue)))
                                        sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.sma", IO.Path.Combine(sCompilerPath, sMatchValue)))
                                    Case IO.File.Exists(String.Format("{0}.p", IO.Path.Combine(sCompilerPath, sMatchValue)))
                                        sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.p", IO.Path.Combine(sCompilerPath, sMatchValue)))
                                    Case IO.File.Exists(String.Format("{0}.pwn", IO.Path.Combine(sCompilerPath, sMatchValue)))
                                        sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.pwn", IO.Path.Combine(sCompilerPath, sMatchValue)))
                                    Case IO.File.Exists(String.Format("{0}.inc", IO.Path.Combine(sCompilerPath, sMatchValue)))
                                        sCorrectPath = IO.Path.GetFullPath(String.Format("{0}.inc", IO.Path.Combine(sCompilerPath, sMatchValue)))

                                    Case Else
                                        If (Not lLoadedIncludes.ContainsKey(sMatchValue.ToLower)) Then
                                            lLoadedIncludes(sMatchValue.ToLower) = False
                                        End If
                                        Continue While
                                End Select

                            Case Else
                                Continue While
                        End Select

                        If (Not IO.File.Exists(sCorrectPath)) Then
                            Continue While
                        End If

                        If (lPathList.Contains(sCorrectPath.ToLower)) Then
                            Continue While
                        End If

                        lPathList.Add(sCorrectPath.ToLower)
                    End While
                End Using
            Next

            Exit While
        End While

        For i = 0 To lPathList.Count - 1
            GetIncludeFilesRecursive(mConfig, sActiveSource, sActiveSourceFile, lPathList(i), lList, lLoadedIncludes)
        Next
    End Sub
End Class
