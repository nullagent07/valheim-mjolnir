using UnityEngine;

namespace Gungnir
{
    /// <summary>
    /// Attached by GungnirPatches to the thrown Gungnir projectile.
    /// Flying -> (hit or timeout) -> return to the owner's right hand -> catch.
    /// </summary>
    public class GungnirProjectile : MonoBehaviour
    {
        private enum State
        {
            Flying,
            Returning,
            Done,
        }

        private const float MaxFlyTime = 6f;
        private const float ReturnDelay = 0.3f;
        private const float ReturnSpeedMin = 6f;
        private const float ReturnSpeedMax = 26f;
        private const float CatchDistance = 1.6f;
        private const float AccelTime = 1.5f;
        private const float SpinSpeed = 900f;

        private State m_state = State.Flying;
        private Character m_owner;
        private ItemDrop.ItemData m_item;
        private Projectile m_projectile;
        private ZNetView m_nview;
        private VisEquipment m_visEquipment;
        private float m_flyTime;
        private float m_returnTime;
        private float m_returnStartAt;

        internal static void Attach(GameObject go, Character owner, ItemDrop.ItemData item)
        {
            var comp = go.GetComponent<GungnirProjectile>();
            if (comp == null)
            {
                comp = go.AddComponent<GungnirProjectile>();
            }
            comp.m_owner = owner;
            comp.m_item = item;
            comp.m_projectile = go.GetComponent<Projectile>();
            comp.m_nview = go.GetComponent<ZNetView>();
            comp.m_visEquipment = owner != null ? owner.GetComponent<VisEquipment>() : null;
            comp.m_state = State.Flying;
            comp.m_flyTime = 0f;
        }

        /// <summary>Called from the Projectile.OnHit Harmony postfix.</summary>
        public void OnVanillaHit()
        {
            if (m_state == State.Flying)
            {
                StartReturn("hit");
            }
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
                        StartReturn("timeout");
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
                GungnirPlugin.FileLog("projectile update error: " + e);
                DropSafely("update error");
            }
        }

        private void StartReturn(string reason)
        {
            m_state = State.Returning;
            m_returnStartAt = Time.time + ReturnDelay;
            m_returnTime = 0f;

            // stop vanilla simulation (gravity, raycasts, TTL)
            if (m_projectile != null)
            {
                m_projectile.enabled = false;
            }
            GungnirPlugin.FileLog("return start (" + reason + ")");
        }

        private Vector3 CatchPoint()
        {
            if (m_visEquipment != null && m_visEquipment.m_rightHand != null)
            {
                return m_visEquipment.m_rightHand.position;
            }
            if (m_owner != null)
            {
                return m_owner.GetCenterPoint() + Vector3.up * 1.3f;
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

            m_returnTime += Time.deltaTime;
            Vector3 target = CatchPoint();
            Vector3 delta = target - transform.position;
            float dist = delta.magnitude;
            if (dist <= CatchDistance)
            {
                Catch();
                return;
            }

            float speed = Mathf.Lerp(ReturnSpeedMin, ReturnSpeedMax, Mathf.Clamp01(m_returnTime / AccelTime));
            Vector3 dir = delta / dist;
            transform.position += dir * speed * Time.deltaTime;
            if (dir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(dir);
                // tumble the spear while it flies back so the return reads as an animation
                transform.Rotate(Vector3.right, SpinSpeed * Time.deltaTime, Space.Self);
            }
        }

        private void Catch()
        {
            m_state = State.Done;
            var player = m_owner as Player;
            if (player != null)
            {
                var inv = player.GetInventory();
                if (m_item != null && !inv.ContainsItem(m_item))
                {
                    if (inv.AddItem(m_item))
                    {
                        GungnirPlugin.FileLog("catch: returned to inventory");
                    }
                    else
                    {
                        ItemDrop.DropItem(m_item, m_item.m_stack,
                            player.transform.position + player.transform.forward * 0.7f + Vector3.up * 0.5f,
                            Quaternion.identity);
                        GungnirPlugin.FileLog("catch: inventory full, dropped at feet");
                    }
                }
                else
                {
                    GungnirPlugin.FileLog("catch: already in inventory (no duplicate)");
                }
                player.Message(MessageHud.MessageType.TopLeft, GungnirPlugin.MsgReturnedToken, 0,
                    m_item != null ? m_item.GetIcon() : null);
            }
            else
            {
                if (m_item != null)
                {
                    ItemDrop.DropItem(m_item, m_item.m_stack, transform.position, Quaternion.identity);
                }
                GungnirPlugin.FileLog("catch: non-player owner, dropped");
            }
            DestroySelf();
        }

        private void DropSafely(string reason)
        {
            if (m_state == State.Done) return;
            m_state = State.Done;
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
                    GungnirPlugin.FileLog("drop fail: " + e.Message);
                }
            }
            GungnirPlugin.FileLog("drop safely (" + reason + ")");
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
            // Safety net: the spear must never be lost.
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
