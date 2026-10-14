namespace Season1.OpenMU.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Comandos de stats que reescriben la línea a <c>/add &lt;stat&gt; &lt;cantidad&gt;</c> y la pasan al
/// comando <c>/add</c> de OpenMU. A diferencia de los de OpenMU (<c>/addstr</c> y compañía, que vienen
/// desactivados), estos no son <see cref="IDisabledByDefault"/>: quedan activos al levantar el server,
/// sin tocar la base. Sin clase base propia: OpenMU registraría también esa (hereda <c>[PlugIn]</c>).
/// </summary>
internal static class StatAlias
{
    public static string Rewrite(string command, string alias, string stat) => $"/add {stat}{command[alias.Length..]}";
}

[PlugIn]
[Guid("6a0f3c1e-2b4d-4e8a-9c71-1d2e3f405a01")]
[Display(Name = "/addstr (Season1)", Description = "Suma puntos a Fuerza.")]
public class AddStrSeason1 : AddStatChatCommandPlugIn
{
    /// <inheritdoc />
    public override string Key => "/addstr";

    /// <inheritdoc />
    public override ValueTask HandleCommandAsync(Player player, string command) =>
        base.HandleCommandAsync(player, StatAlias.Rewrite(command, "/addstr", "str"));
}

[PlugIn]
[Guid("6a0f3c1e-2b4d-4e8a-9c71-1d2e3f405a02")]
[Display(Name = "/addagi (Season1)", Description = "Suma puntos a Agilidad.")]
public class AddAgiSeason1 : AddStatChatCommandPlugIn
{
    /// <inheritdoc />
    public override string Key => "/addagi";

    /// <inheritdoc />
    public override ValueTask HandleCommandAsync(Player player, string command) =>
        base.HandleCommandAsync(player, StatAlias.Rewrite(command, "/addagi", "agi"));
}

[PlugIn]
[Guid("6a0f3c1e-2b4d-4e8a-9c71-1d2e3f405a03")]
[Display(Name = "/addvit (Season1)", Description = "Suma puntos a Vitalidad.")]
public class AddVitSeason1 : AddStatChatCommandPlugIn
{
    /// <inheritdoc />
    public override string Key => "/addvit";

    /// <inheritdoc />
    public override ValueTask HandleCommandAsync(Player player, string command) =>
        base.HandleCommandAsync(player, StatAlias.Rewrite(command, "/addvit", "vit"));
}

[PlugIn]
[Guid("6a0f3c1e-2b4d-4e8a-9c71-1d2e3f405a04")]
[Display(Name = "/addene (Season1)", Description = "Suma puntos a Energía.")]
public class AddEneSeason1 : AddStatChatCommandPlugIn
{
    /// <inheritdoc />
    public override string Key => "/addene";

    /// <inheritdoc />
    public override ValueTask HandleCommandAsync(Player player, string command) =>
        base.HandleCommandAsync(player, StatAlias.Rewrite(command, "/addene", "ene"));
}

[PlugIn]
[Guid("6a0f3c1e-2b4d-4e8a-9c71-1d2e3f405a05")]
[Display(Name = "/addcmd (Season1)", Description = "Suma puntos a Comando (Dark Lord).")]
public class AddCmdSeason1 : AddStatChatCommandPlugIn
{
    /// <inheritdoc />
    public override string Key => "/addcmd";

    /// <inheritdoc />
    public override ValueTask HandleCommandAsync(Player player, string command) =>
        base.HandleCommandAsync(player, StatAlias.Rewrite(command, "/addcmd", "cmd"));
}

[PlugIn]
[Guid("6a0f3c1e-2b4d-4e8a-9c71-1d2e3f405a11")]
[Display(Name = "/fuerza (Season1)", Description = "Suma puntos a Fuerza.")]
public class FuerzaSeason1 : AddStatChatCommandPlugIn
{
    /// <inheritdoc />
    public override string Key => "/fuerza";

    /// <inheritdoc />
    public override ValueTask HandleCommandAsync(Player player, string command) =>
        base.HandleCommandAsync(player, StatAlias.Rewrite(command, "/fuerza", "str"));
}

[PlugIn]
[Guid("6a0f3c1e-2b4d-4e8a-9c71-1d2e3f405a12")]
[Display(Name = "/agilidad (Season1)", Description = "Suma puntos a Agilidad.")]
public class AgilidadSeason1 : AddStatChatCommandPlugIn
{
    /// <inheritdoc />
    public override string Key => "/agilidad";

    /// <inheritdoc />
    public override ValueTask HandleCommandAsync(Player player, string command) =>
        base.HandleCommandAsync(player, StatAlias.Rewrite(command, "/agilidad", "agi"));
}

[PlugIn]
[Guid("6a0f3c1e-2b4d-4e8a-9c71-1d2e3f405a13")]
[Display(Name = "/vitalidad (Season1)", Description = "Suma puntos a Vitalidad.")]
public class VitalidadSeason1 : AddStatChatCommandPlugIn
{
    /// <inheritdoc />
    public override string Key => "/vitalidad";

    /// <inheritdoc />
    public override ValueTask HandleCommandAsync(Player player, string command) =>
        base.HandleCommandAsync(player, StatAlias.Rewrite(command, "/vitalidad", "vit"));
}

[PlugIn]
[Guid("6a0f3c1e-2b4d-4e8a-9c71-1d2e3f405a14")]
[Display(Name = "/energia (Season1)", Description = "Suma puntos a Energía.")]
public class EnergiaSeason1 : AddStatChatCommandPlugIn
{
    /// <inheritdoc />
    public override string Key => "/energia";

    /// <inheritdoc />
    public override ValueTask HandleCommandAsync(Player player, string command) =>
        base.HandleCommandAsync(player, StatAlias.Rewrite(command, "/energia", "ene"));
}

[PlugIn]
[Guid("6a0f3c1e-2b4d-4e8a-9c71-1d2e3f405a15")]
[Display(Name = "/comando (Season1)", Description = "Suma puntos a Comando (Dark Lord).")]
public class ComandoSeason1 : AddStatChatCommandPlugIn
{
    /// <inheritdoc />
    public override string Key => "/comando";

    /// <inheritdoc />
    public override ValueTask HandleCommandAsync(Player player, string command) =>
        base.HandleCommandAsync(player, StatAlias.Rewrite(command, "/comando", "cmd"));
}
