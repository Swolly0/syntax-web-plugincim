using System;
using System.IO;
using System.Text.Json;

using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using MySqlConnector;
using Dapper;
using CounterStrikeSharp.API.Modules.Menu;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace MarketSistemi_Rulet;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("db_host")]
    public string? db_host { get; set; } = "localhost";

    [JsonPropertyName("db_user")]
    public string? db_user { get; set; } = "user";

    [JsonPropertyName("db_name")]
    public string? db_name { get; set; } = "database";

    [JsonPropertyName("db_pass")]
    public string? db_pass { get; set; } = "password";

    [JsonPropertyName("db_port")]
    public int? db_port { get; set; } = 3306;

    [JsonPropertyName("min_credits")]
    public int min_kredi { get; set; } = 1;

    [JsonPropertyName("max_credits")]
    public int max_kredi { get; set; } = 100;

    [JsonPropertyName("red_multiplier")]
    public float kirmizi_carpan { get; set; } = 2.0f;

    [JsonPropertyName("black_multiplier")]
    public float siyah_carpan { get; set; } = 2.0f;

    [JsonPropertyName("green_multiplier")]
    public float yesil_carpan { get; set; } = 14.0f;

    [JsonPropertyName("red_rate")]
    public float kirmizi_oran { get; set; } = 48.0f;

    [JsonPropertyName("green_rate")]
    public float yesil_oran { get; set; } = 4.0f;

    [JsonPropertyName("black_rate")]
    public float siyah_oran { get; set; } = 48.0f;
}

