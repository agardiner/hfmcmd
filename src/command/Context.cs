using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using log4net;


namespace Command
{

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
    /// Extension methods for working with the argument values supplied in a
    /// Dictionary.
    /// </summary>
    public static class DictionaryExtensions
    {

        /// <summary>
        /// Checks if the supplied Dictionary contains all the required arguments
        /// to invoke the specified command.
        /// </summary>
        public static bool ContainsRequiredValuesForCommand(this Dictionary<string, object> args, Command cmd)
        {
            foreach(var param in cmd.Parameters) {
                if(!(args.ContainsKey(param.Name) ||
                     (param.HasAlias && args.ContainsKey(param.Alias)) ||
                     param.HasDefaultValue)) {
                    return false;
                }
            }
            return true;
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

        // A list of object instances representing the current context.
        // When a command is invoked, it is invoked on an object that is in this
        // list.
        protected List<object> _context;


        public object this[Type type] {
            get {
                var result = _context.FirstOrDefault(o => type.IsInstanceOfType(o));
                _log.DebugFormat("Lookup of context object of type {0} {1} successful", type,
                        result != null ? "was" : "was not");
                return result;
            }
        }


        // Constructor
        public Context(Registry registry)
        {
            _registry = registry;
            _context = new List<object>();
        }


        public void Set(object value)
        {
            var valType = value.GetType();
            for(var i = 0; i < _context.Count; ++i) {
                var type = _context[i].GetType();
                if(type.IsAssignableFrom(valType)) {
                    _log.TraceFormat("Replacing object of type {0} in context", type);
                    _context[i] = value;
                    value = null;
                    break;
                }
            }
            if(value != null) {
                _log.TraceFormat("Adding object of type {0} to context", valType);
                _context.Add(value);
            }
        }


        /// <summary>
        /// Returns true if the Context currently contains an instance of the
        /// requested type.
        /// </summary>
        public bool HasObject(Type type)
        {
            var result = _context.Any(o => type.IsInstanceOfType(o));
            _log.DebugFormat("Context {1} contain an object of type {0}", type,
                    result ? "does" : "does not");
            return result;
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
        public Stack<Factory> FindPathToType(Type t, Command cmd)
        {
            var steps = new Stack<Factory>();
            _log.TraceFormat("Determining steps needed to create an instance of {0}", t);

            // Create a lambda for recursion
            Action<Type> scan = null;
            scan = type => {
                if(!HasObject(type)) {
                    // See if we can get an instance from what we do have
                    if(_registry.Contains(type)) {
                        Factory factory = _registry.GetFactory(type);
                        _log.DebugFormat("Found {0} on {1}", factory, factory.DeclaringType);
                        steps.Push(factory);
                        if(factory.IsConstructor) {
                            // Determine steps needed to obtain instances of
                            // constructor arguments (if any)
                            foreach(var param in factory.Constructor.GetParameters()) {
                                scan(param.ParameterType);
                            }
                        }
                        else {
                            // Determine how to obtain an instance of the item
                            // holding the factory method/property
                            scan(factory.DeclaringType);
                        }
                    }
                    else {
                        throw new ContextException(string.Format("No method, property, or constructor " +
                                    "is registered as a Factory for {0} objects, which are required by " +
                                    "{1}", type, cmd.Name));
                    }
                }
            };
            scan(t);
            return steps;
        }


        /// <summary>
        /// Returns the SettingAttributes for the specified type.
        /// </summary>
        public List<SettingAttribute> GetSettings(Type type)
        {
            return _registry.GetSettings(type);
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

            foreach(var step in FindPathToType(cmd.Type, cmd)) {
                Instantiate(step, args);
            }
            return InvokeCommand(cmd, args);
        }


        /// Instantiate an instance of a Factory return type from the current
        /// context. If the Factory is a Command, attempts to invoke the command
        /// using the supplied arguments, provided the arguments contain the
        /// necessary parameter values the command needs. If not, we then look
        /// to see if any alternate Factories have been registered, looking to
        /// see which of these might succeed from the current context.
        protected object Instantiate(Factory step, Dictionary<string, object> args)
        {
            object ctxt = null;

            _log.TraceFormat("Attempting to create an instance of {0} via {1}", step.ReturnType, step);
            if(step.IsConstructor) {
                ctxt = InvokeConstructor(step.Constructor);
            }
            else if(step.IsProperty) {
                ctxt = step.Property.GetValue(this[step.DeclaringType], new object[] {});
            }
            else if(step.IsCommand) {
                if(args.ContainsRequiredValuesForCommand(step.Command)) {
                    ctxt = InvokeCommand(step.Command, args);
                }
                else {
                    // Check for alternate factories
                    foreach(var factory in _registry.GetAlternates(step.ReturnType)) {
                        if(!factory.IsCommand || args.ContainsRequiredValuesForCommand(factory.Command)) {
                            ctxt = Instantiate(factory, args);
                            break;
                        }
                    }
                }
            }
            else {
                throw new Exception(string.Format("Unrecognised factory type: {0}", step));
            }

            if(ctxt != null) {
                Set(ctxt);
            }
            else {
                throw new ContextException(string.Format("No object of type {0} can be constructed " +
                            "from the current context, using the supplied arguments", step.ReturnType));
            }
            return ctxt;
        }


        /// Invoke a Factory constructor, supplying any arguments needed by the
        /// constructor from the current context.
        protected object InvokeConstructor(ConstructorInfo ctor)
        {
            ParameterInfo[] pi = ctor.GetParameters();
            object[] parms = new object[pi.Length];
            var i = 0;
            foreach(var param in pi) {
                if(HasObject(param.ParameterType)) {
                    parms[i++] = this[param.ParameterType];
                }
                else {
                    throw new ContextException(string.Format("No object of type {0} " +
                                "can be obtained from the current context", param.ParameterType));
                }
            }
            return ctor.Invoke(parms);
        }


        /// Invoke an instance of the supplied Command object, using the supplied
        /// arguments dictionary to obtain parameter values. An instance of the
        /// host object must already be available in the context.
        protected object InvokeCommand(Command cmd, Dictionary<string, object> args)
        {
            if(!HasObject(cmd.Type)) {
                throw new ContextException(String.Format("No object of type {0} " +
                            "is available in the current context", cmd.Type));
            }

            string paramLog;
            object[] parms = PrepareCommandArguments(cmd, args, out paramLog);
            if(paramLog.Length > 0) {
                _log.InfoFormat("Executing {0} command {1}:{2}", cmd.Type.Name, cmd.Name, paramLog);
            }
            else {
                _log.InfoFormat("Executing {0} command {1}...", cmd.Type.Name, cmd.Name);
            }

            // Execute the method corresponding to this command
            var ctxt = this[cmd.Type];
            object result;
            try {
                result = cmd.MethodInfo.Invoke(ctxt, parms);
            }
            catch(TargetInvocationException ex) {
                if(ex.InnerException == null || _log.IsDebugEnabled) {
                    _log.Error(string.Format("Command {0} thre an exception:", cmd.Name),
                            ex);
                }
                else {
                    _log.Error(string.Format("Command {0} threw an exception:", cmd.Name),
                            ex.InnerException);
                }
                throw;
            }

            // If the method is a factory method, set the returned object in the context
            if(result != null && cmd.IsFactory) {
                Set(result);
            }

            return result;
        }


        /// Prepares the parameters to be passed to a command.
        protected object[] PrepareCommandArguments(Command cmd, Dictionary<string, object> args, out string paramLog)
        {
            // Create an array of parameters in the order expected
            var parms = new object[cmd.Parameters.Count];
            var sb = new StringBuilder();
            Action<ISetting, object> logParam = (p, v) => {
                if(v != null) {
                    sb.AppendFormat("\n          {0}{1,-18}: {2}",
                          char.ToUpper(p.Name[0]),
                          p.Name.Substring(1),
                          p.IsSensitive ? "******" : v);
                }
            };
            var i = 0;

            foreach(var param in cmd.Parameters) {
                _log.TraceFormat("Processing parameter {0}", param.Name);
                if(args.ContainsKey(param.Name)) {
                    parms[i] = ConvertSetting(args[param.Name], param);
                    logParam(param, parms[i]);
                }
                else if(param.HasAlias && args.ContainsKey(param.Alias)) {
                    parms[i] = ConvertSetting(args[param.Alias], param);
                    logParam(param, parms[i]);
                }
                else if(param.IsSettingsCollection) {
                    // Attempt to create an instance of the collection class if necessary
                    if(!HasObject(param.ParameterType)) {
                        foreach(var step in FindPathToType(param.ParameterType, cmd)) {
                            Instantiate(step, args);
                        }
                    }
                    // Set each setting that has a value in the supplied args
                    var coll = this[param.ParameterType] as ISettingsCollection;
                    foreach(var setting in GetSettings(param.ParameterType)) {
                        if(args.ContainsKey(setting.Name)) {
                            coll[setting.InternalName] = ConvertSetting(args[setting.Name], setting);
                            logParam(setting, coll[setting.InternalName]);
                        }
                        else if(setting.HasAlias && args.ContainsKey(setting.Alias)) {
                            coll[setting.InternalName] = ConvertSetting(args[setting.Alias], setting);
                            logParam(setting, coll[setting.InternalName]);
                        }
                    }
                    parms[i] = coll;
                }
                else if(param.HasDefaultValue) {
                    // Deal with missing arg values, default values, etc
                    _log.DebugFormat("No value supplied for {0}; using default value '{1}'",
                            param.Name, param.DefaultValue);
                    parms[i] = param.DefaultValue;
                    logParam(param, parms[i]);
                }
                else if(HasObject(param.ParameterType)) {
                    parms[i] = this[param.ParameterType];
                    if(param.HasParameterAttribute) {
                        logParam(param, parms[i]);
                    }
                }
                // If there is a factory to create this type, then try to create it
                else if(_registry.Contains(param.ParameterType)) {
                    foreach(var step in FindPathToType(param.ParameterType, cmd)) {
                        Instantiate(step, args);
                    }
                    parms[i] = this[param.ParameterType];
                    if(param.HasParameterAttribute) {
                        logParam(param, parms[i]);
                    }
                }
                else {
                    throw new ArgumentException(
                            String.Format("No value was specified for the required argument '{0}' to command '{1}'",
                            param.Name, cmd.Name));
                }
                i++;
            }
            paramLog = sb.ToString();
            return parms;
        }


        /// Ensures a value to be passed as a setting is of the right ParameterType
        protected object ConvertSetting(object val, ISetting setting)
        {
            if(setting.ParameterType.IsAssignableFrom(val.GetType())) {
                return val;
            }
            else if(val is string) {
                return _registry.TypeConverter.ConvertTo(val as string, setting.ParameterType);
            }
            else {
                throw new ArgumentException(string.Format("Unable to convert {0} to {1} for {2}",
                            val.GetType().Name, setting.ParameterType.Name, setting.Name));
            }
        }

    }

}
