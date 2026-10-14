namespace Season1.OpenMU.ItemConsumeActions;

using System.ComponentModel;
using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Bless y Soul sobre anillos y colgantes, con la lógica de armas y sets pero chances propias (`Bless`, `Soul`): OpenMU los rechaza
/// (<c>CanLevelBeUpgraded</c> sólo acepta casillas hasta las alas y estos tienen nivel máximo 4). Para el
/// resto de los ítems se usa el handler original sin cambios. Life (la opción) también tiene chance propia.
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
    // Las chances viven en la configuración del interruptor (JewelryUpgradeConfiguration), editable en el panel.
    private static JewelryUpgradeConfiguration Rates => JewelryUpgradeSwitch.Current ?? JewelryUpgradeConfiguration.Default;

    public static JewelryRates Bless => new(Rates.BlessSuccessPercent, Rates.BlessLuckBonusPercent, Rates.BlessResetToZeroFromLevel);

    public static JewelryRates Soul => new(Rates.SoulSuccessPercent, Rates.SoulLuckBonusPercent, Rates.SoulResetToZeroFromLevel);

    /// <summary>Los anillos y colgantes comunes (grupo 13): Ice, Poison, Lighting, Fire, Earth, Wind, Magic, Water, Ability.</summary>
    private static readonly HashSet<short> Numbers = [8, 9, 12, 13, 21, 22, 23, 24, 25, 26, 27, 28];

    /// <summary>El tope de un arma o un set.</summary>
    private const byte MaximumLevel = 15;

    /// <summary>El juego del uso en curso: lo fija cada handler para que <see cref="Applies"/> consulte el interruptor.</summary>
    public static readonly AsyncLocal<IGameContext?> Context = new();

    /// <summary>Un anillo o colgante común, con el interruptor <see cref="JewelryUpgradeSwitch"/> prendido en el panel.</summary>
    public static bool Applies(Item item) =>
        item.Definition is { Group: 13 } definition && Numbers.Contains(definition.Number)
        && Context.Value?.PlugInManager.IsPlugInActive(typeof(JewelryUpgradeSwitch)) == true;

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

    /// <summary>Lo de <c>ItemUpgradeConsumeHandlerPlugIn</c> para la opción (Life), con <c>LifeSuccessPercent</c>.</summary>
    public static bool UpgradeOption(Item item, IContext persistenceContext)
    {
        var options = item.Definition!.PossibleItemOptions.SelectMany(o => o.PossibleOptions)
            .Where(o => o.OptionType == ItemOptionTypes.Option).ToList();
        if (options.Count == 0)
        {
            return false;
        }

        var current = item.ItemOptions.FirstOrDefault(o => o.ItemOption?.OptionType == ItemOptionTypes.Option);
        if (current is not null)
        {
            var higherPossible = current.ItemOption?.LevelDependentOptions.Any(o => o.Level > current.Level && o.RequiredItemLevel <= item.Level) ?? false;
            if (!higherPossible)
            {
                return false;
            }

            if (Rand.NextRandomBool((int)Rates.LifeSuccessPercent))
            {
                current.Level++;
            }
            else
            {
                item.ItemOptions.Remove(current);
            }

            return true;
        }

        if (Rand.NextRandomBool((int)Rates.LifeSuccessPercent))
        {
            var possible = options.Where(o => !o.LevelDependentOptions.Any() || o.LevelDependentOptions.Any(l => l.RequiredItemLevel <= item.Level)).ToList();
            if (possible.Count == 0)
            {
                return false;
            }

            var link = persistenceContext.CreateNew<ItemOptionLink>();
            link.ItemOption = possible.SelectRandom();
            link.Level = 1;
            item.ItemOptions.Add(link);
        }

        return true;
    }
}

[PlugIn]
[Guid("5b2c8e41-7d0a-4f6e-9a3b-1c4d5e6f7a81")]
[Display(Name = "Jewel of Bless (Season1)", Description = "El de OpenMU; sube anillos y colgantes si está prendida «Joyería: subir anillos y colgantes». No desactivar.")]
public class BlessJewelSeason1 : BlessJewelConsumeHandlerPlugIn
{
    /// <inheritdoc />
    public override ValueTask<bool> ConsumeItemAsync(Player player, Item item, Item? targetItem, FruitUsage fruitUsage)
    {
        JewelryUpgrade.Context.Value = player.GameContext;
        return base.ConsumeItemAsync(player, item, targetItem, fruitUsage);
    }

    /// <inheritdoc />
    protected override bool ModifyItem(Item item, IContext persistenceContext) =>
        JewelryUpgrade.Applies(item)
            ? JewelryUpgrade.Upgrade(item, this.Configuration ??= (BlessJewelConsumeHandlerPlugInConfiguration)this.CreateDefaultConfig(), JewelryUpgrade.Bless, Rand.GetRandomizer())
            : base.ModifyItem(item, persistenceContext);
}

