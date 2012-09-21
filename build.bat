@ECHO OFF
IF "%1"=="" (SET TARGET_DOTNET=35) ELSE (SET TARGET_DOTNET=%1)


IF NOT EXIST gen MKDIR gen
IF NOT EXIST bin MKDIR bin
DEL /Q bin\*.*
DEL /Q gen\*.*


ECHO Generating resources...
tools\ResGen.exe resources\Help.resx gen\HFMCmd.Resource.Help.resources /str:cs,HFMCmd.Resource,Help,gen\HelpResource.cs
tools\ResGen.exe resources\CommandLine.resx gen\CommandLine.Resource.resources /str:cs,CommandLine,Resource,gen\CommandLineResource.cs
IF ERRORLEVEL 1 EXIT /B %ERRORLEVEL%


IF %TARGET_DOTNET%==35 GOTO DOTNET35
IF %TARGET_DOTNET%==40 GOTO DOTNET40


ECHO Usage: build.bat *Version*, where *Version* is one of: 35 or 40 (default is 35)
EXIT /B 99



:DOTNET35
ECHO Targeting .NET Framework 3.5...
SET DOTNET=C:\WINDOWS\Microsoft.NET\Framework\v3.5
SET HFM_LIB=lib\hfm-11.1.2.2
SET LOG4NET_LIB=lib\log4net-1.2.11\bin\net\3.5\release

ECHO Compiling HFMCmd...
%DOTNET%\csc.exe /nologo /target:exe /main:HFMCmd.Launcher /out:gen\HFMCmd.exe /debug /optimize+ /lib:%LOG4NET_LIB% /reference:log4net.dll /lib:%HFM_LIB% /reference:Interop.HFMCONSTANTSLib.dll /reference:Interop.HFMSLICECOMLib.dll /reference:Interop.HSVCALCULATELib.dll /reference:Interop.HSVCDATALOADLib.dll /reference:Interop.HSVDATALib.dll /reference:Interop.HSVJOURNALSLib.dll /reference:Interop.HSVJOURNALLOADACVLib.dll /reference:Interop.HSVMETADATALib.dll /reference:Interop.HSVMETADATALOADACVLib.dll /reference:Interop.HSVRESOURCEMANAGERLib.dll /reference:Interop.HSVRULESLOADACVLib.dll /reference:Interop.HSVSECURITYACCESSLib.dll /reference:Interop.HSVSECURITYLOADACVLib.dll /reference:Interop.HSVSESSIONLib.dll /reference:Interop.HSVSTARSCHEMAACMLib.dll /reference:Interop.HSVSYSTEMINFOLib.dll /reference:Interop.HSXCLIENTLib.dll /reference:Interop.HSXSERVERLib.dll /reference:Interop.HFMWAPPLICATIONSLib.dll /reference:Interop.HFMWSESSIONLib.dll /reference:Interop.HFMWDOCUMENTSLib.dll /resource:gen\HFMCmd.Resource.Help.resources /resource:gen\CommandLine.Resource.resources src\*.cs src\command\*.cs src\hfm\*.cs gen\*.cs properties\*.cs
IF ERRORLEVEL 1 EXIT /B %ERRORLEVEL%

ECHO Bundling HFMCmd and libraries into HFMCmd.exe...
tools\ILMerge\ILMerge.exe /wildcards /lib:%DOTNET% /out:bin\HFMCmd.exe gen\HFMCmd.exe %HFM_LIB%\*.dll %LOG4NET_LIB%\log4net.dll

GOTO END



:DOTNET40
ECHO Targeting .NET Framework 4.0...
SET DOTNET=C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319
SET HFM_LIB=lib\hfm-11.1.2.2
SET LOG4NET_LIB=lib\log4net-1.2.11\bin\net\4.0\release

ECHO Compiling HFMCmd...
%DOTNET%\csc.exe /nologo /target:exe /main:HFMCmd.Launcher /out:gen\HFMCmd.exe /debug /optimize+ /lib:%LOG4NET_LIB% /reference:log4net.dll /lib:%HFM_LIB% /link:Interop.HFMCONSTANTSLib.dll /link:Interop.HFMSLICECOMLib.dll /link:Interop.HSVCALCULATELib.dll /link:Interop.HSVCDATALOADLib.dll /link:Interop.HSVDATALib.dll /link:Interop.HSVJOURNALSLib.dll /link:Interop.HSVJOURNALLOADACVLib.dll /link:Interop.HSVMETADATALib.dll /link:Interop.HSVMETADATALOADACVLib.dll /link:Interop.HSVRESOURCEMANAGERLib.dll /link:Interop.HSVRULESLOADACVLib.dll /link:Interop.HSVSECURITYACCESSLib.dll /link:Interop.HSVSECURITYLOADACVLib.dll /link:Interop.HSVSESSIONLib.dll /link:Interop.HSVSTARSCHEMAACMLib.dll /link:Interop.HSVSYSTEMINFOLib.dll /link:Interop.HSXCLIENTLib.dll /link:Interop.HSXSERVERLib.dll /link:Interop.HFMWAPPLICATIONSLib.dll /link:Interop.HFMWSESSIONLib.dll /link:Interop.HFMWDOCUMENTSLib.dll /resource:gen\HFMCmd.Resource.Help.resources /resource:gen\CommandLine.Resource.resources src\*.cs src\command\*.cs src\hfm\*.cs gen\*.cs properties\*.cs
IF ERRORLEVEL 1 EXIT /B %ERRORLEVEL%

ECHO Bundling HFMCmd and libraries into HFMCmd.exe...
tools\ILMerge\ILMerge.exe /targetplatform:v4,%DOTNET% /wildcards /out:bin\HFMCmd.exe gen\HFMCmd.exe %LOG4NET_LIB%\log4net.dll

GOTO END


:END
ECHO Build complete!
