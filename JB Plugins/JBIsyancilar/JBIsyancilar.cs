using System;
using System.Linq;
using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using MySqlConnector;
using Dapper;
using CounterStrikeSharp.API.Modules.Admin;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBIsyancilar;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("db_host")]
    public string? db_host { get; set; }
	
	[JsonPropertyName("db_user")]
    public string? db_user { get; set; }
	
	[JsonPropertyName("db_name")]
    public string? db_name { get; set; }
	
	[JsonPropertyName("db_pass")]
    public string? db_pass { get; set; }
	
	[JsonPropertyName("db_port")]
    public string? db_port { get; set; }
	
	[JsonPropertyName("TopRebelsLimit")]
    public int? TopRebelsLimit { get; set; }
}
public class JBIsyancilar : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Isyancilar";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!isyancilar komutu ile en cok isyan yapan oyuncular goruntulenir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;


    public string ConnectionString = "";

    private readonly Dictionary<CCSPlayerController, int> iIsyan = new();
    private readonly Dictionary<CCSPlayerController, Timer?> timer_ex = new();


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
            Console.WriteLine($"[JB Rebels] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
			AddCommand("css_isyancilar", "Isyancilar listesini goruntuler.", (player, command) => Isyancilar(player, command));
			AddCommand("css_isyancilar0", "Isyancilar listesini sifirlar.", (player, command) => Isyancilar0(player, command));

			RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
			RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
			RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);

			// CONFIG //
			ConnectionString = $"Server={Config.db_host};Port={Config.db_port};User ID={Config.db_user};Password={Config.db_pass};Database={Config.db_name};";
			// CONFIG //


			using (var connection = new MySqlConnection(ConnectionString))
			{
				connection.Open();
				connection.Execute(@"CREATE TABLE IF NOT EXISTS `isyancilar` (`id` INT AUTO_INCREMENT PRIMARY KEY, `name` VARCHAR(255) NOT NULL, `steamid` VARCHAR(17) UNIQUE NOT NULL, `isyan` INT NOT NULL DEFAULT 0);");
			}


			foreach (var p in Utilities.GetPlayers())
				if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
					GetPlayerData(p);
		}
    }

    HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            var attacker = @event.Attacker;
            var victim = @event.Userid;

            if (attacker != null && attacker.IsValid && !attacker.IsBot)
                if (victim != null && victim.IsValid && !victim.IsBot)
                    if (victim != attacker && victim.TeamNum == 3)
                        iIsyan[attacker]++;
        }

        return HookResult.Continue;
    }


    public void Isyancilar(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["total_rebel_count", iIsyan[player]]);

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    var topPlayersQuery = $@"
                            SELECT name, isyan
                            FROM isyancilar
                            WHERE isyan > 0
                            ORDER BY isyan DESC
                            LIMIT {Config.TopRebelsLimit};";

                    var topPlayers = connection.Query(topPlayersQuery).ToList();

                    if (topPlayers.Any())
                    {
                        player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["menu_title"]);
                        player.PrintToChat($"-----------------------------------------------");

                        for (int i = 0; i < topPlayers.Count; i++)
                        {
                            var topPlayerInfo = topPlayers[i];

                            player.PrintToChat(Localizer["menu_option", i + 1, topPlayerInfo.name, topPlayerInfo.isyan]);
                        }

                        player.PrintToChat($"-----------------------------------------------");
                    }
                    else
                        player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["toprebels_empty"]);
                }
            }
            catch (Exception ex)
            {
                player.PrintToChat(Localizer["command_error"] + ex.Message);
            }
        }
    }

    public void Isyancilar0(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        if (NativeAPI.GetMapName().Contains("jb_"))
            if (AdminManager.PlayerHasPermissions(player, "@css/root"))
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["toprebels_reset", player.PlayerName]);
                Reset();
            }
            else
            {
                command.ReplyToCommand(ReplaceTags($"{Config.EklentiTagi} ", true) + Localizer["permissions"]);
                return;
            }
    }

    private void OnClientConnected(int playerSlot)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            var player = Utilities.GetPlayerFromSlot(playerSlot);
            if (player == null) return;
            iIsyan[player] = 0;

            if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
            {
                GetPlayerData(player);

                timer_ex[player] = AddTimer(60.0f, () =>
                {
                    using (var connection = new MySqlConnection(ConnectionString))
                    {
                        connection.Open();
                        connection.Execute($"UPDATE isyancilar SET name = '{Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", "")}', isyan = {iIsyan[player]} WHERE steamid = '{player.SteamID}';");
                    }
                }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
            }
        }
    }

    private void OnClientDisconnect(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null) return;

        if (timer_ex[player] != null)
        {
            timer_ex[player]!.Kill();
        }

        timer_ex[player] = null;
    }

    public void GetPlayerData(CCSPlayerController? player)
    {
        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
        {
            iIsyan[player] = 0;
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var playerData = connection.QueryFirstOrDefault($"SELECT * FROM isyancilar WHERE steamid = '{player.SteamID}'");

                if (playerData != null)
                    iIsyan[player] = playerData.isyan;
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
                var insertQuery = $"INSERT INTO isyancilar (name, steamid) VALUES (@Name, @SteamID);";
                connection.Execute(insertQuery, new { Name = Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", ""), SteamID = player.SteamID });
            }

            iIsyan[player] = 0;
        }
    }

    public void Reset()
    {
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Execute("TRUNCATE TABLE isyancilar;");
        }

        foreach (var p in Utilities.GetPlayers())
            if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
                InsertPlayer(p);
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

    // yes i know the null check is redundant but C# is dumb
    static public bool is_valid_alive(this CCSPlayerController? player)
    {
        return player != null && !player.is_valid() && player.PawnIsAlive && player.get_health() > 0;
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