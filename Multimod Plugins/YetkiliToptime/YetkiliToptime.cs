using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using Dapper;
using Microsoft.Extensions.Localization;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace YetkiliToptime;

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

    [JsonPropertyName("ToptimeLimit")]
    public int? ToptimeLimit { get; set; } = 10;

    [JsonPropertyName("WebHook")]
    public string? WebHook { get; set; } = "";
}

public class YetkiliToptime : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "Yetkili Toptime";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!ytoptime komutu ile yetkili oynama sureleri goruntulenebilir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    private static readonly HttpClient _httpClient;
    private const string BridgeUrl = "https://syntax-web.com/cs2-dc.php"; // sabit köprü

    static YetkiliToptime()
    {
        _httpClient = new HttpClient();
    }

    public string ConnectionString = "";

    private readonly Dictionary<CCSPlayerController, int> iOynamaSuresi = new();
    private readonly Dictionary<CCSPlayerController, int> iSpecSuresi = new();
    private readonly Dictionary<CCSPlayerController, Timer?> timer_ex = new();


    // LISANS
    public int lisans_bitis_yil = 2025; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 20; // Lisansin bitecegi gun
    // LISANS

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[YetkiliToptime] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
			AddCommand("css_ytoptime", "Yetkili toptime listesini goruntuler.", (player, command) => YToptime(player, command));
			AddCommand("css_ytop", "Yetkili toptime listesini goruntuler.", (player, command) => YToptime(player, command));
			AddCommand("css_ytoptime0", "Yetkili toptime listesini sifirlar.", (player, command) => YToptime0(player, command));
            AddCommand("css_ytop0", "Yetkili toptime listesini sifirlar.", (player, command) => YToptime0(player, command));
            AddCommand("css_ytopdc", "Yetkili toptime listesini discord'a aktarir.", (player, command) => YTopDC(player, command));

            RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
			RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
			
			// CONFIG //
			ConnectionString = $"Server={Config.db_host};Port={Config.db_port};User ID={Config.db_user};Password={Config.db_pass};Database={Config.db_name};";
			// CONFIG //


			using (var connection = new MySqlConnection(ConnectionString))
			{
				connection.Open();
				connection.Execute(@"CREATE TABLE IF NOT EXISTS `yetkili_toptime` (`id` INT AUTO_INCREMENT PRIMARY KEY, `name` VARCHAR(255) NOT NULL, `steamid` VARCHAR(17) UNIQUE NOT NULL, `isadmin` INT NOT NULL DEFAULT 0, `oynama_suresi` INT NOT NULL DEFAULT 0, `spec_suresi` INT NOT NULL DEFAULT 0);");
			}


			/*foreach (var p in Utilities.GetPlayers())
				if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
					GetPlayerData(p);*/
		}
    }

    public void YToptime(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        if (AdminManager.PlayerHasPermissions(player, "@css/ban"))
        {
            int toplam_dakika = iOynamaSuresi[player];
            int toplam_sdakika = iSpecSuresi[player];

            int toplam_saat = toplam_dakika / 60;
            int kalan_dakika = toplam_dakika % 60;

            int toplam_ssaat = toplam_sdakika / 60;
            int kalan_sdakika = toplam_sdakika % 60;

            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_time", toplam_saat, kalan_dakika, toplam_ssaat, kalan_sdakika]);

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    var topPlayersQuery = $@"
                            SELECT name, oynama_suresi, spec_suresi
                            FROM yetkili_toptime
                            WHERE oynama_suresi > 0 AND isadmin = 1
                            ORDER BY oynama_suresi DESC
                            LIMIT {Config.ToptimeLimit};";

                    var topPlayers = connection.Query(topPlayersQuery).ToList();

                    if (topPlayers.Any())
                    {
                        player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["menu_title"]);
                        player.PrintToChat($"-----------------------------------------------");

                        for (int i = 0; i < topPlayers.Count; i++)
                        {
                            var topPlayerInfo = topPlayers[i];

                            toplam_dakika = topPlayerInfo.oynama_suresi;
                            toplam_sdakika = topPlayerInfo.spec_suresi;

                            toplam_saat = toplam_dakika / 60;
                            kalan_dakika = toplam_dakika % 60;

                            toplam_ssaat = toplam_sdakika / 60;
                            kalan_sdakika = toplam_sdakika % 60;

                            player.PrintToChat(Localizer["toptime_option", i + 1, topPlayerInfo.name, toplam_saat, kalan_dakika, toplam_ssaat, kalan_sdakika]);
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
        else
        {
            command.ReplyToCommand(ReplaceTags($"{Config.EklentiTagi} ", true) + Localizer["permissions"]);
            return;
        }
    }

    public void YTopDC(CCSPlayerController? player, CommandInfo command)
    {
        if (AdminManager.PlayerHasPermissions(player, "@css/root"))
        {
            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["send_discord"]);

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    var topPlayersQuery = $@"
                            SELECT name, oynama_suresi, spec_suresi
                            FROM yetkili_toptime
                            WHERE oynama_suresi > 0 AND isadmin = 1
                            ORDER BY oynama_suresi DESC
                            LIMIT {Config.ToptimeLimit};";

                    var topPlayers = connection.Query(topPlayersQuery).ToList();

                    if (topPlayers.Any())
                    {
                        Server.NextFrame(async () =>
                        {
                            // Başlangıç mesajı
                            var message = "Yetkili Toptime listesi sunucudan discord'a aktarıldı.\n\n";

                            // Tüm oyuncuların bilgilerini ekle
                            for (int i = 0; i < topPlayers.Count; i++)
                            {
                                var topPlayerInfo = topPlayers[i];

                                int toplam_dakika = topPlayerInfo.oynama_suresi;
                                int toplam_sdakika = topPlayerInfo.spec_suresi;

                                int toplam_saat = toplam_dakika / 60;
                                int kalan_dakika = toplam_dakika % 60;

                                int toplam_ssaat = toplam_sdakika / 60;
                                int kalan_sdakika = toplam_sdakika % 60;

                                message += $"{i + 1}. {topPlayerInfo.name} - {toplam_saat}s {kalan_dakika}dk (spec {toplam_ssaat}s {kalan_sdakika}dk)\n";
                            }

                            // Tek seferde gönder
                            await PostAsync(BridgeUrl, Config.WebHook, message);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                player.PrintToChat(Localizer["command_error"] + ex.Message);
            }
        }
        else
        {
            command.ReplyToCommand(ReplaceTags($"{Config.EklentiTagi} ", true) + Localizer["permissions"]);
            return;
        }
    }

    public void YToptime0(CCSPlayerController? player, CommandInfo command)
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

    private void OnClientConnected(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null) return;

        iOynamaSuresi[player] = 0;
        iSpecSuresi[player] = 0;

        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
        {
            GetPlayerData(player);

            timer_ex[player] = AddTimer(60.0f, () =>
            {
                if (AdminManager.PlayerHasPermissions(player, "@css/ban"))
                {
                    iOynamaSuresi[player]++;
                    if(player.TeamNum != 2 && player.TeamNum != 3)
                        iSpecSuresi[player]++;

                    using (var connection = new MySqlConnection(ConnectionString))
                    {
                        connection.Open();
                        connection.Execute($"UPDATE yetkili_toptime SET name = '{Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", "")}', oynama_suresi = {iOynamaSuresi[player]}, spec_suresi = {iSpecSuresi[player]}, isadmin = 1 WHERE steamid = '{player.SteamID}';");
                    }
                }
                else
                {
                    using (var connection = new MySqlConnection(ConnectionString))
                    {
                        connection.Open();
                        connection.Execute($"UPDATE yetkili_toptime SET name = '{Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", "")}', isadmin = 0 WHERE steamid = '{player.SteamID}';");
                    }
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

    public void GetPlayerData(CCSPlayerController? player)
    {
        if (player == null) return;
        iOynamaSuresi[player] = 0;
        iSpecSuresi[player] = 0;

        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var playerData = connection.QueryFirstOrDefault($"SELECT * FROM yetkili_toptime WHERE steamid = '{player.SteamID}'");

                if (playerData != null)
                {
                    iOynamaSuresi[player] = playerData.oynama_suresi;
                    iSpecSuresi[player] = playerData.spec_suresi;
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
                var insertQuery = $"INSERT INTO yetkili_toptime (name, steamid) VALUES (@Name, @SteamID);";
                connection.Execute(insertQuery, new { Name = Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", ""), SteamID = player.SteamID });
            }

            iOynamaSuresi[player] = 0;
            iSpecSuresi[player] = 0;
        }
    }

    public void Reset()
    {
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Execute("TRUNCATE TABLE yetkili_toptime;");
        }

        foreach (var p in Utilities.GetPlayers())
            if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
                InsertPlayer(p);
    }

    private async Task PostAsync(string bridgeUri, string webhook, string message)
    {
        try
        {
            var body = JsonSerializer.Serialize(new { webhook = webhook, content = message });
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            await _httpClient.PostAsync(bridgeUri, content);
        }
        catch
        {
            // hata sessizce geçilir
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