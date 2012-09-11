using System;

using Command;



namespace HFMCmd
{

    class ParameterMapper// : IParameterMapper
    {
        public object MapParameter(string value, Type targetType)
        {
            if(targetType == typeof(string[])) {
                return value.Split(',');
            }
            else {
                return value;
            }
        }
    }

}