[PlugIn]
[Guid("5b2c8e41-7d0a-4f6e-9a3b-1c4d5e6f7a82")]
[Display(Name = "Jewel of Soul (Season1)", Description = "El de OpenMU; sube anillos y colgantes si está prendida «Joyería: subir anillos y colgantes». No desactivar.")]
public class SoulJewelSeason1 : SoulJewelConsumeHandlerPlugIn
{
    /// <inheritdoc />
    public override ValueTask<bool> ConsumeItemAsync(Player player, Item item, Item? targetItem, FruitUsage fruitUsage)
    {
        JewelryUpgrade.Context.Value = player.GameContext;
        return base.ConsumeItemAsync(player, item, targetItem, fruitUsage);
    }

    /// <inheritdoc />
    protected override bool ModifyItem(Item item, IContext persistenceContext) =>
        JewelryUpgrade.Applies(item)
            ? JewelryUpgrade.Upgrade(item, this.Configuration ??= (UpgradeItemLevelConfiguration)this.CreateDefaultConfig(), JewelryUpgrade.Soul, Rand.GetRandomizer())
            : base.ModifyItem(item, persistenceContext);
}

[PlugIn]
[Guid("5b2c8e41-7d0a-4f6e-9a3b-1c4d5e6f7a83")]
[Display(Name = "Jewel of Life (Season1)", Description = "El de OpenMU; chance propia en anillos y colgantes si está prendida «Joyería: subir anillos y colgantes». No desactivar.")]
public class LifeJewelSeason1 : LifeJewelConsumeHandlerPlugIn
{
    /// <inheritdoc />
    public override ValueTask<bool> ConsumeItemAsync(Player player, Item item, Item? targetItem, FruitUsage fruitUsage)
    {
        JewelryUpgrade.Context.Value = player.GameContext;
        return base.ConsumeItemAsync(player, item, targetItem, fruitUsage);
    }

    /// <inheritdoc />
    protected override bool ModifyItem(Item item, IContext persistenceContext) =>
        JewelryUpgrade.Applies(item) ? JewelryUpgrade.UpgradeOption(item, persistenceContext) : base.ModifyItem(item, persistenceContext);
}

/// <summary>
/// El interruptor del panel de admin: prendido, Bless, Soul y Life suben anillos y colgantes; apagado, las tres joyas
/// hacen lo de OpenMU. Se toma en vivo, sin reiniciar. No hace nada más (el punto de extensión es sólo para figurar).
/// </summary>
[PlugIn]
[Guid("5b2c8e41-7d0a-4f6e-9a3b-1c4d5e6f7a90")]
[Display(Name = "Joyería: subir anillos y colgantes con Bless, Soul y Life (Season1)", Description = "Prendido: las tres joyas suben anillos y colgantes comunes, con las chances de JewelryUpgrade.cs. Apagado: como OpenMU.")]
public class JewelryUpgradeSwitch : IItemConsumingPlugIn, ISupportCustomConfiguration<JewelryUpgradeConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <summary>Lo último que puso el panel; OpenMU lo asigna al arrancar y cada vez que se guarda.</summary>
    public static JewelryUpgradeConfiguration? Current { get; private set; }

    /// <inheritdoc />
    public JewelryUpgradeConfiguration? Configuration
    {
        get => Current;
        set => Current = value;
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new JewelryUpgradeConfiguration();

    /// <inheritdoc />
    public void ItemConsuming(Player player, Item item, Item? targetItem, CancelEventArgs eventArgs)
    {
    }
}

/// <summary>
/// Cuánto cuesta subir anillos y colgantes, editable en el panel de admin (interruptor «Joyería…»), en vivo.
/// Armas y sets no cambian: usan la configuración de OpenMU.
/// </summary>
public class JewelryUpgradeConfiguration
{
    internal static readonly JewelryUpgradeConfiguration Default = new();

    [Display(Name = "Bless: % de éxito", Description = "Sube de +0 a +6. 100 = siempre.")]
    public byte BlessSuccessPercent { get; set; } = 100;

    [Display(Name = "Bless: % extra con suerte")]
    public byte BlessLuckBonusPercent { get; set; }

    [Display(Name = "Bless: si falla desde este nivel vuelve a +0", Description = "Por debajo baja un nivel. 255 = nunca vuelve a +0.")]
    public byte BlessResetToZeroFromLevel { get; set; } = byte.MaxValue;

    [Display(Name = "Soul: % de éxito", Description = "Sube hasta +9.")]
    public byte SoulSuccessPercent { get; set; } = 30;

    [Display(Name = "Soul: % extra con suerte")]
    public byte SoulLuckBonusPercent { get; set; } = 15;

    [Display(Name = "Soul: si falla desde este nivel vuelve a +0", Description = "Por debajo baja un nivel.")]
    public byte SoulResetToZeroFromLevel { get; set; } = 7;

    [Display(Name = "Life: % de éxito", Description = "Agrega o sube la opción; si falla, el ítem la pierde.")]
    public byte LifeSuccessPercent { get; set; } = 10;
}
