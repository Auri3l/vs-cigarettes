using Cigarettes.Blocks;
using Cigarettes.Items;
using Vintagestory.API.Common;

namespace Cigarettes;

public class CigarettesModSystem : ModSystem
{
    public const string ModId = "cigarettes";

    // Called on server and client, registers every class the JSON assets refer to by name
    public override void Start(ICoreAPI api)
    {
        api.RegisterItemClass(ModId + ".ItemCigarette", typeof(ItemCigarette));
        api.RegisterItemClass(ModId + ".ItemPipe", typeof(ItemPipe));
        api.RegisterItemClass(ModId + ".ItemCigarettePack", typeof(ItemCigarettePack));
        api.RegisterItemClass(ModId + ".ItemVape", typeof(ItemVape));
        api.RegisterItemClass(ModId + ".ItemButt", typeof(ItemButt));
        api.RegisterItemClass(ModId + ".ItemIgniter", typeof(ItemIgniter));
        api.RegisterEntity(ModId + ".EntityFlickedButt", typeof(Entities.EntityFlickedButt));

        api.RegisterBlockClass(ModId + ".BlockDryingRack", typeof(BlockDryingRack));
        api.RegisterBlockEntityClass(ModId + ".DryingRack", typeof(BlockEntityDryingRack));

        api.RegisterBlockClass(ModId + ".BlockAshtray", typeof(BlockAshtray));
        api.RegisterBlockEntityClass(ModId + ".Ashtray", typeof(BlockEntityAshtray));

        api.RegisterBlockClass(ModId + ".BlockWorkstation", typeof(BlockWorkstation));
        api.RegisterBlockEntityClass(ModId + ".TobaccoCutter", typeof(BlockEntityTobaccoCutter));
        api.RegisterBlockEntityClass(ModId + ".RollingTable", typeof(BlockEntityRollingTable));
    }
}
