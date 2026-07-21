using System;
using System.IO;
using System.Text.Json;
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
using CounterStrikeSharp.API.Modules.Entities;

namespace SureliToptime;

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

    [JsonPropertyName("ResetPeriod")]
    public int? ResetPeriod { get; set; } = 604800;

    [JsonPropertyName("ToptimeLimit")]
    public int? ToptimeLimit { get; set; } = 10;
}

public class SureliToptime : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "Sureli Toptime";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!stoptime komutu ile sureli toptime'i goruntulemenizi saglar.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;


    public string ConnectionString = "";
    public double ResetTime = 0;

    private readonly Dictionary<CCSPlayerController, int> iAll = new();
    private readonly Dictionary<CCSPlayerController, int> iCT = new();
    private readonly Dictionary<CCSPlayerController, int> iT = new();
    private readonly Dictionary<CCSPlayerController, int> iSpec = new();
    private readonly Dictionary<CCSPlayerController, int> iDead = new();
    private readonly Dictionary<CCSPlayerController, int> iAlive = new();
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
            Console.WriteLine($"[Toptime] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
            AddCommand("css_toptime", "Sureli toptime'i goruntulemenizi saglar.", (player, command) => S_Toptime(player, command));
            AddCommand("css_stoptime", "Sureli toptime'i goruntulemenizi saglar.", (player, command) => S_Toptime(player, command));
            AddCommand("css_stop", "Sureli toptime'i goruntulemenizi saglar.", (player, command) => S_Toptime(player, command));
            AddCommand("css_stoptime0", "Sureli toptime'i manuel sifirlamanizi saglar.", (player, command) => S_Toptime0(player, command));

            AddCommand("css_htoptime", "Sureli toptime'i goruntulemenizi saglar.", (player, command) => S_Toptime(player, command));
            AddCommand("css_htop", "Sureli toptime'i goruntulemenizi saglar.", (player, command) => S_Toptime(player, command));
            AddCommand("css_htoptime0", "Sureli toptime'i manuel sifirlamanizi saglar.", (player, command) => S_Toptime0(player, command));

            AddCommand("css_surem", "Sureli toptime surenizi goruntulemenizi saglar.", (player, command) => Surem(player, command));

            RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);

            // CONFIG //
            ConnectionString = $"Server={Config.db_host};Port={Config.db_port};User ID={Config.db_user};Password={Config.db_pass};Database={Config.db_name};";
            // CONFIG //





            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                connection.Execute(@"CREATE TABLE IF NOT EXISTS `sureli_toptime` (`id` INT AUTO_INCREMENT PRIMARY KEY, `name` VARCHAR(255) NOT NULL, `steamid` VARCHAR(17) UNIQUE NOT NULL, `alll` INT NOT NULL DEFAULT 0, `ct` INT NOT NULL DEFAULT 0, `t` INT NOT NULL DEFAULT 0, `spec` INT NOT NULL DEFAULT 0, `dead` INT NOT NULL DEFAULT 0, `alive` INT NOT NULL DEFAULT 0);");

                var playerData = connection.QueryFirstOrDefault($"SELECT * FROM sureli_toptime WHERE steamid = '1'");

                if (playerData != null)
                {
                    ResetTime = Convert.ToInt32(playerData.name);
                }
                else
                {
                    ResetTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Convert.ToInt32(Config.ResetPeriod);

                    var insertQuery = $"INSERT INTO sureli_toptime (name, steamid) VALUES (@Name, @SteamID);";
                    connection.Execute(insertQuery, new { Name = ResetTime.ToString(), SteamID = '1' });
                }
            }


            AddTimer(10.0f, () =>
            {
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= ResetTime)
                {
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["toptime_auto_reset"]);
                    Reset();
                }
            }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);


            /*foreach (var p in Utilities.GetPlayers())
                if (p.is_valid())
                    GetPlayerData(p);*/
        }
    }

    public void S_Toptime(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        try
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var topPlayersQuery = $@"
                        SELECT name, alll
                        FROM sureli_toptime
                        WHERE steamid != '1' AND alll > 0
                        ORDER BY alll DESC
                        LIMIT 
                {Config.ToptimeLimit};";
                var topPlayers = connection.Query(topPlayersQuery).ToList();
                if (topPlayers.Any())
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["menu_title"]);
                    player.PrintToChat(Localizer["auto_reset_date", GetDate(Convert.ToInt32(ResetTime))]);
                    player.PrintToChat($"-----------------------------------------------");
                    for (int i = 0; i < topPlayers.Count; i++)
                    {
                        var topPlayerInfo = topPlayers[i];
                        int dakika = topPlayerInfo.alll;
                        int saat = 0;
                        while (dakika - 60 >= 0)
                        {
                            dakika -= 60;
                            saat++;
                        }

                        player.PrintToChat(Localizer["toptime_option", i + 1, topPlayerInfo.name, saat, dakika]);
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

    public void S_Toptime0(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        if (AdminManager.PlayerHasPermissions(player, "@css/root"))
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

    public void Surem(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        int dakika = iAll[player];
        int saat = 0;

        while(dakika - 60 >= 0)
        {
            dakika -= 60;
            saat++;
        }

        player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_time", saat, dakika]);
        player.PrintToChat(Localizer["auto_reset_date", GetDate(Convert.ToInt32(ResetTime))]);
    }

    private void OnClientConnected(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null) return;

        iAll[player] = 0;
        iCT[player] = 0;
        iT[player] = 0;
        iSpec[player] = 0;
        iDead[player] = 0;
        iAlive[player] = 0;
        timer_ex[player] = null;

        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
        {
            GetPlayerData(player);

            timer_ex[player] = AddTimer(60.0f, () =>
            {
                iAll[player]++;

                if (player.TeamNum == 2)
                    iT[player]++;
                else
                if (player.TeamNum == 3)
                    iCT[player]++;
                else
                if (player.TeamNum == 1)
                {
                    iAll[player]--;
                    iSpec[player]++;
                }
                if (player.is_valid_alive())
                    iAlive[player]++;
                else
                    iDead[player]++;

                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    connection.Execute($"UPDATE sureli_toptime SET name = '{Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", "")}', alll = {iAll[player]}, ct = {iCT[player]}, t = {iT[player]}, spec = {iSpec[player]}, alive = {iAlive[player]}, dead = {iDead[player]} WHERE steamid = '{player.SteamID}';");
                }
            }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
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

    public void Reset()
    {
        ResetTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Convert.ToInt32(Config.ResetPeriod);

        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Execute("TRUNCATE TABLE sureli_toptime;");

            connection.Open();
            var insertQuery = $"INSERT INTO sureli_toptime (name, steamid) VALUES (@Name, @SteamID);";
            connection.Execute(insertQuery, new { Name = ResetTime.ToString(), SteamID = '1' });
        }

        foreach (var p in Utilities.GetPlayers())
            if (p.is_valid())
                InsertPlayer(p);


        Server.PrintToChatAll(Localizer["auto_reset_date", GetDate(Convert.ToInt32(ResetTime))]);
    }

    public void GetPlayerData(CCSPlayerController? player)
    {
        if (player == null) return;
        iAll[player] = 0;
        iCT[player] = 0;
        iT[player] = 0;
        iSpec[player] = 0;
        iDead[player] = 0;
        iAlive[player] = 0;

        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();
            var playerData = connection.QueryFirstOrDefault($"SELECT * FROM sureli_toptime WHERE steamid = '{player.SteamID}'");

            if (playerData != null)
            {
                iAll[player] = playerData.alll;
                iCT[player] = playerData.ct;
                iT[player] = playerData.t;
                iSpec[player] = playerData.spec;
                iDead[player] = playerData.dead;
                iAlive[player] = playerData.alive;
            }
            else
                InsertPlayer(player);
        }
    }

    public void InsertPlayer(CCSPlayerController? player)
    {
        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var insertQuery = $"INSERT INTO sureli_toptime (name, steamid) VALUES (@Name, @SteamID);";
                connection.Execute(insertQuery, new { Name = Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", ""), SteamID = player.SteamID });
            }

            iAll[player] = 0;
            iCT[player] = 0;
            iT[player] = 0;
            iSpec[player] = 0;
            iDead[player] = 0;
            iAlive[player] = 0;
        }
    }

    public static string GetDate(int iResetTime)
    {
        System.DateTime dat_Time = new System.DateTime(1970, 1, 1, 0, 0, 0).ToLocalTime();
        dat_Time = dat_Time.AddSeconds(iResetTime);
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


public static class Lib
{

    static public bool is_valid(this CCSPlayerController? player)
    {
        return player != null && player.IsValid && !player.IsBot && player.PlayerPawn.IsValid;
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