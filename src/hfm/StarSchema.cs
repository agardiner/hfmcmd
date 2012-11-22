using System;
using System.IO;

using log4net;
using HSVSESSIONLib;
using HSVSTARSCHEMAACMLib;

using Command;
using HFMCmd;


namespace HFM
{

    public enum EPushType
    {
        Create = SS_PUSH_OPTIONS.ssCREATE,
        Update = SS_PUSH_OPTIONS.ssUPDATE
    }


    public enum EStarSchemaExtractType
    {
        Standard = EA_EXTRACT_TYPE_FLAGS.EA_EXTRACT_TYPE_STANDARD,
        MetadataAll = EA_EXTRACT_TYPE_FLAGS.EA_EXTRACT_TYPE_METADATA_ALL,
        MetadataSelected = EA_EXTRACT_TYPE_FLAGS.EA_EXTRACT_TYPE_METADATA_SELECTED,
        SQLAggregation = EA_EXTRACT_TYPE_FLAGS.EA_EXTRACT_TYPE_SQL_AGG,
        Essbase = EA_EXTRACT_TYPE_FLAGS.EA_EXTRACT_TYPE_ESSBASE,
        Warehouse = EA_EXTRACT_TYPE_FLAGS.EA_EXTRACT_TYPE_WAREHOUSE
    }

    public enum EFileExtractType
    {
        FlatFile = EA_EXTRACT_TYPE_FLAGS.EA_EXTRACT_TYPE_FLATFILE,
        FlatFileNoHeader = EA_EXTRACT_TYPE_FLAGS.EA_EXTRACT_TYPE_FLATFILE_NOHEADER
    }

    public enum ELineItems
    {
        Exclude = EA_LINEITEM_OPTIONS.EA_LINEITEM_EXCLUDE,
        Summary = EA_LINEITEM_OPTIONS.EA_LINEITEM_SUMMARY,
        Detail = EA_LINEITEM_OPTIONS.EA_LINEITEM_DETAIL
    }


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
                [Parameter("The name of the DSN that contains the connection details for the " +
                           "database where the star schema extract is to be created. This DSN " +
                           "must exist on the HFM server, and have been registered via the " +
                           "HFM Configuration utility.")]
                string DSN,
                [Parameter("The prefix that should appear at the start of each table name created " +
                           "by the extract process.")]
                string tablePrefix,
                [Parameter("Whether to delete any existing data before performing the extract",
                           DefaultValue = true)]
                bool deleteExisting,
                [Parameter("The type of star schema to produce.")]
                EStarSchemaExtractType extractType,
                [Parameter("Whether to include dynamic accounts",
                           DefaultValue = false)]
                bool includeDynamicAccts,
                ExtractSpecification slice)
        {

            DoEAExtract("", "", (SS_PUSH_OPTIONS)(deleteExisting ? EPushType.Create : EPushType.Update),
                        (EA_EXTRACT_TYPE_FLAGS)extractType, includeDynamicAccts,
                        (EA_LINEITEM_OPTIONS)ELineItems.Summary, "", slice);
        }


        [Command("Extracts HFM data to a flat file",
                 Since = "11.1.1")]
        public void ExtractDataToFlatFile(
                [Parameter("Include file header containing extract details",
                           DefaultValue = false)]
                bool includeHeader,
                [Parameter("Whether to include dynamic accounts",
                           DefaultValue = false)]
                bool includeDynamicAccts,
                [Parameter("Level of detail to be extracted for line item detail accounts",
                           DefaultValue = ELineItems.Summary)]
                ELineItems lineItems,
                [Parameter("The field delimiter to use",
                           DefaultValue = ";")]
                string delimiter,
                ExtractSpecification slice)
        {
            DoEAExtract("", "", (SS_PUSH_OPTIONS)EPushType.Create,
                        (EA_EXTRACT_TYPE_FLAGS)(includeHeader ? EFileExtractType.FlatFile :
                                                                EFileExtractType.FlatFileNoHeader),
                        includeDynamicAccts, (EA_LINEITEM_OPTIONS)lineItems, delimiter, slice);

            // TODO: Download the extract file
        }


        private void DoEAExtract(string dsn, string tablePrefix, SS_PUSH_OPTIONS pushType,
                EA_EXTRACT_TYPE_FLAGS extractType, bool includeDynamicAccts, EA_LINEITEM_OPTIONS lineItems,
                string delimiter, ExtractSpecification slice)
        {
            int taskId = 0;

            // TODO: Check user is administrator

            if(HFM.HasVariableCustoms) {
                HFM.Try(() => HsvStarSchemaACM.CreateStarSchemaExtDim(dsn, tablePrefix, pushType,
                                extractType, includeDynamicAccts, true, true, lineItems,
                                false, false, delimiter, slice.HfmSliceCOM, out taskId));
            }
            else {
                HFM.Try(() => HsvStarSchemaACM.CreateStarSchema(dsn, tablePrefix, pushType,
                                extractType, !includeDynamicAccts, slice.Scenarios.MemberIds,
                                slice.Years.MemberIds, slice.Periods.MemberIds, slice.Views.MemberIds,
                                slice.Entities.MemberIds, slice.Entities.ParentIds, slice.Values.MemberIds,
                                slice.Accounts.MemberIds, slice.ICPs.MemberIds, slice.Custom1.MemberIds,
                                slice.Custom2.MemberIds, slice.Custom3.MemberIds, slice.Custom4.MemberIds));
            }

            // TODO: Monitor progress

            // TODO: Retrieve log file
        }

    }

}
