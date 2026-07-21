using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace AutoSlay;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("CountDown")]
    public int CountDown { get; set; } = 30;

    [JsonPropertyName("MinPlayer")]
    public int MinPlayer { get; set; } = 3;
}

public class AutoSlay : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "1v1 auto slay";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleDescription => "1v1 kaldiginda geri sayim baslar ve round bitirilir.";
    public override string ModuleAuthor => "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    public bool isActive = false;
    public bool isTimerActive = false;
    public int CountDown = 0;

    // LISANS
    public int lisans_bitis_yil = 2025;
    public int lisans_bitis_ay = 12;
    public int lisans_bitis_gun = 30;
    // LISANS

    public required Config Config { get; set; }

    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[1v1-auto-slay] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
            AddCommand("css_resetcd", "", (player, command) => ResetCD(player, command));

            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            isTimerActive = false;
        }
    }

    public void ResetCD(CCSPlayerController? player, CommandInfo command)
    {
        if (player.is_valid()) return;
        CountDown = Config.CountDown;
    }

    HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (!IsSupportedMap()) return HookResult.Continue;
        isActive = false;

        if (!isTimerActive)
        {
            isTimerActive = true;
            AddTimer(1.0f, () =>
            {
                var (t, ct) = GetPlayerCount();
                var (totalT, totalCT) = GetPlayerCount(false);

                if (!isActive)
                {
                    if ((totalT + totalCT) >= Config.MinPlayer && t == 1 && ct == 1)
                    {
                        CountDown = Config.CountDown;
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["1v1", CountDown]);
                        isActive = true;
                    }
                }
                else
                {
                    // Son kalan da ölürse iptal
                    if (t == 0 || ct == 0 || t >= 2 || ct >= 2)
                        isActive = false;

                    CountDown--;
                    if (CountDown <= 0)
                    {
                        var alivePlayers = Utilities.GetPlayers().Where(p => p.is_valid_alive() && (p.is_ct() || p.is_t())).ToList();
                        foreach (var p in alivePlayers)
                            p.CommitSuicide(false, true);

                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["end_round"]);
                        isActive = false;
                    }
                    else
                    if (CountDown % 5 == 0 && CountDown != Config.CountDown)
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["countdown", CountDown]);
                }

            }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
        }
        return HookResult.Continue;
    }

    // --- Helper Methods ---

    private (int T, int CT) GetPlayerCount(bool Alive = true)
    {
        int t = 0, ct = 0;
        foreach (var p in Utilities.GetPlayers())
        {
            if (Alive && !p.is_valid_alive()) continue;
            if (p.is_t()) t++;
            else if (p.is_ct()) ct++;
        }
        return (t, ct);
    }


    private static bool IsSupportedMap()
    {
        string map = NativeAPI.GetMapName();
        return map.Contains("awp_") || map.Contains("de_") || map.Contains("cs_");
    }

    private static string ReplaceTags(string message, bool remove = false)
    {
        string[] colorPatterns =
        {
            "{Default}", "{White}", "{Darkred}", "{Green}", "{LightYellow}", "{LightBlue}", "{Olive}", "{Lime}", "{Red}",
            "{LightPurple}", "{Purple}", "{Grey}", "{Yellow}", "{Gold}", "{Silver}", "{Blue}", "{DarkBlue}", "{BlueGrey}",
            "{Magenta}", "{LightRed}", "{Orange}"
        };
        string[] colorReplacements =
        {
            "\x01", "\x01", "\x02", "\x04", "\x09", "\x0B", "\x05", "\x06", "\x07", "\x03", "\x0E", "\x08", "\x09", "\x10",
            "\x0A", "\x0B", "\x0C", "\x0A", "\x0E", "\x0F", "\x10"
        };

        for (var i = 0; i < colorPatterns.Length; i++)
            message = "\u200e" + (remove
                ? message.Replace(colorPatterns[i], "")
                : message.Replace(colorPatterns[i], colorReplacements[i]));

        return message;
    }

    private static bool IsValidConfigString(string value) => !string.IsNullOrEmpty(value) && value != "-";
}

public static class Lib
{
    public static void Freeze(this CBasePlayerPawn pawn) => pawn.MoveType = MoveType_t.MOVETYPE_OBSOLETE;

    static public bool is_valid(this CCSPlayerController? player)
        => player != null && player.IsValid && player.PlayerPawn.IsValid;

    static public bool is_t(this CCSPlayerController? player)
        => player != null && is_valid(player) && player.TeamNum == 2;

    static public bool is_ct(this CCSPlayerController? player)
        => player != null && is_valid(player) && player.TeamNum == 3;

    static public bool is_valid_alive(this CCSPlayerController? player)
        => player != null && player.is_valid() && player.PawnIsAlive && player.get_health() > 0;

    static public CCSPlayerPawn? pawn(this CCSPlayerController? player)
        => (player == null || !player.is_valid()) ? null : player.PlayerPawn.Value;

    static public int get_health(this CCSPlayerController? player)
    {
        CCSPlayerPawn? pawn = player.pawn();
        return pawn?.Health ?? 100;
    }
}
