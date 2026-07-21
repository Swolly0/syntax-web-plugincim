using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using System.Runtime.InteropServices;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBCTRev;
public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[BabunGang]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
	
    [JsonPropertyName("respawn_limit")]
    public int respawn_hakki { get; set; } = 4;
}

public class JBCTRev : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB CT Rev Menu";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "JB | Komutcunun korumalarini belirli bir limit icerisinde canlandirabilir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    private static int Kalan_Hak = 0;

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
            Console.WriteLine($"[CT Respawn Menu] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
            AddCommand("css_ctres", "CT Res", (player, command) => CT_Res(player, command));
            AddCommand("css_resct", "CT Res", (player, command) => CT_Res(player, command));
            AddCommand("css_haksifirla", "CT rev hakki sifirlama.", (player, command) => Hak_Sifirla(player, command));
			AddCommand("css_haksifir", "CT rev hakki sifirlama.", (player, command) => Hak_Sifirla(player, command));

            AddCommand("css_hook", "Hook komutu.", (player, command) => HookKomut(player, command));


            RegisterEventHandler<EventRoundStart>(OnRoundStart);
		}
	}

    public void HookKomut(CCSPlayerController? player, CommandInfo commandInfo)
    {
        player.PrintToChat(" \x0bHook komutu: \x10 bind v +hook; alias +hook hook1; alias -hook hook0;");
        player.PrintToChat(" \x0bHook komutu: \x10 bind v +hook; alias +hook hook1; alias -hook hook0;");

        player.PrintToConsole("Hook komutu:  bind v +hook; alias +hook hook1; alias -hook hook0;");
        player.PrintToConsole("Hook komutu:  bind v +hook; alias +hook hook1; alias -hook hook0;");
    }

    public void CT_Res(CCSPlayerController? player, CommandInfo commandInfo)
    {	
		if(player != null && NativeAPI.GetMapName().Contains("jb_")){
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct())
            {
                if (Kalan_Hak >= 1)
                {
                    Kalan_Hak--;
                    Server.PrintToChatAll($" \x0b[CT RES]\x04 {player.PlayerName} \x01tarafından ctres \x04uyguladı. {Kalan_Hak} \x01respawn hakkı kaldı.");

                    foreach (var p in Utilities.GetPlayers())
                        if (p.is_valid() && p.is_ct() && !p.is_valid_alive())
                        {
                            var playerPawn = p.PlayerPawn.Value;
                            if (playerPawn == null) return;
                            p.Respawn();
                        }

                    return;
                }

            } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);			
		}
    }
	
	public void Hak_Sifirla(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if(player != null && NativeAPI.GetMapName().Contains("jb_"))
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct())
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["respawn_reset", player.PlayerName]);
				Kalan_Hak = Config.respawn_hakki;
            } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
    }	
	
    HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
		if(NativeAPI.GetMapName().Contains("jb_"))
        {
			Kalan_Hak = Config.respawn_hakki;
		}

        return HookResult.Continue;
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
            if(!remove)
                message = "\u200e" + message.Replace(colorPatterns[i], colorReplacements[i]);
            else
                message = "\u200e" + message.Replace(colorPatterns[i], "");

        return message;
    }
    private static bool IsValidConfigString(string value) => !string.IsNullOrEmpty(value) && value != "-"; // This is a "lambda expression body method"

    private static string GetSignature()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"\x44\x88\x4C\x24\x2A\x55\x57"
            : @"\x55\x48\x89\xE5\x41\x57\x41\x56\x41\x55\x41\x54\x49\x89\xFC\x53\x48\x89\xF3\x48\x81\xEC\xC8\x00\x00\x00";
    }
}






public static class Lib{

    static public bool is_valid(this CCSPlayerController? player)
    {
        return player != null && player.IsValid &&  player.PlayerPawn.IsValid;
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
        if(player == null || !player.is_valid())
        {
            return null;
        }

        CCSPlayerPawn? pawn = player.PlayerPawn.Value;

        return pawn;
    }	
	
    static public int get_health(this CCSPlayerController? player)
    {
        CCSPlayerPawn? pawn = player.pawn();

        if(pawn == null)
        {
            return 100;
        }

        return pawn.Health;
    }	
}