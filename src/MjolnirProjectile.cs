using UnityEngine;

namespace Mjolnir
{
    /// <summary>
    /// Attached by MjolnirPatches to the thrown Mjolnir projectile.
    /// Flying -> Lying (waits where it landed) -> Returning (recalled by aiming at it
    /// and pressing secondary attack while unarmed) -> catch into the hand.
    /// </summary>
    public class MjolnirProjectile : MonoBehaviour
    {
        internal enum State
        {
            Flying,
            Lying,
            Returning,
            Done,
        }

        private const float MaxFlyTime = 6f;
        private const float ReturnDelay = 0.15f;
        private const float ReturnSpeedMin = 10f;
        private const float ReturnSpeedMax = 34f;
        private const float ApproachDistance = 5f;
        private const float ApproachSpeed = 2.5f;
        private const float CatchDistance = 1.3f;
        private const float AccelTime = 1.4f;
        private const float FlipTime = 0.5f;         // one flip right after takeoff, then stable
        private const float HandleLeadDistance = 4f;  // on approach the handle turns toward the hand
        private const float SummonMaxDistance = 100f;
        private const float SummonCloseDistance = 3f;
        private const float SummonCosAngle = 0.906f; // ~25 degrees of the crosshair
        private const float ReachDistance = 3f;      // play the "reach out" animation this close
        private const float EarlyCatchDistance = 4.5f; // a wheel press this close grabs it early

        /// <summary>The currently deployed hammer (one per client).</summary>
        internal static MjolnirProjectile Current;

        internal State m_state = State.Flying;
        private Character m_owner;
        private ItemDrop.ItemData m_item;
        private Projectile m_projectile;
        private ZNetView m_nview;
        private VisEquipment m_visEquipment;
        private float m_flyTime;
        private float m_returnTime;
        private float m_returnStartAt;
        private bool m_reachPlayed;

        internal static void Attach(GameObject go, Character owner, ItemDrop.ItemData item)
        {
            var comp = go.GetComponent<MjolnirProjectile>();
            if (comp == null)
            {
                comp = go.AddComponent<MjolnirProjectile>();
            }
            comp.m_owner = owner;
            comp.m_item = item;
            comp.m_projectile = go.GetComponent<Projectile>();
            comp.m_nview = go.GetComponent<ZNetView>();
            comp.m_visEquipment = owner != null ? owner.GetComponent<VisEquipment>() : null;
            comp.m_state = State.Flying;
            comp.m_flyTime = 0f;
            Current = comp;
        }

        /// <summary>Called from the Projectile.OnHit Harmony postfix: the hammer lands and waits.</summary>
        public void OnVanillaHit()
        {
            if (m_state != State.Flying) return;
            m_state = State.Lying;

            // freeze: no vanilla simulation, no TTL destroy - it waits for the recall
            if (m_projectile != null)
            {
                m_projectile.enabled = false;
                m_projectile.m_ttl = 0f;
            }
            MjolnirPlugin.FileLog("mjolnir deployed at " + transform.position);

            var player = m_owner as Player;
            if (player != null)
            {
                player.Message(MessageHud.MessageType.TopLeft, MjolnirPlugin.MsgDeployedToken, 0,
                    m_item != null ? m_item.GetIcon() : null);
            }
        }

        /// <summary>
        /// Called from the Humanoid.StartAttack prefix when the local player presses secondary
        /// attack while unarmed. Returns true when the hammer was recalled.
        /// </summary>
        public bool TrySummon(Player player)
        {
            if (m_state != State.Lying) return false;
            if (m_owner == null || m_owner != player) return false;

            Vector3 delta = transform.position - player.GetEyePoint();
            float dist = delta.magnitude;

            bool aimed = dist <= SummonCloseDistance;
            if (!aimed && dist <= SummonMaxDistance)
            {
                Vector3 aimDir = player.GetAimDir(player.GetEyePoint());
                if (aimDir.sqrMagnitude > 0.0001f && Vector3.Dot(aimDir.normalized, delta.normalized) >= SummonCosAngle)
                {
                    aimed = true;
                }
            }
            if (!aimed) return false;

            StartReturn("summon");
            MjolnirPlugin.PlayFxAt("sfx_mistlands_thunder", transform.position);
            player.Message(MessageHud.MessageType.TopLeft, MjolnirPlugin.MsgRecallToken, 0,
                m_item != null ? m_item.GetIcon() : null);
            return true;
        }

