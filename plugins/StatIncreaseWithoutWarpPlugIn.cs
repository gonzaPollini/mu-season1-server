namespace Season1.OpenMU.RemoteView;

using System.Reflection;
using System.Runtime.InteropServices;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.GameServer;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.GameServer.RemoteView.Character;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Reemplaza al StatIncreaseResultPlugIn de OpenMU para el cliente de Season 6: al sumar varios puntos
/// (/addstr 100) manda una respuesta de +1 por punto en vez de teletransportar al jugador.
/// </summary>
/// <remarks>
/// El paquete original (C1 F3 06) solo informa de a un punto y el cliente suma 1 por cada uno, así que
/// N respuestas dejan los stats igual que el teletransporte, sin pantalla de carga. Por encima de
/// <see cref="MaxPacketsPerIncrease"/> puntos vuelve al comportamiento de OpenMU para no inundar la conexión.
/// </remarks>
[PlugIn]
[Display(Name = "Stat increase without warp (Season1)", Description = "Sends one +1 stat response per added point instead of warping the player.")]
[Guid("8f1d1c2e-5b7a-4c0e-9f43-2d6a1b7e9c51")]
[MinimumClient(6, 3, ClientLanguage.Invariant)]
public class StatIncreaseWithoutWarpPlugIn : IStatIncreaseResultPlugIn
{
    private const int MaxPacketsPerIncrease = 2000;

    /// <summary>`RemotePlayer.Connection` es internal en OpenMU; se lee una vez por reflexión.</summary>
    private static readonly PropertyInfo ConnectionProperty =
        typeof(RemotePlayer).GetProperty("Connection", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        ?? throw new MissingMemberException(nameof(RemotePlayer), "Connection");

    private readonly RemotePlayer _player;
    private readonly StatIncreaseResultPlugIn _original;

    public StatIncreaseWithoutWarpPlugIn(RemotePlayer player)
    {
        this._player = player;
        this._original = new StatIncreaseResultPlugIn(player);
    }

    /// <inheritdoc />
    public async ValueTask StatIncreaseResultAsync(AttributeDefinition attribute, ushort addedPoints)
    {
        if (addedPoints <= 1 || addedPoints > MaxPacketsPerIncrease
            || ConnectionProperty.GetValue(this._player) is not IConnection connection
            || this._player.Attributes is not { } attributes)
        {
            await this._original.StatIncreaseResultAsync(attribute, addedPoints).ConfigureAwait(false);
            return;
        }

        var dependentMaximum = attribute == Stats.BaseEnergy
            ? (ushort)attributes[Stats.MaximumMana]
            : attribute == Stats.BaseVitality ? (ushort)attributes[Stats.MaximumHealth] : default;
        var statType = attribute.GetStatType();
        var maximumShield = (ushort)attributes[Stats.MaximumShield];
        var maximumAbility = (ushort)attributes[Stats.MaximumAbility];

        for (var i = 0; i < addedPoints; i++)
        {
            await connection.SendCharacterStatIncreaseResponseAsync(true, statType, dependentMaximum, maximumShield, maximumAbility).ConfigureAwait(false);
        }
    }
}
