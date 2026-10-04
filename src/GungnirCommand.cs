using Jotunn.Entities;

namespace Gungnir
{
    /// <summary>
    /// F5 console command: `gungnir give` puts the spear into the local player's inventory.
    /// </summary>
    public class GungnirCommand : ConsoleCommand
    {
        public override string Name => "gungnir";
        public override string Help => "gungnir give - add Gungnir to your inventory";

        public override void Run(string[] args)
        {
            if (args.Length >= 1 && args[0].ToLowerInvariant() == "give")
            {
                GungnirPlugin.GiveToLocalPlayer();
            }
            else
            {
                GungnirPlugin.FileLog(Help);
            }
        }
    }
}
