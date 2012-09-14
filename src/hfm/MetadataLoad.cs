using System;

using log4net;
using HSVSESSIONLib;
using HSVMETADATALOADACVLib;

using Command;


namespace HFM
{

    public class MetadataLoad
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Reference to HFM HsvMetadataLoadACV object
        private HsvMetadataLoadACV _metadataLoad;


        [Factory]
        public MetadataLoad(Session session)
        {
            _metadataLoad = new HsvMetadataLoadACVClass();
            _metadataLoad.SetSession(session.HsvSession);
        }


        [Command,
         Description("Extracts an HFM application's metadata to a native ASCII or XML file")]
        public void ExtractMetadata()
        {
            for(var i = 0; i < _metadataLoad.ExtractOptions.Count; ++i) {
                _log.InfoFormat("Extract option {0}: {1}", i, _metadataLoad.ExtractOptions.get_Item(i).Name);
            }
        }

    }

}
