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


Imports System.Text.RegularExpressions
Imports ICSharpCode.TextEditor
Imports ICSharpCode.TextEditor.Document

''' <summary>
''' Usefull tools for the TextEditor
''' </summary>
Public Class ClassTextEditorTools
    Private g_mFormMain As FormMain

    Public Sub New(f As FormMain)
        g_mFormMain = f
    End Sub

    ''' <summary>
    ''' Marks a selected word in the text editor
    ''' </summary>
    Public Sub MarkSelectedWord()
        Dim mActiveTextEditor As TextEditorControlEx = g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor

        Dim sLastWord As String = ClassSyntaxTools.g_sHighlightWord
        ClassSyntaxTools.g_sHighlightWord = ""

        If (mActiveTextEditor.ActiveTextAreaControl.SelectionManager.HasSomethingSelected) Then
            Dim m_CurrentSelection As ISelection = mActiveTextEditor.ActiveTextAreaControl.SelectionManager.SelectionCollection(0)

            If (Regex.IsMatch(m_CurrentSelection.SelectedText, "^[a-zA-Z0-9_]+$")) Then
                ClassSyntaxTools.g_sHighlightWord = m_CurrentSelection.SelectedText
            End If
        End If

        If (ClassSyntaxTools.g_sHighlightWord = sLastWord) Then
            Return
        End If

        g_mFormMain.g_ClassSyntaxTools.g_ClassSyntaxHighlighting.UpdateSyntax(ClassSyntaxTools.ENUM_SYNTAX_UPDATE_TYPE.HIGHLIGHT_WORD)
        g_mFormMain.g_ClassSyntaxTools.g_ClassSyntaxHighlighting.UpdateTextEditorSyntax()
    End Sub

    ''' <summary>
    ''' Marks a selected word in the text editor
    ''' </summary>
    Public Sub MarkCaretWord()
        Dim mActiveTextEditor As TextEditorControlEx = g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor

        Dim sLastWord As String = ClassSyntaxTools.g_sCaretWord
        ClassSyntaxTools.g_sCaretWord = ""

        If (Not mActiveTextEditor.ActiveTextAreaControl.SelectionManager.HasSomethingSelected) Then
            Dim sWord As String = GetCaretWord(False, False, False)

            If (Not String.IsNullOrEmpty(sWord)) Then
                ClassSyntaxTools.g_sCaretWord = sWord
            End If
        End If

        If (ClassSyntaxTools.g_sCaretWord = sLastWord) Then
            Return
        End If

        If (Not String.IsNullOrEmpty(ClassSyntaxTools.g_sCaretWord) AndAlso
                    Regex.Matches(mActiveTextEditor.Document.TextContent, String.Format("\b{0}\b", Regex.Escape(ClassSyntaxTools.g_sCaretWord)), RegexOptions.Multiline).Count < 2) Then
            Return
        End If

        g_mFormMain.g_ClassSyntaxTools.g_ClassSyntaxHighlighting.UpdateSyntax(ClassSyntaxTools.ENUM_SYNTAX_UPDATE_TYPE.CARET_WORD)
        g_mFormMain.g_ClassSyntaxTools.g_ClassSyntaxHighlighting.UpdateTextEditorSyntax()
    End Sub

    ''' <summary>
    ''' Lists all references of a word from current opeened source and all include files.
    ''' </summary>
    ''' <param name="sText">Word to search, otherwise it will get the word under the caret</param>
    Public Sub ListReferences(Optional sText As String = Nothing)
        Dim mActiveTextEditor As TextEditorControlEx = g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor

        Dim sWord As String

        If (Not String.IsNullOrEmpty(sText)) Then
            sWord = sText
        Else
            sWord = g_mFormMain.g_ClassTextEditorTools.GetCaretWord(False, False, False)
        End If

        If (String.IsNullOrEmpty(sWord)) Then
            g_mFormMain.PrintInformation("[ERRO]", "Can't check references! Nothing valid selected!", False, True, True)
            Return
        End If

        If (g_mFormMain.g_ClassTabControl.m_ActiveTab.m_IsUnsaved OrElse g_mFormMain.g_ClassTabControl.m_ActiveTab.m_InvalidFile) Then
            g_mFormMain.PrintInformation("[ERRO]", "Can't check references! Could not get current source file!", False, True, True)
            Return
        End If

        g_mFormMain.PrintInformation("[INFO]", String.Format("Listing references of: {0}", sWord), False, True, True)

        Dim mIncludeFiles As DictionaryEntry() = g_mFormMain.g_ClassTabControl.m_ActiveTab.m_IncludeFiles.ToArray

        Dim lRefList As New List(Of String)

        For Each mInclude As DictionaryEntry In mIncludeFiles
            If (Not IO.File.Exists(CStr(mInclude.Value))) Then
                Continue For
            End If

            If (CStr(mInclude.Value).ToLower = g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File.ToLower) Then
                Dim iLineCount As Integer = 0
                Using mSR As New IO.StringReader(mActiveTextEditor.Document.TextContent)
                    While True
                        Dim sLine As String = mSR.ReadLine
                        If (sLine Is Nothing) Then
                            Exit While
                        End If

                        iLineCount += 1

                        If (sLine.Contains(sWord) AndAlso Regex.IsMatch(sLine, String.Format("\b{0}\b", Regex.Escape(sWord)))) Then
                            lRefList.Add(vbTab & String.Format("Reference found: {0}({1}) : {2}", CStr(mInclude.Value), iLineCount, sLine.Trim))
                        End If
                    End While
                End Using
            Else
                Dim iLineCount As Integer = 0
                Using SR As New IO.StreamReader(CStr(mInclude.Value))
                    While True
                        Dim sLine As String = SR.ReadLine
                        If (sLine Is Nothing) Then
                            Exit While
                        End If

                        iLineCount += 1

                        If (sLine.Contains(sWord) AndAlso Regex.IsMatch(sLine, String.Format("\b{0}\b", Regex.Escape(sWord)))) Then
                            lRefList.Add(vbTab & String.Format("Reference found: {0}({1}) : {2}", CStr(mInclude.Value), iLineCount, sLine.Trim))
                        End If
                    End While
                End Using
            End If
        Next

        For Each sRef As String In lRefList
            g_mFormMain.PrintInformation("[INFO]", sRef, False)
        Next


        g_mFormMain.PrintInformation("[INFO]", "All references listed!", False, True, True)
    End Sub

    ''' <summary>
    ''' Opens a 'Search and Replace' form
    ''' </summary>
    ''' <param name="sSearchText"></param>
    Public Sub ShowSearchAndReplace(sSearchText As String)
        Dim iFormCount As Integer = 0
        For Each c As Form In Application.OpenForms
            If (TypeOf c Is FormSearch) Then
                iFormCount += 1
            End If
        Next

        If (iFormCount > 100) Then
            MessageBox.Show("Too many 'Search & Replace' windows open!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Else
            Dim i As New FormSearch(g_mFormMain, sSearchText)
            i.Show(g_mFormMain)
        End If
    End Sub

    ''' <summary>
    ''' Gets the caret word in the text editor
    ''' </summary>
    ''' <param name="bIncludeMethodmaps">If true, includes methodmaps (e.g Methodmap.Name)</param>
    ''' <param name="bIncludeMethodNames">If true, includes method names. Skips arguments. (e.g method().Name -> Method.Name)</param>
    ''' <returns></returns>
    Public Function GetCaretWord(bIncludeMethodmaps As Boolean, bIncludeMethodNames As Boolean, bIncludeArrayNames As Boolean) As String
        Dim mActiveTextEditor As TextEditorControlEx = g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor

        Dim iOffset As Integer = mActiveTextEditor.ActiveTextAreaControl.TextArea.Caret.Offset
        Dim iPosition As Integer = mActiveTextEditor.ActiveTextAreaControl.TextArea.Caret.Position.Column
        Dim iLineOffset As Integer = mActiveTextEditor.ActiveTextAreaControl.Document.GetLineSegmentForOffset(iOffset).Offset
        Dim iLineLen As Integer = mActiveTextEditor.ActiveTextAreaControl.Document.GetLineSegmentForOffset(iOffset).Length

        Dim sWordLeft As String = ""
        Dim sWordRight As String = ""

        If (bIncludeMethodmaps OrElse bIncludeMethodNames OrElse bIncludeArrayNames) Then
            Dim bIsMethod As Boolean = Regex.Match(mActiveTextEditor.Document.GetText(iLineOffset, iPosition), "(\))(\.)(\b[a-zA-Z0-9_]+\b){0,1}$").Success
            Dim bIsArray As Boolean = Regex.Match(mActiveTextEditor.Document.GetText(iLineOffset, iPosition), "(\])(\.)(\b[a-zA-Z0-9_]+\b){0,1}$").Success

            If ((bIncludeMethodNames AndAlso bIsMethod) OrElse (bIncludeArrayNames AndAlso bIsArray)) Then
                Dim sSource As String = mActiveTextEditor.Document.GetText(0, iOffset)
                If (sSource.Length > 0) Then
                    Dim mSourceBuilder As New Text.StringBuilder(sSource.Length)
                    Dim mSourceAnalysis As New ClassSyntaxTools.ClassSyntaxSourceAnalysis(sSource, g_mFormMain.g_ClassTabControl.m_ActiveTab.m_Language)
                    Dim iLastParenthesisLevel As Integer = mSourceAnalysis.GetParenthesisLevel(sSource.Length - 1, Nothing)

                    For i = sSource.Length - 1 To 0 Step -1
                        If (mSourceAnalysis.GetBraceLevel(i, Nothing) < 1) Then
                            Exit For
                        End If

                        If (mSourceAnalysis.m_InNonCode(i)) Then
                            Continue For
                        End If

                        If (bIncludeMethodNames AndAlso bIsMethod) Then
                            If (mSourceAnalysis.GetParenthesisLevel(i, Nothing) <> iLastParenthesisLevel) Then
                                Continue For
                            End If

                        ElseIf (bIncludeArrayNames AndAlso bIsArray) Then
                            If (mSourceAnalysis.GetBracketLevel(i, Nothing) > 0) Then
                                Continue For
                            End If
                        Else
                            Throw New ArgumentException("Unknown action")
                        End If


                        mSourceBuilder.Append(sSource(i))
                    Next

                    Dim sFilteredSource As String = StrReverse(mSourceBuilder.ToString)

                    sWordLeft = Regex.Match(sFilteredSource, "((\b[a-zA-Z0-9_]+\b)(\.){0,1}(\b[a-zA-Z0-9_]+\b){0,1})$").Value
                    If (sWordLeft.Contains("."c)) Then
                        'If the left contains already 2 parts of a methodmap, then only get the name on the right side.
                        sWordRight = Regex.Match(mActiveTextEditor.Document.GetText(iLineOffset + iPosition, iLineLen - iPosition), "^(\b[a-zA-Z0-9_]+\b)").Value
                    Else
                        sWordRight = Regex.Match(mActiveTextEditor.Document.GetText(iLineOffset + iPosition, iLineLen - iPosition), "^((\b[a-zA-Z0-9_]+\b){0,1}(\.){0,1}(\b[a-zA-Z0-9_]+\b))").Value
                    End If
                End If

            Else
                sWordLeft = Regex.Match(mActiveTextEditor.Document.GetText(iLineOffset, iPosition), "((\b[a-zA-Z0-9_]+\b)(\.){0,1}(\b[a-zA-Z0-9_]+\b){0,1})$").Value
                If (sWordLeft.Contains("."c)) Then
                    'If the left contains already 2 parts of a methodmap, then only get the name on the right side.
                    sWordRight = Regex.Match(mActiveTextEditor.Document.GetText(iLineOffset + iPosition, iLineLen - iPosition), "^(\b[a-zA-Z0-9_]+\b)").Value
                Else
                    sWordRight = Regex.Match(mActiveTextEditor.Document.GetText(iLineOffset + iPosition, iLineLen - iPosition), "^((\b[a-zA-Z0-9_]+\b){0,1}(\.){0,1}(\b[a-zA-Z0-9_]+\b))").Value
                End If
            End If
        Else
            sWordLeft = Regex.Match(mActiveTextEditor.Document.GetText(iLineOffset, iPosition), "(\b[a-zA-Z0-9_]+\b)$").Value
            sWordRight = Regex.Match(mActiveTextEditor.Document.GetText(iLineOffset + iPosition, iLineLen - iPosition), "^(\b[a-zA-Z0-9_]+\b)").Value
        End If

        Return (sWordLeft & sWordRight)
    End Function

    Enum ENUM_COMPILER_TYPE
        UNKNOWN
        SOURCEPAWN
        AMXX
        AMX
    End Enum

    ''' <summary>
    ''' Compiles the source.
    ''' </summary>
    ''' <param name="bTesting">Just creates a temporary file and removes it after compile.</param>
    ''' <param name="sSource">The source to compile. Use |Nothing| to get the source from the active tab.</param>
    ''' <param name="sOutputFile">The output file. This may change the extenstion if you using different compilers (e.g *.smx, *.amx, *.amxx). And extension is still required! Use |Nothing| to get the output by active tab.</param>
    ''' <param name="sWorkingDirectory">Sets the compiler working directory. Use |Nothing| to use the working directioy from the active tab.</param>
    ''' <param name="sCompilerPath">The compiler path. Use |Nothing| to get the source from the active tab.</param>
    ''' <param name="sIncludePaths">The include paths seperated by ';'. Use |Nothing| to get the source from the active tab.</param>
    ''' <param name="sEmulateSourceFile">Replaces the printed temporary path.</param>
    ''' <param name="bUseCustomCompilerOptions">If true it will use the configs custom compiler options, false otherwise.</param>
    ''' <param name="sCompilerOutput">The compiler output.</param>
    ''' <param name="iCompilerType"></param>
    ''' <returns>True on success, false otherwise.</returns>
    Public Function CompileSource(bTesting As Boolean,
                                  sSource As String,
                                  ByRef sOutputFile As String,
                                  Optional mConfig As ClassConfigs.STRUC_CONFIG_ITEM = Nothing,
                                  Optional sWorkingDirectory As String = Nothing,
                                  Optional sCompilerPath As String = Nothing,
                                  Optional sIncludePaths As String = Nothing,
                                  Optional sEmulateSourceFile As String = Nothing,
                                  Optional bUseCustomCompilerOptions As Boolean = True,
                                  ByRef Optional sCompilerOutput As String = Nothing,
                                  ByRef Optional iCompilerType As ENUM_COMPILER_TYPE = ENUM_COMPILER_TYPE.UNKNOWN) As Boolean
        Try
            If (sSource Is Nothing) Then
                sSource = g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor.Document.TextContent
            End If

            If (mConfig Is Nothing) Then
                mConfig = g_mFormMain.g_ClassTabControl.m_ActiveTab.m_ActiveConfig
            End If

            If (Not String.IsNullOrEmpty(sEmulateSourceFile)) Then
                With New Text.StringBuilder
                    .AppendLine(String.Format("#file ""{0}""", sEmulateSourceFile))
                    .AppendLine("#line 0")
                    .AppendLine(sSource)

                    sSource = .ToString
                End With
            End If

            If (g_mFormMain.SplitContainer_ToolboxSourceAndDetails.Panel2Collapsed) Then
                g_mFormMain.SplitContainer_ToolboxSourceAndDetails.Panel2Collapsed = False
                g_mFormMain.SplitContainer_ToolboxSourceAndDetails.SplitterDistance = g_mFormMain.SplitContainer_ToolboxSourceAndDetails.Height - 200
            End If
            g_mFormMain.TabControl_Details.SelectTab(1)

            g_mFormMain.PrintInformation("[INFO]", "Compiling source started!", False, False, True)

            Dim iExitCode As Integer = 0
            Dim sOutput As String = ""
            Dim sLines As String()

            'Check compiler
            If (sCompilerPath Is Nothing) Then
                If (mConfig.g_iCompilingType = ClassSettings.ENUM_COMPILING_TYPE.AUTOMATIC) Then
                    If (g_mFormMain.g_ClassTabControl.m_ActiveTab.m_IsUnsaved AndAlso
                                g_mFormMain.g_ClassTabControl.PromptSaveTab(g_mFormMain.g_ClassTabControl.m_ActiveTabIndex, False, True, True)) Then
                        g_mFormMain.PrintInformation("[ERRO]", "Compiling failed! Could not get current source file!", False, False, True)
                        Return False
                    End If

                    If (g_mFormMain.g_ClassTabControl.m_ActiveTab.m_IsUnsaved) Then
                        g_mFormMain.PrintInformation("[ERRO]", "Compiling failed! Could not get current source file!", False, False, True)
                        Return False
                    End If

                    While True
                        'SourcePawn
                        sCompilerPath = IO.Path.Combine(IO.Path.GetDirectoryName(g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File), "spcomp.exe")
                        If (IO.File.Exists(sCompilerPath)) Then
                            Exit While
                        End If

                        'AMX Mod X
                        sCompilerPath = IO.Path.Combine(IO.Path.GetDirectoryName(g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File), "amxxpc.exe")
                        If (IO.File.Exists(sCompilerPath)) Then
                            Exit While
                        End If

                        'Small
                        sCompilerPath = IO.Path.Combine(IO.Path.GetDirectoryName(g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File), "sc.exe")
                        If (IO.File.Exists(sCompilerPath)) Then
                            Exit While
                        End If

                        'Pawn
                        sCompilerPath = IO.Path.Combine(IO.Path.GetDirectoryName(g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File), "pawncc.exe")
                        If (IO.File.Exists(sCompilerPath)) Then
                            Exit While
                        End If

                        g_mFormMain.PrintInformation("[ERRO]", "Compiling failed! Compiler can not be found!", False, False, True)
                        Return False
                    End While
                Else
                    sCompilerPath = mConfig.g_sCompilerPath
                    If (Not IO.File.Exists(sCompilerPath)) Then
                        g_mFormMain.PrintInformation("[ERRO]", "Compiling failed! Compiler can not be found!", False, False, True)
                        Return False
                    End If
                End If
            Else
                If (Not IO.File.Exists(sCompilerPath)) Then
                    g_mFormMain.PrintInformation("[ERRO]", "Compiling failed! Compiler can not be found!", False, False, True)
                    Return False
                End If
            End If

            'Check include path
            If (sIncludePaths Is Nothing) Then
                If (mConfig.g_iCompilingType = ClassSettings.ENUM_COMPILING_TYPE.AUTOMATIC) Then
                    If (g_mFormMain.g_ClassTabControl.m_ActiveTab.m_IsUnsaved AndAlso
                                g_mFormMain.g_ClassTabControl.PromptSaveTab(g_mFormMain.g_ClassTabControl.m_ActiveTabIndex, False, True, True)) Then
                        g_mFormMain.PrintInformation("[ERRO]", "Compiling failed! Could not get current source file!", False, False, True)
                        Return False
                    End If

                    If (g_mFormMain.g_ClassTabControl.m_ActiveTab.m_IsUnsaved) Then
                        g_mFormMain.PrintInformation("[ERRO]", "Compiling failed! Could not get current source file!", False, False, True)
                        Return False
                    End If

                    sIncludePaths = IO.Path.Combine(IO.Path.GetDirectoryName(g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File), "include")
                    If (Not IO.Directory.Exists(sIncludePaths)) Then
                        g_mFormMain.PrintInformation("[ERRO]", "Compiling failed! Include path can not be found!", False, False, True)
                        Return False
                    End If
                Else
                    sIncludePaths = mConfig.g_sIncludeFolders
                    For Each sInclude As String In sIncludePaths.Split(";"c)
                        If (Not IO.Directory.Exists(sInclude)) Then
                            g_mFormMain.PrintInformation("[ERRO]", "Compiling failed! Include path can not be found!", False, False, True)
                            Return False
                        End If
                    Next
                End If
            Else
                For Each sInclude As String In sIncludePaths.Split(";"c)
                    If (Not IO.Directory.Exists(sInclude)) Then
                        g_mFormMain.PrintInformation("[ERRO]", "Compiling failed! Include path can not be found!", False, False, True)
                        Return False
                    End If
                Next
            End If

            'Set output path
            If (Not bTesting) Then
                If (String.IsNullOrEmpty(sOutputFile)) Then
                    If (g_mFormMain.g_ClassTabControl.m_ActiveTab.m_IsUnsaved AndAlso
                                g_mFormMain.g_ClassTabControl.PromptSaveTab(g_mFormMain.g_ClassTabControl.m_ActiveTabIndex, False, True, True)) Then
                        g_mFormMain.PrintInformation("[ERRO]", "Compiling failed! Could not get current source file!", False, False, True)
                        Return False
                    End If

                    If (g_mFormMain.g_ClassTabControl.m_ActiveTab.m_IsUnsaved) Then
                        g_mFormMain.PrintInformation("[ERRO]", "Compiling failed! Could not get current source file!", False, False, True)
                        Return False
                    End If

                    If (mConfig.g_iCompilingType = ClassSettings.ENUM_COMPILING_TYPE.AUTOMATIC) Then
                        sOutputFile = IO.Path.Combine(IO.Path.GetDirectoryName(g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File), String.Format("compiled\{0}.unk", IO.Path.GetFileNameWithoutExtension(g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File)))
                    Else
                        If (Not IO.Directory.Exists(mConfig.g_sOutputFolder)) Then
                            g_mFormMain.PrintInformation("[ERRO]", "Compiling failed! Invalid output directory!", False, False, True)
                            Return False
                        End If
                        sOutputFile = IO.Path.Combine(mConfig.g_sOutputFolder, String.Format("{0}.unk", IO.Path.GetFileNameWithoutExtension(g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File)))
                    End If
                End If
            End If

            Dim sTmpOutputFile As String = IO.Path.Combine(IO.Path.GetTempPath, Guid.NewGuid.ToString)

            Dim TmpSourceFile As String = String.Format("{0}.src", IO.Path.Combine(IO.Path.GetTempPath, Guid.NewGuid.ToString))
            IO.File.WriteAllText(TmpSourceFile, sSource)

            Dim lIncludeList As New List(Of String)
            For Each sInclude As String In sIncludePaths.Split(";"c)
                lIncludeList.Add("-i""" & sInclude & """")
            Next

            'Get compiler type first.
            'Normaly every compiler should print help without any process arguments, but AMX Mod X compiler wants input...
            'Use an unfinished argument to force help.
            'TODO: Should detect compiler type using file info instead? This is a bad hack.
            ClassTools.ClassProcess.ExecuteProgram(sCompilerPath, "-", iExitCode, sOutput)

            iCompilerType = ENUM_COMPILER_TYPE.UNKNOWN

            sLines = sOutput.Split(New String() {Environment.NewLine, vbLf}, 0)
            If (sLines.Length > 0) Then
                Select Case (True)
                    Case Regex.IsMatch(sLines(0), "\b(SourcePawn)\b \b(Compiler)\b", RegexOptions.IgnoreCase)
                        iCompilerType = ENUM_COMPILER_TYPE.SOURCEPAWN

                    Case Regex.IsMatch(sLines(0), "\b(AMX)\b \b(Mod)\b \b(X)\b", RegexOptions.IgnoreCase)
                        iCompilerType = ENUM_COMPILER_TYPE.AMXX

                    'Old AMX Mod still uses Small compiler
                    Case Regex.IsMatch(sLines(0), "\b(Pawn)\b \b(compiler)\b", RegexOptions.IgnoreCase), Regex.IsMatch(sLines(0), "\b(Small)\b \b(compiler)\b", RegexOptions.IgnoreCase)
                        iCompilerType = ENUM_COMPILER_TYPE.AMX

                End Select
            End If

            'Build arguments 
            Dim lArguments As New List(Of String) From {
               String.Format("""{0}"" {1} -o""{2}""", TmpSourceFile, String.Join(" ", lIncludeList.ToArray), sTmpOutputFile)
            }

            If (bUseCustomCompilerOptions) Then
                Select Case (iCompilerType)
                    Case ENUM_COMPILER_TYPE.SOURCEPAWN
                        lArguments.Add(mConfig.g_mCompilerOptionsSP.BuildCommandline)

                    Case ENUM_COMPILER_TYPE.AMXX
                        lArguments.Add(mConfig.g_mCompilerOptionsAMXX.BuildCommandline)

                End Select
            End If

            'Compile 
            If (String.IsNullOrEmpty(sWorkingDirectory) OrElse Not IO.Directory.Exists(sWorkingDirectory)) Then
                ClassTools.ClassProcess.ExecuteProgram(sCompilerPath, String.Join(" "c, lArguments.ToArray), iExitCode, sOutput)
            Else
                ClassTools.ClassProcess.ExecuteProgram(sCompilerPath, String.Join(" "c, lArguments.ToArray), sWorkingDirectory, iExitCode, sOutput)
            End If

            sCompilerOutput = sOutput

            IO.File.Delete(TmpSourceFile)

            sLines = sOutput.Split(New String() {Environment.NewLine, vbLf}, 0)
            For i = 0 To sLines.Length - 1
                g_mFormMain.PrintInformation("[INFO]", vbTab & sLines(i))
            Next

            sCompilerOutput = String.Join(Environment.NewLine, sLines)

            'The AMX Mod X compiler seem to overwrite the *.unk extension, just find them using the unique GUID filename.
            Dim sOutputMatches As String() = IO.Directory.GetFiles(IO.Path.GetDirectoryName(sTmpOutputFile), String.Format("{0}.*", IO.Path.GetFileNameWithoutExtension(sTmpOutputFile)), IO.SearchOption.TopDirectoryOnly)
            If (sOutputMatches.Length = 1) Then
                sTmpOutputFile = sOutputMatches(0)
            Else
                g_mFormMain.PrintInformation("[ERRO]", "Compiling failed! Compiled output file can not be found!", False, False, True)
                Return False
            End If

            If (bTesting) Then
                IO.File.Delete(sTmpOutputFile)
            Else
                Select Case (iCompilerType)
                    Case ENUM_COMPILER_TYPE.SOURCEPAWN
                        Dim sNewOutputFile As String = IO.Path.ChangeExtension(sOutputFile, ".smx")
                        IO.File.Delete(sNewOutputFile)
                        IO.File.Move(sTmpOutputFile, sNewOutputFile)
                        sOutputFile = sNewOutputFile

                    Case ENUM_COMPILER_TYPE.AMXX
                        Dim sNewOutputFile As String = IO.Path.ChangeExtension(sOutputFile, ".amxx")
                        IO.File.Delete(sNewOutputFile)
                        IO.File.Move(sTmpOutputFile, sNewOutputFile)
                        sOutputFile = sNewOutputFile

                    Case ENUM_COMPILER_TYPE.AMX
                        Dim sNewOutputFile As String = IO.Path.ChangeExtension(sOutputFile, ".amx")
                        IO.File.Delete(sNewOutputFile)
                        IO.File.Move(sTmpOutputFile, sNewOutputFile)
                        sOutputFile = sNewOutputFile

                    Case Else
                        g_mFormMain.PrintInformation("[WARN]", vbTab & "Unsupported compiler!")

                        Dim sNewOutputFile As String = IO.Path.ChangeExtension(sOutputFile, ".bin")
                        IO.File.Delete(sNewOutputFile)
                        IO.File.Move(sTmpOutputFile, sNewOutputFile)
                        sOutputFile = sNewOutputFile

                End Select

                If (Not bTesting) Then
                    g_mFormMain.PrintInformation("[INFO]", vbTab & String.Format("Saved compiled source: {0}", sOutputFile))
                End If
            End If

            g_mFormMain.PrintInformation("[INFO]", "Compiling source finished!", False, False, True)
            Return True
        Catch ex As Exception
            ClassExceptionLog.WriteToLogMessageBox(ex)
        End Try

        Return False
    End Function

    ''' <summary>
    ''' Generates pre-process source. Cleaned up, defines resolved etc.
    ''' </summary>
    ''' <param name="sSource">The source to compile. Use |Nothing| to get the source from the active tab.</param>
    ''' <param name="bCleanUpSourcemodDuplicate">Removes duplicated sourcemod includes.</param>
    ''' <param name="bCleanupForCompile">Removes pre-processor entries which hinders compiling.</param>
    ''' <param name="sTempOutputFile">The last used temporary file. Note: This is only the path, the file will be removed!</param>
    ''' <param name="sWorkingDirectory">Sets the compiler working directory. Use |Nothing| to use the working directioy from the active tab.</param>
    ''' <param name="sCompilerPath">The compiler path. Use |Nothing| to get the source from the active tab.</param>
    ''' <param name="sIncludePaths">The include paths seperated by ';'. Use |Nothing| to get the source from the active tab.</param>
    ''' <param name="sEmulateSourceFile">Replaces the printed temporary path.</param>
    ''' <param name="bUseCustomCompilerOptions">If true it will use the configs custom compiler options, false otherwise.</param>
    ''' <param name="sCompilerOutput">The compiler output.</param>
    ''' <param name="iCompilerType"></param>
    ''' <returns>New source on success, |Nothing| in otherwise.</returns>
    Public Function GetCompilerPreProcessCode(sSource As String,
                                              bCleanUpSourcemodDuplicate As Boolean,
                                              bCleanupForCompile As Boolean,
                                              ByRef sTempOutputFile As String,
                                              Optional mConfig As ClassConfigs.STRUC_CONFIG_ITEM = Nothing,
                                              Optional sWorkingDirectory As String = Nothing,
                                              Optional sCompilerPath As String = Nothing,
                                              Optional sIncludePaths As String = Nothing,
                                              Optional sEmulateSourceFile As String = Nothing,
                                              Optional bUseCustomCompilerOptions As Boolean = True,
                                              ByRef Optional sCompilerOutput As String = Nothing,
                                              ByRef Optional iCompilerType As ENUM_COMPILER_TYPE = ENUM_COMPILER_TYPE.UNKNOWN) As String
        Try
            If (sSource Is Nothing) Then
                sSource = g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor.Document.TextContent
            End If

            If (mConfig Is Nothing) Then
                mConfig = g_mFormMain.g_ClassTabControl.m_ActiveTab.m_ActiveConfig
            End If

            If (Not String.IsNullOrEmpty(sEmulateSourceFile)) Then
                With New Text.StringBuilder
                    .AppendLine(String.Format("#file ""{0}""", sEmulateSourceFile))
                    .AppendLine("#line 0")
                    .AppendLine(sSource)

                    sSource = .ToString
                End With
            End If

            If (g_mFormMain.SplitContainer_ToolboxSourceAndDetails.Panel2Collapsed) Then
                g_mFormMain.SplitContainer_ToolboxSourceAndDetails.Panel2Collapsed = False
                g_mFormMain.SplitContainer_ToolboxSourceAndDetails.SplitterDistance = g_mFormMain.SplitContainer_ToolboxSourceAndDetails.Height - 200
            End If
            g_mFormMain.TabControl_Details.SelectTab(1)

            g_mFormMain.PrintInformation("[INFO]", "Pre-Processing source started!", False, False, True)

            Dim sMarkStart As String = Guid.NewGuid.ToString
            Dim sMarkEnd As String = Guid.NewGuid.ToString

            Dim sOutputFile As String = ""

            Dim iExitCode As Integer = 0
            Dim sOutput As String = ""
            Dim sLines As String()

            'Check compiler
            If (sCompilerPath Is Nothing) Then
                If (mConfig.g_iCompilingType = ClassSettings.ENUM_COMPILING_TYPE.AUTOMATIC) Then
                    If (g_mFormMain.g_ClassTabControl.m_ActiveTab.m_IsUnsaved AndAlso
                                g_mFormMain.g_ClassTabControl.PromptSaveTab(g_mFormMain.g_ClassTabControl.m_ActiveTabIndex, False, True, True)) Then
                        g_mFormMain.PrintInformation("[ERRO]", "Pre-Processing failed! Could not get current source file!", False, False, True)
                        Return Nothing
                    End If

                    If (g_mFormMain.g_ClassTabControl.m_ActiveTab.m_IsUnsaved) Then
                        g_mFormMain.PrintInformation("[ERRO]", "Pre-Processing failed! Could not get current source file!", False, False, True)
                        Return Nothing
                    End If

                    While True
                        'SourcePawn
                        sCompilerPath = IO.Path.Combine(IO.Path.GetDirectoryName(g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File), "spcomp.exe")
                        If (IO.File.Exists(sCompilerPath)) Then
                            Exit While
                        End If

                        'AMX Mod X
                        sCompilerPath = IO.Path.Combine(IO.Path.GetDirectoryName(g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File), "amxxpc.exe")
                        If (IO.File.Exists(sCompilerPath)) Then
                            Exit While
                        End If

                        'Small
                        sCompilerPath = IO.Path.Combine(IO.Path.GetDirectoryName(g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File), "sc.exe")
                        If (IO.File.Exists(sCompilerPath)) Then
                            Exit While
                        End If

                        'Pawn
                        sCompilerPath = IO.Path.Combine(IO.Path.GetDirectoryName(g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File), "pawncc.exe")
                        If (IO.File.Exists(sCompilerPath)) Then
                            Exit While
                        End If

                        g_mFormMain.PrintInformation("[ERRO]", "Pre-Processing failed! Compiler can not be found!", False, False, True)
                        Return Nothing
                    End While
                Else
                    sCompilerPath = mConfig.g_sCompilerPath
                    If (Not IO.File.Exists(sCompilerPath)) Then
                        g_mFormMain.PrintInformation("[ERRO]", "Pre-Processing failed! Compiler can not be found!", False, False, True)
                        Return Nothing
                    End If
                End If
            Else
                If (Not IO.File.Exists(sCompilerPath)) Then
                    g_mFormMain.PrintInformation("[ERRO]", "Pre-Processing failed! Compiler can not be found!", False, False, True)
                    Return Nothing
                End If
            End If

            'Check include path
            If (sIncludePaths Is Nothing) Then
                If (mConfig.g_iCompilingType = ClassSettings.ENUM_COMPILING_TYPE.AUTOMATIC) Then
                    If (g_mFormMain.g_ClassTabControl.m_ActiveTab.m_IsUnsaved AndAlso
                                g_mFormMain.g_ClassTabControl.PromptSaveTab(g_mFormMain.g_ClassTabControl.m_ActiveTabIndex, False, True, True)) Then
                        g_mFormMain.PrintInformation("[ERRO]", "Pre-Processing failed! Could not get current source file!", False, False, True)
                        Return Nothing
                    End If

                    If (g_mFormMain.g_ClassTabControl.m_ActiveTab.m_IsUnsaved) Then
                        g_mFormMain.PrintInformation("[ERRO]", "Pre-Processing failed! Could not get current source file!", False, False, True)
                        Return Nothing
                    End If

                    sIncludePaths = IO.Path.Combine(IO.Path.GetDirectoryName(g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File), "include")
                    If (Not IO.Directory.Exists(sIncludePaths)) Then
                        g_mFormMain.PrintInformation("[ERRO]", "Pre-Processing failed! Include path can not be found!", False, False, True)
                        Return Nothing
                    End If
                Else
                    sIncludePaths = mConfig.g_sIncludeFolders
                    For Each sInclude As String In sIncludePaths.Split(";"c)
                        If (Not IO.Directory.Exists(sInclude)) Then
                            g_mFormMain.PrintInformation("[ERRO]", "Pre-Processing failed! Include path can not be found!", False, False, True)
                            Return Nothing
                        End If
                    Next
                End If
            Else
                If (Not IO.Directory.Exists(sIncludePaths)) Then
                    g_mFormMain.PrintInformation("[ERRO]", "Pre-Processing failed! Include path can not be found!", False, False, True)
                    Return Nothing
                End If
            End If


            Dim sTmpSource As String = sSource
            Dim sTmpSourcePath As String = String.Format("{0}.src", IO.Path.Combine(IO.Path.GetTempPath, Guid.NewGuid.ToString))

            sTempOutputFile = sTmpSourcePath

            If (bCleanUpSourcemodDuplicate) Then
                '#file pushes the lines +1 in the main source, add #line 0 to make them even again
                With New Text.StringBuilder
                    .AppendLine("#file " & sMarkStart)
                    .AppendLine("#line 0")
                    .AppendLine(sTmpSource)
                    .AppendLine("#file " & sMarkEnd)

                    sTmpSource = .ToString
                End With
            End If

            IO.File.WriteAllText(sTmpSourcePath, sTmpSource)

            sOutputFile = String.Format("{0}.lst", IO.Path.Combine(IO.Path.GetTempPath, Guid.NewGuid.ToString))

            Dim lIncludeList As New List(Of String)
            For Each sInclude As String In sIncludePaths.Split(";"c)
                lIncludeList.Add("-i""" & sInclude & """")
            Next

            'Get compiler type first.
            'Normaly every compiler should print help without any process arguments, but AMX Mod X compiler wants input...
            'Use an unfinished argument to force help.
            'TODO: Should detect compiler type using file info instead? This is a bad hack.
            ClassTools.ClassProcess.ExecuteProgram(sCompilerPath, "-", iExitCode, sOutput)

            iCompilerType = ENUM_COMPILER_TYPE.UNKNOWN

            sLines = sOutput.Split(New String() {Environment.NewLine, vbLf}, 0)
            If (sLines.Length > 0) Then
                Select Case (True)
                    Case Regex.IsMatch(sLines(0), "\b(SourcePawn)\b \b(Compiler)\b", RegexOptions.IgnoreCase)
                        iCompilerType = ENUM_COMPILER_TYPE.SOURCEPAWN

                    Case Regex.IsMatch(sLines(0), "\b(AMX)\b \b(Mod)\b \b(X)\b", RegexOptions.IgnoreCase)
                        iCompilerType = ENUM_COMPILER_TYPE.AMXX

                            'Old AMX Mod still uses Small compiler
                    Case Regex.IsMatch(sLines(0), "\b(Pawn)\b \b(compiler)\b", RegexOptions.IgnoreCase), Regex.IsMatch(sLines(0), "\b(Small)\b \b(compiler)\b", RegexOptions.IgnoreCase)
                        iCompilerType = ENUM_COMPILER_TYPE.AMX

                End Select
            End If

            'Build arguments 
            Dim lArguments As New List(Of String) From {
               String.Format("""{0}"" -l {1} -o""{2}""", sTmpSourcePath, String.Join(" ", lIncludeList.ToArray), sOutputFile)
            }

            If (bUseCustomCompilerOptions) Then
                Select Case (iCompilerType)
                    Case ENUM_COMPILER_TYPE.SOURCEPAWN
                        lArguments.Add(mConfig.g_mCompilerOptionsSP.BuildCommandline)

                    Case ENUM_COMPILER_TYPE.AMXX
                        lArguments.Add(mConfig.g_mCompilerOptionsAMXX.BuildCommandline)

                End Select
            End If

            'Compile 
            If (String.IsNullOrEmpty(sWorkingDirectory) OrElse Not IO.Directory.Exists(sWorkingDirectory)) Then
                ClassTools.ClassProcess.ExecuteProgram(sCompilerPath, String.Join(" "c, lArguments.ToArray), iExitCode, sOutput)
            Else
                ClassTools.ClassProcess.ExecuteProgram(sCompilerPath, String.Join(" "c, lArguments.ToArray), sWorkingDirectory, iExitCode, sOutput)
            End If

            sCompilerOutput = sOutput

            IO.File.Delete(sTmpSourcePath)

            sLines = sOutput.Split(New String() {Environment.NewLine, vbLf}, 0)
            For i = 0 To sLines.Length - 1
                g_mFormMain.PrintInformation("[INFO]", vbTab & sLines(i))
            Next

            sCompilerOutput = String.Join(Environment.NewLine, sLines)

            If (String.IsNullOrEmpty(sOutputFile) OrElse Not IO.File.Exists(sOutputFile)) Then
                g_mFormMain.PrintInformation("[ERRO]", "Pre-Processing failed! Could not get Pre-Processed source file!", False, False, True)
                Return Nothing
            End If

            Dim sOutputSource As String = IO.File.ReadAllText(sOutputFile)

            IO.File.Delete(sOutputFile)


            Dim sList As New List(Of String)
            Dim bRecord As Boolean = False

            Dim sOutputLine As String
            Using mSR As New IO.StringReader(sOutputSource)
                While True
                    sOutputLine = mSR.ReadLine()
                    If (sOutputLine Is Nothing) Then
                        Exit While
                    End If

                    If (bCleanUpSourcemodDuplicate) Then
                        If (Not bRecord) Then
                            If (sOutputLine.Contains("#file " & sMarkStart)) Then
                                bRecord = True
                                Continue While
                            Else
                                If (sList.Count > 0) Then
                                    sList.Clear()
                                End If
                                Continue While
                            End If
                        Else
                            'Remove invalid lines
                            If (sOutputLine.Contains("#line 0")) Then
                                Continue While
                            End If

                            If (sOutputLine.Contains("#file " & sMarkEnd)) Then
                                Exit While
                            End If
                        End If
                    End If

                    sList.Add(sOutputLine)
                End While
            End Using

            Dim sNewSource = String.Join(Environment.NewLine, sList.ToArray)

            If (bCleanupForCompile) Then
                sNewSource = Regex.Replace(sNewSource, "^\s*#\b(endinput)\b", "", RegexOptions.Multiline)
            End If

            g_mFormMain.PrintInformation("[INFO]", "Pre-Processing source finished!", False, False, True)
            Return sNewSource
        Catch ex As Exception
            ClassExceptionLog.WriteToLogMessageBox(ex)
        End Try

        Return Nothing
    End Function

    ''' <summary>
    ''' Compiles new assembly from source.
    ''' </summary>
    ''' <param name="bTesting">Just creates a temporary file and removes it after compile.</param>
    ''' <param name="sSource">The source to compile. Use |Nothing| to get the source from the active tab.</param>
    ''' <param name="sOutputFile">The output file. An extension is still required! (default is *.asm)</param>
    ''' <param name="sWorkingDirectory">Sets the compiler working directory. Use |Nothing| to use the working directioy from the active tab.</param>
    ''' <param name="sCompilerPath">The compiler path. Use |Nothing| to get the source from the active tab.</param>
    ''' <param name="sIncludePaths">The include paths seperated by ';'. Use |Nothing| to get the source from the active tab.</param>
    ''' <param name="sEmulateSourceFile">Replaces the printed temporary path.</param>
    ''' <param name="bUseCustomCompilerOptions">If true it will use the configs custom compiler options, false otherwise.</param>
    ''' <param name="sCompilerOutput">The compiler output.</param>
    ''' <param name="iCompilerType"></param>
    ''' <returns>New assembly on success, |Nothing| otherwise.</returns>
    Public Function GetCompilerAssemblyCode(bTesting As Boolean,
                                            sSource As String,
                                            ByRef sOutputFile As String,
                                            Optional mConfig As ClassConfigs.STRUC_CONFIG_ITEM = Nothing,
                                            Optional sWorkingDirectory As String = Nothing,
                                            Optional sCompilerPath As String = Nothing,
                                            Optional sIncludePaths As String = Nothing,
                                            Optional sEmulateSourceFile As String = Nothing,
                                            Optional bUseCustomCompilerOptions As Boolean = True,
                                            ByRef Optional sCompilerOutput As String = Nothing,
                                            ByRef Optional iCompilerType As ENUM_COMPILER_TYPE = ENUM_COMPILER_TYPE.UNKNOWN) As String
        Try
            If (sSource Is Nothing) Then
                sSource = g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor.Document.TextContent
            End If

            If (mConfig Is Nothing) Then
                mConfig = g_mFormMain.g_ClassTabControl.m_ActiveTab.m_ActiveConfig
            End If

            If (Not String.IsNullOrEmpty(sEmulateSourceFile)) Then
                With New Text.StringBuilder
                    .AppendLine(String.Format("#file ""{0}""", sEmulateSourceFile))
                    .AppendLine("#line 0")
                    .AppendLine(sSource)

                    sSource = .ToString
                End With
            End If

            If (g_mFormMain.SplitContainer_ToolboxSourceAndDetails.Panel2Collapsed) Then
                g_mFormMain.SplitContainer_ToolboxSourceAndDetails.Panel2Collapsed = False
                g_mFormMain.SplitContainer_ToolboxSourceAndDetails.SplitterDistance = g_mFormMain.SplitContainer_ToolboxSourceAndDetails.Height - 200
            End If
            g_mFormMain.TabControl_Details.SelectTab(1)

            g_mFormMain.PrintInformation("[INFO]", "DIASM source started!", False, False, True)

            Dim iExitCode As Integer = 0
            Dim sOutput As String = ""
            Dim sLines As String()

            'Check compiler
            If (sCompilerPath Is Nothing) Then
                If (mConfig.g_iCompilingType = ClassSettings.ENUM_COMPILING_TYPE.AUTOMATIC) Then
                    If (g_mFormMain.g_ClassTabControl.m_ActiveTab.m_IsUnsaved AndAlso
                                g_mFormMain.g_ClassTabControl.PromptSaveTab(g_mFormMain.g_ClassTabControl.m_ActiveTabIndex, False, True, True)) Then
                        g_mFormMain.PrintInformation("[ERRO]", "DIASM failed! Could not get current source file!", False, False, True)
                        Return Nothing
                    End If

                    If (g_mFormMain.g_ClassTabControl.m_ActiveTab.m_IsUnsaved) Then
                        g_mFormMain.PrintInformation("[ERRO]", "DIASM failed! Could not get current source file!", False, False, True)
                        Return Nothing
                    End If

                    While True
                        'SourcePawn
                        sCompilerPath = IO.Path.Combine(IO.Path.GetDirectoryName(g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File), "spcomp.exe")
                        If (IO.File.Exists(sCompilerPath)) Then
                            Exit While
                        End If

                        'AMX Mod X
                        sCompilerPath = IO.Path.Combine(IO.Path.GetDirectoryName(g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File), "amxxpc.exe")
                        If (IO.File.Exists(sCompilerPath)) Then
                            Exit While
                        End If

                        'Small
                        sCompilerPath = IO.Path.Combine(IO.Path.GetDirectoryName(g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File), "sc.exe")
                        If (IO.File.Exists(sCompilerPath)) Then
                            Exit While
                        End If

                        'Pawn
                        sCompilerPath = IO.Path.Combine(IO.Path.GetDirectoryName(g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File), "pawncc.exe")
                        If (IO.File.Exists(sCompilerPath)) Then
                            Exit While
                        End If

                        g_mFormMain.PrintInformation("[ERRO]", "DIASM failed! Compiler can not be found!", False, False, True)
                        Return Nothing
                    End While
                Else
                    sCompilerPath = mConfig.g_sCompilerPath
                    If (Not IO.File.Exists(sCompilerPath)) Then
                        g_mFormMain.PrintInformation("[ERRO]", "DIASM failed! Compiler can not be found!", False, False, True)
                        Return Nothing
                    End If
                End If
            Else
                If (Not IO.File.Exists(sCompilerPath)) Then
                    g_mFormMain.PrintInformation("[ERRO]", "DIASM failed! Compiler can not be found!", False, False, True)
                    Return Nothing
                End If
            End If

            'Check include path
            If (sIncludePaths Is Nothing) Then
                If (mConfig.g_iCompilingType = ClassSettings.ENUM_COMPILING_TYPE.AUTOMATIC) Then
                    If (g_mFormMain.g_ClassTabControl.m_ActiveTab.m_IsUnsaved AndAlso
                                g_mFormMain.g_ClassTabControl.PromptSaveTab(g_mFormMain.g_ClassTabControl.m_ActiveTabIndex, False, True, True)) Then
                        g_mFormMain.PrintInformation("[ERRO]", "DIASM failed! Could not get current source file!", False, False, True)
                        Return Nothing
                    End If

                    If (g_mFormMain.g_ClassTabControl.m_ActiveTab.m_IsUnsaved) Then
                        g_mFormMain.PrintInformation("[ERRO]", "DIASM failed! Could not get current source file!", False, False, True)
                        Return Nothing
                    End If

                    sIncludePaths = IO.Path.Combine(IO.Path.GetDirectoryName(g_mFormMain.g_ClassTabControl.m_ActiveTab.m_File), "include")
                    If (Not IO.Directory.Exists(sIncludePaths)) Then
                        g_mFormMain.PrintInformation("[ERRO]", "DIASM failed! Include path can not be found!", False, False, True)
                        Return Nothing
                    End If
                Else
                    sIncludePaths = mConfig.g_sIncludeFolders
                    For Each sInclude As String In sIncludePaths.Split(";"c)
                        If (Not IO.Directory.Exists(sInclude)) Then
                            g_mFormMain.PrintInformation("[ERRO]", "DIASM failed! Include path can not be found!", False, False, True)
                            Return Nothing
                        End If
                    Next
                End If
            Else
                If (Not IO.Directory.Exists(sIncludePaths)) Then
                    g_mFormMain.PrintInformation("[ERRO]", "DIASM failed! Include path can not be found!", False, False, True)
                    Return Nothing
                End If
            End If

            'Set output path
            If (bTesting) Then
                sOutputFile = String.Format("{0}.asm", IO.Path.Combine(IO.Path.GetTempPath, Guid.NewGuid.ToString))
            Else
                If (String.IsNullOrEmpty(sOutputFile)) Then
                    Throw New ArgumentException("Invalid output file")
                End If
            End If

            Dim TmpSourceFile As String = String.Format("{0}.src", IO.Path.Combine(IO.Path.GetTempPath, Guid.NewGuid.ToString))
            IO.File.WriteAllText(TmpSourceFile, sSource)

            Dim lIncludeList As New List(Of String)
            For Each sInclude As String In sIncludePaths.Split(";"c)
                lIncludeList.Add("-i""" & sInclude & """")
            Next

            'Get compiler type first.
            'Normaly every compiler should print help without any process arguments, but AMX Mod X compiler wants input...
            'Use an unfinished argument to force help.
            'TODO: Should detect compiler type using file info instead? This is a bad hack.
            ClassTools.ClassProcess.ExecuteProgram(sCompilerPath, "-", iExitCode, sOutput)

            iCompilerType = ENUM_COMPILER_TYPE.UNKNOWN

            sLines = sOutput.Split(New String() {Environment.NewLine, vbLf}, 0)
            If (sLines.Length > 0) Then
                Select Case (True)
                    Case Regex.IsMatch(sLines(0), "\b(SourcePawn)\b \b(Compiler)\b", RegexOptions.IgnoreCase)
                        iCompilerType = ENUM_COMPILER_TYPE.SOURCEPAWN

                    Case Regex.IsMatch(sLines(0), "\b(AMX)\b \b(Mod)\b \b(X)\b", RegexOptions.IgnoreCase)
                        iCompilerType = ENUM_COMPILER_TYPE.AMXX

                            'Old AMX Mod still uses Small compiler
                    Case Regex.IsMatch(sLines(0), "\b(Pawn)\b \b(compiler)\b", RegexOptions.IgnoreCase), Regex.IsMatch(sLines(0), "\b(Small)\b \b(compiler)\b", RegexOptions.IgnoreCase)
                        iCompilerType = ENUM_COMPILER_TYPE.AMX

                End Select
            End If

            'Build arguments 
            Dim lArguments As New List(Of String) From {
                String.Format("""{0}"" -a {1} -o""{2}""", TmpSourceFile, String.Join(" ", lIncludeList.ToArray), sOutputFile)
            }

            If (bUseCustomCompilerOptions) Then
                Select Case (iCompilerType)
                    Case ENUM_COMPILER_TYPE.SOURCEPAWN
                        lArguments.Add(mConfig.g_mCompilerOptionsSP.BuildCommandline)

                    Case ENUM_COMPILER_TYPE.AMXX
                        lArguments.Add(mConfig.g_mCompilerOptionsAMXX.BuildCommandline)

                End Select
            End If

            'Compile 
            If (String.IsNullOrEmpty(sWorkingDirectory) OrElse Not IO.Directory.Exists(sWorkingDirectory)) Then
                ClassTools.ClassProcess.ExecuteProgram(sCompilerPath, String.Join(" "c, lArguments.ToArray), iExitCode, sOutput)
            Else
                ClassTools.ClassProcess.ExecuteProgram(sCompilerPath, String.Join(" "c, lArguments.ToArray), sWorkingDirectory, iExitCode, sOutput)
            End If

            sCompilerOutput = sOutput

            sLines = sOutput.Split(New String() {Environment.NewLine, vbLf}, 0)
            For i = 0 To sLines.Length - 1
                g_mFormMain.PrintInformation("[INFO]", vbTab & sLines(i))
            Next

            sCompilerOutput = String.Join(Environment.NewLine, sLines)

            Dim sAssemblySource As String = IO.File.ReadAllText(sOutputFile)

            IO.File.Delete(TmpSourceFile)

            If (bTesting) Then
                IO.File.Delete(sOutputFile)
            End If

            If (Not bTesting) Then
                g_mFormMain.PrintInformation("[INFO]", vbTab & String.Format("Saved DIASM source: {0}", sOutputFile))
            End If

            g_mFormMain.PrintInformation("[INFO]", "DIASM source finished!", False, False, True)
            Return sAssemblySource
        Catch ex As Exception
            ClassExceptionLog.WriteToLogMessageBox(ex)
        End Try

        Return Nothing
    End Function

    ''' <summary>
    ''' Class for custom color highlighting.
    ''' </summary>
    Public Class ClassCustomHighlighting
        Implements IDisposable

        Private g_mFormMain As FormMain
        Private Const g_sColorTextName As String = "Highlighting"

        Class STRUC_HIGHLIGHT_ITEM
            Public sIdentifier As String
            Public sWord As String
            Public mToolStripItem As ToolStripMenuItem
        End Class

        Private g_lHighlightItemList As New List(Of STRUC_HIGHLIGHT_ITEM)

        ReadOnly Property m_HightlightItems As STRUC_HIGHLIGHT_ITEM()
            Get
                Return g_lHighlightItemList.ToArray
            End Get
        End Property

        Public Sub New(f As FormMain)
            g_mFormMain = f
        End Sub

        ''' <summary>
        ''' Adds a highlight item. If exist, it will be ignored.
        ''' The highlight list will automatically allocated.
        ''' </summary>
        ''' <param name="iIndex"></param>
        Public Sub Add(iIndex As Integer)
            AllocateList(iIndex)

            If (g_lHighlightItemList(iIndex) IsNot Nothing) Then
                Return
            End If

            Dim sIdentifier = Guid.NewGuid.ToString

            Dim mMenuItem As ToolStripMenuItem = DirectCast(g_mFormMain.ToolStripMenuItem_HightlightCustom.DropDownItems.Add(String.Format("{0} {1}", g_sColorTextName, iIndex)), ToolStripMenuItem)
            mMenuItem.Name = sIdentifier
            mMenuItem.BackColor = Color.White

            RemoveHandler mMenuItem.Click, AddressOf OnClick
            AddHandler mMenuItem.Click, AddressOf OnClick

            g_lHighlightItemList(iIndex) = New STRUC_HIGHLIGHT_ITEM With {
                .sIdentifier = sIdentifier,
                .sWord = "",
                .mToolStripItem = mMenuItem
            }

        End Sub

        ''' <summary>
        ''' Cleans the highlight list, events and disposes all ToolStrip controls.
        ''' </summary>
        Public Sub Clear()
            For i = 0 To g_lHighlightItemList.Count - 1
                If (g_lHighlightItemList(i) Is Nothing) Then
                    Continue For
                End If

                If (g_lHighlightItemList(i).mToolStripItem Is Nothing OrElse g_lHighlightItemList(i).mToolStripItem.IsDisposed) Then
                    Continue For
                End If

                RemoveHandler g_lHighlightItemList(i).mToolStripItem.Click, AddressOf OnClick

                g_lHighlightItemList(i).mToolStripItem.Dispose()
                g_lHighlightItemList(i).mToolStripItem = Nothing
            Next

            g_lHighlightItemList.Clear()
        End Sub

        Public Function AllocateList(iSize As Integer) As Boolean
            Dim bAllocated As Boolean = False

            While (iSize > g_lHighlightItemList.Count - 1)
                bAllocated = True
                g_lHighlightItemList.Add(Nothing)
            End While

            Return bAllocated
        End Function

        Private Sub OnClick(sender As Object, e As EventArgs)
            Try
                Dim toolStripItem As ToolStripMenuItem = TryCast(sender, ToolStripMenuItem)
                If (toolStripItem Is Nothing) Then
                    Throw New ArgumentException("Invalid ToolStripMenuItem")
                End If

                Dim sIdentifier As String = toolStripItem.Name
                If (String.IsNullOrEmpty(sIdentifier)) Then
                    Throw New ArgumentException("Invalid ToolStripMenuItem name")
                End If

                Dim iIndex As Integer = -1
                Dim highlightItem As STRUC_HIGHLIGHT_ITEM = Nothing

                For i = 0 To g_lHighlightItemList.Count - 1
                    If (g_lHighlightItemList(i) Is Nothing) Then
                        Continue For
                    End If

                    If (g_lHighlightItemList(i).sIdentifier <> sIdentifier) Then
                        Continue For
                    End If

                    iIndex = i
                    highlightItem = g_lHighlightItemList(i)
                Next

                If (highlightItem Is Nothing OrElse iIndex < 0) Then
                    Throw New ArgumentException("No matching ToolStripMenuItem found")
                End If

                highlightItem.sWord = ""

                Dim mActiveTextEditor As TextEditorControlEx = g_mFormMain.g_ClassTabControl.m_ActiveTab.m_TextEditor

                If (mActiveTextEditor.ActiveTextAreaControl.SelectionManager.HasSomethingSelected) Then
                    Dim m_CurrentSelection As ISelection = mActiveTextEditor.ActiveTextAreaControl.SelectionManager.SelectionCollection(0)

                    If (Regex.IsMatch(m_CurrentSelection.SelectedText, "^[a-zA-Z0-9_]+$")) Then
                        highlightItem.sWord = m_CurrentSelection.SelectedText
                    End If
                Else
                    Dim sWord As String = g_mFormMain.g_ClassTextEditorTools.GetCaretWord(False, False, False)

                    If (Not String.IsNullOrEmpty(sWord)) Then
                        highlightItem.sWord = sWord
                    End If
                End If

                If (String.IsNullOrEmpty(highlightItem.sWord)) Then
                    toolStripItem.Text = String.Format("{0} {1}", g_sColorTextName, iIndex)
                Else
                    toolStripItem.Text = String.Format("{0} {1} {2}", g_sColorTextName, iIndex, "(Visible)")
                End If

                g_mFormMain.g_ClassSyntaxTools.g_ClassSyntaxHighlighting.UpdateSyntax(ClassSyntaxTools.ENUM_SYNTAX_UPDATE_TYPE.HIGHLIGHT_WORD_CUSTOM)
                g_mFormMain.g_ClassSyntaxTools.g_ClassSyntaxHighlighting.UpdateTextEditorSyntax()
            Catch ex As Exception
                ClassExceptionLog.WriteToLogMessageBox(ex)
            End Try
        End Sub

#Region "IDisposable Support"
        Private disposedValue As Boolean ' To detect redundant calls

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not Me.disposedValue Then
                If disposing Then
                    ' TODO: dispose managed state (managed objects).
                    Clear()
                End If

                ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
                ' TODO: set large fields to null.
            End If
            Me.disposedValue = True
        End Sub

        ' TODO: override Finalize() only if Dispose(disposing As Boolean) above has code to free unmanaged resources.
        'Protected Overrides Sub Finalize()
        '    ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        '    Dispose(False)
        '    MyBase.Finalize()
        'End Sub

        ' This code added by Visual Basic to correctly implement the disposable pattern.
        Public Sub Dispose() Implements IDisposable.Dispose
            ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(True)
            ' TODO: uncomment the following line if Finalize() is overridden above.
            ' GC.SuppressFinalize(Me)
        End Sub
#End Region
    End Class

    ''' <summary>
    ''' Class for custom text editor iconbar icons if a line has been changed/saved.
    ''' </summary>
    Class ClassLineState
        Private g_mFormMain As FormMain

        Public Sub New(f As FormMain)
            g_mFormMain = f
        End Sub

        Property m_IgnoreUpdates As Boolean

        Property m_LineStateMarks(mTextEditor As TextEditorControlEx) As LineStateBookmark()
            Get
                Dim lBookmarks As New List(Of LineStateBookmark)

                For i = 0 To mTextEditor.Document.BookmarkManager.Marks.Count - 1
                    If (TypeOf mTextEditor.Document.BookmarkManager.Marks(i) Is LineStateBookmark) Then
                        lBookmarks.Add(DirectCast(mTextEditor.Document.BookmarkManager.Marks(i), LineStateBookmark))
                    End If
                Next

                Return lBookmarks.ToArray
            End Get
            Set(value As LineStateBookmark())
                ClearStates(mTextEditor)


                If (value Is Nothing) Then
                    Return
                End If

                For i = 0 To value.Length - 1
                    mTextEditor.Document.BookmarkManager.AddMark(value(i))
                Next
            End Set
        End Property

        Property m_LineState(mTextEditor As TextEditorControlEx, iIndex As Integer) As LineStateBookmark.ENUM_BOOKMARK_TYPE
            Get
                For Each item In mTextEditor.Document.BookmarkManager.Marks
                    Dim lineStateBookmark As LineStateBookmark = TryCast(item, LineStateBookmark)
                    If (lineStateBookmark Is Nothing OrElse lineStateBookmark.Anchor.IsDeleted) Then
                        Continue For
                    End If

                    If (lineStateBookmark.LineNumber <> iIndex) Then
                        Continue For
                    End If

                    Return lineStateBookmark.m_Type
                Next

                Return LineStateBookmark.ENUM_BOOKMARK_TYPE.NONE
            End Get
            Set(value As LineStateBookmark.ENUM_BOOKMARK_TYPE)
                If (m_IgnoreUpdates) Then
                    mTextEditor.Document.RequestUpdate(New TextAreaUpdate(TextAreaUpdateType.SingleLine, iIndex))
                    mTextEditor.Document.CommitUpdate()
                    Return
                End If

                For Each item In mTextEditor.Document.BookmarkManager.Marks
                    Dim lineStateBookmark As LineStateBookmark = TryCast(item, LineStateBookmark)
                    If (lineStateBookmark Is Nothing OrElse lineStateBookmark.Anchor.IsDeleted) Then
                        Continue For
                    End If

                    If (lineStateBookmark.LineNumber <> iIndex) Then
                        Continue For
                    End If

                    lineStateBookmark.m_Type = value
                    Return
                Next

                Dim mBookmark As New LineStateBookmark(mTextEditor.Document, New TextLocation(0, iIndex), True, LineStateBookmark.ENUM_BOOKMARK_TYPE.CHANGED)
                mTextEditor.Document.BookmarkManager.AddMark(mBookmark)

                mTextEditor.Document.RequestUpdate(New TextAreaUpdate(TextAreaUpdateType.SingleLine, iIndex))
                mTextEditor.Document.CommitUpdate()
            End Set
        End Property

        ''' <summary>
        ''' Change all 'changed' states to 'saved'.
        ''' </summary>
        Public Sub SaveStates(mTextEditor As TextEditorControlEx)
            For Each item In mTextEditor.Document.BookmarkManager.Marks
                Dim lineStateBookmark As LineStateBookmark = TryCast(item, LineStateBookmark)
                If (lineStateBookmark Is Nothing) Then
                    Continue For
                End If

                If (lineStateBookmark.m_Type = LineStateBookmark.ENUM_BOOKMARK_TYPE.CHANGED) Then
                    lineStateBookmark.m_Type = LineStateBookmark.ENUM_BOOKMARK_TYPE.SAVED

                    mTextEditor.Document.RequestUpdate(New TextAreaUpdate(TextAreaUpdateType.SingleLine, lineStateBookmark.LineNumber))
                End If
            Next

            mTextEditor.Document.CommitUpdate()
        End Sub

        ''' <summary>
        ''' Change all 'changed' states to 'saved'.
        ''' </summary>
        Public Sub SaveStates(ByRef arrStateBookmarks As LineStateBookmark())
            If (arrStateBookmarks Is Nothing) Then
                Return
            End If

            For i = 0 To arrStateBookmarks.Length - 1
                If (arrStateBookmarks(i).m_Type = LineStateBookmark.ENUM_BOOKMARK_TYPE.CHANGED) Then
                    arrStateBookmarks(i).m_Type = LineStateBookmark.ENUM_BOOKMARK_TYPE.SAVED
                End If
            Next
        End Sub

        ''' <summary>
        ''' Clear all line states. Like on loading or new source.
        ''' </summary>
        Public Sub ClearStates(mTextEditor As TextEditorControlEx)
            mTextEditor.Document.BookmarkManager.RemoveMarks(Function(mBookmark As Bookmark) TypeOf mBookmark Is LineStateBookmark)
        End Sub

        ''' <summary>
        ''' Custom bookmark to display custom icons in the iconbar.
        ''' </summary>
        Public Class LineStateBookmark
            Inherits Bookmark

            Enum ENUM_BOOKMARK_TYPE
                NONE
                CHANGED
                SAVED
            End Enum

            Private g_sIdentifier As String = Guid.NewGuid.ToString

            Property m_Type As ENUM_BOOKMARK_TYPE

            Public Sub New(iDocument As IDocument, textLocation As TextLocation, bEnabled As Boolean, iType As ENUM_BOOKMARK_TYPE)
                MyBase.New(iDocument, textLocation, bEnabled)
                Me.m_Type = iType
            End Sub

            Public Overrides ReadOnly Property CanToggle As Boolean
                Get
                    Return False
                    'Return MyBase.CanToggle
                End Get
            End Property

            Protected Overrides Sub OnIsEnabledChanged(e As EventArgs)
                IsEnabled = True
                'MyBase.OnIsEnabledChanged(e)
            End Sub

            ''' <summary>
            ''' Lets override the bookmarks drawing, so we can show our custom coloring!
            ''' The bookmark is the only real icon we can change.
            ''' </summary>
            ''' <param name="iconBarMargin"></param>
            ''' <param name="iGraphics"></param>
            ''' <param name="iPoint"></param>
            Public Overrides Sub Draw(iconBarMargin As IconBarMargin, iGraphics As Graphics, iPoint As Point)
                'Dim num As Integer = margin.TextArea.TextView.FontHeight / 8
                'Dim r As Rectangle = New Rectangle(1, p.Y + num, margin.DrawingPosition.Width - 4, margin.TextArea.TextView.FontHeight - num * 2)
                Dim calcFontHeight As Integer = CInt(iconBarMargin.TextArea.TextView.FontHeight / 32)
                Dim r As New Rectangle(CInt(iconBarMargin.DrawingPosition.Width / 1.25),
                                        iPoint.Y + calcFontHeight,
                                        iconBarMargin.DrawingPosition.Width - 4,
                                        iconBarMargin.TextArea.TextView.FontHeight - calcFontHeight * 2)

                Dim iColor As Color = If(m_Type = ENUM_BOOKMARK_TYPE.CHANGED, Color.Orange, Color.Green)

                Using mBrush As New Drawing2D.LinearGradientBrush(New Point(r.Left, r.Top), New Point(r.Right, r.Bottom), iColor, iColor)
                    iGraphics.FillRectangle(mBrush, r)
                End Using
            End Sub
        End Class
    End Class
End Class
