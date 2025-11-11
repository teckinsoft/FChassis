Name "SEU Test"
OutFile "SEU_Test.exe"
RequestExecutionLevel admin
!addplugindir "C:\Program Files (x86)\NSIS\Plugins\x86-unicode"

Section
  ClearErrors
  ; Try to run a harmless command in user session
  ShellExecAsUser::ShellExec 'cmd.exe /C echo ShellExecAsUser OK > "%TEMP%\seu_test.txt"' "" SW_HIDE
  Pop $0
  MessageBox MB_OK "Plugin return: $0"
SectionEnd