public class MarketSistemi_Rulet : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "Market Sistemi - Rulet";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!rulet komutu ile oyuncular kredileri ile rulet oynayabilirler.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    private readonly Dictionary<CCSPlayerController, int> Bahis = new();
    private readonly Dictionary<CCSPlayerController, string> Oynadi = new();
    public int ToplamBahis = 0, ToplamBahisci = 0;
    public int KirmiziBahis = 0, YesilBahis = 0, SiyahBahis = 0;
    public CounterStrikeSharp.API.Modules.Timers.Timer? RuletTimer;

    // LISANS
    public int lisans_bitis_yil = 2024; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 6; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 8; // Lisansin bitecegi gun
    // LISANS

    private Config? _config = new Config();
    public string ConnectionString = "";

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[Store Roulette] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
            AddCommand("css_rulet", "Rulet oynama komutu.", (player, command) => Rulet(player, command));

            RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);

            ConnectionString = $"Server={Config.db_host};Port={Config.db_port};User ID={Config.db_user};Password={Config.db_pass};Database={Config.db_name};";


            RegisterListener<Listeners.OnTick>(() =>
            {
                if (RuletTimer != null)
                {
                    foreach (var p in Utilities.GetPlayers())
                        if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV && Oynadi[p] != "")
                        {
                            string bahistext = "";

                            if (Oynadi[p] == "kirmizi")
                                bahistext = $"<br/><font color='red' style='font-weight: 700; font-size: 20px;'>" + Localizer["center_roulette_spin", Localizer["red"], Bahis[p]] + "</font>";
                            else
                            if (Oynadi[p] == "yesil")
                                bahistext = $"<br/><font color='green' style='font-weight: 700; font-size: 20px;'>" + Localizer["center_roulette_spin", Localizer["green"], Bahis[p]] + "</font>";
                            else
                            if (Oynadi[p] == "siyah")
                                bahistext = $"<br/><font color='grey' style='font-weight: 700; font-size: 20px;'>" + Localizer["center_roulette_spin", Localizer["black"], Bahis[p]] + "</font>";

                            p.PrintToCenterHtml($"<img src='https://www.plugincim.com/forum/data/assets/logo/yataybeyaz.png'/><br/><br/><br/><font color='red' style='font-weight: 700; font-size: 20px;'>" + Localizer["center_roulette_spin"] + $"</font>{bahistext}<br/><font color='red' style='font-weight: 700; font-size: 20px;'>" + Localizer["red"] + $": {KirmiziBahis}</font>  |  <font color='green' style='font-weight: 700; font-size: 20px;'>" + Localizer["green"] + $": {YesilBahis}</font>  |  <font color='grey' style='font-weight: 700; font-size: 20px;'>" + Localizer["black"] + $": {SiyahBahis}</font><br/>");
                        }
                }
            });
        }
    }

    public void Rulet(CCSPlayerController? player, CommandInfo info)
    {
        if (player != null)
        {
            if (Oynadi[player] != "")
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["bet_made"]);

                if (Oynadi[player] == "kirmizi")
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["bet_made", Localizer["red"], Bahis[player]]);
                else
                if (Oynadi[player] == "yesil")
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["bet_made", Localizer["green"], Bahis[player]]);
                else
                if (Oynadi[player] == "siyah")
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["bet_made", Localizer["black"], Bahis[player]]);

                return;
            }

            var Kredi = info.ArgByIndex(1);

            if (Kredi != null && Kredi != "" && IsInt(Kredi))
            {
                Bahis[player] = Convert.ToInt32(Kredi);

                if (Bahis[player] >= Config.min_kredi && Bahis[player] <= Config.max_kredi)
                {
                    var menu = new CenterHtmlMenu(Localizer["choose_color", Bahis[player]]);
                    menu.AddMenuOption(Localizer["choose_color_option", Localizer["red"], Bahis[player], Config.kirmizi_carpan, Bahis[player] * Config.kirmizi_carpan], (player, option) => Rulet_(player, 1));
                    menu.AddMenuOption(Localizer["choose_color_option", Localizer["green"], Bahis[player], Config.yesil_carpan, Bahis[player] * Config.yesil_carpan], (player, option) => Rulet_(player, 2));
                    menu.AddMenuOption(Localizer["choose_color_option", Localizer["black"], Bahis[player], Config.siyah_carpan, Bahis[player] * Config.siyah_carpan], (player, option) => Rulet_(player, 3));

                    MenuManager.OpenCenterHtmlMenu(this, player, menu);
                }
                else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage", Config.min_kredi, Config.max_kredi]);

            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage", Config.min_kredi, Config.max_kredi]);
        }
    }

    public void Rulet_(CCSPlayerController player, int option)
    {
        if (player == null || !player.IsValid || Oynadi[player] != "") return;

        if (Bahis[player] > KrediMiktari(player))
        {
            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["invalid_credit_amount"]);
            return;
        }
        else
            Server.ExecuteCommand($"css_krediver #{player.UserId} -{Bahis[player]}");

        if (option == 1)
        {
            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_bet_chatall", player.PlayerName, Localizer["red"], Bahis[player]]);
            KirmiziBahis += Bahis[player];
            ToplamBahis += Bahis[player];
            ToplamBahisci++;

            Oynadi[player] = "kirmizi";
        }
        else
        if (option == 2)
        {
            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_bet_chatall", player.PlayerName, Localizer["green"], Bahis[player]]);
            YesilBahis += Bahis[player];
            ToplamBahis += Bahis[player];
            ToplamBahisci++;

            Oynadi[player] = "yesil";
        }
        else
        if (option == 3)
        {
            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_bet_chatall", player.PlayerName, Localizer["black"], Bahis[player]]);
            SiyahBahis += Bahis[player];
            ToplamBahis += Bahis[player];
            ToplamBahisci++;

            Oynadi[player] = "siyah";
        }
    }

    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (ToplamBahisci > 0)
        {
            foreach (var p in Utilities.GetPlayers())
                if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV && Oynadi[p] != "")
                {
                    p.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["roulette_spin"]);
                    p.ExecuteClientCommand("play diger/rulet");
                }


            RuletTimer = AddTimer(5.0f, () =>
            {
                if (Config.kirmizi_oran + Config.yesil_oran + Config.siyah_oran != 100.0)
                {
                    Config.kirmizi_oran = 48.0f;
                    Config.yesil_oran = 4.0f;
                    Config.siyah_oran = 48.0f;
                }

                int random = new Random().Next(1, 100);
                string Kazanan = "";
                float Carpan = 0.0f;

                if (Config.kirmizi_oran >= random)
                {
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["roulette_win_color", Localizer["red"]]);
                    Kazanan = "kirmizi";
                    Carpan = Config.kirmizi_carpan;
                }
                else
                if (Config.kirmizi_oran < random && Config.kirmizi_oran + Config.yesil_oran >= random)
                {
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["roulette_win_color", Localizer["green"]]);
                    Kazanan = "yesil";
                    Carpan = Config.yesil_carpan;
                }
                else
                {
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["roulette_win_color", Localizer["black"]]);
                    Kazanan = "siyah";
                    Carpan = Config.siyah_carpan;
                }

                foreach (var p in Utilities.GetPlayers())
                    if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV && Oynadi[p] != "")
                    {
                        if (Oynadi[p] == Kazanan)
                        {
                            Server.ExecuteCommand($"css_krediver #{p.UserId} {Bahis[p] * Carpan}");
                            p.PrintToChat(ReplaceTags($"{Config.EklentiTagi} - " + Localizer["roulette_prefix"]) + Localizer["player_win", Bahis[p], Bahis[p] * Carpan]);
                        }
                        else p.PrintToChat(ReplaceTags($"{Config.EklentiTagi} - " + Localizer["roulette_prefix"]) + Localizer["player_lose", Bahis[p]]);

                        Bahis[p] = 0;
                        Oynadi[p] = "";
                    }

                ToplamBahis = 0; ToplamBahisci = 0;
                KirmiziBahis = 0; YesilBahis = 0; SiyahBahis = 0;
                RuletTimer = null;
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }


        return HookResult.Continue;
    }
    private void OnClientConnected(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null) return;
        Bahis[player] = 0;
        Oynadi[player] = "";
    }


    public int KrediMiktari(CCSPlayerController? player)
    {
        int iKrediMiktari = 0;
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();
            var playerData = connection.QueryFirstOrDefault($"SELECT * FROM marketi_sistemi WHERE steamid = '{player.SteamID}'");

            if (playerData != null)
                iKrediMiktari = playerData.kredi_miktari;

            connection.Close();
        }

        return iKrediMiktari;
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

    private bool IsInt(string sVal)
    {
        foreach (char c in sVal)
        {
            int iN = (int)c;
            if ((iN > 57) || (iN < 48))
                return false;
        }
        return true;
    }
}