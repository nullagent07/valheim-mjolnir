using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Mjolnir
{
    /// <summary>
    /// Mjolnir — a thunder hammer that returns to the thrower's hand.
    /// Built on a vanilla sledge hammer (SledgeDemolisher), with the lightning throw
    /// of the Splitner spear (projectile_splitner_lightning) and a re-skinned projectile.
    /// BepInEx 5 + Jotunn plugin for Valheim 1.0 (Unity 6, Mono).
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class MjolnirPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "morovin.mjolnir";
        public const string PluginName = "Mjolnir - returning thunder hammer";
        public const string PluginVersion = "0.3.0";

        public const string ItemPrefabName = "Mjolnir";
        public const string ItemNameToken = "$item_mjolnir";
        public const string ItemDescToken = "$item_mjolnir_desc";
        public const string MsgReturnedToken = "$msg_mjolnir_returned";
        public const string MsgDeployedToken = "$msg_mjolnir_deployed";
        public const string MsgRecallToken = "$msg_mjolnir_recall";

        // Temporary shim: keep old Gungnir prefabs resolvable so `clean` can remove
        // the leftover world drops / inventory items from v0.1.x. Remove in a later version.
        public const string GungnirPrefabName = "Gungnir";
        public const string GungnirToken = "$item_gungnir";

        internal static MjolnirPlugin Instance;
        internal static string PersistentDir;
        internal static string LogPath;
        internal static string CmdPath;

        private static bool s_itemCreated;
        private Harmony m_harmony;

        private void Awake()
        {
            Instance = this;

            PersistentDir = Application.persistentDataPath;
            LogPath = Path.Combine(PersistentDir, "mjolnir.log");
            CmdPath = Path.Combine(PersistentDir, "mjolnir-cmd.txt");

            FileLog("=== Mjolnir " + PluginVersion + " starting ===");
            FileLog("persistentDataPath: " + PersistentDir);

            m_harmony = new Harmony(PluginGuid);
            m_harmony.PatchAll();

            var host = new GameObject("Mjolnir_Host");
            DontDestroyOnLoad(host);
            host.AddComponent<MjolnirHost>();

            AddLocalizations();
            TryCreateItem();
            PrefabManager.OnVanillaPrefabsAvailable += TryCreateItem;

            CommandManager.Instance.AddConsoleCommand(new MjolnirCommand());

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
                loc.AddTranslation("English", "item_mjolnir", "Mjölnir");
                loc.AddTranslation("English", "item_mjolnir_desc",
                    "The hammer of Thor. It strikes with lightning and always returns to the hand that threw it.");
                loc.AddTranslation("Russian", "item_mjolnir", "Мьёльнир");
                loc.AddTranslation("Russian", "item_mjolnir_desc",
                    "Молот Тора. Бьёт молниями и всегда возвращается в руку бросившему.");
                loc.AddTranslation("English", "msg_mjolnir_returned", "Mjölnir returns to your hand!");
                loc.AddTranslation("Russian", "msg_mjolnir_returned", "Мьёльнир возвращается в руку!");
                loc.AddTranslation("English", "msg_mjolnir_deployed", "Mjölnir awaits your call. Aim at it and press secondary attack.");
                loc.AddTranslation("Russian", "msg_mjolnir_deployed", "Мьёльнир ждёт зова. Наведи на него курсор и нажми вторичную атаку.");
                loc.AddTranslation("English", "msg_mjolnir_recall", "Mjölnir returns!");
                loc.AddTranslation("Russian", "msg_mjolnir_recall", "Мьёльнир возвращается!");

                // old Gungnir shim strings
                loc.AddTranslation("English", "item_gungnir", "Gungnir (old)");
                loc.AddTranslation("English", "item_gungnir_desc", "Old returning spear. Leftover item, clean it up.");
                loc.AddTranslation("Russian", "item_gungnir", "Гунгнир (старый)");
                loc.AddTranslation("Russian", "item_gungnir_desc", "Старое возвращающееся копьё. Остаток, можно убрать.");
                FileLog("localization added (English/Russian)");
            }
            catch (Exception e)
            {
                FileLog("localization failed: " + e);
            }
        }

        private static string FirstAvailable(params string[] names)
        {
            foreach (string n in names)
            {
                if (PrefabManager.Instance.GetPrefab(n) != null) return n;
            }
            return null;
        }

        private static Attack PickThrow(ItemDrop.ItemData.SharedData sh)
        {
            if (sh.m_secondaryAttack != null && sh.m_secondaryAttack.m_attackProjectile != null) return sh.m_secondaryAttack;
            if (sh.m_attack != null && sh.m_attack.m_attackProjectile != null) return sh.m_attack;
            return null;
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

                // Frostner was removed in Valheim 1.0; its successor is the one-handed MaceEldner line.
                string baseName = FirstAvailable("MaceEldner", "MaceSilver", "MaceIron", "SledgeDemolisher");
                if (baseName == null)
                {
                    FileLog("no base hammer/mace available yet");
                    return;
                }

                var config = new ItemConfig
                {
                    Name = ItemNameToken,
                    Description = ItemDescToken,
                };

                var item = new CustomItem(ItemPrefabName, baseName, config);
                if (!ItemManager.Instance.AddItem(item))
                {
                    FileLog("AddItem failed for base " + baseName);
                    return;
                }
                FileLog("Mjolnir item created from " + baseName);

                var shared = item.ItemDrop.m_itemData.m_shared;

                // --- secondary attack: lightning throw, copied from a throwing spear ---
                Attack source = null;
                string sourceName = null;
                foreach (string spear in new[] { "SpearSplitner_Lightning", "SpearSplitner", "SpearCarapace", "SpearWolfFang", "SpearFlint" })
                {
                    var go = PrefabManager.Instance.GetPrefab(spear);
                    if (go == null) continue;
                    var drop = go.GetComponent<ItemDrop>();
                    var sh = drop != null ? drop.m_itemData?.m_shared : null;
                    if (sh == null) continue;
                    Attack a = PickThrow(sh);
                    if (a != null)
                    {
                        source = a;
                        sourceName = spear;
                        break;
                    }
                }

                if (source != null)
                {
                    shared.m_secondaryAttack = source.Clone();
                    // The Splitner throw fires a big launch wave from the attack effects at the
                    // moment of the throw - move that drama to the impact instead.
                    shared.m_secondaryAttack.m_startEffect = new EffectList();
                    shared.m_secondaryAttack.m_triggerEffect = new EffectList();
                    shared.m_secondaryAttack.m_burstEffect = new EffectList();
                    shared.m_secondaryAttack.m_trailStartEffect = new EffectList();
                    string projName = source.m_attackProjectile != null ? source.m_attackProjectile.name : "null";
                    FileLog($"throw attack copied from {sourceName}: projectile={projName} consume={source.m_consumeItem} anim={source.m_attackAnimation} type={source.m_attackType} (launch fx cleared)");
                }
                else
                {
                    FileLog("WARNING: no throwing spear found - Mjolnir cannot be thrown");
                }

                // --- projectile: clone the lightning projectile and re-skin it as a hammer ---
                if (shared.m_secondaryAttack != null && shared.m_secondaryAttack.m_attackProjectile != null)
                {
                    var projSrc = shared.m_secondaryAttack.m_attackProjectile;
                    var projClone = PrefabManager.Instance.CreateClonedPrefab("mjolnir_projectile", projSrc);
                    if (projClone != null)
                    {
                        ReskinProjectile(projClone, item.ItemDrop.gameObject);
                        var projComp = projClone.GetComponent<Projectile>();
                        if (projComp != null)
                        {
                            AppendEffect(projComp.m_hitEffects, "demolisher_shockwave");
                            AppendEffect(projComp.m_hitEffects, "fx_lightningweapon_hit");
                        }
                        PrefabManager.Instance.AddPrefab(projClone);
                        shared.m_secondaryAttack.m_attackProjectile = projClone;
                        FileLog("mjolnir_projectile created from " + projSrc.name + " and registered (impact wave added)");
                    }
                    else
                    {
                        FileLog("WARNING: projectile clone failed, keeping " + projSrc.name);
                    }
                }

                // --- lightning damage on top of the base weapon ---
                shared.m_damages.m_lightning = Mathf.Max(shared.m_damages.m_lightning, 30f);
                FileLog($"damage: blunt={shared.m_damages.m_blunt} slash={shared.m_damages.m_slash} lightning={shared.m_damages.m_lightning}");

                // --- lightning sparks on melee hits ---
                if (shared.m_attack != null)
                {
                    AppendEffect(shared.m_attack.m_hitEffect, "fx_lightningweapon_hit");
                    AppendEffect(shared.m_attack.m_hitTerrainEffect, "fx_lightningweapon_hit");
                }
                if (shared.m_secondaryAttack != null)
                {
                    AppendEffect(shared.m_secondaryAttack.m_hitEffect, "fx_lightningweapon_hit");
                }
                AppendEffect(shared.m_hitEffect, "fx_lightningweapon_hit");

                CreateGungnirShim();

                s_itemCreated = true;
            }
            catch (Exception e)
            {
                FileLog("TryCreateItem exception: " + e);
            }
        }

        /// <summary>
        /// Copy the item's held model ("attach" child) onto the projectile and make it spin.
        /// The original projectile meshes are hidden but its particles/sounds stay.
        /// </summary>
        private static void ReskinProjectile(GameObject proj, GameObject itemGo)
        {
            try
            {
                Transform srcAttach = itemGo.transform.Find("attach");
                GameObject srcModel = srcAttach != null ? srcAttach.gameObject : itemGo;

                var projComp = proj.GetComponent<Projectile>();
                GameObject oldVisual = projComp != null ? projComp.m_visual : null;

                var modelCopy = UnityEngine.Object.Instantiate(srcModel, proj.transform, false);
                modelCopy.name = "mjolnir_visual";
                modelCopy.transform.localPosition = Vector3.zero;
                // lay the weapon along the flight direction (handle forward instead of upright)
                modelCopy.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                modelCopy.SetActive(true);

                foreach (var r in modelCopy.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = true;

                if (oldVisual != null && oldVisual != modelCopy)
                {
                    foreach (var r in oldVisual.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
                    foreach (var r in oldVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.enabled = false;
                }
                else if (oldVisual == null)
                {
                    foreach (var r in proj.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (r.transform != modelCopy.transform && !r.transform.IsChildOf(modelCopy.transform)) r.enabled = false;
                    }
                    foreach (var r in proj.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (r.transform != modelCopy.transform && !r.transform.IsChildOf(modelCopy.transform)) r.enabled = false;
                    }
                }

                if (projComp != null)
                {
                    projComp.m_visual = modelCopy;
                    projComp.m_rotateVisual = 720f;
                }
                FileLog("projectile reskinned to hammer model (source=" + srcModel.name + ")");
            }
            catch (Exception e)
            {
                FileLog("ReskinProjectile failed: " + e.Message);
            }
        }

        /// <summary>Appends a vanilla effect prefab to an EffectList (sparks, trails...).</summary>
        private static void AppendEffect(EffectList list, string prefabName)
        {
            try
            {
                if (list == null) return;
                var prefab = PrefabManager.Instance.GetPrefab(prefabName);
                if (prefab == null)
                {
                    FileLog("fx prefab not found: " + prefabName);
                    return;
                }
                var data = new EffectList.EffectData { m_prefab = prefab, m_enabled = true };
                var arr = list.m_effectPrefabs ?? new EffectList.EffectData[0];
                var newArr = new EffectList.EffectData[arr.Length + 1];
                Array.Copy(arr, newArr, arr.Length);
                newArr[arr.Length] = data;
                list.m_effectPrefabs = newArr;
                FileLog("fx appended: " + prefabName);
            }
            catch (Exception e)
            {
                FileLog("AppendEffect failed: " + e.Message);
            }
        }

        /// <summary>One-shot effect/sound prefab at a position (destroyed after a few seconds).</summary>
        internal static void PlayFxAt(string prefabName, Vector3 pos)
        {
            try
            {
                var prefab = PrefabManager.Instance.GetPrefab(prefabName);
                if (prefab == null) return;
                var go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
                UnityEngine.Object.Destroy(go, 6f);
            }
            catch (Exception e)
            {
                FileLog("PlayFx failed: " + e.Message);
            }
        }

        /// <summary>Temporary: lets old Gungnir drops/items resolve so `clean` can remove them.</summary>
        private static void CreateGungnirShim()
        {
            try
            {
                if (ItemManager.Instance.GetItem(GungnirPrefabName) != null) return;
                string baseSpear = FirstAvailable("SpearCarapace", "SpearWolfFang", "SpearFlint");
                if (baseSpear == null)
                {
                    FileLog("gungnir shim: no base spear available");
                    return;
                }
                var cfg = new ItemConfig { Name = GungnirToken, Description = "$item_gungnir_desc" };
                var shim = new CustomItem(GungnirPrefabName, baseSpear, cfg);
                if (ItemManager.Instance.AddItem(shim)) FileLog("gungnir cleanup shim created from " + baseSpear);
                else FileLog("gungnir cleanup shim: AddItem failed");
            }
            catch (Exception e)
            {
                FileLog("shim error: " + e);
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

        /// <summary>
        /// Removes leftover world drops of Mjolnir and old Gungnir, plus old Gungnir items
        /// in the local inventory. The flying projectile is not an ItemDrop, so it is never touched.
        /// </summary>
        internal static void CleanWorldDrops()
        {
            try
            {
                int worldCount = 0;
                if (ZNetScene.instance != null)
                {
                    var field = AccessTools.Field(typeof(ZNetScene), "m_instances");
                    var dict = field != null ? field.GetValue(ZNetScene.instance) as System.Collections.IDictionary : null;
                    if (dict != null)
                    {
                        var toDestroy = new List<GameObject>();
                        foreach (System.Collections.DictionaryEntry entry in dict)
                        {
                            var nview = entry.Value as ZNetView;
                            if (nview == null) continue;
                            var drop = nview.GetComponent<ItemDrop>();
                            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) continue;
                            string n = drop.m_itemData.m_shared.m_name;
                            if (n == ItemNameToken || n == GungnirToken) toDestroy.Add(drop.gameObject);
                        }
                        foreach (var go in toDestroy)
                        {
                            ZNetScene.instance.Destroy(go);
                            worldCount++;
                        }
                    }
                    else
                    {
                        FileLog("clean: ZNetScene.m_instances not accessible");
                    }
                }
                else
                {
                    FileLog("clean: no ZNetScene (not in world?)");
                }

                int invCount = 0;
                var player = Player.m_localPlayer;
                if (player != null)
                {
                    var inv = player.GetInventory();
                    var toRemove = new List<ItemDrop.ItemData>();
                    foreach (var it in inv.GetAllItems())
                    {
                        if (it != null && it.m_shared != null && it.m_shared.m_name == GungnirToken) toRemove.Add(it);
                    }
                    foreach (var it in toRemove)
                    {
                        inv.RemoveItem(it);
                        invCount++;
                    }
                }
                FileLog($"clean: destroyed {worldCount} world drop(s), removed {invCount} old Gungnir item(s) from inventory");
            }
            catch (Exception e)
            {
                FileLog("clean error: " + e);
            }
        }
    }
}
