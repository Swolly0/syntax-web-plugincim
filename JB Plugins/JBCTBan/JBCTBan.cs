using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Admin;
using MySqlConnector;
using Dapper;
using CounterStrikeSharp.API.Modules.Utils;
using System.Diagnostics.CodeAnalysis;
using static CounterStrikeSharp.API.Core.Listeners;

namespace JBCTBan
{
    public class Config : IBasePluginConfig
    {
        [JsonPropertyName("Prefix")]
        public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

        [JsonPropertyName("ConfigVersion")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("db_host")]
        public string db_host { get; set; } = "localhost";

        [JsonPropertyName("db_user")]
        public string db_user { get; set; } = "root";

        [JsonPropertyName("db_name")]
        public string db_name { get; set; } = "cs2";

        [JsonPropertyName("db_pass")]
        public string db_pass { get; set; } = "";

        [JsonPropertyName("db_port")]
        public string db_port { get; set; } = "3306";
    }

    public class JBCTBan : BasePlugin, IPluginConfig<Config>
    {
        public override string ModuleName { get; } = "CT Team Ban";
        public override string ModuleVersion { get; } = "1.0.0";
        public override string ModuleDescription { get; } = "css_ctban <oyuncu> <dakika> komutu ile oyuncunun CT takımına geçmesini yasaklar; css_ctunban ile yasağı kaldırır. (Sadece JB haritalarında)";
        public override string ModuleAuthor { get; } = "www.plugincim.com";

        public required Config Config { get; set; }
        public void OnConfigParsed(Config config)
        {
            Config = config;
        }

        private static Dictionary<string, long> PlayerCTBanExpiry = new Dictionary<string, long>();

        // Veritabanı bağlantı cümlesi
        private string ConnectionString => $"Server={Config.db_host};Port={Config.db_port};User ID={Config.db_user};Password={Config.db_pass};Database={Config.db_name};";

        // Bu sözlük, banlı oyuncular için timer referanslarını tutar
        private readonly Dictionary<CCSPlayerController, Timer?> bannedTimers = new();

        public override void Load(bool hotReload)
        {
            // Komutları kaydet
            AddCommand("css_ctban", "css_ctban <oyuncu> <dakika> - Oyuncunun CT takımına geçmesini engeller.", CTBanCommand);
            AddCommand("css_ctunban", "css_ctunban <oyuncu> - Oyuncunun CT yasağını kaldırır.", CTUnbanCommand);

            // Olayları kaydet
            RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);

