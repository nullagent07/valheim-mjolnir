using Jotunn.Entities;

namespace Mjolnir
{
    /// <summary>
    /// F5 console command: `mjolnir give` / `mjolnir clean`.
    /// </summary>
    public class MjolnirCommand : ConsoleCommand
    {
        public override string Name => "mjolnir";
        public override string Help => "mjolnir give - add Mjolnir to your inventory; mjolnir clean - remove leftover drops";

        public override void Run(string[] args)
        {
            string sub = args.Length >= 1 ? args[0].ToLowerInvariant() : "";
            switch (sub)
            {
                case "give":
                    MjolnirPlugin.GiveToLocalPlayer();
                    break;
                case "clean":
                    MjolnirPlugin.CleanWorldDrops();
                    break;
                default:
                    MjolnirPlugin.FileLog(Help);
                    break;
            }
        }
    }
}
