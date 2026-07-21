using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace SohbetTemizleyici;
public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}
public class SohbetTemizleyici : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "Sohbet Temizleyici";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!cc - !temizle komutlari ile sohbet temizlenir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

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
            Console.WriteLine($"[ST] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
			AddCommand("css_cc", "Sohbet temizleyici.", (player, command) => Temizle(player, command));
			AddCommand("css_clear", "Sohbet temizleyici.", (player, command) => Temizle(player, command));
			AddCommand("css_temizle", "Sohbet temizleyici.", (player, command) => Temizle(player, command));

			Console.WriteLine("[Genel - RR - www.plugincim.com] Aktif edildi.");
		}
	}

    [RequiresPermissions("@css/generic")]
    public void Temizle(CCSPlayerController? player, CommandInfo command)
    {
        if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            for (int i = 0; i < 100; i++)
                Server.PrintToChatAll(" ");

            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["chat_clear", player.PlayerName]);
        }
        else
        {
            command.ReplyToCommand(ReplaceTags($"{Config.EklentiTagi} ", true) + Localizer["permissions"]);
            return;
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