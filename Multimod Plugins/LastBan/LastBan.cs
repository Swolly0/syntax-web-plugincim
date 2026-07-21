using System;
using System.Linq;
using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using MySqlConnector;
using Dapper;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Menu;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace LastBan;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("db_host")]
    public string? db_host { get; set; } = "localhost";

    [JsonPropertyName("db_user")]
    public string? db_user { get; set; } = "root";
	
	[JsonPropertyName("db_name")]
    public string? db_name { get; set; } = "cs2";

    [JsonPropertyName("db_pass")]
    public string? db_pass { get; set; } = "";

    [JsonPropertyName("db_port")]
    public string? db_port { get; set; } = "3306";
}

public class LastBan : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "Last Ban";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!lastban komutu ile sunucudan cikan oyunculari yasaklayabilirsiniz.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;


    public string ConnectionString = "";

    private readonly Dictionary<CCSPlayerController, string> iTarget = new();

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[LBAN] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
        }

        Config = config;
        Stringlocalizer = Localizer;
    }
	
    public override void Load(bool hotReload)
    {
		AddCommand("css_lastban", "Son cikan oyunculari goruntule.", (player, command) => Last_Ban(player, command));
		AddCommand("css_lastunban", "Son cikan oyuncularin yasagini kaldir.", (player, command) => Last_Unban(player, command));

		RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
		RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);

		// CONFIG //
		ConnectionString = $"Server={Config.db_host};Port={Config.db_port};User ID={Config.db_user};Password={Config.db_pass};Database={Config.db_name};";
		// CONFIG //


		using (var connection = new MySqlConnection(ConnectionString))
		{
			connection.Open();
			connection.Execute(@"CREATE TABLE IF NOT EXISTS `lastban` (`id` INT AUTO_INCREMENT PRIMARY KEY, `name` VARCHAR(255) NOT NULL, `steamid` VARCHAR(17) UNIQUE NOT NULL, `lastconnect` INT NOT NULL DEFAULT 0, `lastdisconnect` INT NOT NULL DEFAULT 0, `banned` INT NOT NULL DEFAULT 0);");
		}
    }


    public void Last_Unban(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null) return;

        if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            var Hedef = info.ArgByIndex(1);

            if (Hedef != null && Hedef != "")
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    var playerData = connection.QueryFirstOrDefault($"SELECT * FROM lastban WHERE steamid = '{Hedef}'");

                    if (playerData != null)
                    {
                        connection.Execute($"UPDATE lastban SET banned = '0' WHERE steamid = '{playerData.steamid}';");

                        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
                            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["last_unban", player.PlayerName, playerData.name]);
                    }
                    else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["target_not_found"]);
                }
            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage"]);

        }
        else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
    }


    public void Last_Ban(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
            if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
            {
                try
                {
                    using (var connection = new MySqlConnection(ConnectionString))
                    {
                        connection.Open();
                        var topPlayersQuery = $@"
                                SELECT name, steamid, lastconnect, lastdisconnect
                                FROM lastban
                                WHERE lastdisconnect > 0
                                ORDER BY lastdisconnect DESC
                                LIMIT 20;";

                        var topPlayers = connection.Query(topPlayersQuery).ToList();

                        if (topPlayers.Any())
                        {
                            var menu = new ChatMenu($"{ReplaceTags($"{Config.EklentiTagi}", true)} " + Localizer["menu_title"]);
                            for (int i = 0; i < topPlayers.Count; i++)
                            {
                                var topPlayerInfo = topPlayers[i];

                                string menutitle = Localizer["menu_option", topPlayerInfo.name, GetDate(Convert.ToInt32(topPlayerInfo.lastconnect)), GetDate(Convert.ToInt32(topPlayerInfo.lastdisconnect))];
                                menu.AddMenuOption(menutitle, (player, option) => Last_Ban_(player, topPlayerInfo.steamid));
                            }
                            MenuManager.OpenChatMenu(player, menu);
                        }
                        else
                            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["lastban_empty"]);
                    }
                }
                catch (Exception ex)
                {
                    player.PrintToChat($"!lastban komut hatasi: " + ex.Message);
                }
            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
    }

    public void Last_Ban_(CCSPlayerController player, string option)
    {
        if (player == null) return;

        if (player.IsValid && !player.IsBot)
        {
            iTarget[player] = option;
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var playerData = connection.QueryFirstOrDefault($"SELECT * FROM lastban WHERE steamid = '{option}'");

                if (playerData != null)
                {
                    var menu = new ChatMenu(ReplaceTags($"{Config.EklentiTagi} ", true) + Localizer["time_menu_title", playerData.name]);
                    menu.AddMenuOption(Localizer["1hour"], (player, option) => Last_Ban_Time(player, 3600));
                    menu.AddMenuOption(Localizer["1day"], (player, option) => Last_Ban_Time(player, 86400));
                    menu.AddMenuOption(Localizer["1week"], (player, option) => Last_Ban_Time(player, 604800));
                    menu.AddMenuOption(Localizer["1month"], (player, option) => Last_Ban_Time(player, 2592000));
                    menu.AddMenuOption(Localizer["1year"], (player, option) => Last_Ban_Time(player, 31536000));
                    menu.AddMenuOption(Localizer["perma"], (player, option) => Last_Ban_Time(player, 99999999));
                    MenuManager.OpenChatMenu(player, menu);
                }
            }
        }
    }

    public void Last_Ban_Time(CCSPlayerController player, int option)
    {
        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV && iTarget[player] != "")
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var playerData = connection.QueryFirstOrDefault($"SELECT * FROM lastban WHERE steamid = '{iTarget[player]}'");

                if (playerData != null)
                {
                    int total = option, saat = 0, gun = 0;
                    while (total - 86400 >= 0)
                    {
                        total -= 86400;
                        gun++;
                    }

                    while (total - 3600 >= 0)
                    {
                        total -= 3600;
                        saat++;
                    }

                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ban_msg", playerData.name, player.PlayerName]);
                    connection.Execute($"UPDATE lastban SET banned = '{DateTimeOffset.UtcNow.ToUnixTimeSeconds() + option}' WHERE steamid = '{iTarget[player]}';");

                    foreach (var p in Utilities.GetPlayers())
                        if (p != null && p.IsValid && !p.IsBot && Convert.ToString(p.SteamID) == iTarget[player])
                            Server.ExecuteCommand($"kickid {p.UserId} " + Localizer["kick_msg", player.PlayerName, gun, saat]);
                }
            }

            iTarget[player] = "";
        }
    }

    private void OnClientConnected(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);

        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
            GetPlayerData(player);
    }

    public void GetPlayerData(CCSPlayerController? player)
    {
        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var playerData = connection.QueryFirstOrDefault($"SELECT * FROM lastban WHERE steamid = '{player.SteamID}'");

                if (playerData != null)
                {
                    if (playerData.banned > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    {
                        Server.ExecuteCommand($"kickid {player.UserId} " + Localizer["ban_info_msg", GetDate(playerData.banned)]);
                    }
                    else
                        connection.Execute($"UPDATE lastban SET name = '{Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", "")}', lastconnect = {DateTimeOffset.UtcNow.ToUnixTimeSeconds()} WHERE steamid = '{player.SteamID}';");
                }
                else
                    InsertPlayer(player);
            }
        }
    }

    public void InsertPlayer(CCSPlayerController? player)
    {
        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var insertQuery = $"INSERT INTO lastban (name, steamid, lastconnect) VALUES (@Name, @SteamID, @lastconnect);";
                connection.Execute(insertQuery, new { Name = Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", ""), SteamID = player.SteamID, lastconnect = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
            }
        }
    }

    private void OnClientDisconnect(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);

        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                connection.Execute($"UPDATE lastban SET name = '{Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", "")}', lastdisconnect = {DateTimeOffset.UtcNow.ToUnixTimeSeconds()} WHERE steamid = '{player.SteamID}';");
            }
        }
    }

    public static string GetDate(int iUnix)
    {
        System.DateTime dat_Time = new System.DateTime(1970, 1, 1, 0, 0, 0).ToLocalTime();
        dat_Time = dat_Time.AddSeconds(iUnix);
        string print_the_Date = dat_Time.ToShortDateString() + " " + dat_Time.ToShortTimeString();

        return print_the_Date;
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