using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

using log4net;


namespace Command
{

    /// <summary>
    /// Define an attribute which will be used to set descriptions for Commands
    /// and CommandParameters.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter,
     AllowMultiple = false)]
    class DescriptionAttribute : Attribute
    {
        public string Description;

        public DescriptionAttribute(string desc)
        {
            this.Description = desc;
        }
    }


    /// <summary>
    /// Define an attribute which will be used to tag methods that can be
    /// invoked from a script or command file.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    class CommandAttribute : Attribute
    {
    }


    /// </summary>
    /// Define an attribute that can be used to tag a method, property, or
    /// constructor as a source of new instances of the class returned by
    /// the member. This information will be used by Context objects to
    /// determine how to obtain objects of the required type when invoking a
    /// Command.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property,
     AllowMultiple = false)]
    class FactoryAttribute : Attribute
    {
    }


    /// <summary>
    /// Define an attribute which will be used to specify default values for
    /// optional parameters on a Command.
    /// A DefaultValue attribute is used instead of default values on the
    /// actual method, since a) default values are only available from v4 of
    /// .Net, and b) they have restrictions on where they can appear (e.g. only
    /// at the end of the list of parameters).
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    class DefaultValueAttribute : Attribute
    {
        public object Value;

        public DefaultValueAttribute(object val)
        {
            this.Value = val;
        }
    }


    /// <summary>
    /// Define an attribute which will be used to tag parameters that contain
    /// sensitive information such as passwords. These will be masked if logged.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    class SensitiveValueAttribute : Attribute
    {
    }



    /// <summary>
    /// Represents a method that can be invoked by some external means.
    /// </summary>
    public class Command
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Fields

        /// The MethodInfo object describing the underlying method.
        internal readonly MethodInfo MethodInfo;
        /// The class on which this Command is found.
        public readonly Type Type;
        /// List of CommandParamter definitions describing the parameters to
        /// this command.
        public readonly List<CommandParameter> Parameters = new List<CommandParameter>();
        /// Link to the associated Factory definition if this Command is also a
        /// Factory.
        protected internal Factory _factory;
        /// The Command description
        public string Description;

        // Properties

        /// The name of the Command
        public string Name { get { return MethodInfo.Name; } }
        /// The return type of this Command
        public Type ReturnType { get { return MethodInfo.ReturnType; } }
        /// True if this Command is also a Factory for objects
        public bool IsFactory { get { return _factory != null; } }
        /// Link to the associated Factory instance if this Command is a Factory
        public Factory Factory { get { return _factory; } }


        // Constructor
        public Command(Type t, MethodInfo mi)
        {
            this.MethodInfo = mi;
            this.Type = t;

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
    /// Records details of a Command parameter.
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
    /// Represents a constructor, method, or property that can be invoked to
    /// obtain an instance of a given type.
    /// </summary>
    public class Factory
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        protected readonly MemberInfo _memberInfo;
        public readonly Type ReturnType;
        protected internal Command _command;

        /// Public properties

        public bool            IsCommand { get { return _command != null; } }
        public Command         Command { get { return _command; } }
        public bool            IsConstructor { get { return _memberInfo is ConstructorInfo; } }
        public ConstructorInfo Constructor { get { return _memberInfo as ConstructorInfo; } }
        public bool            IsProperty { get { return _memberInfo is PropertyInfo; } }
        public PropertyInfo    Property { get { return _memberInfo as PropertyInfo; } }
        /// The Type of the class on which this Factory is declared
        public Type            DeclaringType { get { return _memberInfo.DeclaringType; } }


        /// Constructor
        public Factory(MemberInfo mi)
        {
            this._memberInfo = mi;
            if(mi is MethodInfo) {
                ReturnType = (mi as MethodInfo).ReturnType;
            }
            else if(mi is PropertyInfo) {
                ReturnType = (mi as PropertyInfo).PropertyType;
            }
            else if(mi is ConstructorInfo) {
                ReturnType = (mi as ConstructorInfo).DeclaringType;
            }
            _log.DebugFormat("Found factory for {0} objects", ReturnType);
        }


        public override string ToString()
        {
            if(_memberInfo is MethodInfo) {
                return string.Format("Factory method for {0}", ReturnType);
            }
            else if(_memberInfo is PropertyInfo) {
                return string.Format("Factory property for {0}", ReturnType);
            }
            else if(_memberInfo is ConstructorInfo) {
                return string.Format("Factory constructor for {0}", ReturnType);
            }
            else return base.ToString();
        }
    }



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
        private IDictionary<string, Command> _commands;

        // Dictionary of types to factory constructors/methods/properties
        private IDictionary<Type, Factory> _factories;

        /// Return the Command object corresponding to the requested name.
        public Command this[string cmdName] {
            get {
                return _commands[cmdName];
            }
        }


        /// Constructor
        public Registry()
        {
            _commands = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);
            _factories = new Dictionary<Type, Factory>();
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
            string desc;

            if(t.IsClass) {
                foreach(var mi in t.GetMembers(BindingFlags.Public|BindingFlags.Instance)) {
                    cmd = null;
                    factory = null;
                    desc = null;
                    foreach(var attr in mi.GetCustomAttributes(false)) {
                        if(attr is DescriptionAttribute) {
                            desc = (attr as DescriptionAttribute).Description;
                        }
                        if(attr is CommandAttribute) {
                            cmd = new Command(t, mi as MethodInfo);
                            Add(cmd);
                        }
                        if(attr is FactoryAttribute) {
                            factory = new Factory(mi);
                            Add(factory);
                        }
                    }
                    if(cmd != null) {
                        cmd.Description = desc;
                        if(factory != null) {
                            cmd._factory = factory;
                            factory._command = cmd;
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Registers the specified Command instance.
        /// </summary>
        public void Add(Command cmd)
        {
            _commands.Add(cmd.Name, cmd);
        }


        /// <summary>
        /// Registers the specified Factory instance.
        /// </summary>
        public void Add(Factory factory)
        {
            _factories.Add(factory.ReturnType, factory);
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
    /// Context is like a session object; it holds the current set of live
    /// object instances on which commands will be invoked. The Context is
    /// also able to use the Factory registration information to create new
    /// object instances as required, provided they can be created from objects
    /// currently in the context.
    /// </summary>
    public class Context
    {
        // Reference to class logger
        protected static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Registry of known Commands and Factories
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
        public void Set(object val)
        {
            if(val != null) {
                _context[val.GetType()] = val;
            }
        }


        /// <summary>
        /// Returns an object of the requested Type, if possible. This may
        // involve the construction of one or more intermediate objects.
        /// </summary>
        public object Get(Type t)
        {
            object result = null;
            if(HasObject(t)) {
                result = _context[t];
            }
            else {
                foreach(var step in FindPathToType(t)) {
                    _log.DebugFormat("Attempting to create an instance of {0}", step.ReturnType);
                    if(step.IsConstructor) {
                        result = step.Constructor.Invoke(new object[] {});
                    }
                    else if(step.IsProperty) {
                        result = step.Property.GetValue(_context[step.DeclaringType], new object[] {});
                    }
                    else if(step.IsCommand) {
                        throw new ContextException(string.Format("The command {0} must be run first", step.Command.Name));
                    }
                    else {
                        throw new Exception("Unrecognised factory type");
                    }
                    Set(result);
                }
            }
            return result;
        }


        /// <summary>
        /// Returns true if the Context currently contains an instance of the
        /// requested type.
        /// </summary>
        public bool HasObject(Type type)
        {
            return _context.ContainsKey(type);
        }


        /// <summary>
        /// Determines the sequence of command invocations, property accesses,
        /// and constructor calls that are required to instantiate an object of
        /// the requested type, given the current state of the context.
        /// </summary>
        /// <returns>A Stack containing the methods/properties/ctors to be invoked
        /// to create an instance of the desire type. The stack may be empty if an
        /// object of the desired type is already available.
        /// </returns>
        /// <exception>Throws ContextException if an object of the desired type
        /// cannot be created from the current context.
        /// </exception>
        public Stack<Factory> FindPathToType(Type t)
        {
            var steps = new Stack<Factory>();
            _log.DebugFormat("Determining steps needed to create an instance of {0}", t);

            // Create a lambda for recursion
            Action<Type> scan = null;
            scan = type => {
                if(!HasObject(type)) {
                    // See if we can get an instance from what we do have
                    if(_registry.Contains(type)) {
                        Factory factory = _registry.GetFactory(type);
                        _log.DebugFormat("Found {0} on {1}", factory, factory.DeclaringType);
                        steps.Push(factory);
                        if(!factory.IsConstructor) {
                            scan(factory.DeclaringType);
                        }
                    }
                    else {
                        throw new ContextException(string.Format("No Factory method, property, or constructor is registered for {0}", type));
                    }
                }
            };
            scan(t);
            return steps;
        }


        /// <summary>
        /// Invoke an instance of the named command, using the supplied arguments
        /// dictionary to obtain parameter values. If necessary, this will execute
        /// Factory constructors, properties, and commands to obtain the necessary
        /// source object on which to invoke the command.
        /// </summary>
        public object Invoke(string command, Dictionary<string, object> args)
        {
            Command cmd = _registry[command];

            foreach(var step in FindPathToType(cmd.Type)) {
                _log.DebugFormat("Attempting to create an instance of {0}", step.ReturnType);
                object ctxt;
                if(step.IsConstructor) {
                    ctxt = step.Constructor.Invoke(new object[] {});
                }
                else if(step.IsProperty) {
                    ctxt = step.Property.GetValue(_context[step.DeclaringType], new object[] {});
                }
                else if(step.IsCommand) {
                    ctxt = InvokeCommand(step.Command, args);
                }
                else {
                    throw new Exception("Unrecognised factory type");
                }
                Set(ctxt);
            }
            return InvokeCommand(cmd, args);
        }


        /// <summary>
        /// Invoke an instance of the supplied Command object, using the supplied
        /// arguments dictionary to obtain parameter values. An instance of the
        /// host object must already be available in the context.
        /// </summary>
        public object InvokeCommand(Command cmd, Dictionary<string, object> args)
        {
            if(!HasObject(cmd.Type)) {
                throw new ContextException(String.Format("No object of type {0} is available in the current context", cmd.Type.Name));
            }

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

            var ctxt = _context[cmd.Type];
            var result = cmd.MethodInfo.Invoke(ctxt, parms);

            // If the method is a factory method, set the returned object in the context
            if(result != null && cmd.IsFactory) {
                this.Set(result);
            }

            return result;
        }
    }

}
