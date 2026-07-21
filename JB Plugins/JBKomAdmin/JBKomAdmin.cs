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
using YamlDotNet.Core.Tokens;

namespace JBKomAdmin;

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
	
	[JsonPropertyName("ka_prefix")]
    public string ka_tag { get; set; } = "[KA]";

    [JsonPropertyName("ka_immunity")]
    public int ka_doku { get; set; } = 99;
}
public class JBKomAdmin : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Kom Admin";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "JB haritalarinda !komadmin komutu ile komutcu admin secilir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;


    private readonly Dictionary<CCSPlayerController, int> EskiDoku = new();
    private readonly Dictionary<CCSPlayerController, int> iKaSuresi = new();
    private readonly Dictionary<CCSPlayerController, Timer?> timer_ex = new();
    public string ConnectionString = "";

    CCSPlayerController? iWarden = null;
    CCSPlayerController? KomutcuAdmini = null;

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
            Console.WriteLine($"[Warden Admin] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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

			AddCommand("css_komadmin", "Komutcu admin sec veya goruntule.", (player, command) => KomAdmin(player, command));
			AddCommand("css_ka", "Komutcu admin sec veya goruntule.", (player, command) => KomAdmin(player, command));
			AddCommand("css_komadmin0", "Komutcu admini sil.", (player, command) => KomAdmin0(player, command));
			AddCommand("css_ka0", "Komutcu admini sil.", (player, command) => KomAdmin0(player, command));

			AddCommand("css_topka", "Komutcu admin siralamasini goruntuler.", (player, command) => TopKomutcu(player, command));
			AddCommand("css_topka0", "Komutcu admin siralamasini sifirlar.", (player, command) => TopKomutcu0(player, command));


			RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
			RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);

			AddCommandListener("say", (player, info) => {
				if (player == null || !player.IsValid || info.GetArg(1).Length == 0 || player.AuthorizedSteamID == null) return HookResult.Continue;
				if (info.GetArg(1).StartsWith("!") || info.GetArg(1).StartsWith("@") || info.GetArg(1).StartsWith("/") || info.GetArg(1).StartsWith(".") || info.GetArg(1) == "rtv") return HookResult.Continue;

				if (KomutcuAdmini.is_valid() && player == KomutcuAdmini)
				{
                    var deathstring = "☠ ";
                    if (KomutcuAdmini.is_valid_alive())
                        deathstring = "";

                    Server.PrintToChatAll($"{deathstring} \x0b{Config.ka_tag} \x08{player.PlayerName} \x01: {info.GetCommandString.Replace("say ", "").Replace("\"", "")}");
					return HookResult.Handled;
				}

				return HookResult.Continue;
			}, HookMode.Pre);

			AddCommandListener("say_team", (player, info) => {
				if (player == null || !player.IsValid || info.GetArg(1).Length == 0 || player.AuthorizedSteamID == null) return HookResult.Continue;
				if (info.GetArg(1).StartsWith("!") || info.GetArg(1).StartsWith("@") || info.GetArg(1).StartsWith("/") || info.GetArg(1).StartsWith(".") || info.GetArg(1) == "rtv") return HookResult.Continue;

				if (KomutcuAdmini.is_valid() && player == KomutcuAdmini)
				{
                    var deathstring = "☠ ";
                    if (KomutcuAdmini.is_valid_alive())
                        deathstring = "";

                    foreach (var p in Utilities.GetPlayers())
						if (p.is_valid() && p.TeamNum == KomutcuAdmini.TeamNum)
							p.PrintToChat($"{deathstring} \x0f[T] \x0b{Config.ka_tag} \x08{player.PlayerName} \x01: {info.GetCommandString.Replace("say_team ", "").Replace("\"", "")}");
					
					return HookResult.Handled;
				}

				return HookResult.Continue;
            }, HookMode.Pre);



            ConnectionString = $"Server={Config.db_host};Port={Config.db_port};User ID={Config.db_user};Password={Config.db_pass};Database={Config.db_name};";
			using (var connection = new MySqlConnection(ConnectionString))
			{
				connection.Open();
				connection.Execute(@"CREATE TABLE IF NOT EXISTS `komutcu_admin` (`id` INT AUTO_INCREMENT PRIMARY KEY, `name` VARCHAR(255) NOT NULL, `steamid` VARCHAR(17) UNIQUE NOT NULL, `ka_suresi` INT NOT NULL DEFAULT 0);");
			}
		}
    }

    public void KomAdmin(CCSPlayerController? player, CommandInfo command)
    {
        if(player == null || !NativeAPI.GetMapName().Contains("jb_")) return;

        if (command.ArgCount <= 1)
        {
            if (KomutcuAdmini.is_valid())
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["warden_admin_info", KomutcuAdmini.PlayerName]);
            else
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["warden_admin_no"]);

            return;
        }


        if (AdminManager.PlayerHasPermissions(player, "@css/generic") || iWarden == player)
        {
            var target = GetPlayerFromUserIdOrName(command.GetArg(1).ToLower());

            if (target.is_valid())
            {
                var oKomutcuAdmini = KomutcuAdmini;

                KomutcuAdmini = target;
                Server.ExecuteCommand($"css_tag_mute {KomutcuAdmini.AuthorizedSteamID.SteamId64}");
                if (KomutcuAdmini == null) return;

                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["warden_admin_new", KomutcuAdmini.PlayerName]);

                EskiDoku[KomutcuAdmini] = Convert.ToUInt16(AdminManager.GetPlayerImmunity(KomutcuAdmini));
                AdminManager.SetPlayerImmunity(KomutcuAdmini, Convert.ToUInt16(Config.ka_doku));
                SetPlayerClanTag(KomutcuAdmini, Config.ka_tag);

                if (oKomutcuAdmini.is_valid())
                {
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["warden_admin_removed", oKomutcuAdmini.PlayerName]);
                    AdminManager.SetPlayerImmunity(oKomutcuAdmini, Convert.ToUInt16(EskiDoku[oKomutcuAdmini]));
                    SetPlayerClanTag(oKomutcuAdmini, "");

                    Server.ExecuteCommand($"css_tag_unmute {oKomutcuAdmini.AuthorizedSteamID.SteamId64}");
                }
            }
            else
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["target_not_found"]);
        }
        else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
    }

    public void KomAdmin0(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !NativeAPI.GetMapName().Contains("jb_")) return;

        if (AdminManager.PlayerHasPermissions(player, "@css/generic") || iWarden == player)
        {
            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["unwarden_admin", player.PlayerName]);
            
            var oKomutcuAdmini = KomutcuAdmini;
            KomutcuAdmini = null;

            if (oKomutcuAdmini.is_valid())
            {
                SetPlayerClanTag(oKomutcuAdmini, "");
                AdminManager.SetPlayerImmunity(oKomutcuAdmini, Convert.ToUInt16(EskiDoku[oKomutcuAdmini]));
                Server.ExecuteCommand($"css_tag_unmute {oKomutcuAdmini.AuthorizedSteamID.SteamId64}");
            }

         } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
    }

















    public void TopKomutcu(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        int dakika = iKaSuresi[player];
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
                var topPlayersQuery = $@"
                        SELECT name, ka_suresi
                        FROM komutcu_admin
                        WHERE ka_suresi > 0
                        ORDER BY ka_suresi DESC
                        LIMIT {Config.ToptimeLimit};";

                var topPlayers = connection.Query(topPlayersQuery).ToList();

                if (topPlayers.Any())
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["menu_title"]);
                    player.PrintToChat($"-----------------------------------------------");

                    for (int i = 0; i < topPlayers.Count; i++)
                    {
                        var topPlayerInfo = topPlayers[i];

                        dakika = topPlayerInfo.ka_suresi;
                        saat = 0;

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

    public void TopKomutcu0(CCSPlayerController? player, CommandInfo command)
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
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            var player = Utilities.GetPlayerFromSlot(playerSlot);
            iKaSuresi[player] = 0;

            AddTimer(5.0f, () =>
            {
                if (player.is_valid())
                {
                    GetPlayerData(player);

                    timer_ex[player] = AddTimer(60.0f, () =>
                    {
                        if (KomutcuAdmini != null && KomutcuAdmini.is_valid() && KomutcuAdmini == player)
                        {
                            iKaSuresi[player]++;

                            using (var connection = new MySqlConnection(ConnectionString))
                            {
                                connection.Open();
                                connection.Execute($"UPDATE komutcu_admin SET name = '{Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", "")}', ka_suresi = {iKaSuresi[player]} WHERE steamid = '{player.SteamID}';");
                            }
                        }
                    }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
                }
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }
    }
    private void OnClientDisconnect(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);

        if (!player.is_valid()) return;
        if (player == KomutcuAdmini) KomutcuAdmini = null;

        if (timer_ex[player] != null)
            timer_ex[player]!.Kill();

        timer_ex[player] = null;
    }

    public void GetPlayerData(CCSPlayerController? player)
    {
        if (player.is_valid())
        {
            iKaSuresi[player] = 0;
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var playerData = connection.QueryFirstOrDefault($"SELECT * FROM komutcu_admin WHERE steamid = '{player.SteamID}'");

                if (playerData != null)
                    iKaSuresi[player] = playerData.ka_suresi;
                else
                    InsertPlayer(player);
            }
        }
    }

    public void InsertPlayer(CCSPlayerController? player)
    {
        if (player.is_valid())
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var insertQuery = $"INSERT INTO komutcu_admin (name, steamid) VALUES (@Name, @SteamID);";
                connection.Execute(insertQuery, new { Name = Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", ""), SteamID = player.SteamID });
            }

            iKaSuresi[player] = 0;
        }
    }

    public void Reset()
    {
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Execute("TRUNCATE TABLE komutcu_admin;");
        }

        foreach (var p in Utilities.GetPlayers())
            if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
                InsertPlayer(p);
    }





    public void SetPlayerClanTag(CCSPlayerController player, string tag)
    {
        player.Clan = tag;

        AddTimer(0.2f, () =>
        {
            Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
            Utilities.SetStateChanged(player, "CBasePlayerController", "m_iszPlayerName");
        }, TimerFlags.STOP_ON_MAPCHANGE);
    }



    private CCSPlayerController? GetPlayerFromUserIdOrName(string player, bool isSteamId = false)
    {
        if (player.StartsWith('#') && int.TryParse(player.Trim('#'), out var index))
            return Utilities.GetPlayerFromUserid(index);


        foreach (var p in Utilities.GetPlayers())
            if (p.is_valid() && AdminManager.PlayerHasPermissions(p, "@css/generic")){

                string playername = p.PlayerName.ToLower();
                bool check = playername.Contains(player);

                if (check)
                    return p;
            }

        return null;
    }

    public void Warden(CCSPlayerController? player, CommandInfo command)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
            if ((iWarden == null || !iWarden.is_valid() || !iWarden.is_ct()) && player.is_valid() && player.is_ct())
                iWarden = player;

        return;
    }

    public void UnWarden(CCSPlayerController? player, CommandInfo command)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
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