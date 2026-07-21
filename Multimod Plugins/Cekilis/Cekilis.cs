using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace Cekilis;
public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}
public class Cekilis : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "Cekilis";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!cekt !cekct komutlari ile rastgele bir oyuncu secebilirsiniz.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    // LISANS
    public int lisans_bitis_yil = 2025; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 21; // Lisansin bitecegi gun
    // LISANS

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[Cekilis] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
            AddCommand("css_cek", "Rastgele oyuncu cek.", (player, command) => Cek(player, command));
            AddCommand("css_cekt", "Rastgele T cek.", (player, command) => CekT(player, command));
            AddCommand("css_cekct", "Rastgele CT cek.", (player, command) => CekCT(player, command));
        }
    }

    public void Cek(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null)
            if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
            {
                CCSPlayerController? randomplayer;
                do
                {
                    randomplayer = null;
                    int rand = new Random().Next(0, Server.MaxPlayers + 1);
                    randomplayer = Utilities.GetPlayerFromSlot(rand);
                }
                while (randomplayer == null || !randomplayer.IsValid || randomplayer.IsBot);

                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["cek_command_used", player.PlayerName]);
                Server.PrintToChatAll(Localizer["random_chosen", randomplayer.PlayerName]);
            }
            else
            {
                command.ReplyToCommand(ReplaceTags($"{Config.EklentiTagi} ", true) + Localizer["permissions"]);
                return;
            }
    }

    public void CekT(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null)
            if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
            {
                if (get_player_count(2) >= 2)
                {
                    CCSPlayerController? randomplayer;
                    do
                    {
                        randomplayer = null;
                        int rand = new Random().Next(0, Server.MaxPlayers + 1);
                        randomplayer = Utilities.GetPlayerFromSlot(rand);
                    }
                    while (randomplayer == null || !randomplayer.IsValid || randomplayer.IsBot || randomplayer.TeamNum != 2);

                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["cekt_command_used", player.PlayerName]);
                    Server.PrintToChatAll(Localizer["random_chosen", randomplayer.PlayerName]);
                }
                else
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["cekt_min_player"]);
                    return;
                }
            }
            else
            {
                command.ReplyToCommand(ReplaceTags($"{Config.EklentiTagi} ", true) + Localizer["permissions"]);
                return;
            }
    }

    public void CekCT(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null)
            if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
            {
                if (get_player_count(3) >= 2)
                {
                    CCSPlayerController? randomplayer;
                    do
                    {
                        randomplayer = null;
                        int rand = new Random().Next(0, Server.MaxPlayers + 1);
                        randomplayer = Utilities.GetPlayerFromSlot(rand);
                    }
                    while (randomplayer == null || !randomplayer.IsValid || randomplayer.IsBot || randomplayer.TeamNum != 3);

                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["cekct_command_used", player.PlayerName]);
                    Server.PrintToChatAll(Localizer["random_chosen", randomplayer.PlayerName]);
                }
                else
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["cekct_min_player"]);
                    return;
                }
            }
            else
            {
                command.ReplyToCommand(ReplaceTags($"{Config.EklentiTagi} ", true) + Localizer["permissions"]);
                return;
            }
    }

    static public int get_player_count(int teamnum = 0)
    {
        int playercount = 0;
        foreach (var p in Utilities.GetPlayers())
            if (p != null && p.IsValid && !p.IsBot && (teamnum == 0 || p.TeamNum == teamnum))
                playercount++;

        return playercount;
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