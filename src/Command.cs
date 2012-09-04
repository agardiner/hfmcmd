using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

using log4net;


namespace Command
{

    /// Define an attribute which will be used to tag methods that can be
    /// invoked from a script or command file.
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    class CommandAttribute : Attribute
    {
    }


    /// Define an attribute which will be used to specify default values for
    /// optional parameters on a Command.
    /// A DefaultValue attribute is used instead of default values on the
    /// actual method, since a) default values are only available from v4 of
    /// .Net, and b) they have restrictions on where they can appear (e.g. only
    /// at the end of the list of parameters).
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    class DefaultValueAttribute : Attribute
    {
        public object Value;

        public DefaultValueAttribute(object val)
        {
            this.Value = val;
        }
    }


    /// Define an attribute which will be used to tag parameters that contain
    /// sensitive information such as passwords. These will be masked if logged.
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    class SensitiveValueAttribute : Attribute
    {
    }


    /// Define an attribute which will be used to set descriptions for command
    /// parameters.
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    class DescriptionAttribute : Attribute
    {
        public string Description;

        public DescriptionAttribute(string desc)
        {
            this.Description = desc;
        }
    }


    /// <summary>
    /// Records details of a parameter to a Command.
    /// </summary>
    public class CommandParameter
    {
        public readonly string Name;
        public readonly Type ParameterType;
        public string Description;
        public bool HasDefaultValue;
        public bool IsSensitive;
        public object DefaultValue;

        /// Constructor
        public CommandParameter(ParameterInfo pi)
        {
            this.Name = pi.Name;
            this.ParameterType = pi.ParameterType;
        }

        public override string ToString()
        {
            return string.Format("{0} ({1})", this.Name, this.ParameterType.Name);
        }
    }



    /// <summary>
    /// Represents a method that can be invoked by some external means.
    /// </summary>
    public class Command
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public readonly string Namespace;
        public readonly string Name;
        public readonly Type Type;
        public readonly MethodInfo MethodInfo;
        public readonly List<CommandParameter> Parameters = new List<CommandParameter>();

        public Type ReturnType {
            get {
                return MethodInfo.ReturnType;
            }
        }

