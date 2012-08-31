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
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    class DefaultValueAttribute : Attribute
    {
        public object Value;

        public DefaultValueAttribute(object val)
        {
            this.Value = val;
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

        public Command(Type t, MethodInfo mi)
        {
            _type = t;
            _mi = mi;

            this.Name = mi.Name;

            _log.DebugFormat("Found command {0}", this.Name);

            foreach(var pi in mi.GetParameters()) {
                _log.DebugFormat("Found parameter {0}", pi.Name);
            }
        }
    }


    public class Registry
    {
        // Dictionary of command instances keyed by command
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
