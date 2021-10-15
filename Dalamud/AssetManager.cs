﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;

namespace SapphireBootWPF.Dalamud {
    internal class AssetManager
    {
        private const string ASSET_STORE_URL = "https://goatcorp.github.io/DalamudAssets/";

        internal class AssetInfo
        {
            public int Version { get; set; }
            public List<Asset> Assets { get; set; }
            
            public class Asset
            {
                public string Url { get; set; }
                public string FileName { get; set; }
                public string Hash { get; set; }
            }
        }

        public static bool EnsureAssets(DirectoryInfo baseDir)
        {
            using var client = new WebClient();
            using var sha1 = SHA1.Create();

            Console.WriteLine("[DASSET] Starting asset download");

            var (isRefreshNeeded, info) = CheckAssetRefreshNeeded(baseDir);

            if (info == null)
                return false;

            foreach (var entry in info.Assets)
            {
                var filePath = Path.Combine(baseDir.FullName, entry.FileName);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                
                var refreshFile = false;
                if (File.Exists(filePath) && !string.IsNullOrEmpty(entry.Hash))
                {
                    try
                    {
                        using var file = File.OpenRead(filePath);
                        var fileHash = sha1.ComputeHash(file);
                        var stringHash = BitConverter.ToString(fileHash).Replace("-", "");
                        refreshFile = stringHash != entry.Hash;
                        Console.WriteLine("[DASSET] {0} has hash {1} when remote asset has {2}.", entry.FileName, stringHash, entry.Hash);
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Information(ex, "[DASSET] Could not read asset.");
                    }
                }
                
                if (!File.Exists(filePath) || isRefreshNeeded || refreshFile)
                {
                    Console.WriteLine("[DASSET] Downloading {0} to {1}...", entry.Url, entry.FileName);
                    try
                    {
                        File.WriteAllBytes(filePath, client.DownloadData(entry.Url));
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Information(ex, "[DASSET] Could not download asset.");
                        return false;
                    }
                }
            }

            if (isRefreshNeeded)
                SetLocalAssetVer(baseDir, info.Version);

            Console.WriteLine("[DASSET] Assets OK");

            return true;
        }

        private static string GetAssetVerPath(DirectoryInfo baseDir)
        {
            return Path.Combine(baseDir.FullName, "asset.ver");
        }


        /// <summary>
        ///     Check if an asset update is needed. When this fails, just return false - the route to github
        ///     might be bad, don't wanna just bail out in that case
        /// </summary>
        /// <param name="baseDir">Base directory for assets</param>
        /// <returns>Update state</returns>
        private static (bool isRefreshNeeded, AssetInfo info) CheckAssetRefreshNeeded(DirectoryInfo baseDir)
        {
            using var client = new WebClient();

            try
            {
                var localVerFile = GetAssetVerPath(baseDir);
                var localVer = 0;

                try
                {
                    if (File.Exists(localVerFile))
                        localVer = int.Parse(File.ReadAllText(localVerFile));
                }
                catch (Exception ex)
                {
                    // This means it'll stay on 0, which will redownload all assets - good by me
                    Serilog.Log.Information(ex, "[DASSET] Could not read asset.ver");
                }
                
                var remoteVer = JsonConvert.DeserializeObject<AssetInfo>(client.DownloadString(ASSET_STORE_URL + "asset.json"));

                Console.WriteLine("[DASSET] Ver check - local:{0} remote:{1}", localVer, remoteVer.Version);

                var needsUpdate = remoteVer.Version > localVer;

                return (needsUpdate, remoteVer);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString(), "[DASSET] Could not check asset version");
                return (false, null);
            }
        }

        private static void SetLocalAssetVer(DirectoryInfo baseDir, int version)
        {
            try
            {
                var localVerFile = GetAssetVerPath(baseDir);
                File.WriteAllText(localVerFile, version.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString(), "[DASSET] Could not write local asset version");
            }
        }
    }
}
