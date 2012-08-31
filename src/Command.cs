using System;
using System.Reflection;
using System.Collections.Generic;

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


    /// Records details of a parameter to a Command.
    public class CommandParameter
    {
        public string Name;
        public Type ParameterType;
        public object DefaultValue;

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


    /// Represents a method that can be invoked by some external means.
    public class Command
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        protected Type _type;
        protected MethodInfo _mi;

        public string Name;
        public List<CommandParameter> Parameters = new List<CommandParameter>();

        public Command(Type t, MethodInfo mi)
        {
            _type = t;
            _mi = mi;
            this.Name = mi.Name;

            _log.DebugFormat("Found command {0}", this.Name);

            foreach(var pi in mi.GetParameters()) {
                var param = new CommandParameter(pi);
                _log.DebugFormat("Found parameter {0}", param);
                foreach(var attr in pi.GetCustomAttributes(typeof(DefaultValueAttribute), false)) {
                    param.DefaultValue = (attr as DefaultValueAttribute).Value;
                }
                this.Parameters.Add(param);
            }
        }
    }


    /// Provides a registry of discovered Commands, as well as methods for
    /// discovering them.
    public class Registry
    {
        // Dictionary of command instances keyed by command name
        private IDictionary<string, Command> _commands = new Dictionary<string, Command>();


        /// Registers commands (i.e. methods tagged with the Command attribute)
        /// in the current assembly.
        public static Registry FindCommands(string ns)
        {
            return FindCommands(Assembly.GetExecutingAssembly(), ns, null);
        }


        /// Registers commands from the supplied assembly. Commands methods must
        /// be tagged with the attribute Command to be locatable.
        public static Registry FindCommands(Assembly asm, string ns, Registry registry)
        {
            if(registry == null) {
                registry  = new Registry();
            }

            foreach(var t in asm.GetExportedTypes()) {
                if(t.IsClass && t.Namespace == ns) {
                    foreach(var mi in t.GetMethods()) {
                        foreach(var attr in mi.GetCustomAttributes(typeof(CommandAttribute), false)) {
                            Command cmd = new Command(t, mi);
                            registry.Add(cmd);
                        }
                    }
                }
            }

            return registry;
        }


        public Registry()
        {
        }


        public void Add(Command cmd)
        {
            _commands.Add(cmd.Name, cmd);
        }
    }
}
