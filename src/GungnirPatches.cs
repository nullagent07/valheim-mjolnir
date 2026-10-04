using HarmonyLib;

namespace Gungnir
{
    /// <summary>
    /// When a Gungnir projectile is set up (thrown), make it returnable:
    /// no item respawn on hit, no despawn on hit, no attaching to rigidbodies,
    /// no TTL, and attach the returning behaviour.
    /// </summary>
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
    internal static class PatchProjectileSetup
    {
        [HarmonyPostfix]
        private static void Postfix(Projectile __instance, Character owner, ItemDrop.ItemData item)
        {
            try
            {
                if (item == null || item.m_shared == null) return;
                if (item.m_shared.m_name != GungnirPlugin.ItemNameToken) return;

                __instance.m_respawnItemOnHit = false;
                __instance.m_stayAfterHitStatic = true;
                __instance.m_stayAfterHitDynamic = true;
                __instance.m_attachToRigidBody = false;
                __instance.m_attachToClosestBone = false;
                __instance.m_ttl = 0f;

                GungnirProjectile.Attach(__instance.gameObject, owner, item);
                GungnirPlugin.FileLog("projectile setup: Gungnir thrown by " + (owner != null ? owner.name : "null"));
            }
            catch (System.Exception e)
            {
                GungnirPlugin.FileLog("Setup postfix error: " + e);
            }
        }
    }

    /// <summary>
    /// Any hit on a Gungnir projectile starts the return flight.
    /// </summary>
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.OnHit))]
    internal static class PatchProjectileOnHit
    {
        [HarmonyPostfix]
        private static void Postfix(Projectile __instance)
        {
            try
            {
                var gungnir = __instance.GetComponent<GungnirProjectile>();
                if (gungnir != null)
                {
                    gungnir.OnVanillaHit();
                }
            }
            catch (System.Exception e)
            {
                GungnirPlugin.FileLog("OnHit postfix error: " + e);
            }
        }
    }
}
