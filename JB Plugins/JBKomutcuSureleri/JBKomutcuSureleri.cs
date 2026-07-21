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
using CounterStrikeSharp.API.Core.Attributes.Registration;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBKomutcuSureleri;

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
	
	[JsonPropertyName("ToptimeLimit")]
    public int? ToptimeLimit { get; set; }
}

public class JBKomutcuSureleri : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Komutcu Sureleri";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!topkomutcu komutu ile en cok komut veren komutcularin siralamasi goruntulenebilir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;


    public string ConnectionString = "";

    private readonly Dictionary<CCSPlayerController, int> iKomutSuresi = new();
    private readonly Dictionary<CCSPlayerController, Timer?> timer_ex = new();
    CCSPlayerController? iWarden = null;


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
            Console.WriteLine($"[Top Warden] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
			AddCommand("css_k", "", (player, command) => Warden(player, command));
			AddCommand("css_uw", "", (player, command) => UnWarden(player, command));
			AddCommand("css_kcik", "", (player, command) => UnWarden(player, command));

			RegisterEventHandler<EventPlayerTeam>(OnPlayerTeam);
			RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
			
			AddTimer(1.0f, () =>
			{
				if (iWarden != null && (!iWarden.is_valid() || !iWarden.is_ct()))
					iWarden = null;
			}, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);		
			// KOMUTÇU SİSTEMİ


			AddCommand("css_topkom", "Komutcu toptime listesini goruntuler.", (player, command) => TopKomutcu(player, command));
			AddCommand("css_topkomutcu", "Komutcu toptime listesini goruntuler.", (player, command) => TopKomutcu(player, command));
			AddCommand("css_topkom0", "Komutcu toptime listesini sifirlar.", (player, command) => TopKomutcu0(player, command));
			AddCommand("css_topkomutcu0", "Komutcu toptime listesini sifirlar.", (player, command) => TopKomutcu0(player, command));

			RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
			RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);

			// CONFIG //
			ConnectionString = $"Server={Config.db_host};Port={Config.db_port};User ID={Config.db_user};Password={Config.db_pass};Database={Config.db_name};";
			// CONFIG //


			using (var connection = new MySqlConnection(ConnectionString))
			{
				connection.Open();
				connection.Execute(@"CREATE TABLE IF NOT EXISTS `top_komutcu` (`id` INT AUTO_INCREMENT PRIMARY KEY, `name` VARCHAR(255) NOT NULL, `steamid` VARCHAR(17) UNIQUE NOT NULL, `komut_suresi` INT NOT NULL DEFAULT 0);");
			}


			foreach (var p in Utilities.GetPlayers())
				if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
					GetPlayerData(p);
		}
    }

    public void TopKomutcu(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            int dakika = iKomutSuresi[player];
            int saat = 0;

            while (dakika - 60 >= 0)
            {
                dakika -= 60;
                saat++;
            }

            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_time", saat, dakika]);

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    var topPlayersQuery = $@"SELECT name, komut_suresi FROM top_komutcu WHERE komut_suresi > 0 ORDER BY komut_suresi DESC LIMIT {Config.ToptimeLimit};";

                    var topPlayers = connection.Query(topPlayersQuery).ToList();

                    if (topPlayers.Any())
                    {
                        player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["menu_title"]);
                        player.PrintToChat($"-----------------------------------------------");

                        for (int i = 0; i < topPlayers.Count; i++)
                        {
                            var topPlayerInfo = topPlayers[i];

                            dakika = topPlayerInfo.komut_suresi;
                            saat = 0;

                            while (dakika - 60 >= 0)
                            {
                                dakika -= 60;
                                saat++;
                            }

                            player.PrintToChat(Localizer["toptime_option", topPlayerInfo.name, saat, dakika]);
                        }

                        player.PrintToChat($"-----------------------------------------------");
                    }
                    else
                        player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["toptime_empty"]);
                }
            }
            catch (Exception ex)
            {
                player.PrintToChat(Localizer["command_error"] + ex.Message);
            }
        }
    }

	public void TopKomutcu0(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["toptime_reset", player.PlayerName]);
                Reset();
            }
            else
            {
                command.ReplyToCommand(ReplaceTags($"{Config.EklentiTagi} ", true) + Localizer["permissions"]);
                return;
            }
        }
    }

    private void OnClientConnected(int playerSlot)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            var player = Utilities.GetPlayerFromSlot(playerSlot);
            iKomutSuresi[player] = 0;

            if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
            {
                GetPlayerData(player);

                timer_ex[player] = AddTimer(60.0f, () =>
                {
                    if (iWarden != null && iWarden.IsValid && !iWarden.IsBot && iWarden == player)
                    {
                        iKomutSuresi[iWarden]++;

                        using (var connection = new MySqlConnection(ConnectionString))
                        {
                            connection.Open();
                            connection.Execute($"UPDATE top_komutcu SET name = '{Regex.Replace(iWarden.PlayerName, @"[^a-zA-Z0-9\s]", "")}', komut_suresi = {iKomutSuresi[iWarden]} WHERE steamid = '{iWarden.SteamID}';");
                        }
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
            iKomutSuresi[player] = 0;
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var playerData = connection.QueryFirstOrDefault($"SELECT * FROM top_komutcu WHERE steamid = '{player.SteamID}'");

                if (playerData != null)
                    iKomutSuresi[player] = playerData.komut_suresi;
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
                var insertQuery = $"INSERT INTO top_komutcu (name, steamid) VALUES (@Name, @SteamID);";
                connection.Execute(insertQuery, new { Name = Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", ""), SteamID = player.SteamID });
            }

            iKomutSuresi[player] = 0;
        }
    }

    public void Reset()
    {
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Execute("TRUNCATE TABLE top_komutcu;");
        }

        foreach (var p in Utilities.GetPlayers())
            if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
                InsertPlayer(p);
    }

    public void Warden(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        if ((iWarden == null || !iWarden.is_valid() || !iWarden.is_ct()) && player.is_valid() && player.is_ct())
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