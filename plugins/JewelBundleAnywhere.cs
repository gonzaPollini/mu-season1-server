namespace Season1.OpenMU.MessageHandler;

using System.ComponentModel;
using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameServer.MessageHandler;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Reemplaza al JewelMixHandlerPlugIn de OpenMU (misma clave: <c>LahapJewelMixRequest.Code</c>). Con el interruptor
/// prendido empaqueta y desempaqueta gratis, haya o no Lahap (mismas validaciones que OpenMU, sin cobro). Apagado
/// rechaza todo con la marca <c>#jewelpack:off</c>, que el cliente muestra como mensaje rojo. Un Mix con joya 0xFF
/// es la consulta del cliente: se contesta <c>#jewelpack:on</c> o <c>#jewelpack:off</c>.
/// </summary>
[PlugIn]
[Guid("5b2c8e41-7d0a-4f6e-9a3b-1c4d5e6f7a91")]
[Display(Name = "Joyas: motor de empaquetado (Season1), NO DESACTIVAR", Description = "Atiende los pedidos de empaquetar y desempaquetar joyas (reemplaza al de OpenMU). Si se desactiva, el empaquetado deja de funcionar en todo el juego. Para apagar el empaquetado usar «Joyas: empaquetar y desempaquetar (Season1)».")]
public class JewelMixHandlerSeason1 : IPacketHandlerPlugIn
{
    /// <summary>Número de joya de la consulta del cliente; el real manda 0 a 9.</summary>
    private const byte QueryJewel = 0xFF;

    /// <inheritdoc />
    public bool IsEncryptionExpected => LahapJewelMixRequest.HeaderType >= 0xC3;

    /// <inheritdoc />
    public byte Key => LahapJewelMixRequest.Code;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        LahapJewelMixRequest message = packet;
        if (packet.Length < 6)
        {
            return;
        }

        var on = player.GameContext.PlugInManager.IsPlugInActive(typeof(JewelBundleSwitch));
        switch (message.Operation)
        {
            case LahapJewelMixRequest.MixType.Mix:
                if ((byte)message.Item == QueryJewel)
                {
                    // Consulta del cliente (nunca la manda el cliente real): contesta con la marca, no con un mensaje visible.
                    await player.ShowBlueMessageAsync(on ? "#jewelpack:on" : "#jewelpack:off").ConfigureAwait(false);
                    break;
                }

                if (!on)
                {
                    await player.ShowBlueMessageAsync("#jewelpack:off").ConfigureAwait(false);
                    break;
                }

                await StackForFreeAsync(player, (byte)message.Item, GetStackSize(message.MixingStackSize)).ConfigureAwait(false);
                break;
            case LahapJewelMixRequest.MixType.Unmix:
                if (!on)
                {
                    await player.ShowBlueMessageAsync("#jewelpack:off").ConfigureAwait(false);
                    break;
                }

                await UnstackForFreeAsync(player, (byte)message.Item, message.UnmixingSourceSlot).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentException($"The mix operation {message.Operation} is unknown.");
        }
    }

    private static byte GetStackSize(LahapJewelMixRequest.StackSize stackSize) => stackSize switch
    {
        LahapJewelMixRequest.StackSize.Ten => 10,
        LahapJewelMixRequest.StackSize.Twenty => 20,
        LahapJewelMixRequest.StackSize.Thirty => 30,
        _ => throw new InvalidEnumArgumentException(nameof(stackSize), (int)stackSize, typeof(LahapJewelMixRequest.StackSize)),
    };

    /// <summary>Lo de <c>ItemStackAction.StackItemsAsync</c> sin el cobro ni la exigencia de Lahap.</summary>
    private static async ValueTask StackForFreeAsync(Player player, byte stackId, byte stackSize)
    {
        var mix = player.GameContext.Configuration.JewelMixes.FirstOrDefault(m => m.Number == stackId);
        if (mix is null || player.Inventory is null)
        {
            return;
        }

        var jewels = player.Inventory.Items.Where(item => item.Definition == mix.SingleJewel).Take(stackSize).ToList();
        if (jewels.Count != stackSize)
        {
            // OpenMU no tiene mensaje rojo (sólo dorado, azul y de gremio); usa el azul para los errores.
            await player.ShowBlueMessageAsync("No podés empaquetar").ConfigureAwait(false);
            return;
        }

        foreach (var jewel in jewels)
        {
            await player.Inventory.RemoveItemAsync(jewel).ConfigureAwait(false);
            await player.PersistenceContext.DeleteAsync(jewel).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(jewel.ItemSlot)).ConfigureAwait(false);
        }

        var stacked = player.PersistenceContext.CreateNew<Item>();
        stacked.Definition = mix.MixedJewel;
        stacked.Level = (byte)((stackSize / 10) - 1);
        stacked.Durability = 1;
        await player.Inventory.AddItemAsync(stacked).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(stacked)).ConfigureAwait(false);
    }

    /// <summary>Lo de <c>ItemStackAction.UnstackItemsAsync</c> sin el cobro ni la exigencia de Lahap.</summary>
    private static async ValueTask UnstackForFreeAsync(Player player, byte stackId, byte slot)
    {
        var mix = player.GameContext.Configuration.JewelMixes.FirstOrDefault(m => m.Number == stackId);
        if (mix is null || player.Inventory is null)
        {
            return;
        }

        var stacked = player.Inventory.GetItem(slot);
        if (stacked is null || stacked.Definition != mix.MixedJewel)
        {
            await player.ShowBlueMessageAsync("No es un paquete de joyas").ConfigureAwait(false);
            return;
        }

        var pieces = (byte)((stacked.Level + 1) * 10);
        var freeSlots = player.Inventory.FreeSlots.Take(pieces).ToList();
        if (freeSlots.Count < pieces)
        {
            await player.ShowBlueMessageAsync("No hay espacio en el inventario").ConfigureAwait(false);
            return;
        }

        await player.Inventory.RemoveItemAsync(stacked).ConfigureAwait(false);
        await player.PersistenceContext.DeleteAsync(stacked).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(slot)).ConfigureAwait(false);
        foreach (var freeSlot in freeSlots)
        {
            var jewel = player.PersistenceContext.CreateNew<Item>();
            jewel.Definition = mix.SingleJewel;
            jewel.Durability = 1;
            jewel.ItemSlot = freeSlot;
            await player.Inventory.AddItemAsync(freeSlot, jewel).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(jewel)).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// El interruptor del panel de admin: prendido, empaquetar y desempaquetar joyas funciona gratis (inventario y Lahap);
/// apagado, todo el empaquetado se rechaza. Se toma en vivo. No hace nada más (el punto de extensión es sólo para figurar).
/// </summary>
[PlugIn]
[Guid("5b2c8e41-7d0a-4f6e-9a3b-1c4d5e6f7a92")]
[Display(Name = "Joyas: empaquetar y desempaquetar (Season1)", Description = "Prendido: clic derecho en el inventario y Lahap, gratis sin Lahap. Apagado: desactivado en todo el juego.")]
public class JewelBundleSwitch : IItemConsumingPlugIn
{
    /// <inheritdoc />
    public void ItemConsuming(Player player, Item item, Item? targetItem, CancelEventArgs eventArgs)
    {
    }
}
