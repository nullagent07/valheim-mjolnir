using Jotunn.Entities;

namespace Mjolnir
{
    /// <summary>
    /// F5 console command: `mjolnir give` / `mjolnir clean`.
    /// </summary>
    public class MjolnirCommand : ConsoleCommand
    {
        public override string Name => "mjolnir";
        public override string Help => "mjolnir give | mjolnir restyle <prefab> [scale]";

        public override void Run(string[] args)
        {
            string sub = args.Length >= 1 ? args[0].ToLowerInvariant() : "";
            switch (sub)
            {
                case "give":
                    MjolnirPlugin.GiveToLocalPlayer();
                    break;
                case "restyle":
                    if (args.Length >= 2)
                    {
                        MjolnirPlugin.Restyle(string.Join(" ", args, 1, args.Length - 1));
                    }
                    else
                    {
                        MjolnirPlugin.FileLog("usage: mjolnir restyle <prefab> [scale]");
                    }
                    break;
                default:
                    MjolnirPlugin.FileLog(Help);
                    break;
            }
        }
    }
}