        /// <summary>
        /// Called from the StartAttack prefix on every unarmed secondary press while a Mjolnir is out.
        /// Returns true when the press is consumed by the mod (no vanilla kick).
        /// </summary>
        public bool OnRecallPress(Player player)
        {
            if (m_owner == null || m_owner != player) return false;

            if (m_state == State.Lying)
            {
                // only consume when actually recalled; a missed aim keeps the vanilla kick
                return TrySummon(player);
            }
            if (m_state == State.Returning)
            {
                // grab it early when it's close, and never kick while it's flying back
                Vector3 delta = transform.position - CatchPoint();
                if (delta.magnitude <= EarlyCatchDistance)
                {
                    PlayReach();
                    Catch();
                }
                return true;
            }
            return false; // still flying out after the throw: vanilla kick is fine
        }

        private void Update()
        {
            try
            {
                if (m_nview != null && m_nview.IsValid() && !m_nview.IsOwner())
                {
                    return; // only the owner simulates; others just see the sync
                }

                if (m_state == State.Flying)
                {
                    m_flyTime += Time.deltaTime;
                    if (m_flyTime >= MaxFlyTime)
                    {
                        DropSafely("timeout");
                    }
                }
                else if (m_state == State.Returning)
                {
                    if (Time.time < m_returnStartAt) return;
                    ReturnStep();
                }
            }
            catch (System.Exception e)
            {
                MjolnirPlugin.FileLog("projectile update error: " + e);
                DropSafely("update error");
            }
        }

        private void StartReturn(string reason)
        {
            m_state = State.Returning;
            m_returnStartAt = Time.time + ReturnDelay;
            m_returnTime = 0f;
            m_reachPlayed = false;
            if (m_projectile != null)
            {
                m_projectile.enabled = false;
            }
            MjolnirPlugin.FileLog("return start (" + reason + ")");
        }

        private Vector3 CatchPoint()
        {
            if (m_visEquipment != null && m_visEquipment.m_rightHand != null)
            {
                return m_visEquipment.m_rightHand.position;
            }
            if (m_owner != null)
            {
                return m_owner.GetCenterPoint() + Vector3.up * 1.2f;
            }
            return transform.position;
        }

        private void ReturnStep()
        {
            if (m_owner == null)
            {
                DropSafely("owner gone");
                return;
            }

            Vector3 target = CatchPoint();
            Vector3 delta = target - transform.position;
            float dist = delta.magnitude;
            if (dist <= CatchDistance)
            {
                Catch();
                return;
            }

            m_returnTime += Time.deltaTime;
            float t = Mathf.Clamp01(m_returnTime / AccelTime);
            float speed = Mathf.Lerp(ReturnSpeedMin, ReturnSpeedMax, t);
            float approach = Mathf.Clamp01(dist / ApproachDistance);
            speed = Mathf.Lerp(ApproachSpeed, speed, approach); // gentle final approach into the hand

            if (!m_reachPlayed && dist <= ReachDistance)
            {
                PlayReach(); // the character extends the hand to grab it
            }

            Vector3 dir = delta / dist;
            transform.position += dir * speed * Time.deltaTime;

            // Orientation: one flip at takeoff, then a stable head-first flight toward the
            // owner; on the final approach the handle turns toward the hand (head away).
            Quaternion desired;
            if (dist <= HandleLeadDistance)
            {
                float leadT = Mathf.Clamp01(1f - dist / HandleLeadDistance);
                Quaternion stable = Quaternion.LookRotation(dir);
                Quaternion handleLead = Quaternion.LookRotation(-dir);
                desired = Quaternion.Slerp(stable, handleLead, leadT);
            }
            else
            {
                float flipT = Mathf.Clamp01(m_returnTime / FlipTime);
                desired = Quaternion.LookRotation(dir) * Quaternion.Euler(360f * flipT, 0f, 0f);
            }
            transform.rotation = Quaternion.Slerp(transform.rotation, desired, 14f * Time.deltaTime);
        }

