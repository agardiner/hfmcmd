using System;
using System.IO;

using log4net;
using HSVSESSIONLib;
using HSVSTARSCHEMAACMLib;

using Command;
using HFMCmd;


namespace HFM
{

    /// <summary>
    /// Class for working with HFM Extended Analytics extracts.
    /// </summary>
    public class StarSchema
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Reference to HFM HsvStarSchemaACM object
        internal readonly HsvStarSchemaACM HsvStarSchemaACM;


        [Factory]
        public StarSchema(Session session)
        {
            HsvStarSchemaACM = new HsvStarSchemaACM();
            HsvStarSchemaACM.SetSession(session.HsvSession);
        }


        [Command("Extracts HFM data to a set of tables in a relational database")]
        public void ExtractDataToStarSchema(
                Slice slice)
        {
        }
    }

}
