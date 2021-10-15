using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using Dalamud;
using Microsoft.Win32;
using Newtonsoft.Json;
using SapphireBootWPF.Addon;
using SapphireBootWPF.Properties;
using SapphireBootWPF.Dalamud;
using Serilog;
using static SapphireBootWPF.RepoExtensions;

namespace SapphireBootWPF.Dalamud
{
    class DalamudLauncher : IRunnableAddon
    {
        private Process _gameProcess;
        private DirectoryInfo _gamePath;
        private bool _optOutMbCollection;

        public DalamudLauncher()
        {}

        public void Setup(Process gameProcess)
        {
            _gameProcess = gameProcess;
            _gamePath = new DirectoryInfo(Settings.Default.ClientPath.Replace("\\game\\ffxiv_dx11.exe", ""));
        }

        public void Run()
        {
            Run(_gamePath, _gameProcess);
        }

        public const string REMOTE_BASE = "https://goatcorp.github.io/dalamud-distrib/";

        private void Run(DirectoryInfo gamePath, Process gameProcess)
        {
            Log.Information("[HOOKS] DalamudLauncher::Run(gp:{0}, cl:0, d:0", gamePath.FullName);

            if (!CheckVcRedist())
                return;

            var ingamePluginPath = Path.Combine(Paths.RoamingPath, "installedPlugins");
            var defaultPluginPath = Path.Combine(Paths.RoamingPath, "devPlugins");

            Directory.CreateDirectory(ingamePluginPath);
            Directory.CreateDirectory(defaultPluginPath);

            Thread.Sleep(1000);

            if (DalamudUpdater.State != DalamudUpdater.DownloadState.Done)
                DalamudUpdater.Run(gamePath);

            while (DalamudUpdater.State != DalamudUpdater.DownloadState.Done)
            {
                if (DalamudUpdater.State == DalamudUpdater.DownloadState.Failed)
                {
                    Serilog.Log.Information("Update failed");
                    return;
                }

                if (DalamudUpdater.State == DalamudUpdater.DownloadState.NoIntegrity)
                {
                    MessageBox.Show("DalamudAntivirusHint",
                        "The in-game addon ran into an error.\n\nThis is most likely caused by your antivirus. Please whitelist the quarantined files or disable the in-game addon.");
                    return;
                }

                Thread.Yield();
            }

            if (!DalamudUpdater.Runner.Exists)
            {
                return;
            }

            if (!ReCheckVersion(gamePath))
            {
                DalamudUpdater.ShowOverlay();
                Log.Error("[HOOKS] ReCheckVersion fail.");

                return;
            }

            var startInfo = new DalamudStartInfo {
                Language = (ClientLanguage)Settings.Default.SavedLanguage,
                PluginDirectory = ingamePluginPath,
                DefaultPluginDirectory = defaultPluginPath,
                ConfigurationPath = DalamudSettings.ConfigPath,
                AssetDirectory = DalamudUpdater.AssetDirectory.FullName,
                GameVersion = Repository.Ffxiv.GetVer(gamePath),
                OptOutMbCollection = _optOutMbCollection,
                WorkingDirectory = DalamudUpdater.Runner.Directory?.FullName,
            };

            Console.WriteLine(startInfo.GameVersion);

            var parameters = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(startInfo)));

            var process = new Process
            {
                StartInfo =
                {
                    FileName = DalamudUpdater.Runner.FullName, WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true,
                    Arguments = gameProcess.Id.ToString() + " " + parameters, WorkingDirectory = DalamudUpdater.Runner.DirectoryName
                }
            };

            process.Start();

            DalamudUpdater.CloseOverlay();

            Log.Information("[HOOKS] Started dalamud!");

            // Reset security protocol after updating
            ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;
        }

        private static bool ReCheckVersion(DirectoryInfo gamePath)
        {
            if (DalamudUpdater.State != DalamudUpdater.DownloadState.Done)
                return false;

            var info = DalamudVersionInfo.Load(new FileInfo(Path.Combine(DalamudUpdater.Runner.DirectoryName,
                "version.json")));

            if (Repository.Ffxiv.GetVer(gamePath) != info.SupportedGameVer)
                return false;

            return true;
        }

        public static bool CanRunDalamud(DirectoryInfo gamePath)
        {
            using var client = new WebClient();

            var versionInfoJson = client.DownloadString(REMOTE_BASE + "version");
            var remoteVersionInfo = JsonConvert.DeserializeObject<DalamudVersionInfo>(versionInfoJson);


            if (Repository.Ffxiv.GetVer(gamePath) != remoteVersionInfo.SupportedGameVer)
                return false;

            return true;
        }

        private static bool CheckVcRedist()
        {
            // we only need to run these once.
            var checkForDotNet48 = CheckDotNet48();
            var checkForVc2019 = CheckVc2019(); // this also checks all the dll locations now

            if (checkForDotNet48 && checkForVc2019)
            {
                return true;
            }
            else if (!checkForDotNet48 && checkForVc2019)
            {
                Log.Error(".Net 4.8 or later not found");


                return false;
            }
            else if (checkForDotNet48 && !checkForVc2019)
            {
                Log.Error("VC 2015-2019 redistributable not found");


                return false;
            }
            else
            {
                Log.Error(".Net 4.8 or later not found");
                Log.Error("VC 2015-2019 redistributable not found");


                return false;
            }
        }

        private static bool CheckDotNet48()
        {
            // copied and adjusted from https://docs.microsoft.com/en-us/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed

            const string subkey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";

            using var ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(subkey);

            if (ndpKey?.GetValue("Release") != null && (int)ndpKey.GetValue("Release") >= 528040)
            {
                return true;
            }
            else return false;
        }

        [DllImport("kernel32", SetLastError = true)]
        static extern IntPtr LoadLibrary(string lpFileName);

        private static bool CheckLibrary(string fileName)
        {
            return LoadLibrary(fileName) != IntPtr.Zero;
        }

        private static bool CheckVc2019()
        {
            // snipped from https://stackoverflow.com/questions/12206314/detect-if-visual-c-redistributable-for-visual-studio-2012-is-installed
            // and https://github.com/bitbeans/RedistributableChecker

            var vcreg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\DevDiv\VC\Servicing\14.0\RuntimeMinimum", false);
            if (vcreg == null) return false;
            var vcVersion = vcreg.GetValue("Version");
            if (((string)vcVersion).StartsWith("14"))
            {
                if (CheckLibrary("ucrtbase_clr0400") &&
                    CheckLibrary("vcruntime140_clr0400") &&
                    CheckLibrary("vcruntime140"))
                    return true;
            }
            return false;
        }


        public string Name => "XIVLauncher in-game features";
    }
}
