﻿using System.IO;
using Newtonsoft.Json;

namespace SapphireBootWPF.Dalamud {
    class DalamudSettings {
        public bool DoDalamudTest { get; set; } = false;
        public bool DoDalamudRuntime { get; set; } = false;
        public string DalamudBetaKind { get; set; }
        public bool? OptOutMbCollection { get; set; }


        public static readonly string ConfigPath = Path.Combine(Paths.RoamingPath, "dalamudConfig.json");

        public static DalamudSettings GetSettings() {
            return File.Exists(ConfigPath) ? JsonConvert.DeserializeObject<DalamudSettings>(File.ReadAllText(ConfigPath)) : new DalamudSettings();
        }
    }
}