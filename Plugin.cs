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

namespace StartWith
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class ModTemplatePlugin : BaseUnityPlugin
    {
        internal const string ModName = "StarWith";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "WackyMole";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

        internal static string ConnectionError = "";

        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource ItemManagerModTemplateLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new(ModGUID)
            { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        private static bool firstTime = false;


        [HarmonyPatch(typeof(Game), "SpawnPlayer")]
		private static class Game_OnNewCharacterDone_PatchSpawnWith
		{
			[HarmonyPostfix]
			private static void Postfix()
			{
				{
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

            SpawnWithConfig = config("General", "SpawnWithList", "Stone:2,", "You can add starting items by \"Prefab:amount,Item2Prefab:Item2Amount\" - Like \"Stone:2,\" - No spaces" );

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);

        }


        private static void StartingitemPrefab()
		{
            string goodvalue = (string)SpawnWithConfig.BoxedValue;
            ItemManagerModTemplateLogger.LogInfo($"ReadConfigValues called {goodvalue}");

            string[] t = goodvalue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            Dictionary<string, string> GoodDictionary =
                       t.Select(item => item.Split(':')).ToDictionary(s => s[0], s => s[1]);

            if (firstTime)
			{
                ItemManagerModTemplateLogger.LogInfo("New Starting Item Set");
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


        #region ConfigOptions

        private static ConfigEntry<bool>? _serverConfigLocked;
        private static ConfigEntry<string>? SpawnWithConfig;

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