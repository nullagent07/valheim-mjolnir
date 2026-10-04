using System;
using System.IO;
using BepInEx;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Gungnir
{
    /// <summary>
    /// Gungnir — a spear that returns to the thrower's hand.
    /// BepInEx 5 + Jotunn plugin for Valheim 1.0 (Unity 6, Mono).
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class GungnirPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "morovin.gungnir";
        public const string PluginName = "Gungnir - returning spear";
        public const string PluginVersion = "0.1.0";

        public const string ItemPrefabName = "Gungnir";
        public const string ItemNameToken = "$item_gungnir";
        public const string ItemDescToken = "$item_gungnir_desc";

        internal static GungnirPlugin Instance;
        internal static string PersistentDir;
        internal static string LogPath;
        internal static string CmdPath;

        private static bool s_itemCreated;
        private Harmony m_harmony;

        private void Awake()
        {
            Instance = this;

            PersistentDir = Application.persistentDataPath;
            LogPath = Path.Combine(PersistentDir, "gungnir.log");
            CmdPath = Path.Combine(PersistentDir, "gungnir-cmd.txt");

            FileLog("=== Gungnir " + PluginVersion + " starting ===");
            FileLog("persistentDataPath: " + PersistentDir);

            m_harmony = new Harmony(PluginGuid);
            m_harmony.PatchAll();

            var host = new GameObject("Gungnir_Host");
            DontDestroyOnLoad(host);
            host.AddComponent<GungnirHost>();

            AddLocalizations();
            TryCreateItem();
            PrefabManager.OnVanillaPrefabsAvailable += TryCreateItem;

            CommandManager.Instance.AddConsoleCommand(new GungnirCommand());

            FileLog("Awake complete");
        }

        private void OnDestroy()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= TryCreateItem;
            m_harmony?.UnpatchSelf();
            FileLog("OnDestroy");
        }

        private void AddLocalizations()
        {
            try
            {
                var loc = LocalizationManager.Instance.GetLocalization();
                loc.AddTranslation("English", "item_gungnir", "Gungnir");
                loc.AddTranslation("English", "item_gungnir_desc",
                    "The Allfather's spear. Throw it - it always finds its way back to your hand.");
                loc.AddTranslation("Russian", "item_gungnir", "Гунгнир");
                loc.AddTranslation("Russian", "item_gungnir_desc",
                    "Копьё Всеотца. Брось его — оно всегда возвращается в руку.");
                FileLog("localization added (English/Russian)");
            }
            catch (Exception e)
            {
                FileLog("localization failed: " + e);
            }
        }

        private static void TryCreateItem()
        {
            if (s_itemCreated) return;
            try
            {
                if (ItemManager.Instance.GetItem(ItemPrefabName) != null)
                {
                    s_itemCreated = true;
                    return;
                }

                string[] candidates = { "SpearDeepNorth", "SpearCarapace", "SpearWolfFang", "SpearFlint" };
                foreach (string baseName in candidates)
                {
                    GameObject basePrefab = PrefabManager.Instance.GetPrefab(baseName);
                    if (basePrefab == null)
                    {
                        FileLog("base prefab not available yet: " + baseName);
                        continue;
                    }

                    var config = new ItemConfig
                    {
                        Name = ItemNameToken,
                        Description = ItemDescToken,
                    };

                    var item = new CustomItem(ItemPrefabName, baseName, config);
                    if (ItemManager.Instance.AddItem(item))
                    {
                        s_itemCreated = true;
                        FileLog("Gungnir item created from " + baseName);
                        LogAttackDetails(item);
                        return;
                    }
                    FileLog("AddItem returned false for base " + baseName);
                }
                FileLog("item not created yet (no base prefab available)");
            }
            catch (Exception e)
            {
                FileLog("TryCreateItem exception: " + e);
            }
        }

        private static void LogAttackDetails(CustomItem item)
        {
            try
            {
                var shared = item.ItemDrop.m_itemData.m_shared;
                var atk = shared.m_attack;
                string proj = atk.m_attackProjectile != null ? atk.m_attackProjectile.name : "null";
                FileLog($"attack: type={atk.m_attackType} projectile={proj} consume={atk.m_consumeItem} maxStack={shared.m_maxStackSize} skill={shared.m_skillType}");
            }
            catch (Exception e)
            {
                FileLog("LogAttackDetails failed: " + e.Message);
            }
        }

        internal static void FileLog(string msg)
        {
            try
            {
                File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + Environment.NewLine);
            }
            catch
            {
                // never let logging break the game
            }
            if (Instance != null)
            {
                Instance.Logger.LogInfo(msg);
            }
        }

        internal static void GiveToLocalPlayer()
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                FileLog("give: no local player (not in world yet?)");
                return;
            }
            var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(ItemPrefabName) : null;
            if (prefab == null)
            {
                FileLog("give: prefab " + ItemPrefabName + " is not registered (not in world yet?)");
                return;
            }

            bool ok = player.GetInventory().AddItem(prefab, 1);
            FileLog("give: AddItem -> " + ok);
            if (ok)
            {
                var icon = prefab.GetComponent<ItemDrop>().m_itemData.GetIcon();
                player.Message(MessageHud.MessageType.TopLeft, ItemNameToken, 1, icon);
            }
        }
    }
}
