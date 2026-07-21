using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using System.Text.Json.Serialization;

namespace JBKomutcuIcon;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}

public class JBKomutcuIcon : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Komutcu Icon";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "Komutcunun kafasinda donen bir icon olur.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;

    CCSPlayerController? iWarden = null;
    CDynamicProp? entity = null;

    // LISANS
    public int lisans_bitis_yil = 2024; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 20; // Lisansin bitecegi gun
    // LISANS

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[Warden Icon] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
        }

        Config = config;
    }

    public override void Load(bool hotReload)
    {
        var dateTime = new DateTime(lisans_bitis_yil, lisans_bitis_ay, lisans_bitis_gun, 0, 0, 0, DateTimeKind.Utc);
        var dateWithOffset = new DateTimeOffset(dateTime).ToUniversalTime();
        long timestamp = dateWithOffset.ToUnixTimeSeconds();

        if (timestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            AddCommand("css_w", "", (player, command) => Warden(player, command));
            AddCommand("css_k", "", (player, command) => Warden(player, command));
            AddCommand("css_uw", "", (player, command) => UnWarden(player, command));

            RegisterEventHandler<EventPlayerTeam>(OnPlayerTeam);
            RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
            RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn, HookMode.Pre);
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Pre);

            RegisterListener<Listeners.OnTick>(() =>
            {
                if (iWarden != null && (!iWarden.is_valid() || !iWarden.is_ct()))
                    iWarden = null;
                else
                if (iWarden.is_valid_alive())
                {
                    if (entity == null) return;

                    var playerPawn = iWarden.PlayerPawn.Value;
                    var position = playerPawn.AbsOrigin!;

                    if (position.X != entity.AbsOrigin.X)
                        entity.Teleport(new CounterStrikeSharp.API.Modules.Utils.Vector(position.X, position.Y, position.Z + 100), new QAngle(), new CounterStrikeSharp.API.Modules.Utils.Vector());
                }
            });
        }
    }

    public void Warden(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        if (NativeAPI.GetMapName().Contains("jb_"))
            if ((iWarden == null || !iWarden.is_valid() || !iWarden.is_ct()) && player.is_valid() && player.is_ct())
                CreateIcon(player);

        return;
    }

    public void UnWarden(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        if (NativeAPI.GetMapName().Contains("jb_"))
            if (iWarden == player)
            {
                iWarden = null;
                if (entity != null && entity.IsValid)
                    entity.AcceptInput("kill");
            }

        return;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            CCSPlayerController player = @event.Userid;

            if (player != null && iWarden == player)
                iWarden = null;

            return HookResult.Continue;
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            CCSPlayerController player = @event.Userid;

            if (player != null && iWarden == player)
            {
                if (@event.Team != 3)
                    iWarden = null;

                return HookResult.Continue;
            }
        }

        return HookResult.Continue;
    }


    HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            CCSPlayerController player = @event.Userid;

            if (player != null && iWarden == player)
                CreateIcon(player);
        }

        return HookResult.Continue;
    }

    HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            CCSPlayerController player = @event.Userid;

            if (player != null && iWarden == player)
                if (entity != null && entity.IsValid)
                    entity.AcceptInput("kill");
        }

        return HookResult.Continue;
    }

    public void CreateIcon(CCSPlayerController player)
    {
        if (player.is_valid_alive())
        {
            if (entity != null && entity.IsValid)
                entity.AcceptInput("kill");

            entity = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic_override");
            if (entity is null || !entity.IsValid)
                return;

            entity.SetModel("models/coop/challenge_coin.vmdl");
            entity.Entity.Name = "Komutcu Icon";
            entity.IdleAnim = "challenge_coin_idle";
            entity.DispatchSpawn();

            iWarden = player;
        }
    }
}

public static class Lib
{
    static public bool is_valid(this CCSPlayerController? player)
    {
        return player != null && player.IsValid && player.PlayerPawn.IsValid;
    }

    static public bool is_t(this CCSPlayerController? player)
    {
        return player != null && is_valid(player) && player.TeamNum == 2;
    }

    static public bool is_ct(this CCSPlayerController? player)
    {
        return player != null && is_valid(player) && player.TeamNum == 3;
    }

    // yes i know the null check is redundant but C# is dumb
    static public bool is_valid_alive(this CCSPlayerController? player)
    {
        return player != null && player.is_valid() && player.PawnIsAlive && player.get_health() > 0;
    }

    static public CCSPlayerPawn? pawn(this CCSPlayerController? player)
    {
        if (player == null || !player.is_valid())
        {
            return null;
        }

        CCSPlayerPawn? pawn = player.PlayerPawn.Value;

        return pawn;
    }

    static public int get_health(this CCSPlayerController? player)
    {
        CCSPlayerPawn? pawn = player.pawn();

        if (pawn == null)
        {
            return 100;
        }

        return pawn.Health;
    }
}