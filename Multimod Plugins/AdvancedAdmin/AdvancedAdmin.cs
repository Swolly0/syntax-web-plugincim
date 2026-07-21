using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using Dapper;
using MenuManager;
using Microsoft.Extensions.Localization;
using MySqlConnector;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AdvancedAdmin
{
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

    public class AdvancedAdmin : BasePlugin, IPluginConfig<Config>
    {
        public override string ModuleName { get; } = "Advanced Admin";
        public override string ModuleVersion { get; } = "1.0.0";
        public override string ModuleDescription { get; } = "!admin komutuyla ban-mute-gag menusu acilir.";
        public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
        private int ModuleConfigVersion => 1;
        internal static IStringLocalizer? Stringlocalizer;
        public string? ConnectionString;

        public string BannedFilePath = string.Empty;
        public string LogsFilePath = string.Empty;

        private IMenuApi? _api;
        private readonly PluginCapability<IMenuApi?> _pluginCapability = new("menu:nfcore");

        // Seçim durumları
        // iTarget: yönetici -> seçilen hedef oyuncu nesnesi
        private static readonly Dictionary<CCSPlayerController, CCSPlayerController?> iTarget = new();
        private static readonly Dictionary<CCSPlayerController, string> iProcess = new();
        // Dakika, kalıcı = -1
        private static readonly Dictionary<CCSPlayerController, int> iTime = new();

        private static readonly Dictionary<CCSPlayerController, int> iMute = new();
        private static readonly Dictionary<CCSPlayerController, int> iGag = new();



        // LISANS
        public int lisans_bitis_yil = 2025;
        public int lisans_bitis_ay = 12;
        public int lisans_bitis_gun = 30;
        // LISANS

        public required Config Config { get; set; }
        public void OnConfigParsed(Config config)
        {
            if (config.Version < ModuleConfigVersion)
            {
                Console.WriteLine($"[AdvancedAdmin] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
                AddCommand("css_admin", "", (player, command) => AdminMenu(player, command));
                AddCommand("css_bn", "", (player, command) => Command_Ban(player, command));
                AddCommand("css_unbn", "", (player, command) => Command_Unban(player, command));

                RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
                RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);

                AddCommandListener("say", Command_Say, HookMode.Pre);
                AddCommandListener("say_team", Command_SayTeam, HookMode.Pre);

                ConnectionString = $"Server={Config.db_host};Port={Config.db_port};User ID={Config.db_user};Password={Config.db_pass};Database={Config.db_name};";
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    connection.Execute(@"CREATE TABLE IF NOT EXISTS `advanced_admin` (`id` INT AUTO_INCREMENT PRIMARY KEY, `name` VARCHAR(255) NOT NULL, `steamid` VARCHAR(32) UNIQUE NOT NULL, `ban` INT NOT NULL DEFAULT 0, `mute` INT NOT NULL DEFAULT 0, `gag` INT NOT NULL DEFAULT 0);");
                    connection.Execute(@"CREATE TABLE IF NOT EXISTS `logs` (`id` INT AUTO_INCREMENT PRIMARY KEY, `type` VARCHAR(32) NOT NULL, `admin_steamid` VARCHAR(32) NOT NULL, `admin_name` VARCHAR(32) NOT NULL, `target_steamid` VARCHAR(32) NOT NULL, `target_name` VARCHAR(32) NOT NULL, `reason` VARCHAR(255), `time` BIGINT NOT NULL, `action_time` BIGINT NOT NULL);");
                }

                BannedFilePath = Path.Combine(ModuleDirectory, "banned.txt");
                LogsFilePath = Path.Combine(ModuleDirectory, "logs");
                //CleanupOldLogs();
            }
        }

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            _api = _pluginCapability.Get();
            if (_api == null) Console.WriteLine("MenuManager Core not found...");
        }


        // -------------------
        // YENİ KOMUTLAR
        // -------------------
        private void Command_Ban(CCSPlayerController? admin, CommandInfo info)
        {
            if (!admin.is_valid()) return;

            if (!AdminManager.PlayerHasPermissions(admin, "@css/ban"))
            {
                admin?.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["no_perm"]}"));
                return;
            }

            if (info.ArgCount < 3)
            {
                admin?.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["ban_usage"]}"));
                return;
            }

            var targetArg = info.GetArgTargetResult(1);
            string timeArg = info.GetArg(2);
            string reason = string.Join(" ", Enumerable.Range(3, info.ArgCount - 3).Select(i => info.GetArg(i)));

            if (!int.TryParse(timeArg, out int minutes))
            {
                admin?.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["ban_invalid_time"]}"));
                return;
            }

            if (targetArg.Players.Count <= 0)
            {
                admin.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["invalid_target"]}"));
                return;
            }

            var target = targetArg.Players.First();
            string targetSteam = target?.SteamID.ToString() ?? "Unknown";
            string targetName = target?.PlayerName ?? "Unknown";

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long endTime = minutes > 0 ? now + minutes * 60 : -1;

            // DB güncelle
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                connection.Execute(
                    "UPDATE advanced_admin SET ban=@EndTime WHERE steamid=@SteamID",
                    new { EndTime = endTime, SteamID = targetSteam }
                );
            }

            // banned.txt güncelle
            AddToBannedFile(targetSteam);

            // Log ekle
            InsertLog("ban", admin?.SteamID.ToString(), admin?.PlayerName ?? "Unknown",
                      targetSteam, targetName, reason, minutes);

            // Oyuncuyu kickle
            if (target != null && target.is_valid())
                Server.ExecuteCommand($"kickid {target.UserId} {Localizer["ban_kick_reason", reason]}");

            admin.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["ban_success", targetName, targetSteam]}"));
        }

        private void Command_Unban(CCSPlayerController? admin, CommandInfo info)
        {
            if (!admin.is_valid()) return;

            if (!AdminManager.PlayerHasPermissions(admin, "@css/ban"))
            {
                admin?.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["no_perm"]}"));
                return;
            }

            if (info.ArgCount < 2)
            {
                admin?.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["unban_usage"]}"));
                return;
            }

            string targetSteam = info.GetArg(1);

            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                connection.Execute(
                    "UPDATE advanced_admin SET ban=0 WHERE steamid=@SteamID",
                    new { SteamID = targetSteam }
                );
            }

            RemoveFromBannedFile(targetSteam);

            InsertLog("unban", admin?.SteamID.ToString(), admin?.PlayerName ?? "Unknown",
                      targetSteam, "Unknown", "Unban", 0);

            admin.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["unban_success", targetSteam]}"));
        }


        // -------------------
        // MENÜ & AKIŞ
        // -------------------
        public void AdminMenu(CCSPlayerController? player, CommandInfo command)
        {
            if (!player.is_valid()) return;


            if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
            {
                ClearState(player);
                var menu = _api!.GetMenu(Localizer["admin_menu_txt"]);
                menu.AddMenuOption(Localizer["admin_menu_ban_list"], (p, _) => AM_Process(p, "ban_list"));
                menu.AddMenuOption(Localizer["admin_menu_ban"], (p, _) => AM_Process(p, "ban"));
                menu.AddMenuOption(Localizer["admin_menu_kick"], (p, _) => AM_Process(p, "kick"));
                menu.AddMenuOption(Localizer["admin_menu_mute"], (p, _) => AM_Process(p, "mute"));
                menu.AddMenuOption(Localizer["admin_menu_gag"], (p, _) => AM_Process(p, "gag"));

                if (AdminManager.PlayerHasPermissions(player, "@css/root"))
                    menu.AddMenuOption(Localizer["logs_menu_title"], (p, _) => ShowLogMenu(p));

                menu.Open(player);
            }
            else
            {
                player?.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["no_perm"]}"));
            }
        }

        public void AM_Process(CCSPlayerController player, string option)
        {
            if (string.IsNullOrWhiteSpace(option))
            {
                _api?.CloseMenu(player);
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["invalid_process"]}"));
                return;
            }

            if (option == "ban_list")
            {
                ShowActiveBans(player);
                return;
            }

            iProcess[player] = option;
            SelectPlayer(player);
        }


        public void SelectPlayer(CCSPlayerController player)
        {
            if (!player.is_valid() || _api == null) return;

            var menu = _api.GetMenu(Localizer["select_player"]);
            menu.AddMenuOption(Localizer["admin_menu_back"], (p, _) => AM_Target(p, "back"));

            // Seçilecek işlem (ban/mute/gag) kontrolü
            string? currentProcess = null;
            iProcess.TryGetValue(player, out currentProcess);

            foreach (var target in Utilities.GetPlayers().Where(p => p.is_valid()).ToList())
            {
                //if (target == player) continue; // Yönetici kendisini seçemesin

                string extraInfo = "";

                // İşlem varsa bitiş tarihini ekle
                if (!string.IsNullOrEmpty(currentProcess))
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    switch (currentProcess.ToLowerInvariant())
                    {
                        case "mute":
                            if (iMute.TryGetValue(target, out int muteEnd))
                            {
                                if (muteEnd == -1)
                                    extraInfo = $" • {Localizer["permanent"]}";
                                else if (muteEnd > now)
                                    extraInfo = $" • Bitiş: {FormatUnixTime(muteEnd)}";
                            }
                            break;
                        case "gag":
                            if (iGag.TryGetValue(target, out int gagEnd))
                            {
                                if (gagEnd == -1)
                                    extraInfo = $" • {Localizer["permanent"]}";
                                else if (gagEnd > now)
                                    extraInfo = $" • Bitiş: {FormatUnixTime(gagEnd)}";
                            }
                            break;
                    }
                }

                // Menü satırı: "Ad (#UserId) • Bitiş: ..."
                var line = $"{target.PlayerName} (#{target.UserId}){extraInfo}";
                menu.AddMenuOption(line, (p, _) => AM_Target(p, $"{target.UserId}"));
            }

            menu.Open(player);
        }

        public void AM_Target(CCSPlayerController player, string option)
        {
            if (option == "back")
            {
                AdminMenu(player, null);
                return;
            }

            // Önceki hedefi sıfırla
            iTarget[player] = null;

            if (string.IsNullOrWhiteSpace(option) || !int.TryParse(option, out int targetId))
            {
                _api?.CloseMenu(player);
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["invalid_target"]}"));
                return;
            }

            var target = Utilities.GetPlayers().FirstOrDefault(p => p != null && p.is_valid() && p.UserId == targetId);
            if (target == null || !target.is_valid())
            {
                _api?.CloseMenu(player);
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["target_not_found"]}"));
                return;
            }

            // Hedefi kaydet
            iTarget[player] = target;

            // Mevcut işlem
            if (!iProcess.TryGetValue(player, out var process) || string.IsNullOrEmpty(process))
            {
                SelectTime(player);
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool removed = false;

            switch (process.ToLowerInvariant())
            {
                case "mute":
                    if (iMute.TryGetValue(target, out int muteEnd) && muteEnd > now || muteEnd == -1)
                    {
                        iMute[target] = 0;
                        target.VoiceFlags = VoiceFlags.Normal;

                        RemoveMuteFromDatabase(target);
                        removed = true;

                        // Logla
                        _ = InsertLog("unmute", player?.SteamID.ToString(), player?.PlayerName ?? "Unknown",
                                           target.SteamID.ToString(), target.PlayerName, "Mute kaldırıldı", 0);
                    }
                    break;

                case "gag":
                    if (iGag.TryGetValue(target, out int gagEnd) && gagEnd > now || gagEnd == -1)
                    {
                        iGag[target] = 0;
                        RemoveGagFromDatabase(target);
                        removed = true;

                        // Logla
                        _ = InsertLog("ungag", player?.SteamID.ToString(), player?.PlayerName ?? "Unknown",
                                           target.SteamID.ToString(), target.PlayerName, "Gag kaldırıldı", 0);
                    }
                    break;
            }

            if (removed)
            {
                _api?.CloseMenu(player);

                string processDisplay = GetProcessDisplayName(process);

                // Hedefe mesaj
                target.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["removed_action_target", processDisplay]}"));

                // Sunucu broadcast
                Server.PrintToChatAll(
                    ReplaceTags($"{Config.EklentiTagi} {Localizer["removed_broadcast_action", player.PlayerName, processDisplay, target.PlayerName]}")
                );

                // Hedefi sıfırla
                iTarget[player] = null;
                return;
            }

            // Eğer kaldırılacak bir şey yoksa süre menüsüne git
            SelectTime(player);
        }

        public void SelectTime(CCSPlayerController player)
        {
            if (!player.is_valid() || _api == null) return;

            // Kick için süre gereksiz → direkt uygula
            if (iProcess.TryGetValue(player, out var proc) && string.Equals(proc, "kick", StringComparison.OrdinalIgnoreCase))
            {
                iTime[player] = 0; // kick süresiz/ani işlem
                SelectReason(player);
                return;
            }

            var menu = _api.GetMenu(Localizer["select_time"]);
            menu.AddMenuOption(Localizer["admin_menu_back"], (p, _) => AM_Time(p, -31));

            // Süre seçenekleri (dakika cinsinden)
            menu.AddMenuOption(Localizer["time_perm"], (p, _) => AM_Time(p, -1));
            menu.AddMenuOption(Localizer["time_1m"], (p, _) => AM_Time(p, 30 * 24 * 60));
            menu.AddMenuOption(Localizer["time_1w"], (p, _) => AM_Time(p, 7 * 24 * 60));
            menu.AddMenuOption(Localizer["time_1d"], (p, _) => AM_Time(p, 24 * 60));
            menu.AddMenuOption(Localizer["time_12h"], (p, _) => AM_Time(p, 12 * 60));
            menu.AddMenuOption(Localizer["time_6h"], (p, _) => AM_Time(p, 6 * 60));
            menu.AddMenuOption(Localizer["time_1h"], (p, _) => AM_Time(p, 60));
            menu.Open(player);
        }

        public void AM_Time(CCSPlayerController player, int minutes)
        {
            if (minutes == -31)
            {
                SelectPlayer(player);
                return;
            }

            iTime[player] = minutes;
            SelectReason(player);
        }

        // -------------------
        // SÜRE SEÇİMİNDEN SONRA SEBEP MENÜSÜ
        // -------------------
        public void SelectReason(CCSPlayerController player)
        {
            if (!player.is_valid() || _api == null) return;

            var menu = _api.GetMenu(Localizer["select_reason"]);
            menu.AddMenuOption(Localizer["admin_menu_back"], (player, _) => ExecuteAction(player, "back"));

            // 1'den 9'a kadar sebep seçenekleri
            for (int i = 1; i <= 9; i++)
            {
                string reasonKey = $"reason_{i}";
                string reasonText = Localizer[reasonKey];

                if (!string.IsNullOrEmpty(reasonText))
                {
                    menu.AddMenuOption(reasonText, (player, _) =>
                    {
                        ExecuteAction(player, reasonText);
                    });
                }
            }

            menu.Open(player);
        }

        // -------------------
        // EYLEMİ GERÇEKLEŞTİR & BROADCAST
        // -------------------

        private void ExecuteAction(CCSPlayerController actor, string reasonText)
        {
            if (reasonText == "back")
            {
                SelectTime(actor);
                return;
            }

            // Menü kapat (her durumda)
            _api?.CloseMenu(actor);

            if (!iProcess.TryGetValue(actor, out var process) ||
                !iTarget.TryGetValue(actor, out var target) ||
                target == null || !target.is_valid())
            {
                actor.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["missing_data"]}"));
                ClearState(actor);
                return;
            }

            int minutes = 0;
            iTime.TryGetValue(actor, out minutes); // yoksa 0 kalır

            string sureMetni = FormatDurationText(minutes);
            string processDisplay = GetProcessDisplayName(process);

            // Hedef oyuncuya mesaj
            target.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["action_target", processDisplay, sureMetni]}"));

            // Sunucu broadcast
            string broadcastMsg = ReplaceTags($"{Config.EklentiTagi} {Localizer["broadcast_action", actor.PlayerName, processDisplay, target.PlayerName, sureMetni]}");
            Server.PrintToChatAll(broadcastMsg);
            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} \x02{reasonText}"));

            // Unix zamanı
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long endTime = minutes > 0 ? now + minutes * 60 : (process == "kick" ? 0 : -1); // -1 = kalıcı

            using var connection = new MySqlConnection(ConnectionString);
            connection.Open();

            switch (process.ToLowerInvariant())
            {
                case "ban":
                    connection.Execute(
                        "UPDATE advanced_admin SET ban = @EndTime WHERE steamid = @SteamID",
                        new { EndTime = endTime, SteamID = target.SteamID }
                    );

                    // banned.txt dosyasına ekle
                    AddToBannedFile(target.SteamID.ToString());

                    Server.ExecuteCommand($"kickid {target.UserId} " + Localizer["ban_target", FormatUnixTime(endTime)]);
                    break;

                case "kick":
                    Server.ExecuteCommand($"kickid {target.UserId} " + Localizer["kick_target"]);
                    break;

                case "mute":
                    connection.Execute(
                        "UPDATE advanced_admin SET mute = @EndTime WHERE steamid = @SteamID",
                        new { EndTime = endTime, SteamID = target.SteamID }
                    );
                    iMute[target] = (int)endTime;
                    target.VoiceFlags = VoiceFlags.Muted;
                    break;

                case "gag":
                    connection.Execute(
                        "UPDATE advanced_admin SET gag = @EndTime WHERE steamid = @SteamID",
                        new { EndTime = endTime, SteamID = target.SteamID }
                    );
                    iGag[target] = (int)endTime;
                    break;

                default:
                    actor.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["unknown_process", process]}"));
                    break;
            }

            InsertLog(process, actor.SteamID.ToString(), actor.PlayerName, target.SteamID.ToString(), target.PlayerName, reasonText, minutes);
            ClearState(actor);
        }
        private void RemoveMuteFromDatabase(CCSPlayerController player)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                connection.Execute($"UPDATE advanced_admin SET name = '{Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", "")}', mute = 0 WHERE steamid = '{player.SteamID}';");
            }
        }

        private void RemoveGagFromDatabase(CCSPlayerController player)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                connection.Execute($"UPDATE advanced_admin SET name = '{Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", "")}', gag = 0 WHERE steamid = '{player.SteamID}';");
            }
        }

        // Helper: işlem kısa adı -> Localizer string (ör. "Ban", "Kick", ...)
        private string GetProcessDisplayName(string processKey)
        {
            return processKey.ToLowerInvariant() switch
            {
                "ban" => Localizer["proc_ban"],
                "kick" => Localizer["proc_kick"],
                "mute" => Localizer["proc_mute"],
                "gag" => Localizer["proc_gag"],
                _ => Localizer["proc_unknown"]
            };
        }

        // Helper: dakika -> okunaklı Localizer destekli süre metni (saat/gün/hafta/ay/kalıcı)
        private string FormatDurationText(int minutes)
        {
            if (minutes < 0) return Localizer["permanent"];
            if (minutes == 0) return Localizer["instant"];

            // dakikayı en uygun birime çevir
            if (minutes % (30 * 24 * 60) == 0) // ay
            {
                int months = minutes / (30 * 24 * 60);
                return Localizer["duration_months", months];
            }
            if (minutes % (7 * 24 * 60) == 0) // hafta
            {
                int weeks = minutes / (7 * 24 * 60);
                return Localizer["duration_weeks", weeks];
            }
            if (minutes % (24 * 60) == 0) // gün
            {
                int days = minutes / (24 * 60);
                return Localizer["duration_days", days];
            }
            if (minutes % 60 == 0) // saat
            {
                int hours = minutes / 60;
                return Localizer["duration_hours", hours];
            }

            // aksi halde dakika göster
            return Localizer["duration_minutes", minutes];
        }

        private static void ClearState(CCSPlayerController player)
        {
            iProcess.Remove(player);
            iTarget.Remove(player);
            iTime.Remove(player);
        }








        private void ShowActiveBans(CCSPlayerController admin)
        {
            if (!admin.is_valid() || _api == null) return;

            var menu = _api.GetMenu(Localizer["admin_menu_ban_list"]);
            menu.AddMenuOption(Localizer["admin_menu_back"], (a, _) => RemoveBan(admin, "back", ""));

            using var connection = new MySqlConnection(ConnectionString);
            connection.Open();

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var activeBans = connection.Query<dynamic>(
                "SELECT name, steamid, ban FROM advanced_admin WHERE ban > @Now OR ban = -1",
                new { Now = now }
            ).ToList();

            if (activeBans.Count == 0)
            {
                admin.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["no_active_bans"]}"));
                return;
            }

            foreach (var p in activeBans)
            {
                string nameWithTime = p.ban == -1
                    ? $"{p.name} ({Localizer["permanent"]})"
                    : $"{p.name} ({FormatUnixTime(p.ban)})";
                string steamId = p.steamid.ToString();

                menu.AddMenuOption(nameWithTime, (a, _) => RemoveBan(a, steamId, p.name));
            }

            menu.Open(admin);
        }
        private void RemoveBan(CCSPlayerController admin, string steamId, string playerName)
        {
            if (!string.IsNullOrWhiteSpace(steamId) && !string.IsNullOrWhiteSpace(playerName))
            {
                if (steamId == "back")
                {
                    AdminMenu(admin, null);
                    return;
                }

                using var connection = new MySqlConnection(ConnectionString);
                connection.Open();

                connection.Execute(
                    "UPDATE advanced_admin SET ban = 0 WHERE steamid = @SteamID",
                    new { SteamID = steamId }
                );

                admin.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["ban_removed_success", playerName]}"));
                InsertLog("unban", admin.SteamID.ToString(), admin.PlayerName, steamId, playerName, "", 0);

                _api?.CloseMenu(admin);
            }
        }

        private void ShowLogMenu(CCSPlayerController player)
        {
            var menu = _api!.GetMenu(Localizer["logs_menu_title"]);
            menu.AddMenuOption(Localizer["admin_menu_back"], (p, _) => ShowLogs(p, "back"));
            menu.AddMenuOption(Localizer["logs_menu_all"], (p, _) => ShowLogs(p, "all"));
            menu.AddMenuOption(Localizer["logs_menu_ban"], (p, _) => ShowLogs(p, "ban"));
            menu.AddMenuOption(Localizer["logs_menu_unban"], (p, _) => ShowLogs(p, "unban"));
            menu.AddMenuOption(Localizer["logs_menu_mute"], (p, _) => ShowLogs(p, "mute"));
            menu.AddMenuOption(Localizer["logs_menu_gag"], (p, _) => ShowLogs(p, "gag"));
            menu.Open(player);
        }

        private void ShowLogs(CCSPlayerController player, string type)
        {
            if (type == "back")
            {
                AdminMenu(player, null);
                return;
            }
            else if (type == "back2")
            {
                ShowLogMenu(player);
                return;
            }

            string title = type switch
            {
                "ban" => Localizer["logs_title_ban"],
                "unban" => Localizer["logs_title_unban"],
                "mute" => Localizer["logs_title_mute"],
                "gag" => Localizer["logs_title_gag"],
                "all" => Localizer["logs_title_all"],
                _ => Localizer["logs_title_default"]
            };

            var menu = _api!.GetMenu(title);
            menu.AddMenuOption(Localizer["admin_menu_back"], (p, _) => ShowLogs(p, "back2"));

            using var connection = new MySqlConnection(ConnectionString);
            connection.Open();

            string sql = type == "all"
                ? "SELECT * FROM logs ORDER BY id DESC LIMIT 100"
                : "SELECT * FROM logs WHERE type=@Type ORDER BY id DESC LIMIT 100";

            var rows = connection.Query<dynamic>(sql, new { Type = type }).ToList();
            if (rows.Count == 0)
                menu.AddMenuOption(Localizer["logs_no_records"], (p, _) => ShowLogs(p, type), true);
            else
            {
                foreach (var row in rows)
                {
                    string prefix = type == "all" ? $"({row.type}) " : "";
                    string line = prefix + Localizer["logs_line_format",
                        row.admin_name,
                        row.target_name,
                        row.reason,
                        row.time,
                        FormatUnixTime(row.action_time)
                    ];
                    menu.AddMenuOption(line, (p, _) => ShowLogs(p, type), true);
                }
            }

            menu.Open(player);
        }










        private void OnClientConnected(int playerSlot)
        {
            var player = Utilities.GetPlayerFromSlot(playerSlot);

            if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
                LoadPlayerData(player);
        }


        private void LoadPlayerData(CCSPlayerController player)
        {
            iMute[player] = 0;
            iGag[player] = 0;

            // banned.txt kontrolü
            var steamId = player.SteamID.ToString();
            var bannedList = File.Exists(BannedFilePath)
                ? File.ReadAllLines(BannedFilePath).ToList()
                : new List<string>();

            bool isInFile = bannedList.Contains(steamId);

            using var connection = new MySqlConnection(ConnectionString);
            connection.Open();

            var playerData = connection.QueryFirstOrDefault(
                "SELECT * FROM advanced_admin WHERE steamid = @SteamID",
                new { SteamID = player.SteamID }
            );

            if (playerData != null)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                if (isInFile)
                {
                    // Ban kontrolü (normal işleyiş)
                    if (playerData.ban == -1 || playerData.ban > now)
                    {
                        Server.ExecuteCommand($"kickid {player.UserId} " +
                            Localizer["ban_info_msg", FormatUnixTime(playerData.ban)]);
                    }
                    else
                        RemoveFromBannedFile(player.SteamID.ToString());
                }
                else
                {
                    // Dosyada yok ama DB’de banlı gözüküyor → banı kaldır
                    if (playerData.ban == -1 || playerData.ban > now)
                    {
                        connection.Execute(
                            "UPDATE advanced_admin SET ban = 0 WHERE steamid = @SteamID",
                            new { SteamID = player.SteamID }
                        );
                    }
                }

                // Oyuncuya özel değişkenleri yükle
                iMute[player] = playerData.mute;
                iGag[player] = playerData.gag;

                // Oyuncu adı güncelle
                var sanitizedName = Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", "");
                connection.Execute(
                    "UPDATE advanced_admin SET name = @Name WHERE steamid = @SteamID",
                    new { Name = sanitizedName, SteamID = player.SteamID }
                );
            }
            else
            {
                InsertPlayer(player);
            }
        }

        private void InsertPlayer(CCSPlayerController player)
        {
            using var connection = new MySqlConnection(ConnectionString);
            connection.Open();

            var sanitizedName = Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", "");

            connection.Execute(
                "INSERT INTO advanced_admin (name, steamid) VALUES (@Name, @SteamID)",
                new { Name = sanitizedName, SteamID = player.SteamID }
            );
        }

        // -------------------
        // LOG EKLEME
        // -------------------
        private async Task InsertLog(string type, string adminSteam, string adminName,
                                          string targetSteam, string targetName, string reason, int time)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long endTime = time > 0 ? now + (time * 60) : -1;

            // 1️⃣ Veritabanına asenkron yaz
            try
            {
                await Task.Run(() =>
                {
                    using var connection = new MySqlConnection(ConnectionString);
                    connection.Open();
                    connection.Execute(
                        "INSERT INTO logs (type, admin_steamid, admin_name, target_steamid, target_name, reason, time, action_time) " +
                        "VALUES (@Type, @AdminSteam, @AdminName, @TargetSteam, @TargetName, @Reason, @Time, @ActionTime)",
                        new
                        {
                            Type = type,
                            AdminSteam = adminSteam ?? "Unknown",
                            AdminName = adminName ?? "Unknown",
                            TargetSteam = targetSteam ?? "Unknown",
                            TargetName = targetName ?? "Unknown",
                            Reason = reason ?? "-",
                            Time = time,
                            ActionTime = now
                        }
                    );
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdvancedAdmin] InsertLog DB error: {ex.Message}");
            }

            // 2️⃣ Dosyaya ekleme (append)
            try
            {
                string typeDir = Path.Combine(LogsFilePath, type);
                if (!Directory.Exists(typeDir))
                    Directory.CreateDirectory(typeDir);

                string fileName = DateTime.Now.ToString("dd.MM.yyyy") + ".txt";
                string filePath = Path.Combine(typeDir, fileName);

                var logContent = new StringBuilder();
                logContent.AppendLine($"ADMIN: {adminName} - {adminSteam}");
                logContent.AppendLine($"HEDEF: {targetName} - {targetSteam}");
                logContent.AppendLine($"İŞLEM T: {FormatUnixTime(now)}");

                // Ungag, unmute, unban ise Sebep/Süre/BitişT ekleme
                if (!(type.Equals("unmute", StringComparison.OrdinalIgnoreCase) ||
                      type.Equals("ungag", StringComparison.OrdinalIgnoreCase) ||
                      type.Equals("unban", StringComparison.OrdinalIgnoreCase)))
                {
                    logContent.AppendLine($"SEBEP: {reason}");
                    logContent.AppendLine($"SÜRE: {(time == -1 ? "Süresiz" : time + "dk")}");
                    logContent.AppendLine($"BİTİŞ T: {FormatUnixTime(endTime)}");
                }

                logContent.AppendLine("------------------------------------");

                await File.AppendAllTextAsync(filePath, logContent.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdvancedAdmin] InsertLog file write error: {ex.Message}");
            }
        }

        private void CleanupOldLogs()
        {
            try
            {
                if (!Directory.Exists(LogsFilePath))
                    return;

                foreach (var file in Directory.GetFiles(LogsFilePath, "*.txt", SearchOption.AllDirectories))
                {
                    var creation = File.GetCreationTime(file);
                    if ((DateTime.Now - creation).TotalDays > 7)
                    {
                        File.Delete(file);
                        Console.WriteLine($"[AdvancedAdmin] Old log deleted: {file}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdvancedAdmin] CleanupOldLogs error: {ex.Message}");
            }
        }


        private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (!player.is_valid()) return HookResult.Continue;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (iMute.TryGetValue(player, out int muteEnd))
                if (muteEnd == -1 || muteEnd > now)
                    player.VoiceFlags = VoiceFlags.Muted;

            return HookResult.Continue;
        }

        private HookResult Command_Say(CCSPlayerController? player, CommandInfo info)
        {
            return Command_Say_Handler(player, info);
        }

        private HookResult Command_SayTeam(CCSPlayerController? player, CommandInfo info)
        {
            return Command_Say_Handler(player, info);
        }

        public HookResult Command_Say_Handler(CCSPlayerController? player, CommandInfo info)
        {
            if (!player.is_valid())
                return HookResult.Continue;

            string arg = info.GetArg(1);
            if (arg.StartsWith("!") || arg.StartsWith("/"))
                return HookResult.Continue;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (iGag.TryGetValue(player, out int gagEnd))
                if (gagEnd == -1 || gagEnd > now)
                {
                    string durationText = gagEnd == -1 ? Localizer["permanent"] : FormatUnixTime(gagEnd);
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} {Localizer["gag_active_msg", durationText]}"));


                    foreach (var target in Utilities.GetPlayers().Where(p => p.is_valid()).ToList())
                        if (AdminManager.PlayerHasPermissions(target, "@css/generic"))
                        {
                            var chattxt = info.GetCommandString;
                            bool chatteam = false;

                            if (chattxt.StartsWith("say "))
                                chattxt = chattxt.Substring(5);
                            else if (chattxt.StartsWith("say_team "))
                            {
                                // "say_team " kısmını sil
                                chattxt = chattxt.Substring(10);
                                chatteam = true;
                            }

                            // Mesajın sonunda çift tırnak var mı, varsa kaldır
                            if (chattxt.EndsWith("\""))
                            {
                                chattxt = chattxt.Substring(0, chattxt.Length - 1);
                            }

                            // Adminlere bildirim at
                            if (chatteam)
                                target.PrintToChat(
                                    ReplaceTags($" \x0b[TEAM-GAG] {Localizer["gag_msg_to_admin", player.PlayerName, chattxt]}")
                                );
                            else
                                target.PrintToChat(
                                ReplaceTags($" \x0b[A-GAG] {Localizer["gag_msg_to_admin", player.PlayerName, chattxt]}")
                            );

                        }

                    return HookResult.Stop;
                }

            return HookResult.Continue;
        }



        // banned.txt yönetimi
        private void AddToBannedFile(string steamId)
        {
            var bannedList = File.Exists(BannedFilePath)
                ? File.ReadAllLines(BannedFilePath).ToList()
                : new List<string>();

            if (!bannedList.Contains(steamId))
            {
                bannedList.Add(steamId);
                File.WriteAllLines(BannedFilePath, bannedList);
            }
        }

        private void RemoveFromBannedFile(string steamId)
        {
            if (!File.Exists(BannedFilePath)) return;

            var bannedList = File.ReadAllLines(BannedFilePath).ToList();
            if (bannedList.Remove(steamId))
                File.WriteAllLines(BannedFilePath, bannedList);
        }


        // Unix timestamp'ı okunabilir tarihe çevir
        private string FormatUnixTime(long unixTime)
        {
            if (unixTime == -1)
                return Localizer["permanent"]; // Kalıcı yazısı

            var dt = DateTimeOffset.FromUnixTimeSeconds(unixTime).ToLocalTime();
            return dt.ToString("dd.MM.yyyy HH:mm"); // Türkiye formatı
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

        private static bool IsValidConfigString(string value) => !string.IsNullOrEmpty(value) && value != "-";
    }

    public static class Lib
    {
        public static void Freeze(this CBasePlayerPawn pawn)
        {
            pawn.MoveType = MoveType_t.MOVETYPE_OBSOLETE;
        }

        public static bool is_valid(this CCSPlayerController? player)
        {
            return player != null && player.IsValid && player.PlayerPawn.IsValid;
        }

        public static bool is_t(this CCSPlayerController? player)
        {
            return player != null && is_valid(player) && player.TeamNum == 2;
        }

        public static bool is_ct(this CCSPlayerController? player)
        {
            return player != null && is_valid(player) && player.TeamNum == 3;
        }

        public static bool is_valid_alive(this CCSPlayerController? player)
        {
            return player != null && player.is_valid() && player.PawnIsAlive && player.get_health() > 0;
        }

        public static CCSPlayerPawn? pawn(this CCSPlayerController? player)
        {
            if (player == null || !player.is_valid())
            {
                return null;
            }

            CCSPlayerPawn? pawn = player.PlayerPawn.Value;
            return pawn;
        }

        public static int get_health(this CCSPlayerController? player)
        {
            CCSPlayerPawn? pawn = player.pawn();
            if (pawn == null) return 100;
            return pawn.Health;
        }
    }
}
