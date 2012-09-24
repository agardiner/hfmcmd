using System.IO;
using System.Text.RegularExpressions;


namespace HFMCmd
{

    public static class Utilities
    {

        /// <summary>
        /// Converts a pattern containing ? and * characters into a case-
        /// insensitive regular expression.
        /// </summary>
        public static Regex ConvertWildcardPatternToRE(string pattern)
        {
            return new Regex("^" + pattern.Replace(".", @"\.")
                    .Replace("*", ".*").Replace("?", ".") + "$",
                    RegexOptions.IgnoreCase);
        }


        /// <summary>
        /// Checks if the specified path exists and can be read.
        /// </summary>
        public static bool FileExists(string path)
        {
            using (FileStream s = File.Open(path, FileMode.Open,
                                            FileAccess.Read))
            {
                return true;
            }
        }


        /// <summary>
        /// Checks if the specified path can be written to.
        /// </summary>
        public static void FileWriteable(string path)
        {
            using (FileStream s = File.Open(path, FileMode.OpenOrCreate,
                                            FileAccess.Write, FileShare.None))
            { }
        }

    }

}

