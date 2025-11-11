Name "EnVar Example"
OutFile "EnVarExample.exe"
 
RequestExecutionLevel User
ShowInstDetails Show
 
Page InstFiles
 
Unicode True
 
Section
 
  ; Check for write access
  EnVar::Check "NULL" "NULL"
  Pop $0
  DetailPrint "EnVar::Check write access HKCU returned=|$0|"
 
  ; Set to HKLM
  EnVar::SetHKLM

  ; Check for write access
  EnVar::Check "NULL" "NULL"
  Pop $0
  DetailPrint "EnVar::Check write access HKLM returned=|$0|"
 
  ; Set back to HKCU
  EnVar::SetHKCU
  DetailPrint "EnVar::SetHKCU"
 
  ; Check if the 'temp' variable exists in EnVar::SetHKxx
  EnVar::Check "temp" "NULL"
  Pop $0
  DetailPrint "EnVar::Check returned=|$0|"
 
  ; Append a value
  EnVar::AddValue "ZTestVariable" "C:\Test"
  Pop $0
  DetailPrint "EnVar::AddValue returned=|$0|"
 
  EnVar::AddValue "ZTestVariable" "C:\TestJas"
  Pop $0
  DetailPrint "EnVar::AddValue returned=|$0|"
 
  EnVar::AddValue "ZTestVariable1" "C:\Test"
  Pop $0
  DetailPrint "EnVar::AddValue returned=|$0|"
 
  ; Append an expanded value (REG_EXPAND_SZ)
  EnVar::AddValueEx "ZTestVariable1" "C:\Test"
  Pop $0
  DetailPrint "EnVar::AddValue returned=|$0|"
 
  EnVar::AddValueEx "ZTestVariable1" "C:\TestVariable"
  Pop $0
  DetailPrint "EnVar::AddValue returned=|$0|"
 
  ; Delete a value from a variable
  EnVar::DeleteValue "ZTestVariable1" "C:\Test"
  Pop $0
  DetailPrint "EnVar::DeleteValue returned=|$0|"
 
  EnVar::DeleteValue "ZTestVariable1" "C:\Test"
  Pop $0
  DetailPrint "EnVar::DeleteValue returned=|$0|"
 
  EnVar::DeleteValue "ZTestVariable1" "C:\TestJason"
  Pop $0
  DetailPrint "EnVar::DeleteValue returned=|$0|"
 
  ; Delete a variable
  EnVar::Delete "ZTestVariable"
  Pop $0
  DetailPrint "EnVar::Delete returned=|$0|"
 
  ; Try deleting "path", this should give an error (%path% is a shared resource)
  EnVar::Delete "path"
  Pop $0
  DetailPrint "EnVar::Delete returned=|$0|"
 
SectionEnd