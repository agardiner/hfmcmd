@ECHO OFF
IF "%1"=="" (SET TARGET_DOTNET=35) ELSE (SET TARGET_DOTNET=%1)


IF NOT EXIST gen MKDIR gen
IF NOT EXIST bin MKDIR bin
DEL /Q gen\*.*
DEL /Q bin\*.*


ECHO Generating resources...

tools\ResGen.exe resources/Help.resx gen\HFMCmd.Resource.Help.resources /str:cs,HFMCmd.Resource,Help,gen\HelpResource.cs
IF ERRORLEVEL 1 EXIT /B %ERRORLEVEL%


IF %TARGET_DOTNET%==35 GOTO DOTNET35
IF %TARGET_DOTNET%==40 GOTO DOTNET40


ECHO Usage: build.bat *Version*, where *Version* is one of: 35 or 40 (default is 35)
EXIT /B 99



:DOTNET35
ECHO Targeting .NET Framework 3.5...
IF NOT EXIST gen\NET_3.5_HFM_11.1.2.2.300 MKDIR gen\NET_3.5_HFM_11.1.2.2.300
IF NOT EXIST bin\NET_3.5_HFM_11.1.2.2.300 MKDIR bin\NET_3.5_HFM_11.1.2.2.300

ECHO Compiling HFMCmd...
C:\WINDOWS\Microsoft.NET\Framework\v3.5\csc.exe /nologo /target:exe /main:HFMCmd.Launcher /out:gen\NET_3.5_HFM_11.1.2.2.300\HFMCmd.exe /debug /optimize+ /define:HFM_11_1_2_2_300 /define:HFM_11_1_2_2 /lib:lib\log4net-1.2.11\bin\net\3.5\release /reference:log4net.dll /lib:lib /reference:Interop.SCRIPTINGLib.dll /lib:lib/hfm-11.1.2.2.300 /reference:Interop.HFMCONSTANTSLib.dll /reference:Interop.HFMSLICECOMLib.dll /reference:Interop.HFMWAPPLICATIONSLib.dll /reference:Interop.HFMWDOCUMENTSLib.dll /reference:Interop.HFMWSESSIONLib.dll /reference:Interop.HSVCALCULATELib.dll /reference:Interop.HSVCDATALOADLib.dll /reference:Interop.HSVDATALib.dll /reference:Interop.HSVJOURNALLOADACVLib.dll /reference:Interop.HSVJOURNALSLib.dll /reference:Interop.HSVMETADATALib.dll /reference:Interop.HSVMETADATALOADACVLib.dll /reference:Interop.HSVPROCESSFLOWLib.dll /reference:Interop.HSVRESOURCEMANAGERLib.dll /reference:Interop.HSVRULESLOADACVLib.dll /reference:Interop.HSVSECURITYACCESSLib.dll /reference:Interop.HSVSECURITYLOADACVLib.dll /reference:Interop.HSVSESSIONLib.dll /reference:Interop.HSVSTARSCHEMAACMLib.dll /reference:Interop.HSVSYSTEMINFOLib.dll /reference:Interop.HSXCLIENTLib.dll /reference:Interop.HSXSERVERFILETRANSFERLib.dll /reference:Interop.HSXSERVERLib.dll /resource:gen\HFMCmd.Resource.Help.resources src\*.cs src\command\*.cs src\commandline\*.cs src\yaml\*.cs src\hfm\*.cs gen\*.cs
IF ERRORLEVEL 1 EXIT /B %ERRORLEVEL%

ECHO Bundling HFMCmd and libraries into HFMCmd.exe...
tools\ILMerge\ILMerge.exe  /wildcards /lib:C:\WINDOWS\Microsoft.NET\Framework\v3.5 /out:bin\NET_3.5_HFM_11.1.2.2.300\HFMCmd.exe gen\NET_3.5_HFM_11.1.2.2.300\HFMCmd.exe lib/hfm-11.1.2.2.300\*.dll lib\log4net-1.2.11\bin\net\3.5\release\log4net.dll lib\Interop.SCRIPTINGLib.dll

GOTO END



:DOTNET40
ECHO Targeting .NET Framework 4.0...
IF NOT EXIST gen\NET_4.0 MKDIR gen\NET_4.0
IF NOT EXIST bin\NET_4.0 MKDIR bin\NET_4.0

ECHO Compiling HFMCmd...
C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /target:exe /main:HFMCmd.Launcher /out:gen\NET_4.0\HFMCmd.exe /debug /optimize+ /define:HFM_11_1_2_2_300 /define:HFM_11_1_2_2 /define:LATE_BIND /lib:lib\log4net-1.2.11\bin\net\4.0\release /reference:log4net.dll  /lib:lib/hfm-11.1.2.2.300 /link:Interop.HFMCONSTANTSLib.dll /link:Interop.HSVSECURITYACCESSLib.dll /link:Interop.HSVSTARSCHEMAACMLib.dll /link:Interop.HSVCDATALOADLib.dll /link:Interop.HSVJOURNALLOADACVLib.dll /link:Interop.HSVMETADATALOADACVLib.dll /link:Interop.HSVSECURITYLOADACVLib.dll /resource:gen\HFMCmd.Resource.Help.resources src\*.cs src\command\*.cs src\commandline\*.cs src\yaml\*.cs src\hfm\*.cs gen\*.cs
IF ERRORLEVEL 1 EXIT /B %ERRORLEVEL%

ECHO Bundling HFMCmd and libraries into HFMCmd.exe...
tools\ILMerge\ILMerge.exe /targetplatform:v4,C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319 /wildcards /lib:C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319 /out:bin\NET_4.0\HFMCmd.exe gen\NET_4.0\HFMCmd.exe  lib\log4net-1.2.11\bin\net\4.0\release\log4net.dll lib\Interop.SCRIPTINGLib.dll

GOTO END


:END
ECHO Build complete!
