using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace Mjolnir
{
    /// <summary>
    /// Mjolnir — a rework of the vanilla Frostner (Ледомор, prefab MaceSilver).
    /// The Frostner itself becomes the thunder hammer: throw it with the secondary attack,
    /// aim at it and press secondary attack again - it returns to the hand.
    /// BepInEx 5 + Jotunn plugin for Valheim 1.0 (Unity 6, Mono).
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class MjolnirPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "morovin.mjolnir";
        public const string PluginName = "Mjolnir - Frostner rework (returning thunder hammer)";
        public const string PluginVersion = "2.1.0";

        /// <summary>The vanilla Frostner (Ледомор) prefab - the mod reworks this exact item.</summary>
        public const string FrostnerPrefabName = "MaceSilver";

        public const string MsgReturnedToken = "$msg_mjolnir_returned";
        public const string MsgDeployedToken = "$msg_mjolnir_deployed";
        public const string MsgRecallToken = "$msg_mjolnir_recall";
        public const string MsgRestyledToken = "$msg_mjolnir_restyled";

        internal static MjolnirPlugin Instance;
        internal static string PersistentDir;
        internal static string LogPath;
        internal static string CmdPath;

        private static bool s_done;
        private static GameObject s_itemPrefab;
        private static GameObject s_projectilePrefab;
        private static ItemDrop.ItemData s_pendingEquipItem;
        private static float s_pendingEquipUntil;
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
            TryModifyFrostner();
            PrefabManager.OnVanillaPrefabsAvailable += TryModifyFrostner;

            CommandManager.Instance.AddConsoleCommand(new MjolnirCommand());

            FileLog("Awake complete");
        }

        private void OnDestroy()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= TryModifyFrostner;
            m_harmony?.UnpatchSelf();
            FileLog("OnDestroy");
        }

        private void AddLocalizations()
        {
            try
            {
                var loc = LocalizationManager.Instance.GetLocalization();
                loc.AddTranslation("English", "msg_mjolnir_returned", "Mjölnir returns to your hand!");
                loc.AddTranslation("Russian", "msg_mjolnir_returned", "Мьёльнир возвращается в руку!");
                loc.AddTranslation("English", "msg_mjolnir_deployed", "Mjölnir awaits your call: secondary attack with empty hands, or its hotbar key.");
                loc.AddTranslation("Russian", "msg_mjolnir_deployed", "Мьёльнир ждёт зова: вторичная атака пустой рукой или кнопка молота на панели.");
                loc.AddTranslation("English", "msg_mjolnir_recall", "Mjölnir returns!");
                loc.AddTranslation("Russian", "msg_mjolnir_recall", "Мьёльнир возвращается!");
                loc.AddTranslation("English", "msg_mjolnir_restyled", "Mjölnir's look updated - re-equip it to see.");
                loc.AddTranslation("Russian", "msg_mjolnir_restyled", "Вид Мьёльнира обновлён — переснарядись, чтобы увидеть.");
                FileLog("localization added (English/Russian)");
            }
            catch (Exception e)
            {
                FileLog("localization failed: " + e);
            }
        }

        private static Attack PickThrow(ItemDrop.ItemData.SharedData sh)
        {
            if (sh.m_secondaryAttack != null && sh.m_secondaryAttack.m_attackProjectile != null) return sh.m_secondaryAttack;
            if (sh.m_attack != null && sh.m_attack.m_attackProjectile != null) return sh.m_attack;
            return null;
        }

        /// <summary>
        /// Reworks the vanilla Frostner prefab itself: lightning damage, a throwing secondary
        /// attack (copied from the Splitner lightning spear) and the returning projectile.
        /// </summary>
        private static void TryModifyFrostner()
        {
            if (s_done) return;
            try
            {
                var frostner = PrefabManager.Instance.GetPrefab(FrostnerPrefabName);
                if (frostner == null)
                {
                    FileLog("MaceSilver (Frostner) not available yet");
                    return;
                }
                var drop = frostner.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
                {
                    FileLog("MaceSilver has no ItemDrop/SharedData");
                    s_done = true;
                    return;
                }
                var shared = drop.m_itemData.m_shared;
                FileLog("Frostner (MaceSilver) found - reworking into Mjolnir...");

                // --- secondary attack: lightning throw, copied from a throwing spear ---
                Attack source = null;
                string sourceName = null;
                foreach (string spear in new[] { "SpearSplitner_Lightning", "SpearSplitner", "SpearCarapace", "SpearWolfFang", "SpearFlint" })
                {
                    var go = PrefabManager.Instance.GetPrefab(spear);
                    if (go == null) continue;
                    var spearDrop = go.GetComponent<ItemDrop>();
                    var sh = spearDrop != null ? spearDrop.m_itemData?.m_shared : null;
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
                    // The hammer never leaves the inventory: the throw only empties the hand
                    // (see PatchProjectileSetup.Postfix), the recall puts it back.
                    shared.m_secondaryAttack.m_consumeItem = false;
                    string projName = source.m_attackProjectile != null ? source.m_attackProjectile.name : "null";
                    FileLog($"throw attack copied from {sourceName}: projectile={projName} consume={source.m_consumeItem} anim={source.m_attackAnimation} type={source.m_attackType} (launch fx cleared)");
                }
                else
                {
                    FileLog("WARNING: no throwing spear found - Frostner cannot be thrown");
                }

                // --- projectile: clone the lightning projectile and re-skin it as the Frostner hammer ---
                if (shared.m_secondaryAttack != null && shared.m_secondaryAttack.m_attackProjectile != null)
                {
                    var projSrc = shared.m_secondaryAttack.m_attackProjectile;
                    var projClone = PrefabManager.Instance.CreateClonedPrefab("mjolnir_projectile", projSrc);
                    if (projClone != null)
                    {
                        ReskinProjectile(projClone, frostner);
                        var projComp = projClone.GetComponent<Projectile>();
                        if (projComp != null)
                        {
                            AppendEffect(projComp.m_hitEffects, "fx_lightningweapon_hit");
                        }
                        PrefabManager.Instance.AddPrefab(projClone);
                        s_projectilePrefab = projClone;
                        shared.m_secondaryAttack.m_attackProjectile = projClone;
                        FileLog("mjolnir_projectile created from " + projSrc.name + " and registered");
                    }
                    else
                    {
                        FileLog("WARNING: projectile clone failed, keeping " + projSrc.name);
                    }
                }

                // --- thunder power (Frostner keeps its frost/spirit) ---
                shared.m_damages.m_lightning = Mathf.Max(shared.m_damages.m_lightning, 30f);
                shared.m_damages.m_blunt = Mathf.Max(shared.m_damages.m_blunt, 60f);
                FileLog($"damage: blunt={shared.m_damages.m_blunt} frost={shared.m_damages.m_frost} spirit={shared.m_damages.m_spirit} lightning={shared.m_damages.m_lightning}");

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

                s_itemPrefab = frostner;
                s_done = true;
            }
            catch (Exception e)
            {
                FileLog("TryModifyFrostner exception: " + e);
            }
        }

        /// <summary>Gets the weapon's actual model root: the attach/attachobj child, or null.</summary>
        private static GameObject GetWeaponModel(GameObject itemGo)
        {
            if (itemGo == null) return null;
            Transform attach = itemGo.transform.Find("attach");
            if (attach == null) return null;
            Transform obj = attach.Find("attachobj");
            return obj != null ? obj.gameObject : attach.gameObject;
        }

        /// <summary>
        /// Copy a weapon's held model onto the projectile and make it spin.
        /// On firstTime the projectile's original meshes are hidden (particles/sounds stay);
        /// on restyle the previous model copy is replaced.
        /// </summary>
        private static void ReskinProjectile(GameObject proj, GameObject itemGo, bool firstTime = true, float scale = 1f)
        {
            try
            {
                GameObject srcModel = GetWeaponModel(itemGo);
                if (srcModel == null)
                {
                    FileLog("reskin: no attach model on " + (itemGo != null ? itemGo.name : "null"));
                    return;
                }
                var projComp = proj.GetComponent<Projectile>();
                if (projComp == null)
                {
                    FileLog("reskin: projectile has no Projectile component");
                    return;
                }

                if (firstTime)
                {
                    GameObject oldVisual = projComp.m_visual;
                    if (oldVisual != null)
                    {
                        foreach (var r in oldVisual.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
                        foreach (var r in oldVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.enabled = false;
                    }
                }
                else
                {
                    var prev = proj.transform.Find("mjolnir_visual");
                    if (prev != null) UnityEngine.Object.DestroyImmediate(prev.gameObject);
                }

                var modelCopy = UnityEngine.Object.Instantiate(srcModel, proj.transform, false);
                modelCopy.name = "mjolnir_visual";
                modelCopy.transform.localPosition = Vector3.zero;
                // lay the weapon along the flight direction (handle forward instead of upright)
                modelCopy.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                modelCopy.transform.localScale = Vector3.one * scale;
                modelCopy.SetActive(true);

                if (firstTime && projComp.m_visual == null)
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

                projComp.m_visual = modelCopy;
                projComp.m_rotateVisual = 720f;
                FileLog("projectile reskinned from " + srcModel.name + (firstTime ? "" : " (restyle)") + " scale=" + scale);
            }
            catch (Exception e)
            {
                FileLog("ReskinProjectile failed: " + e.Message);
            }
        }

        /// <summary>Replaces the held/dropped model of the Mjolnir item with another weapon's model.</summary>
        private static void ReplaceAttachModel(GameObject itemGo, GameObject sourceGo, float scale)
        {
            try
            {
                GameObject srcModel = GetWeaponModel(sourceGo);
                if (srcModel == null)
                {
                    FileLog("restyle: no attach model on " + (sourceGo != null ? sourceGo.name : "null"));
                    return;
                }
                Transform attach = itemGo.transform.Find("attach");
                if (attach == null)
                {
                    var go = new GameObject("attach");
                    attach = go.transform;
                    attach.SetParent(itemGo.transform, false);
                }
                for (int i = attach.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(attach.GetChild(i).gameObject);
                }
                var copy = UnityEngine.Object.Instantiate(srcModel, attach, false);
                copy.name = "attachobj";
                copy.transform.localPosition = Vector3.zero;
                copy.transform.localRotation = Quaternion.identity;
                copy.transform.localScale = Vector3.one * scale;
                copy.SetActive(true);
                FileLog("item model replaced with " + srcModel.name + " scale=" + scale);
            }
            catch (Exception e)
            {
                FileLog("ReplaceAttachModel failed: " + e.Message);
            }
        }

        /// <summary>
        /// Live model swap: `mjolnir restyle <vanillaPrefabName> [scale]`, e.g.
        /// `restyle SledgeDemolisher 0.6`. Re-equip the weapon to see the change.
        /// </summary>
        internal static void Restyle(string args)
        {
            try
            {
                var parts = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 1)
                {
                    FileLog("restyle usage: restyle <prefab> [scale]");
                    return;
                }
                string prefabName = parts[0];
                float scale = 1f;
                if (parts.Length >= 2
                    && !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out scale))
                {
                    scale = 1f;
                }

                var src = PrefabManager.Instance.GetPrefab(prefabName);
                if (src == null)
                {
                    src = FindPrefabIgnoreCase(prefabName);
                }
                if (src == null)
                {
                    FileLog("restyle: prefab not found: " + prefabName);
                    return;
                }
                if (s_itemPrefab != null) ReplaceAttachModel(s_itemPrefab, src, scale);
                else FileLog("restyle: item prefab not created yet");
                if (s_projectilePrefab != null) ReskinProjectile(s_projectilePrefab, src, false, scale);
                else FileLog("restyle: projectile prefab not created yet");

                var player = Player.m_localPlayer;
                if (player != null)
                {
                    player.Message(MessageHud.MessageType.TopLeft, MsgRestyledToken, 0, null);
                }
                FileLog("restyle done: " + prefabName + " scale=" + scale + " (re-equip to see the change)");
            }
            catch (Exception e)
            {
                FileLog("restyle error: " + e);
            }
        }

        /// <summary>Case-insensitive prefab lookup over the live ZNetScene / ObjectDB (fields are private, so via reflection).</summary>
        private static GameObject FindPrefabIgnoreCase(string name)
        {
            try
            {
                var znsField = AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs");
                if (ZNetScene.instance != null && znsField != null
                    && znsField.GetValue(ZNetScene.instance) is System.Collections.IDictionary znsDict)
                {
                    foreach (System.Collections.DictionaryEntry entry in znsDict)
                    {
                        var go = entry.Value as GameObject;
                        if (go != null && string.Equals(go.name, name, StringComparison.OrdinalIgnoreCase)) return go;
                    }
                }

                var odbField = AccessTools.Field(typeof(ObjectDB), "m_itemByHash");
                if (ObjectDB.instance != null && odbField != null
                    && odbField.GetValue(ObjectDB.instance) is System.Collections.IDictionary odbDict)
                {
                    foreach (System.Collections.DictionaryEntry entry in odbDict)
                    {
                        var go = entry.Value as GameObject;
                        if (go != null && string.Equals(go.name, name, StringComparison.OrdinalIgnoreCase)) return go;
                    }
                }
            }
            catch (Exception e)
            {
                FileLog("FindPrefabIgnoreCase error: " + e.Message);
            }
            return null;
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

        // --- "away" state: the hammer is thrown, still in the inventory, but not in the hand ---

        /// <summary>The thrown hammer (an inventory item that is out of the hand), or null.</summary>
        internal static ItemDrop.ItemData AwayItem;
        internal static Player AwayOwner;

        internal static bool IsAway(ItemDrop.ItemData item)
        {
            return item != null && AwayItem != null && item == AwayItem;
        }

        internal static void SetAway(Player player, ItemDrop.ItemData item)
        {
            AwayItem = item;
            AwayOwner = player;
        }

        internal static void ClearAway()
        {
            AwayItem = null;
            AwayOwner = null;
        }

        /// <summary>
        /// Calls the thrown hammer back - no aiming, any distance. If the projectile no longer
        /// exists (its area was unloaded, etc.) the hammer returns to the hand instantly.
        /// Returns false when there is nothing to recall for this player.
        /// </summary>
        internal static bool Recall(Player player, string reason)
        {
            if (AwayItem == null || player == null) return false;
            if (player != AwayOwner)
            {
                ClearAway(); // stale state from another character/session
                return false;
            }

            var proj = MjolnirProjectile.Current;
            if (proj != null && proj.IsAlive)
            {
                proj.Recall(reason);
                return true;
            }

            var item = AwayItem;
            ClearAway();
            FileLog("instant return (" + reason + "): projectile is gone");
            if (player.GetInventory().ContainsItem(item))
            {
                EquipNowOrLater(player, item);
            }
            else
            {
                FileLog("instant return: hammer is no longer in the inventory - nothing to equip");
            }
            PlayFxAt("fx_lightningweapon_hit", player.GetCenterPoint() + Vector3.up * 0.5f);
            PlayFxAt("sfx_mistlands_thunder", player.transform.position);
            player.Message(MessageHud.MessageType.TopLeft, MsgReturnedToken, 0, item.GetIcon());
            return true;
        }

        internal static void EquipNowOrLater(Player player, ItemDrop.ItemData item)
        {
            try
            {
                if (player.EquipItem(item, true))
                {
                    FileLog("equipped to hand");
                }
                else
                {
                    FileLog("equip deferred (busy)");
                    TryEquipLater(item);
                }
            }
            catch (Exception e)
            {
                FileLog("equip failed: " + e.Message);
                TryEquipLater(item);
            }
        }

        /// <summary>Equip retry: EquipItem refuses while InAttack/InDodge; keep trying briefly.</summary>
        internal static void TryEquipLater(ItemDrop.ItemData item)
        {
            s_pendingEquipItem = item;
            s_pendingEquipUntil = Time.realtimeSinceStartup + 2.5f;
        }

        internal static void UpdatePendingEquip()
        {
            if (s_pendingEquipItem == null) return;
            try
            {
                var player = Player.m_localPlayer;
                if (player == null) return;
                if (player.EquipItem(s_pendingEquipItem, true))
                {
                    FileLog("deferred equip ok");
                    s_pendingEquipItem = null;
                    return;
                }
                if (Time.realtimeSinceStartup > s_pendingEquipUntil)
                {
                    FileLog("deferred equip gave up");
                    s_pendingEquipItem = null;
                }
            }
            catch (Exception e)
            {
                FileLog("deferred equip error: " + e.Message);
                s_pendingEquipItem = null;
            }
        }

        /// <summary>Gives the (reworked) Frostner to the local player.</summary>
        internal static void GiveToLocalPlayer()
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                FileLog("give: no local player (not in world yet?)");
                return;
            }
            var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(FrostnerPrefabName) : null;
            if (prefab == null)
            {
                FileLog("give: prefab " + FrostnerPrefabName + " is not registered (not in world yet?)");
                return;
            }

            bool ok = player.GetInventory().AddItem(prefab, 1);
            FileLog("give: AddItem -> " + ok);
            if (ok)
            {
                var drop = prefab.GetComponent<ItemDrop>();
                var icon = drop.m_itemData.GetIcon();
                // the vanilla Frostner keeps its own (localized) name token
                player.Message(MessageHud.MessageType.TopLeft, drop.m_itemData.m_shared.m_name, 1, icon);
            }
        }
    }
}
