SET HFM_HOME=%EPM_ORACLE_HOME%..\products\FinancialManagement
SET OUTPUT_PATH=%~dp0\hfm-11.1.2.2
SET TLBIMP=%~dp0\..\tools\tlbimp

MKDIR %OUTPUT_PATH%


%TLBIMP% %HFM_HOME%\Client\HFMConstants.dll /out: %OUTPUT_PATH%\Interop.HFMCONSTANTSLib.dll /namespace:HFMCONSTANTSLib
%TLBIMP% %HFM_HOME%\Client\HFMSliceCOM.dll /out: %OUTPUT_PATH%\Interop.HFMSLICECOMLib.dll /namespace:HFMSLICECOMLib
%TLBIMP% %HFM_HOME%\Client\HsvcDataLoad.dll /out: %OUTPUT_PATH%\Interop.HSVCDATALOADLib.dll /namespace:HSVCDATALOADLib
%TLBIMP% %HFM_HOME%\Client\HsvJournalLoadACV.dll /out: %OUTPUT_PATH%\Interop.HSVJOURNALLOADACVLib.dll /namespace:HSVJOURNALLOADACVLib
%TLBIMP% %HFM_HOME%\Client\HsvMetadataLoadACV.dll /out: %OUTPUT_PATH%\Interop.HSVMETADATALOADACVLib.dll /namespace:HSVMETADATALOADACVLib
%TLBIMP% %HFM_HOME%\Client\HsvResourceManager.dll /out: %OUTPUT_PATH%\Interop.HSVRESOURCEMANAGERLib.dll /namespace:HSVRESOURCEMANAGERLib
%TLBIMP% %HFM_HOME%\Client\HsvRulesLoadACV.dll /out: %OUTPUT_PATH%\Interop.HSVRULESLOADACVLib.dll /namespace:HSVRULESLOADACVLib
%TLBIMP% %HFM_HOME%\Client\HsvSecurityLoadACV.dll /out: %OUTPUT_PATH%\Interop.HSVSECURITYLOADACVLib.dll /namespace:HSVSECURITYLOADACVLib


%TLBIMP% %HFM_HOME%\Common\HsxClient.dll /out: %OUTPUT_PATH%\Interop.HSXCLIENTLib.dll /namespace:HSXCLIENTLib /transform:DispRet


%TLBIMP% %HFM_HOME%\Server\HsvCalculate.dll /out: %OUTPUT_PATH%\Interop.HSVCALCULATELib.dll /namespace:HSVCALCULATELib
%TLBIMP% %HFM_HOME%\Server\HsvData.dll /out: %OUTPUT_PATH%\Interop.HSVDATALib.dll /namespace:HSVDATALib
%TLBIMP% %HFM_HOME%\Server\HsxServerFileTransfer.dll /out: %OUTPUT_PATH%\Interop.HSXSERVERFILETRANSFERLib.dll /namespace:HSXSERVERFILETRANSFERLib
%TLBIMP% %HFM_HOME%\Server\HsvJournals.dll /out: %OUTPUT_PATH%\Interop.HSVJOURNALSLib.dll /namespace:HSVJOURNALSLib
%TLBIMP% %HFM_HOME%\Server\HsvMetadata.dll /out: %OUTPUT_PATH%\Interop.HSVMETADATALib.dll /namespace:HSVMETADATALib
%TLBIMP% %HFM_HOME%\Server\HsvProcessFlow.dll /out: %OUTPUT_PATH%\Interop.HSVPROCESSFLOWLib.dll /namespace:HSVPROCESSFLOWLib
%TLBIMP% %HFM_HOME%\Server\HsvSecurityAccess.dll /out: %OUTPUT_PATH%\Interop.HSVSECURITYACCESSLib.dll /namespace:HSVSECURITYACCESSLib
%TLBIMP% %HFM_HOME%\Server\HsvSession.dll /out: %OUTPUT_PATH%\Interop.HSVSESSIONLib.dll /namespace:HSVSESSIONLib
%TLBIMP% %HFM_HOME%\Server\HsvStarSchemaACM.dll /out: %OUTPUT_PATH%\Interop.HSVSTARSCHEMAACMLib.dll /namespace:HSVSTARSCHEMAACMLib
%TLBIMP% %HFM_HOME%\Server\HsvSystemInfo.dll /out: %OUTPUT_PATH%\Interop.HSVSYSTEMINFOLib.dll /namespace:HSVSYSTEMINFOLib
%TLBIMP% %HFM_HOME%\Server\HsxServer.exe /out: %OUTPUT_PATH%\Interop.HSXSERVERLib.dll /namespace:HSXSERVERLib


%TLBIMP% "%HFM_HOME%\Web Server\HFMwApplications.dll" /out: %OUTPUT_PATH%\Interop.HFMWAPPLICATIONSLib.dll /namespace:HFMWAPPLICATIONSLib
%TLBIMP% "%HFM_HOME%\Web Server\HFMwSession.dll" /out: %OUTPUT_PATH%\Interop.HFMWSESSIONLib.dll /namespace:HFMWSESSIONLib
%TLBIMP% "%HFM_HOME%\Web Server\HFMwDocuments.dll" /out: %OUTPUT_PATH%\Interop.HFMWDOCUMENTSLib.dll /namespace:HFMWDOCUMENTSLib
