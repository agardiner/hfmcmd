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

        [Setting("POV", "A Point-of-View expression, such as 'S#Actual.Y#2010.P#May." +
                 "W#YTD.E#E1.V#<Entity Currency>'. Use a POV expression to select members " +
                 "from multiple dimensions in one go. Note that if a dimension member is " +
                 "specified in the POV expression and via a setting for the dimension, the " +
                 "dimension setting takes precedence.",
                 ParameterType = typeof(string), Order = 0),
         Setting("Scenario", "Scenario member(s) to include in the slice definition",
                 Alias = "Scenarios", ParameterType = typeof(string), Order = 2),
         Setting("Year", "Year member(s) to include in the slice definition",
                 Alias = "Years", ParameterType = typeof(string), Order = 3),
         Setting("Period", "Period member(s) to include in the slice definition",
                 Alias = "Periods", ParameterType = typeof(string), Order = 4),
         Setting("View", "View member(s) to include in the slice definition",
                 Alias = "Views", ParameterType = typeof(string), Order = 5,
                 DefaultValue = "<Scenario View>"),
         Setting("Entity", "Entity member(s) to include in the slice definition",
                 Alias = "Entities", ParameterType = typeof(string), Order = 6),
         Setting("Value", "Value member(s) to include in the slice definition",
                 Alias = "Values", ParameterType = typeof(string), Order = 7),
         Setting("Account", "Account member(s) to include in the slice definition",
                 Alias = "Accounts", ParameterType = typeof(string), Order = 8),
         Setting("ICP", "ICP member(s) to include in the slice definition",
                 Alias = "ICPs", ParameterType = typeof(string), Order = 9),
         DynamicSetting("CustomDimName", "<CustomDimName> member(s) to include in the slice definition",
                 ParameterType = typeof(string), Order = 10)]
        public class ExtractSpecification : Slice, IDynamicSettingsCollection
        {
            [Factory(SingleUse = true)]
            public ExtractSpecification(Metadata metadata) : base(metadata) {}
        }


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
                ExtractSpecification slice)
        {
        }
    }

}
