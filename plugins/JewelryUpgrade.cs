namespace Season1.OpenMU.ItemConsumeActions;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Bless y Soul sobre anillos y colgantes, con la misma lógica que en armas y sets: OpenMU los rechaza
/// (<c>CanLevelBeUpgraded</c> sólo acepta casillas hasta las alas y estos tienen nivel máximo 4). Para el
/// resto de los ítems se usa el handler original sin cambios. Life ya funciona: tienen la opción.
/// </summary>
internal static class JewelryUpgrade
{
    /// <summary>Los anillos y colgantes comunes (grupo 13): Ice, Poison, Lighting, Fire, Earth, Wind, Magic, Water, Ability.</summary>
    private static readonly HashSet<short> Numbers = [8, 9, 12, 13, 21, 22, 23, 24, 25, 26, 27, 28];

    /// <summary>El tope de un arma o un set.</summary>
    private const byte MaximumLevel = 15;

    public static bool Applies(Item item) =>
        item.Definition is { Group: 13 } definition && Numbers.Contains(definition.Number);

    /// <summary>Lo de <c>UpgradeItemLevelJewelConsumeHandlerPlugIn.ModifyItem</c> con el tope de arriba.</summary>
    public static bool Upgrade(Item item, UpgradeItemLevelConfiguration config, IRandomizer randomizer)
    {
        if (item.Level < config.MinimumLevel)
        {
            return false;
        }

        var levelAmount = Math.Min(config.LevelAmount, Math.Min(config.MaximumLevel + 1, MaximumLevel) - item.Level);
        if (levelAmount <= 0)
        {
            return false;
        }

        var percent = config.SuccessRatePercentage
            + (item.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Luck) ? config.SuccessRateBonusWithLuckPercentage : 0);
        if (randomizer.NextRandomBool(percent))
        {
            item.Level += (byte)levelAmount;
            item.Durability = item.GetMaximumDurabilityOfOnePiece();
        }
        else
        {
            item.Level = item.Level >= config.ResetToLevel0WhenFailMinLevel ? (byte)0 : (byte)Math.Max(item.Level - 1, 0);
        }

        return true;
    }
}

[PlugIn]
[Guid("5b2c8e41-7d0a-4f6e-9a3b-1c4d5e6f7a81")]
[Display(Name = "Jewel of Bless (Season1)", Description = "Como el de OpenMU, y además sube anillos y colgantes.")]
public class BlessJewelSeason1 : BlessJewelConsumeHandlerPlugIn
{
    /// <inheritdoc />
    protected override bool ModifyItem(Item item, IContext persistenceContext) =>
        JewelryUpgrade.Applies(item)
            ? JewelryUpgrade.Upgrade(item, this.Configuration ??= (BlessJewelConsumeHandlerPlugInConfiguration)this.CreateDefaultConfig(), Rand.GetRandomizer())
            : base.ModifyItem(item, persistenceContext);
}

[PlugIn]
[Guid("5b2c8e41-7d0a-4f6e-9a3b-1c4d5e6f7a82")]
[Display(Name = "Jewel of Soul (Season1)", Description = "Como el de OpenMU, y además sube anillos y colgantes.")]
public class SoulJewelSeason1 : SoulJewelConsumeHandlerPlugIn
{
    /// <inheritdoc />
    protected override bool ModifyItem(Item item, IContext persistenceContext) =>
        JewelryUpgrade.Applies(item)
            ? JewelryUpgrade.Upgrade(item, this.Configuration ??= (UpgradeItemLevelConfiguration)this.CreateDefaultConfig(), Rand.GetRandomizer())
            : base.ModifyItem(item, persistenceContext);
}
