using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

using log4net;


namespace Utilities
{

    /// <summary>
    /// Extension methods for strings.
    /// </summary>
    public static class StringUtilities
    {

        /// <summary>
        /// Capitalizes the first letter of a string
        /// </summary>
        public static string Capitalize(this string s)
        {
            if(char.IsUpper(s[0])) {
                return s;
            }
            else {
                return char.ToUpper(s[0]) + s.Substring(1);
            }
        }


        /// <summary>
        /// More generic version of Join, accepting any objects (and calling
        /// ToString on them to get the string values) to join.
        /// </summary>
        public static string Join(IEnumerable items, string separator)
        {
            var sb = new StringBuilder();
            var addSep = false;
            foreach(object item in items) {
                if(addSep) { sb.Append(separator); }
                sb.Append(item);
                addSep = true;
            }
            return sb.ToString();
        }


        /// <summary>
        /// Splits a line of text into comma-separated fields. Handles quoted
        /// fields containing commas.
        /// </summary>
        public static string[] SplitCSV(this string line)
        {
            return line.SplitQuoted(",");
        }


        /// <summary>
        /// Splits a line of text into space-separated fields. Handles quoted
        /// fields containing spaces.
        /// </summary>
        public static string[] SplitSpaces(this string line)
        {
            return line.SplitQuoted(@"\s+");
        }


        /// <summary>
        /// Splits a line of text into fields on the specified separator.
        /// Handles quoted fields that contain the separator.
        /// </summary>
        public static string[] SplitQuoted(this string line, string separator)
        {
            var re = string.Format("{0}(?=(?:[^\"]*\"[^\"]*[\"^{0}]*\")*(?![^\"]*\"))",
                                   separator);
            var fields = Regex.Split(line, re);
            return fields.Select(f => f.Trim().Trim(new char[] { '"' })
                                       .Replace("\"\"", "\"")).ToArray();
        }


        /// <summary>
        /// HFM version numbers don't follow the standard version numbering scheme,
        /// with breaking changes regularly introduced in patch-level updates! To
        /// handle this, we convert 5-part version specs to Version objects where
        /// the 4th and 5th numbers are merged into a single Revision number. The
        /// individual values can be returned using MajorRevision and MinorRevision
        /// properties.
        /// </summary>
        public static Version ToVersion(this string version)
        {
            Version ver = null;
            var re = new Regex(@"^(\d+\.\d+\.\d+).(\d+).?(\d+)?$");
            var match = re.Match(version);
            if(match.Success) {
                var major = Convert.ToInt32(match.Groups[2].Value);
                var minor = match.Groups[3].Value != "" ?
                    Convert.ToInt32(match.Groups[3].Value) : 0;
                ver = new Version(match.Groups[1].Value + "." + ((major << 16) | minor));
            }
            else {
                ver = new Version(version);
            }
            return ver;
        }

    }



    /// <summary>
    /// Utility class providing a collection of static utility methods.
    /// </summary>
    public static class FileUtilities
    {
        // Reference to class logger
        private static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        /// <summary>
        /// Converts a pattern containing ? and * characters into a case-
        /// insensitive regular expression.
        /// </summary>
        public static Regex ConvertWildcardPatternToRE(string pattern)
        {
            return new Regex("^" + pattern.Replace(@"\", @"\\").Replace(".", @"\.")
                    .Replace("*", ".*").Replace("?", ".") + "$",
                    RegexOptions.IgnoreCase);
        }


        /// <summary>
        /// Given a starting directory and a filename pattern, returns a list
        /// paths of files that match the name pattern.
        /// </summary>
        public static List<string> FindMatchingFiles(string sourceDir, string namePattern, bool includeSubDirs)
        {
            _log.TraceFormat("Searching for files like '{0}' {1} {2}", namePattern,
                    includeSubDirs ? "below" : "in", sourceDir);
            var nameRE = ConvertWildcardPatternToRE(namePattern);
            return FindMatchingFiles(sourceDir, nameRE, includeSubDirs, 0);
        }


        private static List<string> FindMatchingFiles(string sourceDir, Regex nameRE, bool includeSubDirs, int depth)
        {
            var files = new List<string>();

            foreach(var filePath in Directory.GetFiles(sourceDir)) {
                var file = Path.GetFileName(filePath);
                if(nameRE.IsMatch(file)) {
                    _log.DebugFormat("Found file {0}", filePath);
                    files.Add(filePath);
                }
            }

            if(includeSubDirs) {
                foreach(var dirPath in Directory.GetDirectories(sourceDir)) {
                    var dir = GetDirectoryName(dirPath);
                    _log.DebugFormat("Recursing into {0}", Path.Combine(sourceDir, dir));
                    files.AddRange(FindMatchingFiles(Path.Combine(sourceDir, dir), nameRE, includeSubDirs, depth + 1));
                }
            }

            return files;
        }


        /// <summary>
        /// Given a path containing wildcards, returns the file(s) that match
        /// pattern.
        /// </summary>
        public static List<string> GetMatchingFiles(string pathPattern)
        {
            var dir = Path.GetDirectoryName(pathPattern);
            var pattern = Path.GetFileName(pathPattern);
            return FindMatchingFiles(dir, pattern, false);
        }


        /// <summary>
        /// Checks if the specified path exists, throwing an exception if it
        /// doesn't.
        /// </summary>
        public static void EnsureFileExists(string path)
        {
            if(!File.Exists(path)) {
                throw new FileNotFoundException(string.Format("No file was found at {0}", path));
            }
        }


        /// <summary>
        /// Checks if the specified directory exists, throwing an exception if
        /// it doesn't.
        /// </summary>
        public static void EnsureDirExists(string path)
        {
            if(!Directory.Exists(path)) {
                throw new DirectoryNotFoundException(string.Format("No directory was found at {0}", path));
            }
        }


        /// <summary>
        /// Checks if the specified path can be written to.
        /// </summary>
        public static void EnsureFileWriteable(string path)
        {
            using (FileStream s = File.Open(path, FileMode.OpenOrCreate,
                                            FileAccess.Write, FileShare.None))
            { }
        }


        /// <summary>
        /// Returns the name of the directory, given a path to a directory
        /// </summary>
        public static string GetDirectoryName(string path)
        {
            var di = new DirectoryInfo(path);
            return di.Name;
        }


        /// <summary>
        /// Returns the part of path2 that is not in path1.
        /// </summary>
        public static string PathDifference(string path1, string path2)
        {
            string diff = "";
            if(path2.Length > path1.Length) {
                if(path2.Substring(0, path1.Length) != path1) {
                    throw new ArgumentException(string.Format("Path {0} is not a parent path of {1}", path1, path2));
                }
                else {
                    return path2.Substring(path1.Length);
                }
            }
            return diff;
        }


        /// <summary>
        /// Convert a string to a byte array, using ASCII encoding.
        /// </summary>
        public static byte[] GetBytes(string str)
        {
            return System.Text.Encoding.ASCII.GetBytes(str);
        }

    }

}