        /// <summary>Plays the "interact" reach-out gesture so the catch reads as grabbing.</summary>
        private void PlayReach()
        {
            m_reachPlayed = true;
            try
            {
                var humanoid = m_owner as Humanoid;
                if (humanoid != null)
                {
                    var zanim = humanoid.GetZAnim();
                    if (zanim != null)
                    {
                        zanim.SetTrigger("interact");
                        MjolnirPlugin.FileLog("reach animation: interact");
                    }
                }
            }
            catch (System.Exception e)
            {
                MjolnirPlugin.FileLog("reach anim failed: " + e.Message);
            }
        }

        private void Catch()
        {
            m_state = State.Done;
            if (Current == this) Current = null;

            var player = m_owner as Player;
            if (player != null)
            {
                var inv = player.GetInventory();
                if (m_item != null && !inv.ContainsItem(m_item))
                {
                    if (inv.AddItem(m_item))
                    {
                        MjolnirPlugin.FileLog("catch: returned to inventory");
                        try
                        {
                            if (player.EquipItem(m_item, true))
                            {
                                MjolnirPlugin.FileLog("catch: equipped to hand");
                            }
                            else
                            {
                                // EquipItem refuses while InAttack/InDodge etc. - retry shortly.
                                MjolnirPlugin.FileLog("catch: equip deferred (busy)");
                                MjolnirPlugin.TryEquipLater(m_item);
                            }
                        }
                        catch (System.Exception e)
                        {
                            MjolnirPlugin.FileLog("equip failed: " + e.Message);
                            MjolnirPlugin.TryEquipLater(m_item);
                        }
                    }
                    else
                    {
                        ItemDrop.DropItem(m_item, m_item.m_stack,
                            player.transform.position + player.transform.forward * 0.7f + Vector3.up * 0.5f,
                            Quaternion.identity);
                        MjolnirPlugin.FileLog("catch: inventory full, dropped at feet");
                    }
                }
                else
                {
                    MjolnirPlugin.FileLog("catch: already in inventory (no duplicate)");
                    if (m_item != null)
                    {
                        try { player.EquipItem(m_item, true); } catch { }
                    }
                }

                player.Message(MessageHud.MessageType.TopLeft, MjolnirPlugin.MsgReturnedToken, 0,
                    m_item != null ? m_item.GetIcon() : null);
                MjolnirPlugin.PlayFxAt("fx_lightningweapon_hit", CatchPoint());
                MjolnirPlugin.PlayFxAt("sfx_mistlands_thunder", player.transform.position);
            }
            else
            {
                if (m_item != null)
                {
                    ItemDrop.DropItem(m_item, m_item.m_stack, transform.position, Quaternion.identity);
                }
                MjolnirPlugin.FileLog("catch: non-player owner, dropped");
            }
            DestroySelf();
        }

        private void DropSafely(string reason)
        {
            if (m_state == State.Done) return;
            m_state = State.Done;
            if (Current == this) Current = null;

            if (m_item != null)
            {
                try
                {
                    var player = m_owner as Player;
                    if (player == null || !player.GetInventory().ContainsItem(m_item))
                    {
                        ItemDrop.DropItem(m_item, m_item.m_stack, transform.position, Quaternion.identity);
                    }
                }
                catch (System.Exception e)
                {
                    MjolnirPlugin.FileLog("drop fail: " + e.Message);
                }
            }
            MjolnirPlugin.FileLog("drop safely (" + reason + ")");
            DestroySelf();
        }

        private void DestroySelf()
        {
            if (ZNetScene.instance != null)
            {
                ZNetScene.instance.Destroy(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            // Safety net: the hammer must never be lost.
            if (Current == this) Current = null;
            if (m_state != State.Done && m_item != null)
            {
                try
                {
                    var player = m_owner as Player;
                    if (player == null || !player.GetInventory().ContainsItem(m_item))
                    {
                        if (ZNetScene.instance != null)
                        {
                            ItemDrop.DropItem(m_item, m_item.m_stack, transform.position, Quaternion.identity);
                        }
                    }
                }
                catch
                {
                    // nothing sensible left to do
                }
            }
        }
    }
}
