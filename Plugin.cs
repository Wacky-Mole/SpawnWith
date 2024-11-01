using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using Steamworks;

namespace StartWith
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class ModTemplatePlugin : BaseUnityPlugin
    {
        internal const string ModName = "StarWith";
        internal const string ModVersion = "1.0.6";
        internal const string Author = "WackyMole";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        private static string SteamCFG = Paths.ConfigPath + Path.DirectorySeparatorChar + "WorldID.cfg";

        internal static string ConnectionError = "";

        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource ItemManagerModTemplateLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new(ModGUID)
        { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        private static bool firstTime = false;
        public static string WorldName = "";
        private static string PlayerSteamID = null;
        private static bool SteamOn = false;


        [HarmonyPatch(typeof(Game), "SpawnPlayer")]
        private static class Game_OnNewCharacterDone_PatchSpawnWith
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                {
                    if (SteamOn)
                        CheckFileforSpawn();

                    StartingitemPrefab();


                }
            }
        }

        [HarmonyPatch(typeof(FejdStartup), "OnNewCharacterDone")]
        private static class FejdStartup_OnNewCharacterDone_PatchSpawnWith
        {
            private static void Postfix()
            {
                StartingFirsttime();

            }

        }

        public void Awake()
        {
            _serverConfigLocked = config("General", "Force Server Config", true, "Force Server Config");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            SpawnWithConfig = config("General", "SpawnWithList", "Stone:2,", "You can add starting items by \"Prefab:amount,Item2Prefab:Item2Amount\" - Like \"Stone:2,\" - No spaces");
            CheckWorldAgainstSteamID = config("General", "WorldSteamCheck", true, "Have world Check Against SteamID");

            SteamOn = (bool)Config["General", "WorldSteamCheck"].BoxedValue;

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);

        }


        private static void StartingitemPrefab()
        {
            if (firstTime)
            {
                string goodvalue = (string)SpawnWithConfig.BoxedValue;
                ItemManagerModTemplateLogger.LogInfo($"ReadConfigValues called {goodvalue}");

                string[] t = goodvalue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                Dictionary<string, string> GoodDictionary =
                           t.Select(item => item.Split(':')).ToDictionary(s => s[0], s => s[1]);

                // ItemManagerModTemplateLogger.LogInfo("New Starting Item Set");
                Inventory inventory = ((Humanoid)Player.m_localPlayer).m_inventory;
                foreach (var item in GoodDictionary)
                {

                    int num = 1;
                    bool isParsable = Int32.TryParse(item.Value, out num);
                    if (!isParsable)
                        num = 1;
                    ItemManagerModTemplateLogger.LogInfo($"Item {item.Key} with amount {num} being added to new Character");
                    inventory.AddItem(item.Key, num, 1, 0, 0L, "");
                }
                firstTime = false;
            }
        }

        private static void CheckFileforSpawn()
        {
            if (SteamOn)
            {
                bool GivePlayerItems = false;
                try
                {

                    WorldName = ZNet.instance.GetWorldName();
                    PlayerSteamID = SteamUser.GetSteamID().ToString();

                    GivePlayerItems = true;
                    // Worldname:SteamID


                    if (!File.Exists(SteamCFG))
                        File.Create(SteamCFG);
                    List<string> LinestoSplit = new List<string>();


                    using (var file = new StreamReader(SteamCFG))
                    {
                        string line;
                        while ((line = file.ReadLine()) != null)
                        {
                            line.Trim();
                            string decoded = Base64Decode(line);
                            LinestoSplit.Add(decoded);
                        }
                    }


                    foreach (string line in LinestoSplit)
                    {

                        string[] Section = line.Split(new[] { ':' });
                        if (Section[0] == WorldName)
                        {
                            if (Section[1] == PlayerSteamID)
                            {
                                GivePlayerItems = false;
                                firstTime = false;
                                break;
                            }
                        }

                    }


                    if (GivePlayerItems)
                    {
                        ItemManagerModTemplateLogger.LogInfo($"GivePlayer");
                        firstTime = true;
                        string WriteLine = WorldName + ":" + PlayerSteamID;
                        string EncodedLine = Base64Encode(WriteLine) + Environment.NewLine;
                        File.AppendAllText(SteamCFG, EncodedLine);
                    }

                }
                catch { ItemManagerModTemplateLogger.LogInfo($"Could not Get SteamID, so using fall back firstTimeSpawn"); }

            }

        }
        private static void StartingFirsttime()
        {
            firstTime = true;

        }
        private void OnDestroy()
        {
            Config.Save();
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                ItemManagerModTemplateLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                ItemManagerModTemplateLogger.LogError($"There was an issue loading your {ConfigFileName}");
                ItemManagerModTemplateLogger.LogError("Please check your config entries for spelling and format!");
            }
        }

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        #region ConfigOptions

        private static ConfigEntry<bool>? _serverConfigLocked;
        private static ConfigEntry<string>? SpawnWithConfig;
        private static ConfigEntry<bool>? CheckWorldAgainstSteamID;


        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            public bool? Browsable = false;
        }

        #endregion
    }
}