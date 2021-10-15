using System;
using System.IO;

namespace SapphireBootWPF {
    public static class Util {
        /// <summary>
        ///     Generates a temporary file name.
        /// </summary>
        /// <returns>A temporary file name that is almost guaranteed to be unique.</returns>
        public static string GetTempFileName() {
            // https://stackoverflow.com/a/50413126
            return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        }
    }
}