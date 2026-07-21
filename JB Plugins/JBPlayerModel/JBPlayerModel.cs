using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBPlayerModel;
public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("warden_model")]
    public string komutcu_modeli { get; set; }

    [JsonPropertyName("ct_model")]
    public string gardiyan_modeli { get; set; }

    [JsonPropertyName("t_model")]
    public string mahkum_modeli { get; set; }
}
public class JBPlayerModel : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Player Model";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "Oyuncularin baslangic modellerini degistirir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    CCSPlayerController? iWarden = null;

    // LISANS
    public int lisans_bitis_yil = 2024; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 30; // Lisansin bitecegi gun
    // LISANS

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[Player Model] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
        }

        Config = config;
        Stringlocalizer = Localizer;
    }

    public override void Load(bool hotReload)
    {
        var dateTime = new DateTime(lisans_bitis_yil, lisans_bitis_ay, lisans_bitis_gun, 0, 0, 0, DateTimeKind.Utc);
        var dateWithOffset = new DateTimeOffset(dateTime).ToUniversalTime();
        long timestamp = dateWithOffset.ToUnixTimeSeconds();

        if (timestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            // KOMUTÇU SİSTEMİ
            AddCommand("css_w", "", (player, command) => Warden(player, command));
            AddCommand("css_uw", "", (player, command) => UnWarden(player, command));

            RegisterEventHandler<EventPlayerTeam>(OnPlayerTeam);
            RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

            AddTimer(1.0f, () =>
            {
                if (iWarden != null && (!iWarden.is_valid() || iWarden.is_t()))
                    iWarden = null;
            }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
            // KOMUTÇU SİSTEMİ

            RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn, HookMode.Pre);
        }
    }

    public void Warden(CCSPlayerController? player, CommandInfo command)
    {
        if ((iWarden == null || !iWarden.is_valid() || iWarden.is_t()) && player.is_valid() && player.is_ct())
            iWarden = player;

        return;
    }

    public void UnWarden(CCSPlayerController? player, CommandInfo command)
    {
        if (iWarden == player)
            iWarden = null;

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
            CCSPlayerController? player = @event.Userid;

            if (player != null && player.IsValid)
            {
                AddTimer(0.5f, () =>
                {
                    if (iWarden == player && !string.IsNullOrEmpty(Config.komutcu_modeli))
                        player.PlayerPawn.Value!.SetModel(@"" + Config.komutcu_modeli);
                    else
                    if (player.is_t() && !string.IsNullOrEmpty(Config.mahkum_modeli))
                        player.PlayerPawn.Value!.SetModel(@"" + Config.mahkum_modeli);
                    else
                    if (player.is_ct() && !string.IsNullOrEmpty(Config.gardiyan_modeli))
                        player.PlayerPawn.Value!.SetModel(@"" + Config.gardiyan_modeli);
                }, TimerFlags.STOP_ON_MAPCHANGE);
            }
        }

        return HookResult.Continue;
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