        // Constructor
        public Command(string ns, Type t, MethodInfo mi)
        {
            this.Namespace = ns;
            this.Type = t;
            this.MethodInfo = mi;
            this.Name = mi.Name;

            _log.DebugFormat("Found command {0}", this.Name);

            foreach(var pi in mi.GetParameters()) {
                var param = new CommandParameter(pi);
                _log.DebugFormat("Found parameter {0}", param);
                foreach(var attr in pi.GetCustomAttributes(false)) {
                    if(attr is DefaultValueAttribute) {
                        param.DefaultValue = (attr as DefaultValueAttribute).Value;
                        param.HasDefaultValue = true;
                    }
                    if(attr is SensitiveValueAttribute) {
                        param.IsSensitive = true;
                    }
                    if(attr is DescriptionAttribute) {
                        param.Description = (attr as DescriptionAttribute).Description;
                    }
                }
                this.Parameters.Add(param);
            }
        }
    }



    /// <summary>
    /// Provides a registry of discovered Commands, as well as methods for
    /// discovering them.
    /// </summary>
    public class Registry
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Dictionary of command instances keyed by command name
        private IDictionary<string, Command> _commands;

        public Command this[string name] {
            get {
                return _commands[name];
            }
        }

        /// <summary>
        /// Registers commands (i.e. methods tagged with the Command attribute)
        /// in the current assembly.
        /// </summary>
        public static Registry FindCommands(string ns)
        {
            return FindCommands(Assembly.GetExecutingAssembly(), ns, null);
        }

        /// <summary>
        /// Registers commands (i.e. methods tagged with the Command attribute)
        /// in the current assembly.
        /// </summary>
        public static Registry FindCommands(string ns, Registry registry)
        {
            return FindCommands(Assembly.GetExecutingAssembly(), ns, registry);
        }


        /// <summary>
        /// Registers commands from the supplied assembly. Commands methods must
        /// be tagged with the attribute Command to be locatable.
        /// </summary>
        public static Registry FindCommands(Assembly asm, string ns, Registry registry)
        {
            if(registry == null) {
                registry  = new Registry();
            }

            _log.DebugFormat("Searching for commands under namespace '{0}'...", ns);
            foreach(var t in asm.GetExportedTypes()) {
                if(t.IsClass && t.Namespace == ns) {
                    foreach(var mi in t.GetMethods()) {
                        foreach(var attr in mi.GetCustomAttributes(typeof(CommandAttribute), false)) {
                            Command cmd = new Command(ns, t, mi);
                            registry.Add(cmd);
                        }
                    }
                }
            }

            return registry;
        }


        /// Constructor
        public Registry()
        {
            _commands = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);
        }


        /// <summary>
        /// Registers the specified Command instance.
        /// </summary>
        public void Add(Command cmd)
        {
            _commands.Add(cmd.Name, cmd);
        }

        /// <summary>
        /// Checks to see if a command with the given name is available.
        /// </summary>
        public bool Contains(string key)
        {
            return _commands.ContainsKey(key);
        }


        /// <summary>
        /// Identifies the first Command that returns an object of the specified
        /// type.
        /// </summary>
        public Command FindCommandByReturnType(Type returnType)
        {
            return _commands.Values.First(cmd => cmd.ReturnType == returnType);
        }


        /// Returns the Type(s) that must be instantiated before the specified
        /// command can be invoked.
        public void FindCommandPrerequisiteObjects(string key)
        {
            var cmd = this[key];
            FindCommandPrerequisiteObjects(cmd.Type);
            // TODO: Follow chain until no further pre-requisite types exist
        }
    }


    /// <summary>
    /// Context exception, thrown when no object of the necessary type is
    /// available in the current context.
    /// </summary>
    public class ContextException : Exception
    {
        public ContextException(string msg) :
            base(msg)
        {
        }
    }



    /// <summary>
    /// Records the current context within which Commands are executed. A
    /// Context is like a session object; it holds the current context within
    /// which a Command will be executed, and the method to which a Command
    /// relates will be executed on the object instance of the Command's Type
    /// which is currently in the Context.
    /// </summary>
    public class Context
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Registry of known Commands
        protected Registry _registry;

        // A map holding object instances keyed by Type, representing the current
        // context. When a command is invoked, it is invoked on the object that
        // is in the map for the Type on which the Command was registered.
        protected Dictionary<Type, object> _context;


        // Constructor
        public Context(Registry registry)
        {
            _registry = registry;
            _context = new Dictionary<Type, object>();
        }


        /// <summary>
        /// Adds an object to the current context.
        /// </summary>
        public void Set(object val) {
            if(val != null) {
                _context[val.GetType()] = val;
            }
        }


        /// <summary>
        /// Invoke an instance of the named command, using the supplied arguments
        /// Dictionary to obtain parameter values.
        /// </summary>
        public object Invoke(string command, Dictionary<string, object> args)
        {
            Command cmd = _registry[command];
            if(!_context.ContainsKey(cmd.Type)) {
                throw new ContextException(String.Format("No object of type {0} is available in the current context", cmd.Type.Name));
            }
            object ctxt = _context[cmd.Type];

            _log.InfoFormat("Executing {0} command {1}...", cmd.Type.Name, cmd.Name);

            // Create an array of parameters in the order expected
            var parms = new object[cmd.Parameters.Count];
            var i = 0;
            foreach(var param in cmd.Parameters) {
                if(args.ContainsKey(param.Name)) {
                    _log.DebugFormat("Setting {0} to '{1}'", param.Name,
                            param.IsSensitive ? "******" : args[param.Name]);
                    parms[i++] = args[param.Name];
                }
                else if(param.HasDefaultValue) {
                    // Deal with missing arg values, default values, etc
                    _log.DebugFormat("No value supplied for {0}; using default value '{1}'",
                            param.Name, param.DefaultValue);
                    parms[i++] = param.DefaultValue;
                }
                else {
                    throw new ArgumentException(
                            String.Format("No value was specified for the required argument '{0}' to command '{1}'",
                            param.Name, cmd.Name));
                }
            }

            var result = cmd.MethodInfo.Invoke(ctxt, parms);

            // If the method returns an object, set it in the context
            if(result != null) {
                this.Set(result);
            }

            return result;
        }
    }

}
