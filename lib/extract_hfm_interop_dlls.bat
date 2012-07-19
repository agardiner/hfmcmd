set HFM_HOME=%EPM_HOME%\products\FinancialManagement
set TARGET=%~dp0%\hfm-11.1.2.1

tlbimp %HFM_HOME%\Client\HFMConstants.dll /out: %OUTPUT_PATH%\Interop.HFMCONSTANTSLib.dll /namespace:HFMCONSTANTSLib
tlbimp %HFM_HOME%\Client\HsvcDataLoad.dll /out: %OUTPUT_PATH%\Interop.HSVCDATALOADLib.dll /namespace:HSVCDATALOADLib
tlbimp %HFM_HOME%\Client\HsvJournalLoadACV.dll /out: %OUTPUT_PATH%\Interop.HSVJOURNALLOADACVLib.dll /namespace:HSVJOURNALLOADACVLib
tlbimp %HFM_HOME%\Client\HsvMetadataLoadACV.dll /out: %OUTPUT_PATH%\Interop.HSVMETADATALOADACVLib.dll /namespace:HSVMETADATALOADACVLib
tlbimp %HFM_HOME%\Client\HsvResourceManager.dll /out: %OUTPUT_PATH%\Interop.HSVRESOURCEMANAGERLib.dll /namespace:HSVRESOURCEMANAGERLib
tlbimp %HFM_HOME%\Client\HsvRulesLoadACV.dll /out: %OUTPUT_PATH%\Interop.HSVRULESLOADACVLib.dll /namespace:HSVRULESLOADACVLib
tlbimp %HFM_HOME%\Client\HsvSecurityLoadACV.dll /out: %OUTPUT_PATH%\Interop.HSVSECURITYLOADACVLib.dll /namespace:HSVSECURITYLOADACVLib

tlbimp %HFM_HOME%\Common\HsxClient.dll /out: %OUTPUT_PATH%\Interop.HSXCLIENTLib.dll /namespace:HSXCLIENTLib /transform:DispRet

tlbimp %HFM_HOME%\Server\HsvCalculate.dll /out: %OUTPUT_PATH%\Interop.HSVCALCULATELib.dll /namespace:HSVCALCULATELib
tlbimp %HFM_HOME%\Server\HsvData.dll /out: %OUTPUT_PATH%\Interop.HSVDATALib.dll /namespace:HSVDATALib
tlbimp %HFM_HOME%\Server\HsvJournals.dll /out: %OUTPUT_PATH%\Interop.HSVJOURNALSLib.dll /namespace:HSVJOURNALSLib
tlbimp %HFM_HOME%\Server\HsvMetadata.dll /out: %OUTPUT_PATH%\Interop.HSVMETADATALib.dll /namespace:HSVMETADATALib
tlbimp %HFM_HOME%\Server\HsvSecurityAccess.dll /out: %OUTPUT_PATH%\Interop.HSVSECURITYACCESSLib.dll /namespace:HSVSECURITYACCESSLib
tlbimp %HFM_HOME%\Server\HsvSession.dll /out: %OUTPUT_PATH%\Interop.HSVSESSIONLib.dll /namespace:HSVSESSIONLib
tlbimp %HFM_HOME%\Server\HsvStarSchemaACM.dll /out: %OUTPUT_PATH%\Interop.HSVSTARSCHEMAACMLib.dll /namespace:HSVSTARSCHEMAACMLib
tlbimp %HFM_HOME%\Server\HsvSystemInfo.dll /out: %OUTPUT_PATH%\Interop.HSVSYSTEMINFOLib.dll /namespace:HSVSYSTEMINFOLib

tlbimp %HFM_HOME%\Server\HsxServer.exe /out: %OUTPUT_PATH%\Interop.HSXSERVERLib.dll /namespace:HSXSERVERLib

