using System;
using System.IO;
using UnityEngine;

namespace Mjolnir
{
    /// <summary>
    /// Long-lived host. Polls the command file so the mod can be driven
    /// without synthetic keyboard/mouse input (works headless and on Linux).
    /// Commands: give, clean, ping.
    /// </summary>
    public class MjolnirHost : MonoBehaviour
    {
        private const float PollInterval = 0.5f;
        private float m_nextPoll;

        private void Update()
        {
            if (Time.time < m_nextPoll) return;
            m_nextPoll = Time.time + PollInterval;

            try
            {
                if (!File.Exists(MjolnirPlugin.CmdPath)) return;

                string[] lines = File.ReadAllLines(MjolnirPlugin.CmdPath);
                File.Delete(MjolnirPlugin.CmdPath);

                foreach (string raw in lines)
                {
                    string rawLine = raw.Trim();
                    string line = rawLine.ToLowerInvariant();
                    if (line.Length == 0) continue;
                    MjolnirPlugin.FileLog("cmd: " + line);

                    switch (line)
                    {
                        case "give":
                            MjolnirPlugin.GiveToLocalPlayer();
                            break;
                        case "clean":
                            MjolnirPlugin.CleanWorldDrops();
                            break;
                        case "ping":
                            MjolnirPlugin.FileLog("pong");
                            break;
                        default:
                            if (line.StartsWith("restyle "))
                            {
                                // prefab names are case-sensitive: pass the original case
                                MjolnirPlugin.Restyle(rawLine.Substring("restyle ".Length).Trim());
                            }
                            else
                            {
                                MjolnirPlugin.FileLog("unknown cmd: " + line);
                            }
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                MjolnirPlugin.FileLog("cmd poll error: " + e.Message);
            }
        }
    }
}
