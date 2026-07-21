using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBAktiflik;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}

public class JBAktiflik : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Aktiflik";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "JB | !aktiflik ile aktif oyuncu kontrolu yapilir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    private static readonly bool?[] Aktif = new bool?[65];

    public float Geri_Sayim;
    public int TotalOnline;
    public CounterStrikeSharp.API.Modules.Timers.Timer? timer_ex;

    // LISANS
    public int lisans_bitis_yil = 2025; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 30; // Lisansin bitecegi gun
    // LISANS

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[Aktiflik] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
            AddCommand("css_aktiflik", "Aktiflik kontrolu.", (player, command) => Aktiflik(player, command));
            AddCommand("css_aktiflik0", "Aktiflik kontrol iptali.", (player, command) => Aktiflik0(player, command));

            AddCommand("css_aktif", "Aktif.", (player, command) => cmdAktif(player, command));

            RegisterListener<Listeners.OnTick>(() =>
            {
                if (Geri_Sayim >= 1)
                {
                    var text = Localizer["command_use_text"];
                    foreach (var p in Utilities.GetPlayers())
                        if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
                        {
                            if (Aktif[p.Index] != null && Aktif[p.Index] == true)
                                text = Localizer["command_used"];

                            p.PrintToCenterHtml($"<img src='https://www.plugincim.com/forum/data/assets/logo/yataybeyaz.png'/><br/><br/>" + Localizer["centerhtml_text", Convert.ToInt32(Geri_Sayim), text, TotalOnline] +"<br/><br/>");
                        }
                }
            });
        }
    }

    public void Aktiflik(CCSPlayerController? player, CommandInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct())
            {
                if (player != null)
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["activity_start", player.PlayerName]);

                foreach (var p in Utilities.GetPlayers())
                    if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
                        Aktif[p.Index] = false;

                Geri_Sayim = 15.0f;
                TotalOnline = 0;

                if (timer_ex != null) { timer_ex?.Kill(); }
                    timer_ex = AddTimer(1.0f, () =>
                    {
                        if (Geri_Sayim == 0.0)
                        {
                            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["activity_complete", TotalOnline]);

                            foreach (var p in Utilities.GetPlayers())
                                if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV && p.is_valid_alive() && p.is_t() && (Aktif[p.Index] == null || Aktif[p.Index] == false))
                                    p.CommitSuicide(false, true);

                            timer_ex?.Kill();
                            timer_ex = null;
                            return;
                        }
                        else
                            Geri_Sayim -= 1.0f;
                    }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }

    public void Aktiflik0(CCSPlayerController? player, CommandInfo info)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct())
            {
                if (timer_ex != null)
                    timer_ex?.Kill();

                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["activity_cancel", player.PlayerName]);
                Geri_Sayim = 0.0f;
                timer_ex = null;

            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }

    public void cmdAktif(CCSPlayerController? player, CommandInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
            if(player != null && player.is_t())
                if (Geri_Sayim >= 1 && (Aktif[player.Index] == null || Aktif[player.Index] == false))
                {
                    Aktif[player.Index] = true;
                    TotalOnline++;
                }
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
            if (!remove)
                message = "\u200e" + message.Replace(colorPatterns[i], colorReplacements[i]);
            else
                message = "\u200e" + message.Replace(colorPatterns[i], "");

        return message;

    }
    private static bool IsValidConfigString(string value) => !string.IsNullOrEmpty(value) && value != "-"; // This is a "lambda expression body method"
}






public static class Lib
{

    public static void Freeze(this CBasePlayerPawn pawn)
    {
        pawn.MoveType = MoveType_t.MOVETYPE_OBSOLETE;
    }

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