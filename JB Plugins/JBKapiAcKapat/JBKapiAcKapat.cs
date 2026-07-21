using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBKapiAcKapat;
public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}
public class JBKapiAcKapat : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Kap Ac Kapat";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!kapiac - !kapikapat komutlari ile kapilar yonetilebilir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    CCSPlayerController iWarden = null;

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
            Console.WriteLine($"[Door Controller] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
			AddCommand("css_kapiac", "Kapilari acar.", (player, command) => KapiAc(player, command));
			AddCommand("css_kapikapat", "Kapilari kapatir", (player, command) => KapiKapat(player, command));
		}
	}

    public void KapiAc(CCSPlayerController? player, CommandInfo command)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.TeamNum == 3)
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["door_open", player.PlayerName]);

                force_ent_input("func_door", "Open");
                force_ent_input("func_movelinear", "Open");
                force_ent_input("func_door_rotating", "Open");
                force_ent_input("prop_door_rotating", "Open");
                force_ent_input("func_breakable", "Break");
            }
            else command.ReplyToCommand(ReplaceTags($"{Config.EklentiTagi} ", true) + Localizer["permissions"]);

        return;
    }

    public void KapiKapat(CCSPlayerController? player, CommandInfo command)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.TeamNum == 3)
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["door_close", player.PlayerName]);

                force_ent_input("func_door", "Close");
                force_ent_input("func_movelinear", "Close");
                force_ent_input("func_door_rotating", "Close");
                force_ent_input("prop_door_rotating", "Close");
            }
            else command.ReplyToCommand(ReplaceTags($"{Config.EklentiTagi} ", true) + Localizer["permissions"]);

        return;
    }

    static void force_ent_input(String name, String input)
    {
        // search for door entitys and open all of them!
        var target = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>(name);

        foreach (var ent in target)
        {
            if (!ent.IsValid)
            {
                continue;
            }

            ent.AcceptInput(input);
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
}