            // Veritabanı tablosunu oluştur (eğer yoksa)
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS `ct_teamban` (
                        `id` INT AUTO_INCREMENT PRIMARY KEY,
                        `steamid` VARCHAR(64) UNIQUE NOT NULL,
                        `name` VARCHAR(255) NOT NULL,
                        `ban_expiry` BIGINT NOT NULL
                    );
                ");
            }
        }

        // css_ctban <oyuncu> <dakika>
        private void CTBanCommand(CCSPlayerController? sender, CommandInfo command)
        {
            if (!NativeAPI.GetMapName().ToLower().Contains("jb_"))
                return;

            if (sender == null || !sender.is_valid())
                return;

            // Komutu kullanan oyuncunun ctban durumunu kontrol et
            if (PlayerCTBanExpiry.ContainsKey(sender.SteamID.ToString()))
            {
                long playerBanExpiry = PlayerCTBanExpiry[sender.SteamID.ToString()];
                if (playerBanExpiry > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                {
                    DateTime banTime = DateTimeOffset.FromUnixTimeSeconds(playerBanExpiry).DateTime;
                    command.ReplyToCommand($"{ReplaceTags(Config.EklentiTagi)} CT yasağınız {banTime} tarihinde sona erecektir.");
                }
            }

            if (!AdminManager.PlayerHasPermissions(sender, "@css/ban"))
            {
                command.ReplyToCommand($"{ReplaceTags(Config.EklentiTagi)} Yetkiniz yok!");
                return;
            }

            if (command.ArgCount < 2)
            {
                command.ReplyToCommand($"{ReplaceTags(Config.EklentiTagi)} Kullanım: css_ctban <oyuncu> <dakika>");
                return;
            }

            string targetName = command.GetArg(1);
            if (!int.TryParse(command.GetArg(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes))
            {
                command.ReplyToCommand($"{ReplaceTags(Config.EklentiTagi)} Lütfen geçerli bir dakika değeri girin.");
                return;
            }

            var targetPlayer = Utilities.GetPlayers()
                .FirstOrDefault(p => p != null && p.IsValid && p.PlayerName.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (targetPlayer == null)
            {
                command.ReplyToCommand($"{ReplaceTags(Config.EklentiTagi)} Oyuncu bulunamadı: {targetName}");
                return;
            }

            // Ban süresi hesapla: Unix zamanı cinsinden (şu anki zamana dakikayı ekle)
            long banExpiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + minutes * 60L;
            PlayerCTBanExpiry[targetPlayer.SteamID.ToString()] = banExpiry;

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    connection.Execute(@"
                        INSERT INTO ct_teamban (steamid, name, ban_expiry) VALUES (@SteamID, @Name, @BanExpiry)
                        ON DUPLICATE KEY UPDATE name = @Name, ban_expiry = @BanExpiry;
                    ", new
                    {
                        SteamID = targetPlayer.AuthorizedSteamID.SteamId64,
                        Name = Regex.Replace(targetPlayer.PlayerName, @"[^a-zA-Z0-9\s]", ""),
                        BanExpiry = banExpiry
                    });
                    connection.Close();
                }

                command.ReplyToCommand($"{ReplaceTags(Config.EklentiTagi)} {targetPlayer.PlayerName} oyuncusu {minutes} dakika CT yasağı aldı.");
                
                // Eğer oyuncu online ise, timer başlat
                if (targetPlayer.is_valid())
                    StartBanTimer(targetPlayer);
            }
            catch (Exception ex)
            {
                command.ReplyToCommand($"{ReplaceTags(Config.EklentiTagi)} Hata: {ex.Message}");
            }
        }

        // css_ctunban <oyuncu>
        private void CTUnbanCommand(CCSPlayerController? sender, CommandInfo command)
        {
            if (!NativeAPI.GetMapName().ToLower().Contains("jb_"))
                return;

            if (sender == null || !sender.is_valid())
                return;

            if (!AdminManager.PlayerHasPermissions(sender, "@css/ban"))
            {
                command.ReplyToCommand($"{ReplaceTags(Config.EklentiTagi)} Yetkiniz yok!");
                return;
            }

            if (command.ArgCount < 1)
            {
                command.ReplyToCommand($"{ReplaceTags(Config.EklentiTagi)} Kullanım: css_ctunban <oyuncu>");
                return;
            }

            string targetName = command.GetArg(1);
            var targetPlayer = Utilities.GetPlayers()
                .FirstOrDefault(p => p != null && p.IsValid && p.PlayerName.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (targetPlayer == null)
            {
                command.ReplyToCommand($"{ReplaceTags(Config.EklentiTagi)} Oyuncu bulunamadı: {targetName}");
                return;
            }

            try
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    connection.Execute("DELETE FROM ct_teamban WHERE steamid = @SteamID;", new { SteamID = targetPlayer.AuthorizedSteamID.SteamId64 });
                    connection.Close();
                }
                StopBanTimer(targetPlayer);
                command.ReplyToCommand($"{ReplaceTags(Config.EklentiTagi)} {targetPlayer.PlayerName} oyuncusunun CT yasağı kaldırıldı.");
            }
            catch (Exception ex)
            {
                command.ReplyToCommand($"{ReplaceTags(Config.EklentiTagi)} Hata: {ex.Message}");
            }
        }

        // Oyuncu sunucuya bağlandığında, eğer "jb" haritasında ise ban durumuna göre timer oluştur.
        private void OnClientConnected(int playerSlot)
        {
            if (!NativeAPI.GetMapName().ToLower().Contains("jb_"))
                return;

            var player = Utilities.GetPlayerFromSlot(playerSlot);
            if (player == null)
                return;

            // 5 saniye beklemek için timer ekleyelim
            AddTimer(5.0f, () =>
            {
                // Ban kontrolü
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    var banRecord = connection.QueryFirstOrDefault<dynamic>(
                        "SELECT ban_expiry FROM ct_teamban WHERE steamid = @SteamID;",
                        new { SteamID = player.AuthorizedSteamID.SteamId64 }
                    );
                    if (banRecord != null)
                    {
                        long banExpiry = banRecord.ban_expiry;
                        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        if (banExpiry > now)
                        {
                            // Başarılıysa, oyuncu için timer oluştur.
                            PlayerCTBanExpiry[player.SteamID.ToString()] = banExpiry;
                            StartBanTimer(player);
                        }
                        else
                        {
                            // Süresi dolmuşsa kayıt silinir.
                            connection.Execute("DELETE FROM ct_teamban WHERE steamid = @SteamID;", new { SteamID = player.AuthorizedSteamID.SteamId64 });
                        }
                    }
                    connection.Close();
                }
            });

            return;
        }


        // Oyuncu disconnect olduğunda timer'ı iptal et.
        private void OnClientDisconnect(int playerSlot)
        {
            var player = Utilities.GetPlayerFromSlot(playerSlot);
            if (player != null)
                StopBanTimer(player);

            return;
        }

        // Belirli aralıklarla CT takımında olup olmadığını kontrol eden timer'ı başlatır.
        private void StartBanTimer(CCSPlayerController player)
        {
            // Eğer zaten timer varsa, yeniden oluşturma.
            if (bannedTimers.ContainsKey(player) && bannedTimers[player] != null)
                return;

            // 3 saniyede bir çalışacak şekilde timer oluştur
            Timer timer = AddTimer(3.0f, () =>
            {
                if (!player.is_valid())
                {
                    StopBanTimer(player);
                    return;
                }

                long banExpiry = PlayerCTBanExpiry[player.SteamID.ToString()];
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (banExpiry <= now)
                {
                    // Süresi dolmuşsa veritabanından sil ve timer'ı iptal et.
                    using (var connection = new MySqlConnection(ConnectionString))
                    {
                        connection.Open();
                        connection.Execute("DELETE FROM ct_teamban WHERE steamid = @SteamID;", new { SteamID = player.AuthorizedSteamID.SteamId64 });
                        connection.Close();
                    }

                    StopBanTimer(player);
                    return;
                }

                // Eğer oyuncu CT takımındaysa (örneğin is_ct() true döndürüyorsa) T takımına aktar
                if (player.is_ct())
                {
                    // Takım değiştirme metodu API'nize göre farklılık gösterebilir.
                    // Burada örnek olarak ChangeTeam metodunu kullanıyoruz ve T takımının numarasını 2 kabul ediyoruz.
                    player.ChangeTeam(CsTeam.Terrorist);
                    player.PrintToChat($"{ReplaceTags(Config.EklentiTagi)} CT yasağınız var, T takımına aktarılıyorsunuz!");
                }

            }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);

            bannedTimers[player] = timer;
        }

        // Timer'ı sonlandırır.
        private void StopBanTimer(CCSPlayerController player)
        {
            if (bannedTimers.ContainsKey(player) && bannedTimers[player] != null)
            {
                bannedTimers[player]!.Kill();
                bannedTimers[player] = null;
            }
        }

        // Basit tag değiştirme fonksiyonu
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

            for (int i = 0; i < colorPatterns.Length; i++)
            {
                message = !remove ? "\u200e" + message.Replace(colorPatterns[i], colorReplacements[i])
                                  : "\u200e" + message.Replace(colorPatterns[i], "");
            }
            return message;
        }
    }
}




public static class Lib
{
    static public bool is_valid(this CCSPlayerController? player)
    {
        return player != null && player.IsValid && player.PlayerPawn.IsValid && player.Connected == PlayerConnectedState.PlayerConnected;
    }

    static public bool is_t(this CCSPlayerController? player)
    {
        return is_valid(player) && player.TeamNum == 2;
    }

    static public bool is_ct(this CCSPlayerController? player)
    {
        return is_valid(player) && player.TeamNum == 3;
    }

    // yes i know the null check is redundant but C# is dumb
    static public bool is_valid_alive(this CCSPlayerController? player)
    {
        return player.is_valid() && player.PawnIsAlive && player.get_health() > 0;
    }

    static public CCSPlayerPawn? pawn(this CCSPlayerController? player)
    {
        if (player == null || !player.is_valid())
        {
            return null;
        }

        CCSPlayerPawn? pawn = player.PlayerPawn.Value!;
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