using System;
using System.IO;

namespace SapphireBootWPF {
    public class Paths {
        public static string RoamingPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher");
    }
}
