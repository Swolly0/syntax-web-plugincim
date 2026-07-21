using System;
using System.Collections.Generic;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using MySqlConnector;
using Dapper;
using CounterStrikeSharp.API.Modules.Admin;
using System.Text.Json.Serialization;
using System.IO;
using Newtonsoft.Json;
using CounterStrikeSharp.API;

namespace ePin;
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
class Admin
{
    [JsonPropertyName("identity")]
    public string identity { get; set; }

    [JsonPropertyName("immunity")]
    public int immunity { get; set; }

    [JsonPropertyName("groups")]
    public List<string> groups { get; set; }

    [JsonPropertyName("comment")]
    public string comment { get; set; }
}

public class ePin : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "ePin Yetki Sistemi";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!epin xxx komutu ile epin kullanilir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;


    public string ConnectionString = "";


    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[ePin] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
        }

        Config = config;
    }
	
    public override void Load(bool hotReload)
    {
        AddCommand("css_epin", "Epin kullan.", (player, command) => ePinKullan(player, command));
        AddCommand("css_epino", "Epin olustur.", (player, command) => ePinOlustur(player, command));
        AddCommand("css_epins", "Tüm epinleri listele.", (player, command) => ListeleEpins(player));


        // CONFIG //
        ConnectionString = $"Server={Config.db_host};Port={Config.db_port};User ID={Config.db_user};Password={Config.db_pass};Database={Config.db_name};";
		// CONFIG //

		using (var connection = new MySqlConnection(ConnectionString))
		{
			connection.Open();
			connection.Execute(@"CREATE TABLE IF NOT EXISTS `epins` (`id` INT AUTO_INCREMENT PRIMARY KEY, `epin` VARCHAR(255) NOT NULL, `flag` VARCHAR(32) UNIQUE NOT NULL, `immunity` INT NOT NULL DEFAULT 0);");
		}
    }

    public void ePinOlustur(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        if(AdminManager.PlayerHasPermissions(player, "@css/root"))
        {
            var key = command.GetArg(1);
            var flag = command.GetArg(2);
            var immunity = command.GetArg(3);

            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(flag) || string.IsNullOrEmpty(immunity))
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage"]);
                return;
            }

            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var epinsData = connection.QueryFirstOrDefault($"SELECT * FROM epins WHERE epin = '{key}'");

                if (epinsData != null)
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["epin_available", key]);
                else
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["epin_created", key]);
                    InsertKey(key, flag, immunity);
                }
            }
        }
        else
            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permission"]);
    }

    public void ePinKullan(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        var key = command.GetArg(1);
        if (string.IsNullOrEmpty(key))
        {
            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["epin_usage"]);
            return;
        }

        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();
            var epinsData = connection.QueryFirstOrDefault($"SELECT * FROM epins WHERE epin = '{key}'");

            if (epinsData != null)
            {
                string filePath = "../../csgo/addons/counterstrikesharp/configs/admins.json";
                string jsonText = File.ReadAllText(filePath);
                var admins = JsonConvert.DeserializeObject<Dictionary<string, Admin>>(jsonText);

                string newAdminName = player.PlayerName;

                if (!admins.ContainsKey(newAdminName))
                {
                    Admin newAdmin = new Admin
                    {
                        identity = player.AuthorizedSteamID.SteamId64.ToString(),
                        immunity = epinsData.immunity,
                        groups = new List<string> { epinsData.flag },
                        comment = "[EPIN] - " + DateTime.Now + " tarihinde eklendi: " + player.PlayerName
                    };

                    admins.Add(newAdminName, newAdmin);

                    string updatedJson = JsonConvert.SerializeObject(admins, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(filePath, updatedJson);

                    var insertQuery = $"DELETE FROM epins WHERE id = @id";
                    connection.Execute(insertQuery, new { id = epinsData.id });

                    Server.ExecuteCommand("css_admins_reload");
                    Server.ExecuteCommand("css_tags_reload");

                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["epin_used", epinsData.flag, epinsData.immunity]);
                }
            }
            else
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["epin_invalid"]);
        }
    }

    public void ListeleEpins(CCSPlayerController? player)
    {
        if (player == null) return;

        if (AdminManager.PlayerHasPermissions(player, "@root"))
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var epinsList = connection.Query("SELECT * FROM epins");

                if (epinsList != null)
                {
                    foreach (var epin in epinsList)
                    {
                        string message = $"Epin: {epin.epin}, Flag: {epin.flag}, Immunity: {epin.immunity}";
                        player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {message}"));
                        Console.WriteLine(message);
                    }
                }
                else
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["no_have"]);
            }
        }
        else
            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permission"]);
    }


    public void InsertKey(string Key = "", string Flag = "", string immunity = "")
    {
        if (Key != "" && Flag != "" && Flag != "")
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var insertQuery = $"INSERT INTO epins (epin, flag, immunity) VALUES (@epin, @flag, @immunity);";
                connection.Execute(insertQuery, new { epin = Key, flag = Flag, immunity = immunity });
            }
        }
    }

    public void Reset()
    {
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Execute("TRUNCATE TABLE epins;");
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