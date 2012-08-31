using System;
using System.Reflection;
using System.Collections.Generic;

using log4net;


namespace Command
{

    /// <summary>
    /// Define an attribute which will be used to tag methods that can be
    /// invoked from a script or command file.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    class CommandAttribute : Attribute
    {
    }
}
