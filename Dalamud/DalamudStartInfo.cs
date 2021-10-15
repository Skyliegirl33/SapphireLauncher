using SapphireBootWPF;
using System;

namespace Dalamud
{
    [Serializable]
    public sealed class DalamudStartInfo
    {
        public string WorkingDirectory;
        public string ConfigurationPath;

        public string PluginDirectory;
        public string DefaultPluginDirectory;
        public string AssetDirectory;
        public ClientLanguage Language;

        public string GameVersion;

        public bool OptOutMbCollection;
    }
}
