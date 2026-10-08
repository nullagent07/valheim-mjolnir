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
        private const float ReturnMaxTime = 1.1f;    // a recall from any distance arrives in about this long
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

        // Thor-style throw: the visual tumbles head-over-handle in the vertical plane of the
        // throw, and lands head-first. The head axis is detected from the model bounds.
        private const float RollDegPerSec = 120f;   // slow roll around the handle; 0 = none
        private const float EmbedDepth = 0.2f;
        private Transform m_visual;
        private Quaternion m_baseLocal = Quaternion.identity; // head -> root forward (+Z)
        private float m_headTip;                              // pivot -> head tip, metres
        private float m_spinAngle;
        private Vector3 m_lastVelDir = Vector3.zero;
        private Vector3 m_lastPos;
        private bool m_visualReady;

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
            comp.SetupVisual();
            Current = comp;
        }

        /// <summary>
        /// Takes the spin over from vanilla (which rotates the model around its own axis) and
        /// works out which end of the model is the head: the long axis of the mesh bounds,
        /// on the side where the bounds extend furthest from the grip pivot.
        /// </summary>
        private void SetupVisual()
        {
            m_visualReady = false;
            m_spinAngle = 0f;
            m_lastPos = transform.position;
            try
            {
                if (m_projectile == null || m_projectile.m_visual == null) return;
                m_visual = m_projectile.m_visual.transform;

                // vanilla aligns the root to the velocity only while m_rotateVisual == 0
                m_projectile.m_rotateVisual = 0f;
                m_projectile.m_rotateVisualY = 0f;
                m_projectile.m_rotateVisualZ = 0f;

                Quaternion savedLocal = m_visual.localRotation;
                m_visual.localRotation = Quaternion.identity;

                bool any = false;
                Vector3 min = Vector3.zero, max = Vector3.zero;
                void Add(Transform t, Bounds b)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 c = b.center + Vector3.Scale(b.extents,
                            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        Vector3 p = m_visual.InverseTransformPoint(t.TransformPoint(c));
                        if (!any) { min = max = p; any = true; }
                        else { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
                    }
                }
                foreach (var mf in m_visual.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh != null) Add(mf.transform, mf.sharedMesh.bounds);
                }
                foreach (var smr in m_visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr.sharedMesh != null) Add(smr.transform, smr.sharedMesh.bounds);
                }

                Vector3 head = Vector3.up;
                float tip = 0.6f;
                if (any)
                {
                    Vector3 size = max - min;
                    int axis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
                    float lo = min[axis], hi = max[axis];
                    float sign = Mathf.Abs(hi) >= Mathf.Abs(lo) ? 1f : -1f;
                    head = Vector3.zero;
                    head[axis] = sign;
                    float scale = m_visual.lossyScale[axis];
                    tip = Mathf.Max(Mathf.Abs(hi), Mathf.Abs(lo)) * (scale > 0.0001f ? scale : 1f);
                }
                m_baseLocal = Quaternion.FromToRotation(head, Vector3.forward);
                m_headTip = tip;
                m_visual.localRotation = savedLocal;
                m_visualReady = true;
                MjolnirPlugin.FileLog($"visual setup: head axis {head}, tip {tip:0.00} m");
            }
            catch (System.Exception e)
            {
                MjolnirPlugin.FileLog("visual setup failed: " + e.Message);
            }
        }

        private void LateUpdate()
        {
            if (!m_visualReady || m_visual == null) return;
            try
            {
                Vector3 pos = transform.position;
                float dt = Time.deltaTime;
                bool moving = dt > 0f && (pos - m_lastPos).sqrMagnitude > 0.0025f * dt * dt * 3600f; // > ~3 m/s
                if (dt > 0f && (pos - m_lastPos).sqrMagnitude > 1e-6f)
                {
                    m_lastVelDir = (pos - m_lastPos).normalized;
                }
                m_lastPos = pos;

                if (m_state == State.Flying && moving)
                {
                    // Thor-style flight: head first, handle trailing, no tumbling - only a slow
                    // roll around the handle axis. The root follows the velocity, so the head
                    // dips naturally along the arc.
                    m_spinAngle = (m_spinAngle + RollDegPerSec * dt) % 360f;
                    Quaternion target = Quaternion.AngleAxis(m_spinAngle, Vector3.forward) * m_baseLocal;
                    m_visual.localRotation = m_flyTime < 0.12f
                        ? Quaternion.Slerp(m_visual.localRotation, target, 25f * dt)
                        : target;
                }
                else if (m_state != State.Returning)
                {
                    m_visual.localRotation = m_baseLocal;
                }
            }
            catch (System.Exception e)
            {
                MjolnirPlugin.FileLog("visual update error: " + e.Message);
                m_visualReady = false;
            }
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

            // land head-first: head along the flight direction, buried a little in the surface
            try
            {
                Vector3 dir = m_projectile != null ? m_projectile.GetVelocity() : Vector3.zero;
                dir = dir.sqrMagnitude > 0.01f ? dir.normalized : m_lastVelDir;
                if (dir.sqrMagnitude > 0.01f && m_visualReady && m_visual != null)
                {
                    transform.rotation = Quaternion.LookRotation(dir);
                    m_visual.localRotation = m_baseLocal;
                    transform.position -= dir * Mathf.Max(0f, m_headTip - EmbedDepth);
                }
            }
            catch (System.Exception e)
            {
                MjolnirPlugin.FileLog("landing pose failed: " + e.Message);
            }
            MjolnirPlugin.FileLog("mjolnir deployed at " + transform.position);

            var player = m_owner as Player;
            if (player != null)
            {
                player.Message(MessageHud.MessageType.TopLeft, MjolnirPlugin.MsgDeployedToken, 0,
                    m_item != null ? m_item.GetIcon() : null);
            }
        }

        internal bool IsAlive => this != null && m_state != State.Done;

        /// <summary>
        /// Calls the hammer back from wherever it is - flying out, lying, any distance,
        /// no aiming. Already returning: nothing to do.
        /// </summary>
        internal void Recall(string reason)
        {
            if (m_state != State.Flying && m_state != State.Lying) return;
            StartReturn(reason);
            MjolnirPlugin.PlayFxAt("sfx_mistlands_thunder", transform.position);
            var player = m_owner as Player;
            if (player != null)
            {
                player.Message(MessageHud.MessageType.TopLeft, MjolnirPlugin.MsgRecallToken, 0,
                    m_item != null ? m_item.GetIcon() : null);
            }
        }

        /// <summary>
        /// Early grab: a recall press while the hammer is already flying back close to the hand
        /// catches it right away. Returns true when it was caught.
        /// </summary>
        public bool OnRecallPress(Player player)
        {
            if (m_owner == null || m_owner != player) return false;
            if (m_state != State.Returning) return false;

            Vector3 delta = transform.position - CatchPoint();
            if (delta.magnitude <= EarlyCatchDistance)
            {
                PlayReach();
                Catch();
                return true;
            }
            return false;
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
                        // missed everything (thrown into the sky / off a cliff): fly back
                        Recall("timeout");
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
                Abort("update error", keepAway: true); // the next recall returns it instantly
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
            if (m_visualReady && m_visual != null)
            {
                // root forward == head, so the head-first flight / handle-to-hand logic holds
                m_visual.localRotation = m_baseLocal;
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
            if (m_owner == null || m_owner.IsDead())
            {
                Abort("owner gone", keepAway: false); // the hammer is in the inventory/tombstone
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
            // from far away it must not take ages: cover any distance in about a second
            speed = Mathf.Max(speed, dist / ReturnMaxTime);
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
                // straight, head-first flight back - no flip
                desired = Quaternion.LookRotation(dir);
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

        /// <summary>
        /// The hammer is back: it never left the inventory, so just put it in the hand again.
        /// If the player moved it elsewhere meanwhile (chest, ground, tombstone) nothing is
        /// created - no duplicates, no losses.
        /// </summary>
        private void Catch()
        {
            m_state = State.Done;
            if (Current == this) Current = null;
            if (MjolnirPlugin.IsAway(m_item)) MjolnirPlugin.ClearAway(); // before equipping (EquipItem patch)

            var player = m_owner as Player;
            if (player != null)
            {
                if (m_item != null && player.GetInventory().ContainsItem(m_item))
                {
                    MjolnirPlugin.EquipNowOrLater(player, m_item);
                }
                else
                {
                    MjolnirPlugin.FileLog("catch: hammer is no longer in the inventory - nothing to equip");
                }

                player.Message(MessageHud.MessageType.TopLeft, MjolnirPlugin.MsgReturnedToken, 0,
                    m_item != null ? m_item.GetIcon() : null);
                MjolnirPlugin.PlayFxAt("fx_lightningweapon_hit", CatchPoint());
                MjolnirPlugin.PlayFxAt("sfx_mistlands_thunder", player.transform.position);
            }
            MjolnirPlugin.FileLog("catch");
            DestroySelf();
        }

        /// <summary>
        /// Removes the flying/lying hammer without giving or dropping anything: the item itself
        /// is still in the inventory. keepAway: the next recall returns it instantly.
        /// </summary>
        private void Abort(string reason, bool keepAway)
        {
            if (m_state == State.Done) return;
            m_state = State.Done;
            if (Current == this) Current = null;
            if (!keepAway && MjolnirPlugin.IsAway(m_item)) MjolnirPlugin.ClearAway();
            MjolnirPlugin.FileLog("abort (" + reason + ")");
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
            // The projectile can vanish on its own (its area unloaded, logout). Nothing is lost:
            // the hammer is still in the inventory and the next recall puts it in the hand.
            if (Current == this) Current = null;
            if (m_state != State.Done)
            {
                MjolnirPlugin.FileLog("projectile vanished while out - next recall returns it instantly");
            }
        }
    }
}
