using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

using log4net;


namespace CommandLine
{

    /// <summary>
    /// Interface for an argument validator. Defines a single method IsValid
    /// that implementations must implement.
    /// </summary>
    public interface ArgumentValidator
    {
        /// <summary>
        /// Primary method for argument validation; returns true if the argument
        /// is valid.
        /// </summary>
        /// <param name="value">The argument string value to be validated. Will
        /// never be null.</param>
        /// <param name="errorMsg">An optional out parameter for returning
        /// additional details on the reason for failure if an argument fails
        /// validation.</param>
        bool IsValid(string value, out string errorMsg);
    }



    /// <summary>
    /// Validator implementation using a regular expression.
    /// </summary>
    public class RegexValidator : ArgumentValidator
    {
        public Regex Expression { get; set; }

        public RegexValidator()
        {
        }

        public RegexValidator(string re)
        {
            Expression = new Regex(re);
        }

        public RegexValidator(Regex re)
        {
            Expression = re;
        }

        public bool IsValid(string value, out string errorMsg) {
            errorMsg = null;
            if(Expression == null) {
                return true;
            }
            else {
                errorMsg = String.Format("Value must satisfy the regular expression: /{0}/", Expression);
                return Expression.IsMatch(value);
            }
        }
    }


    /// <summary>
    /// ArgumentValidator implementation using a list of values.
    /// Supports validations of both single and multiple comma-separated argument
    /// values via the PermitMultipleValues property. Argument validation is not
    /// case-sensitive, unless the CaseSensitive property is set to true.
    /// </summary>
    public class ListValidator : ArgumentValidator
    {
        public List<string> Values { get; set; }
        public bool CaseSensitive { get; set; }
        public bool PermitMultipleValues { get; set; }

        public ListValidator()
        {
        }

        public ListValidator(List<string> values)
        {
            Values = values;
        }

        public ListValidator(params string[] values)
        {
            Values = new List<string>(values);
        }

        public bool IsValid(string value, out string errorMsg) {
            var ok = true;
            errorMsg = null;
            if(Values != null) {
                var values = PermitMultipleValues ? value.Split(',') : new string[] { value };
                foreach(var val in values) {
                    ok = ok && (CaseSensitive ? Values.Contains(val) :
                            Values.Contains(val, StringComparer.OrdinalIgnoreCase));
                }
            }
            if(!ok) {
                errorMsg = String.Format("Value must be {0} of: {1}{2}",
                                         PermitMultipleValues ? "any" : "one",
                                         String.Join(", ", Values.ToArray()),
                                         PermitMultipleValues ? " (Use a comma to separate multiple values)" : "");
            }
            return ok;
        }
    }



    /// <summary>
    /// ArgumentValidator implementation using a range.
    /// </summary>
    public class RangeValidator : ArgumentValidator
    {
        int? Min { get; set; }
        int? Max { get; set; }

        public RangeValidator()
        {
        }

        public RangeValidator(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public bool IsValid(string value, out string errorMsg) {
            int iVal = int.Parse(value);
            errorMsg = null;
            if(Min != null && Max != null) {
                errorMsg = String.Format("Value must be in the range {0} to {1} inclusive", Min, Max);
            }
            else if(Min != null) {
                errorMsg = String.Format("Value must be greater than or equal to {0}", Min);
            }
            else if(Max != null) {
                errorMsg = String.Format("Value must be less than or equal to {0}", Max);
            }
            return (Min == null || iVal >= Min) && (Max == null || iVal <= Max);
        }
    }

}
