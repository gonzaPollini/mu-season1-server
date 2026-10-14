namespace Season1.OpenMU.ItemConsumeActions;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Bless y Soul sobre anillos y colgantes, con la lógica de armas y sets pero chances propias (`Bless`, `Soul`): OpenMU los rechaza
/// (<c>CanLevelBeUpgraded</c> sólo acepta casillas hasta las alas y estos tienen nivel máximo 4). Para el
/// resto de los ítems se usa el handler original sin cambios. Life ya funciona: tienen la opción.
/// </summary>
/// <summary>
/// Las chances de los anillos y colgantes, aparte de las de armas y sets (que siguen siendo las de OpenMU).
/// </summary>
/// <param name="SuccessPercent">Probabilidad de subir un nivel.</param>
/// <param name="LuckBonusPercent">Lo que suma si el ítem tiene suerte.</param>
/// <param name="ResetToZeroFromLevel">Si falla desde este nivel vuelve a +0; por debajo, baja uno.</param>
internal sealed record JewelryRates(byte SuccessPercent, byte LuckBonusPercent, byte ResetToZeroFromLevel);

internal static class JewelryUpgrade
{
    // ===== Configuración: cuánto cuesta subir anillos y colgantes =====

    /// <summary>Bless: de +0 a +6. Falla 1 de cada 2 y, si falla, baja un nivel.</summary>
    public static readonly JewelryRates Bless = new(SuccessPercent: 50, LuckBonusPercent: 10, ResetToZeroFromLevel: byte.MaxValue);

    /// <summary>Soul: hasta +9. Sube 3 de cada 10 (4,5 con suerte); si falla desde +7 vuelve a +0, si no baja uno.</summary>
    public static readonly JewelryRates Soul = new(SuccessPercent: 30, LuckBonusPercent: 15, ResetToZeroFromLevel: 7);

    // ===================================================================

    /// <summary>Los anillos y colgantes comunes (grupo 13): Ice, Poison, Lighting, Fire, Earth, Wind, Magic, Water, Ability.</summary>
    private static readonly HashSet<short> Numbers = [8, 9, 12, 13, 21, 22, 23, 24, 25, 26, 27, 28];

    /// <summary>El tope de un arma o un set.</summary>
    private const byte MaximumLevel = 15;

    public static bool Applies(Item item) =>
        item.Definition is { Group: 13 } definition && Numbers.Contains(definition.Number);

    /// <summary>
    /// Lo de <c>UpgradeItemLevelJewelConsumeHandlerPlugIn.ModifyItem</c>: los niveles que acepta la joya salen
    /// de su configuración de OpenMU; las chances, de <paramref name="rates"/>.
    /// </summary>
    public static bool Upgrade(Item item, UpgradeItemLevelConfiguration config, JewelryRates rates, IRandomizer randomizer)
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

        var percent = rates.SuccessPercent
            + (item.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Luck) ? rates.LuckBonusPercent : 0);
        if (randomizer.NextRandomBool(percent))
        {
            item.Level += (byte)levelAmount;
            item.Durability = item.GetMaximumDurabilityOfOnePiece();
        }
        else
        {
            item.Level = item.Level >= rates.ResetToZeroFromLevel ? (byte)0 : (byte)Math.Max(item.Level - 1, 0);
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
            ? JewelryUpgrade.Upgrade(item, this.Configuration ??= (BlessJewelConsumeHandlerPlugInConfiguration)this.CreateDefaultConfig(), JewelryUpgrade.Bless, Rand.GetRandomizer())
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
            ? JewelryUpgrade.Upgrade(item, this.Configuration ??= (UpgradeItemLevelConfiguration)this.CreateDefaultConfig(), JewelryUpgrade.Soul, Rand.GetRandomizer())
            : base.ModifyItem(item, persistenceContext);
}
