using System;
using System.Collections.Generic;

using log4net;

using Command;
using CommandLine;



namespace HFM
{

    /// <summary>
    /// Base class for all HFM Load/Extract option collections.
    /// These are implementations of the TODO: SettingsCollection class, used to pass
    /// around the myriad options that govern the behaviour of HFM loads and
    /// extracts of Metadata, Security, Rules, Member Lists, Data, and Journals.
    /// </summary>
    /// <remarks>
    /// These are a sort of hybrid struct/collection/enum:
    /// - each collection has a fixed set of members
    /// - members of the collection can be accessed by ordinal or name, and
    ///   the valid ordinals and names can be determined from a corresponding
    ///   enum type.
    /// - members of the collection have a common set of methods and properties,
    ///   which can return information about the valid values/ranges, default
    ///   value etc
    /// Unfortunately, the collections do not share a common base class, and
    /// so to determine the valid members of the collection, get the default
    /// values for an item in the collection, and set a current value in a
    /// collection for all load and extract option sets, we need to make heavy
    /// use of reflection.
    /// </remarks>
    public abstract class LoadExtractOptions : ISettingsCollection
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        protected Type _optionsType;
        protected Type _optionType;
        protected Type _enumType;
        protected object _options;

        internal object IHsvLoadExtractOptions { get { return _options; } }

        public object this[string key]
        {
            get {
                var option = _optionsType.GetMethod("get_Item").Invoke(_options, new object[] { key });
                return _optionType.GetProperty("CurrentValue").GetValue(option, null);
            }
            set {
                var option = _optionsType.GetMethod("get_Item").Invoke(_options, new object[] { key });
                _optionType.GetProperty("CurrentValue").SetValue(option, value, null);
            }
        }


        public LoadExtractOptions(Type optionsType, Type optionType, Type enumType, object options)
        {
            _optionsType = optionsType;
            _optionType = optionType;
            _enumType = enumType;
            _options = options;
        }


        public object DefaultValue(string key)
        {
            var option = _optionsType.GetMethod("get_Item").Invoke(_options, new object[] { key });
            return _optionType.GetProperty("DefaultValue").GetValue(option, null);
        }


        public void GetOptionNames()
        {
            foreach(var val in Enum.GetValues(_enumType)) {
                var option = _optionsType.GetMethod("get_Item").Invoke(_options, new object[] { val });
                var name = _optionType.GetProperty("Name").GetValue(option, null);
                _log.InfoFormat("Option {0} name: {1}", val, name);
            }
        }

    }

}
