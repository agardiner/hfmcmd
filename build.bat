@ECHO OFF
IF "%1"=="" (SET TARGET_DOTNET=35) ELSE (SET TARGET_DOTNET=%1)


IF NOT EXIST gen MKDIR gen
IF NOT EXIST bin MKDIR bin
DEL /Q gen\*.*
DEL /Q bin\*.*


ECHO Generating resources...

tools\ResGen.exe resources/CommandLine.resx gen\HFMCmd.Resource.CommandLine.resources /str:cs,HFMCmd.Resource,CommandLine,gen\CommandLineResource.cs
IF ERRORLEVEL 1 EXIT /B %ERRORLEVEL%

tools\ResGen.exe resources/Help.resx gen\HFMCmd.Resource.Help.resources /str:cs,HFMCmd.Resource,Help,gen\HelpResource.cs
IF ERRORLEVEL 1 EXIT /B %ERRORLEVEL%


IF %TARGET_DOTNET%==35 GOTO DOTNET35
IF %TARGET_DOTNET%==40 GOTO DOTNET40


ECHO Usage: build.bat *Version*, where *Version* is one of: 35 or 40 (default is 35)
EXIT /B 99



:DOTNET35
ECHO Targeting .NET Framework 3.5...

ECHO Compiling HFMCmd...
C:\WINDOWS\Microsoft.NET\Framework\v3.5\csc.exe /nologo /target:exe /main:HFMCmd.Launcher /out:gen\3.5\HFMCmd.exe /debug /optimize+ /lib:lib\log4net-1.2.11\bin\net\3.5\release /reference:log4net.dll /lib:lib/hfm-11.1.2.2 /reference:Interop.HFMCONSTANTSLib.dll /reference:Interop.HFMSLICECOMLib.dll /reference:Interop.HFMWAPPLICATIONSLib.dll /reference:Interop.HFMWDOCUMENTSLib.dll /reference:Interop.HFMWSESSIONLib.dll /reference:Interop.HSVCALCULATELib.dll /reference:Interop.HSVCDATALOADLib.dll /reference:Interop.HSVDATALib.dll /reference:Interop.HSVJOURNALLOADACVLib.dll /reference:Interop.HSVJOURNALSLib.dll /reference:Interop.HSVMETADATALib.dll /reference:Interop.HSVMETADATALOADACVLib.dll /reference:Interop.HSVRESOURCEMANAGERLib.dll /reference:Interop.HSVRULESLOADACVLib.dll /reference:Interop.HSVSECURITYACCESSLib.dll /reference:Interop.HSVSECURITYLOADACVLib.dll /reference:Interop.HSVSESSIONLib.dll /reference:Interop.HSVSTARSCHEMAACMLib.dll /reference:Interop.HSVSYSTEMINFOLib.dll /reference:Interop.HSXCLIENTLib.dll /reference:Interop.HSXSERVERLib.dll /resource:gen\HFMCmd.Resource.CommandLine.resources /resource:gen\HFMCmd.Resource.Help.resources src\*.cs src\command\*.cs src\commandline\*.cs src\yaml\*.cs src\hfm\*.cs gen\*.cs properties\*.cs
IF ERRORLEVEL 1 EXIT /B %ERRORLEVEL%

ECHO Bundling HFMCmd and libraries into HFMCmd.exe...
tools\ILMerge\ILMerge.exe  /wildcards /lib:C:\WINDOWS\Microsoft.NET\Framework\v3.5 /out:bin\3.5\HFMCmd.exe gen\3.5\HFMCmd.exe lib/hfm-11.1.2.2\*.dll lib\log4net-1.2.11\bin\net\3.5\release\log4net.dll

GOTO END



:DOTNET40
ECHO Targeting .NET Framework 4.0...

ECHO Compiling HFMCmd...
C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /target:exe /main:HFMCmd.Launcher /out:gen\4.0\HFMCmd.exe /debug /optimize+ /lib:lib\log4net-1.2.11\bin\net\4.0\release /reference:log4net.dll /lib:lib/hfm-11.1.2.2 /link:Interop.HFMCONSTANTSLib.dll /link:Interop.HFMSLICECOMLib.dll /link:Interop.HFMWAPPLICATIONSLib.dll /link:Interop.HFMWDOCUMENTSLib.dll /link:Interop.HFMWSESSIONLib.dll /link:Interop.HSVCALCULATELib.dll /link:Interop.HSVCDATALOADLib.dll /link:Interop.HSVDATALib.dll /link:Interop.HSVJOURNALLOADACVLib.dll /link:Interop.HSVJOURNALSLib.dll /link:Interop.HSVMETADATALib.dll /link:Interop.HSVMETADATALOADACVLib.dll /link:Interop.HSVRESOURCEMANAGERLib.dll /link:Interop.HSVRULESLOADACVLib.dll /link:Interop.HSVSECURITYACCESSLib.dll /link:Interop.HSVSECURITYLOADACVLib.dll /link:Interop.HSVSESSIONLib.dll /link:Interop.HSVSTARSCHEMAACMLib.dll /link:Interop.HSVSYSTEMINFOLib.dll /link:Interop.HSXCLIENTLib.dll /link:Interop.HSXSERVERLib.dll /resource:gen\HFMCmd.Resource.CommandLine.resources /resource:gen\HFMCmd.Resource.Help.resources src\*.cs src\command\*.cs src\commandline\*.cs src\yaml\*.cs src\hfm\*.cs gen\*.cs properties\*.cs
IF ERRORLEVEL 1 EXIT /B %ERRORLEVEL%

ECHO Bundling HFMCmd and libraries into HFMCmd.exe...
tools\ILMerge\ILMerge.exe /targetplatform:v4,C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319 /wildcards /lib:C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319 /out:bin\4.0\HFMCmd.exe gen\4.0\HFMCmd.exe  lib\log4net-1.2.11\bin\net\4.0\release\log4net.dll

GOTO END


:END
ECHO Build complete!
