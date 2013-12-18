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
        public ContextException(string msg)
            : base(msg)
        { }
    }


    public class CommandException : Exception
    {
        public CommandException(string format, string arg, Exception ex)
            : base(string.Format(format, arg), ex)
        { }
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
                if(!args.ContainsValueForSetting(param)) {
                    return false;
                }
            }
            return true;
        }


        /// <summary>
        /// Checks if the supplied Dictionary contains a value for the specified
        /// ISetting.
        /// </summary>
        public static bool ContainsValueForSetting(this Dictionary<string, object> args, ISetting setting)
        {
            if(!(args.ContainsKey(setting.Name) ||
                 (setting.HasAlias && args.ContainsKey(setting.Alias)) ||
                 setting.HasDefaultValue)) {
                return false;
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
        protected List<object> _context = new List<object>();
        /// A list of single-use objects that should be purged when the current
        /// command completes.
        protected List<object> _purgeList = new List<object>();

        /// A callback that will be invoked when a required argument is missing
        public Func<CommandParameter, object> MissingArgHandler;

        /// Accessor for obtaining an object of the specified type from the context
        public object this[Type type] {
            get {
                var result = _context.FirstOrDefault(o => type.IsInstanceOfType(o));
                _log.DebugFormat("Lookup of context object of type {0} {1} successful", type,
                        result != null ? "was" : "was not");
                return result;
            }
        }


        // Constructor
        public Context(Registry registry) : this(registry, null)
        { }


        // Constructor
        public Context(Registry registry, Func<CommandParameter, object> missingArgHandler)
        {
            _registry = registry;
            MissingArgHandler = missingArgHandler;
        }


        /// <summary>
        /// Verifies that all commands can be invoked successfully.
        /// </summary>
        public void Verify()
        {
            foreach(var cmd in _registry.Commands()) {
                _log.TraceFormat("Validating command {0}", cmd.Name);
                foreach(var parm in cmd.Parameters) {
                    _log.TraceFormat("Validating command parameter {0}", parm.Name);
                    if(!parm.HasParameterAttribute) {
                        // Check that a factory exists for the param type
                        if(!HasObject(parm.ParameterType) && !parm.IsSettingsCollection &&
                           !_registry.Contains(parm.ParameterType)) {
                            _log.ErrorFormat("No factory is registered for type {0} (used in the {1} command parameter {2})",
                                    parm.ParameterType, cmd.Name, parm.Name);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Sets an object in the context. Only a single object of a given type
        /// is permitted, so any existing object of this type is replaced.
        /// </summary>
        public void Set(object value)
        {
            var valType = value.GetType();
            for(var i = 0; i < _context.Count; ++i) {
                var type = _context[i].GetType();
                if(type.IsAssignableFrom(valType)) {
                    if(_context[i] != value) {
                        _log.TraceFormat("Replacing object of type {0} in context", type);
                        _context[i] = value;
                    }
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
            _log.DebugFormat("Context {1} an object of type {0}", type,
                    result ? "contains" : "does not contain");
            return result;
        }


        /// <summary>
        /// Removes the first object that is an instance of type.
        /// </summary>
        public object Remove(Type type)
        {
            object result = null;
            for(int i = 0; i < _context.Count; ++i) {
                if(type.IsInstanceOfType(_context[i])) {
                    result = _context[i];
                    _context.Remove(i);
                    break;
                }
            }
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
            var result = InvokeCommand(cmd, args);
            PurgeSingleUseObjects();
            return result;
        }


        /// Instantiate an instance of a Factory return type from the current
        /// context. If the Factory is a Command, attempts to invoke the command
        /// using the supplied arguments, provided the arguments contain the
        /// necessary parameter values the command needs. If not, we then look
        /// to see if any alternate Factories have been registered, looking to
        /// see which of these might succeed from the current context.
        private object Instantiate(Factory step, Dictionary<string, object> args)
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
                            step = factory;
                            ctxt = Instantiate(factory, args);
                            break;
                        }
                    }
                }
                if(ctxt == null && MissingArgHandler != null) {
                    // Call missing arg handler for each missing arg
                    foreach(var param in step.Command.Parameters) {
                        if(!args.ContainsValueForSetting(param)) {
                            args[param.Name] = MissingArgHandler(param);
                        }
                    }
                }
                if(ctxt == null && args.ContainsRequiredValuesForCommand(step.Command)) {
                    ctxt = InvokeCommand(step.Command, args);
                }
            }
            else {
                throw new Exception(string.Format("Unrecognised factory type: {0}", step));
            }

            if(ctxt != null) {
                Set(ctxt);
                if(step.IsSingleUse) {
                    _purgeList.Add(ctxt);
                }
            }
            else {
                throw new ContextException(string.Format("No object of type {0} can be constructed " +
                            "from the current context, using the supplied arguments", step.ReturnType));
            }
            return ctxt;
        }


        /// Invoke a Factory constructor, supplying any arguments needed by the
        /// constructor from the current context.
        private object InvokeConstructor(ConstructorInfo ctor)
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
        private object InvokeCommand(Command cmd, Dictionary<string, object> args)
        {
            if(!HasObject(cmd.Type)) {
                throw new ContextException(String.Format("No object of type {0} " +
                            "is available in the current context", cmd.Type));
            }

            string paramLog;
            object[] parms = null;
            try {
                parms = PrepareCommandArguments(cmd, args, out paramLog);
            }
            catch(Exception ex) {
                throw new CommandException("An error occurred while preparing arguments for command {0}", cmd.Name, ex);
            }

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
                throw new CommandException("Command {0} threw an exception:", cmd.Name,
                        ex.InnerException != null ? ex.InnerException : ex);
            }

            // If the method is a factory method, set the returned object in the context
            if(result != null && cmd.IsFactory) {
                Set(result);
            }

            return result;
        }


        /// Prepares the parameters to be passed to a command.
        private object[] PrepareCommandArguments(Command cmd, Dictionary<string, object> args, out string paramLog)
        {
            // Create an array of parameters in the order expected
            var parms = new object[cmd.Parameters.Count];
            var sb = new StringBuilder();
            var i = 0;

            foreach(var param in cmd.Parameters) {
                _log.TraceFormat("Processing parameter {0}", param.Name);
                if(args.ContainsKey(param.Name)) {
                    parms[i] = ConvertSetting(args[param.Name], param);
                    LogSettingValue(sb, param, parms[i]);
                }
                else if(param.HasAlias && args.ContainsKey(param.Alias)) {
                    parms[i] = ConvertSetting(args[param.Alias], param);
                    LogSettingValue(sb, param, parms[i]);
                }
                else if(param.IsSettingsCollection) {
                    parms[i] = PrepareSettingsCollectionArg(cmd, param, args, sb);
                }
                else if(param.HasDefaultValue) {
                    // Deal with missing arg values, default values, etc
                    _log.DebugFormat("No value supplied for {0}; using default value '{1}'",
                            param.Name, param.DefaultValue);
                    parms[i] = param.DefaultValue;
                    LogSettingValue(sb, param, parms[i]);
                }
                else if(HasObject(param.ParameterType)) {
                    parms[i] = this[param.ParameterType];
                    if(param.HasParameterAttribute) {
                        LogSettingValue(sb, param, parms[i]);
                    }
                }
                // If there is a factory to create this type, then try to create it
                else if(_registry.Contains(param.ParameterType)) {
                    foreach(var step in FindPathToType(param.ParameterType, cmd)) {
                        Instantiate(step, args);
                    }
                    parms[i] = this[param.ParameterType];
                    if(param.HasParameterAttribute) {
                        LogSettingValue(sb, param, parms[i]);
                    }
                }
                else if(MissingArgHandler != null) {
                    object val = MissingArgHandler(param);
                    if(val != null) {
                        parms[i] = ConvertSetting(val, param);
                        LogSettingValue(sb, param, parms[i]);
                    }
                    else {
                        throw new ArgumentException(
                                String.Format("No value was specified for the required argument '{0}' to command '{1}'",
                                param.Name, cmd.Name));
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


        /// Logs the value of a setting
        private void LogSettingValue(StringBuilder sb, ISetting setting, object val)
        {
            if(setting.IsSensitive) {
                val = "******";
            }
            LogSettingNameValue(sb, setting.Name, val);
        }


        private void LogSettingNameValue(StringBuilder sb, string name, object val)
        {
            if(val != null) {
                sb.AppendLine();
                sb.Append("          ");
                sb.Append(char.ToUpper(name[0]));
                sb.AppendFormat("{0,-24}", name.Substring(1));
                sb.Append(": ");
                if(val.GetType().IsArray) {
                    sb.Append(string.Join(", ", ((object[])val).Select(o =>
                                    o.ToString().IndexOf(",") >= 0 ?
                                        string.Format("\"{0}\"", o.ToString().Replace("\"", "\"\"")) :
                                        o.ToString()
                              ).ToArray()));
                }
                else {
                    sb.Append(val);
                }
            }
        }


        /// Ensures a value to be passed as a setting is of the right ParameterType
        private object ConvertSetting(object val, ISetting setting)
        {
            var type = setting.ParameterType;
            _log.Debug(type);
            if (type.IsAssignableFrom(val.GetType()))
            {
                return val;
            }
            else if(val is string) {
                return _registry.TypeConverter.ConvertTo(val as string, type);
            }
            else if(val is IEnumerable<object>)
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    var elType = type.GetGenericArguments()[0];
                    if (_registry.TypeConverter.CanConvert(elType))
                    {
                        _log.DebugFormat("Converting {0} to {1}[]", val.GetType(), elType);
                        var vals = ((IEnumerable<object>)val).ToArray();
                        var ary = Array.CreateInstance(elType, vals.Length);
                        for (int i = 0; i < vals.Length; ++i)
                        {
                            ary.SetValue(_registry.TypeConverter.ConvertTo(vals[i].ToString(), elType), i);
                        }
                        return ary;
                    }
                }
                else if (IsInstanceOfGenericType(typeof(List<>), val)) //Hack to handle lists since apparently they 
                {                                                      //dont identify as generic and I didnt
                    var vals = ((IEnumerable<object>)val).ToArray();   //really understand the process above.
                    var ary = Array.CreateInstance(typeof(String), vals.Length);
                    for (int i = 0; i < vals.Length; ++i)
                    {
                        ary.SetValue(_registry.TypeConverter.ConvertTo(vals[i].ToString(), typeof(String)), i);
                    }
                    return ary;
                } 
            _log.DebugFormat("IsEnumerable: {0}\r\nIsGeneric: {1}\r\nIsAltGeneric: {2}",
                val is IEnumerable<object>,
                type.IsGenericType,
                IsInstanceOfGenericType(typeof(List<>), val));
            if (type.IsGenericType)
                _log.DebugFormat("TypeOf IEnumerable: {0}", type.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            throw new ArgumentException(string.Format("Unable to convert {0} to {1} for setting {2}",
                        val.GetType().Name, setting.ParameterType.Name, setting.Name));
        }
        /// <summary>
        /// Code to detect deeper generic types
        /// </summary>
        /// <param name="genericType"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        /// <seealso cref="http://stackoverflow.com/questions/982487/testing-if-object-is-of-generic-type-in-c-sharp"/>
        static bool IsInstanceOfGenericType(Type genericType, object instance)
        {
            Type type = instance.GetType();
            while (type != null)
            {
                _log.Debug(type.ToString());
                if (type.IsGenericType &&
                    type.GetGenericTypeDefinition() == genericType)
                {
                    return true;
                }
                type = type.BaseType;
            }
            return false;
        }

        private ISettingsCollection PrepareSettingsCollectionArg(Command cmd, CommandParameter param,
                Dictionary<string, object> args, StringBuilder sb)
        {
            // Attempt to create an instance of the collection class if necessary
            if(!HasObject(param.ParameterType)) {
                foreach(var step in FindPathToType(param.ParameterType, cmd)) {
                    Instantiate(step, args);
                }
            }
            // Set each setting that has a value in the supplied args
            var coll = this[param.ParameterType] as ISettingsCollection;
            foreach(var setting in GetSettings(param.ParameterType)) {
                if(setting is DynamicSettingAttribute) {
                    _log.Trace("Retrieving dynamic setting argument names");
                    foreach(var dynset in ((IDynamicSettingsCollection)coll).DynamicSettingNames) {
                        if(args.ContainsKey(dynset)) {
                            _log.DebugFormat("Processing dynamic setting {0}", dynset);
                            coll[dynset] = ConvertSetting(args[dynset], setting);
                            LogSettingNameValue(sb, dynset, coll[dynset]);
                        }
                    }
                }
                else if(args.ContainsKey(setting.Name)) {
                    coll[setting.InternalName] = ConvertSetting(args[setting.Name], setting);
                    LogSettingValue(sb, setting, coll[setting.InternalName]);
                }
                else if(setting.HasAlias && args.ContainsKey(setting.Alias)) {
                    coll[setting.InternalName] = ConvertSetting(args[setting.Alias], setting);
                    LogSettingValue(sb, setting, coll[setting.InternalName]);
                }
            }
            return coll;
        }


        /// Once a command has been invoked, any single-use objects used to
        /// enable that command must be removed from the context.
        private void PurgeSingleUseObjects()
        {
            foreach(var purgeObj in _purgeList) {
                _log.DebugFormat("Removing {0} single-use object from context", purgeObj.GetType().Name);
                _context.Remove(purgeObj);
            }
            _purgeList.Clear();
        }

    }

}
