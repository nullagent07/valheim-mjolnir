using Jotunn.Entities;

namespace Gungnir
{
    /// <summary>
    /// F5 console command: `gungnir give` puts the spear into the local player's inventory.
    /// </summary>
    public class GungnirCommand : ConsoleCommand
    {
        public override string Name => "gungnir";
        public override string Help => "gungnir give - add Gungnir to your inventory; gungnir clean - remove world drops of Gungnir";

        public override void Run(string[] args)
        {
            string sub = args.Length >= 1 ? args[0].ToLowerInvariant() : "";
            switch (sub)
            {
                case "give":
                    GungnirPlugin.GiveToLocalPlayer();
                    break;
                case "clean":
                    GungnirPlugin.CleanWorldDrops();
                    break;
                default:
                    GungnirPlugin.FileLog(Help);
                    break;
            }
        }
    }
}
