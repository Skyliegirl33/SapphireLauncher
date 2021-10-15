using SapphireBootWPF.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using SapphireBootWPF.Addon;
using System.Threading;
using SapphireBootWPF.Dalamud;
using static SapphireBootWPF.RepoExtensions;
using System.ComponentModel;
using Serilog;

namespace SapphireBootWPF
{
    public static class BootClient
    {

        public static Process LaunchGame(string sessionId, string serverLobbyAddress, string serverFrontierAddress) {
            string args = string.Format(
                "DEV.TestSID={0} DEV.UseSqPack=1 DEV.DataPathType=1 " +
                "DEV.LobbyHost01={1} DEV.LobbyPort01=54994 " +
                "DEV.LobbyHost02={1} DEV.LobbyPort02=54994 " +
                "DEV.LobbyHost03={1} DEV.LobbyPort03=54994 " +
                "DEV.LobbyHost04={1} DEV.LobbyPort04=54994 " +
                "DEV.LobbyHost05={1} DEV.LobbyPort05=54994 " +
                "DEV.LobbyHost06={1} DEV.LobbyPort06=54994 " +
                "DEV.LobbyHost07={1} DEV.LobbyPort07=54994 " +
                "DEV.LobbyHost08={1} DEV.LobbyPort08=54994 " +
                "SYS.Region=3 language={2} ver={3} DEV.MaxEntitledExpansionID={4} DEV.GMServerHost={5} {6}",
                sessionId, serverLobbyAddress, Settings.Default.SavedLanguage,
                Repository.Ffxiv.GetVer(new DirectoryInfo(Settings.Default.ClientPath.Replace("\\game\\ffxiv_dx11.exe", ""))),
                Settings.Default.ExpansionLevel, serverFrontierAddress, Settings.Default.LaunchParams);

            var environment = new Dictionary<string, string>();

            var startInfo = new ProcessStartInfo {
                Arguments = args,
                WorkingDirectory = Path.GetDirectoryName(Settings.Default.ClientPath),
                FileName = Path.GetFileName(Settings.Default.ClientPath)
            };

            Process game;
            try {
                game = NativeAclFix.LaunchGame(Path.GetDirectoryName(Settings.Default.ClientPath), Settings.Default.ClientPath, args, environment);
            } catch (Win32Exception ex) {
                Log.Error(ex, $"NativeLauncher error; {ex.HResult}: {ex.Message}");

                return null;
            }

            for (var tries = 0; tries < 30; tries++) {
                game.Refresh();

                // Something went wrong here, why even bother
                if (game.HasExited) {
                    if (Process.GetProcessesByName("ffxiv_dx11").Length +
                        Process.GetProcessesByName("ffxiv").Length >= 2) {
                        Log.Error("You can't launch more than two instances of the game by default.\n\nPlease check if there is an instance of the game that did not close correctly.");

                        return null;
                    } else {
                        throw new Exception("Game exited prematurely");
                    }
                }

                // Is the main window open? Let's wait so any addons won't run into nothing
                if (game.MainWindowHandle == IntPtr.Zero) {
                    Thread.Sleep(1000);
                    continue;
                }

                break;
            }

            return game;
        }

        public static async Task StartClientWithAddonAsync(string sessionId, string serverLobbyAddress, string serverFrontierAddress)
        {
            var gameProcess = LaunchGame(sessionId, serverLobbyAddress, serverFrontierAddress);

            if (gameProcess == null) {
                Log.Information("GameProcess was null...");
                return;
            }

            var addonMgr = new AddonManager();

            try {
                if (Settings.Default.AddonList == null)
                    Settings.Default.AddonList = new List<AddonEntry>();

                var addons = Settings.Default.AddonList.Where(x => x.IsEnabled).Select(x => x.Addon).Cast<IAddon>().ToList();

                if (Settings.Default.DalamudEnabled && gameProcess.ProcessName.Contains("ffxiv_dx11")) {
                    addons.Add(new DalamudLauncher());
                } else {
                    Log.Information("In-Game addon was not enabled.");
                }

                await Task.Run(() => addonMgr.RunAddons(gameProcess, addons));
            } catch (Exception ex) {
                Log.Information(ex, "Failed to inject Dalamud");
                addonMgr.StopAddons();
            }

            var watchThread = new Thread(() => {
                while (!gameProcess.HasExited) {
                    gameProcess.Refresh();
                    Thread.Sleep(1);
                }

                Log.Information("Game has exited.");
                addonMgr.StopAddons();

                Process.GetCurrentProcess().Kill();
            });
            watchThread.Start();

            Console.WriteLine("Started WatchThread");
        }
    }
}
