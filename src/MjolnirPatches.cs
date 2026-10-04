using HarmonyLib;

namespace Mjolnir
{
    /// <summary>
    /// When a Mjolnir projectile is set up (thrown), make it returnable:
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
                if (item.m_shared.m_name != MjolnirPlugin.ItemNameToken) return;

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
    }

    /// <summary>
    /// Any hit on a Mjolnir projectile starts the return flight.
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
}
