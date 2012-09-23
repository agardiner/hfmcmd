using System.IO;


namespace HFMCmd
{

    public static class Utilities
    {

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

