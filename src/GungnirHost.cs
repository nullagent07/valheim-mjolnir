using System;
using System.IO;
using UnityEngine;

namespace Gungnir
{
    /// <summary>
    /// Long-lived host. Polls the command file so the mod can be driven
    /// without synthetic keyboard/mouse input (works headless and on Linux).
    /// </summary>
    public class GungnirHost : MonoBehaviour
    {
        private const float PollInterval = 0.5f;
        private float m_nextPoll;

        private void Update()
        {
            if (Time.time < m_nextPoll) return;
            m_nextPoll = Time.time + PollInterval;

            try
            {
                if (!File.Exists(GungnirPlugin.CmdPath)) return;

                string[] lines = File.ReadAllLines(GungnirPlugin.CmdPath);
                File.Delete(GungnirPlugin.CmdPath);

                foreach (string raw in lines)
                {
                    string line = raw.Trim().ToLowerInvariant();
                    if (line.Length == 0) continue;
                    GungnirPlugin.FileLog("cmd: " + line);

                    switch (line)
                    {
                        case "give":
                            GungnirPlugin.GiveToLocalPlayer();
                            break;
                        case "ping":
                            GungnirPlugin.FileLog("pong");
                            break;
                        default:
                            GungnirPlugin.FileLog("unknown cmd: " + line);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                GungnirPlugin.FileLog("cmd poll error: " + e.Message);
            }
        }
    }
}
