using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

using log4net;


namespace Command
{

    /// <summary>
    /// Provides a registry of discovered Commands and Factories, as well as
    /// methods for discovering them.
    /// </summary>
    public class Registry
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Dictionary of command instances keyed by command name
        private Dictionary<string, Command> _commands;
        // Dictionary of types to factory constructors/methods/properties
        private Dictionary<Type, Factory> _factories;
        /// Dictionary of class types to settings that instances of the class hold
        private Dictionary<Type, List<SettingAttribute>> _settings;
        // Dictionary of types to alternate factories
        protected List<Factory> _alternates;


        /// Return the Command object corresponding to the requested name.
        public Command this[string cmdName] {
            get {
                if(_commands.ContainsKey(cmdName)) {
                    return _commands[cmdName];
                }
                else {
                    throw new KeyNotFoundException(string.Format(
                                "No command named '{0}' has been registered",
                                cmdName));
                }
            }
        }

        /// Property for accessing the TypeConverter
        public ITypeConverter TypeConverter { get; set; }


        /// Constructor
        public Registry()
        {
            _commands = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);
            _factories = new Dictionary<Type, Factory>();
            _settings = new Dictionary<Type, List<SettingAttribute>>();
            _alternates = new List<Factory>();
            TypeConverter = new PluggableTypeConverter();
        }


        /// <summary>
        /// Registers commands (i.e. methods tagged with the Command attribute)
        /// in the current assembly.
        /// </summary>
        public void RegisterNamespace(string ns)
        {
            RegisterNamespace(Assembly.GetExecutingAssembly(), ns);
        }


        /// <summary>
        /// Registers commands from the supplied assembly. Commands methods must
        /// be tagged with the attribute Command to be locatable.
        /// </summary>
        public void RegisterNamespace(Assembly asm, string ns)
        {
            _log.DebugFormat("Searching for commands under namespace '{0}'...", ns);
            foreach(var t in asm.GetExportedTypes()) {
                if(t.Namespace == ns && t.IsClass) {
                    RegisterClass(t);
                }
            }
        }


        /// <summary>
        /// Registers commands and factories from the supplied class.
        /// Commands must be tagged with the attribute Command to be locatable.
        /// </summary>
        public void RegisterClass(Type t)
        {
            Factory factory;
            Command cmd;

            if(t.IsClass) {
                // Process class level attributes
                List<SettingAttribute> settings = new List<SettingAttribute>();
                foreach(var attr in t.GetCustomAttributes(typeof(SettingAttribute), false)) {
                    if(attr is DynamicSettingAttribute) {
                        // Verify that DynamicSettingAttributes are only used on IDynamicSettingsCollection
                        if(t.GetInterface("IDynamicSettingsCollection") == null) {
                            throw new ArgumentException(string.Format("A DynamicSettingAttribute has " +
                                        "been defined on class {0}, but this class does not implement " +
                                        "the IDynamicSettingsCollection interface", t.Name));
                        }
                    }
                    settings.Add((SettingAttribute)attr);
                }
                // Attributes are returned in random order, so sort by preferred order
                settings.Sort((a, b) => (a.SortKey).CompareTo(b.SortKey));
                _settings[t] = settings;

                // Process members of class
                foreach(var mi in t.GetMembers(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)) {
                    cmd = null;
                    factory = null;
                    foreach(var attr in mi.GetCustomAttributes(false)) {
                        if(attr is CommandAttribute) {
                            cmd = new Command(t, mi as MethodInfo, attr as CommandAttribute);
                            Add(cmd);
                        }
                        if(attr is FactoryAttribute) {
                            factory = new Factory(mi, attr as FactoryAttribute);
                            Add(factory);
                        }
                    }
                    if(cmd != null && factory != null) {
                        cmd._factory = factory;
                        factory._command = cmd;
                    }
                }
            }
        }


        /// <summary>
        /// Registers the specified Command instance.
        /// </summary>
        public void Add(Command cmd)
        {
            if(_commands.ContainsKey(cmd.Name)) {
                throw new ArgumentException(string.Format("A command has already been " +
                            "registered with the name {0} (in class {1})", cmd.Name, cmd.Type));
            }
            _commands.Add(cmd.Name, cmd);
            if(cmd.Alias != null) {
                if(!_commands.ContainsKey(cmd.Alias)) {
                    _commands.Add(cmd.Alias, cmd);
                }
                else {
                    _log.WarnFormat("Attempt to register command {0} under alias {1} failed; " +
                            "alias is already registered", cmd.Name, cmd.Alias);
                }
            }
        }


        /// <summary>
        /// Registers the specified Factory instance as an alternate mechanism
        /// for obtaining objects of the Factory return type. An alternate means
        /// will be tried when no other non-alternate path to create an object
        /// can be found from the current context state.
        /// </summary>
        public void Add(Factory factory)
        {
            if(factory.IsAlternate) {
                _alternates.Add(factory);
            }
            else {
                if(_factories.ContainsKey(factory.ReturnType)) {
                    throw new ArgumentException(string.Format("A factory has already been " +
                                "registered for {0} objects", factory.ReturnType));
                }
                _factories.Add(factory.ReturnType, factory);
            }
        }


        /// <summary>
        /// Checks to see if a command with the given name is available.
        /// </summary>
        public bool Contains(string cmdName)
        {
            return _commands.ContainsKey(cmdName);
        }


        /// <summary>
        /// Checks to see if a Factory for the specified type is available.
        /// </summary>
        public bool Contains(Type type)
        {
            return _factories.ContainsKey(type);
        }


        /// <summary>
        /// Returns the Factory instance for objects of the specified Type.
        /// </summary>
        public Factory GetFactory(Type type)
        {
            return _factories[type];
        }


        /// <summary>
        /// Returns each Command that is currently registered.
        /// </summary>
        public IEnumerable<Command> EachCommand()
        {
            return _commands.Values.OrderBy(c => c.Name);
        }


        /// <summary>
        /// Returns a list of the settings an ISettingsCollection instance
        /// holds.
        /// </summary>
        public List<SettingAttribute> GetSettings(Type type)
        {
            return _settings[type];
        }


        /// <summary>
        /// Returns an IEnumerable of the alternate Factory objects registered
        /// for the specified type.
        /// </summary>
        public IEnumerable<Factory> GetAlternates(Type type)
        {
            return _alternates.Where(f => f.ReturnType == type);
        }

    }

}
