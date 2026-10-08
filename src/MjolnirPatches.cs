using HarmonyLib;

namespace Mjolnir
{
    /// <summary>
    /// When a Mjolnir projectile is set up (thrown), make it controllable:
    /// no item respawn on hit, no despawn on hit, no attaching to rigidbodies,
    /// no TTL, and attach the returning behaviour.
    /// MUST be a Prefix: vanilla Setup reads m_respawnItemOnHit and stores m_spawnItem
    /// inside its own body; a postfix would run too late and the item would drop (duplicate bug).
    /// </summary>
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
    internal static class PatchProjectileSetup
    {
        [HarmonyPrefix]
        private static void Prefix(Projectile __instance, Character owner, ItemDrop.ItemData item)
        {
            try
            {
                if (item == null || item.m_shared == null) return;
                bool isFrostner = item.m_dropPrefab != null && item.m_dropPrefab.name == MjolnirPlugin.FrostnerPrefabName;
                if (!isFrostner) return;

                __instance.m_respawnItemOnHit = false;
                __instance.m_spawnItem = null;
                __instance.m_stayAfterHitStatic = true;
                __instance.m_stayAfterHitDynamic = true;
                __instance.m_attachToRigidBody = false;
                __instance.m_attachToClosestBone = false;
                __instance.m_ttl = 0f;

                MjolnirProjectile.Attach(__instance.gameObject, owner, item);
                MjolnirPlugin.FileLog("projectile setup: Mjolnir thrown by " + (owner != null ? owner.name : "null"));
            }
            catch (System.Exception e)
            {
                MjolnirPlugin.FileLog("Setup prefix error: " + e);
            }
        }

        /// <summary>
        /// After the throw the hammer stays in the inventory (the attack no longer consumes it)
        /// but leaves the hand - like a weapon that is mid-attack.
        /// </summary>
        [HarmonyPostfix]
        private static void Postfix(Projectile __instance, Character owner, ItemDrop.ItemData item)
        {
            try
            {
                if (item == null || __instance.GetComponent<MjolnirProjectile>() == null) return;
                var player = owner as Player;
                if (player == null || player != Player.m_localPlayer) return;

                MjolnirPlugin.SetAway(player, item);
                if (player.IsItemEquiped(item))
                {
                    player.UnequipItem(item, false);
                }
                MjolnirPlugin.FileLog("throw: hammer stays in inventory, hand emptied");
            }
            catch (System.Exception e)
            {
                MjolnirPlugin.FileLog("Setup postfix error: " + e);
            }
        }
    }

    /// <summary>
    /// Equipping the thrown hammer (hotbar key, inventory click) calls it back instead:
    /// it flies into the hand and gets equipped on the catch. This also makes a second
    /// throw impossible while the first one is still out.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
    internal static class PatchHumanoidEquipItem
    {
        [HarmonyPrefix]
        private static bool Prefix(Humanoid __instance, ItemDrop.ItemData item, ref bool __result)
        {
            try
            {
                if (!MjolnirPlugin.IsAway(item)) return true;
                var player = __instance as Player;
                if (player == null || player != Player.m_localPlayer) return true;

                if (MjolnirPlugin.Recall(player, "equip"))
                {
                    __result = false;
                    return false;
                }
            }
            catch (System.Exception e)
            {
                MjolnirPlugin.FileLog("EquipItem prefix error: " + e);
            }
            return true;
        }
    }

    /// <summary>
    /// Any hit on a Mjolnir projectile makes it land and wait (no auto-return).
    /// </summary>
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.OnHit))]
    internal static class PatchProjectileOnHit
    {
        [HarmonyPostfix]
        private static void Postfix(Projectile __instance)
        {
            try
            {
                var mjolnir = __instance.GetComponent<MjolnirProjectile>();
                if (mjolnir != null)
                {
                    mjolnir.OnVanillaHit();
                }
            }
            catch (System.Exception e)
            {
                MjolnirPlugin.FileLog("OnHit postfix error: " + e);
            }
        }
    }

    /// <summary>
    /// While unarmed (a shield or torch in the off hand still counts), a secondary-attack
    /// press recalls the thrown Mjolnir - no aiming, any distance.
    /// Runs before the vanilla busy checks so the recall works even while moving.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    internal static class PatchHumanoidStartAttack
    {
        [HarmonyPrefix]
        private static bool Prefix(Humanoid __instance, Character target, bool secondaryAttack)
        {
            try
            {
                if (!secondaryAttack) return true;
                var player = __instance as Player;
                if (player == null || player != Player.m_localPlayer) return true;

                // Only intercept while unarmed. Note: GetCurrentWeapon() returns the
                // m_unarmedWeapon (fists with a kick) when hands are empty, so check that too.
                var weapon = player.GetCurrentWeapon();
                bool unarmed = weapon == null
                    || (player.m_unarmedWeapon != null && weapon == player.m_unarmedWeapon.m_itemData);
                if (!unarmed) return true;

                if (MjolnirPlugin.AwayItem == null) return true;

                // grab it early when it is already flying back close to the hand
                var deployed = MjolnirProjectile.Current;
                if (deployed != null && deployed.IsAlive && deployed.OnRecallPress(player))
                {
                    return false;
                }
                if (MjolnirPlugin.Recall(player, "secondary attack"))
                {
                    return false; // consumed: no kick
                }
            }
            catch (System.Exception e)
            {
                MjolnirPlugin.FileLog("StartAttack prefix error: " + e);
            }
            return true;
        }
    }
}
