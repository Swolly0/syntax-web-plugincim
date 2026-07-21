using System;

using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using MySqlConnector;
using Dapper;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace MarketSistemi_Jackpot;

public class Config: IBasePluginConfig
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
    public int max_kredi { get; set; } = 1;
}

public class MarketSistemi_Jackpot : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "Market Sistemi - Jackpot";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!jackpot komutu ile oyuncular kredileri ile jackpot oynayabilirler.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    private readonly Dictionary<CCSPlayerController, int> BiletBaslangic = new();
    private readonly Dictionary<CCSPlayerController, int> BiletBitis = new();
    private readonly Dictionary<CCSPlayerController, int> Bahis = new();
    public int ToplamBahis = 0, ToplamBahisci = 0;
    public CounterStrikeSharp.API.Modules.Timers.Timer? JackpotTimer;
    public CounterStrikeSharp.API.Modules.Timers.Timer? JackpotTimer2;
    CCSPlayerController? randomplayer;

    // LISANS
    public int lisans_bitis_yil = 2024; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 7; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 17; // Lisansin bitecegi gun
    // LISANS

    public string ConnectionString = "";

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[Store Jackpot] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
			AddCommand("css_jackpot", "Jackpot oynama komutu.", (player, command) => Jackpot(player, command));

            RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);

            ConnectionString = $"Server={Config.db_host};Port={Config.db_port};User ID={Config.db_user};Password={Config.db_pass};Database={Config.db_name};";


            RegisterListener<Listeners.OnTick>(() =>
            {
                if (JackpotTimer != null)
                {
                    if(randomplayer != null)
                    {
                        foreach (var p in Utilities.GetPlayers())
                            if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV && Bahis[p] > 0)
                            {
                                string bahistext = $"<br/><font color='grey' style='font-weight: 700; font-size: 20px;'>" + Localizer["center_player_bet", Bahis[p]] + "</font>";
                                p.PrintToCenterHtml($"<img src='https://www.plugincim.com/forum/data/assets/logo/yataybeyaz.png'/><br/><br/><br/><font color='red' style='font-weight: 700; font-size: 20px;'>" + Localizer["jackpot_spin"] + $"</font>{bahistext}<br/><br/><font color='red' style='font-weight: 700; font-size: 20px;'>   >>>{randomplayer.PlayerName}<<<   </font><br/>");
                            }
                    }
                }
            });
        }
    }

    public void Jackpot(CCSPlayerController? player, CommandInfo info)
    {
        if (player != null)
        {
            if (Bahis[player] > 0)
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_bet_made", Bahis[player], Math.Round(Convert.ToDouble((Bahis[player] * 100) / ToplamBahis), 2)]);

                foreach (var p in Utilities.GetPlayers())
                    if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV && Bahis[p] > 0)
                        player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["other_player_bet", p.PlayerName, Bahis[p], Math.Round(Convert.ToDouble((Bahis[player] * 100) / ToplamBahis), 2)]);

                return;
            }

            var Kredi = info.ArgByIndex(1);

            if (Kredi != null && Kredi != "" && IsInt(Kredi))
            {
                var iKredi = Convert.ToInt32(Kredi);

                if (iKredi >= Config.min_kredi && iKredi <= Config.max_kredi)
                    if (KrediMiktari(player) >= iKredi)
                    {
                        Bahis[player] = iKredi;
                        Server.ExecuteCommand($"css_krediver #{player.UserId} -{Bahis[player]}");
                        
                        BiletBaslangic[player] = ToplamBahis + 1;
                        ToplamBahis += Bahis[player];
                        BiletBitis[player] = ToplamBahis;
                        ToplamBahisci++;

                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_bet_jackpot", player.PlayerName, Bahis[player], Math.Round(Convert.ToDouble((Bahis[player] * 100) / ToplamBahis), 2)]);
                    }
                    else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["invalid_credits"]);
                else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage", Config.min_kredi, Config.max_kredi]);
            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage", Config.min_kredi, Config.max_kredi]);
        }
    }
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if(ToplamBahisci > 0)
        {
            foreach (var p in Utilities.GetPlayers())
                if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV && Bahis[p] > 0)
                    p.ExecuteClientCommand("play diger/jackpot");

            JackpotTimer2 = AddTimer(0.1f, () =>
            {
                if (JackpotTimer != null)
                {
                    do
                    {
                        randomplayer = null;
                        int rand = new Random().Next(1, 65);

                        foreach (var p in Utilities.GetPlayers())
                            if (p.UserId == rand)
                            {
                                if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV && Bahis[p] > 0)
                                {
                                    randomplayer = p;
                                    break;
                                }
                                else
                                    rand = new Random().Next(1, 65);
                            }
                    }
                    while (randomplayer == null || !randomplayer.IsValid || randomplayer.IsBot || Bahis[randomplayer] == 0);
                }
            }, TimerFlags.STOP_ON_MAPCHANGE | TimerFlags.REPEAT);

            JackpotTimer = AddTimer(5.0f, () =>
            {
                JackpotTimer2.Kill();
                JackpotTimer2 = null;

                int rand = new Random().Next(1, ToplamBahis);
                CCSPlayerController? Kazanan = null;

                foreach (var p in Utilities.GetPlayers())
                    if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV && Bahis[p] > 0 && BiletBaslangic[p] <= rand && rand <= BiletBitis[p])
                    {
                        Kazanan = p;
                        break;
                    }

                if (Kazanan == null)
                {
                    do
                    {
                        rand = new Random().Next(1, ToplamBahis);
                        foreach (var p in Utilities.GetPlayers())
                            if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV && Bahis[p] > 0 && BiletBaslangic[p] <= rand && rand <= BiletBitis[p])
                            {
                                Kazanan = p;
                                break;
                            }
                    }
                    while (Kazanan == null || !Kazanan.IsValid || Kazanan.IsBot);
                }

                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["chat_player_win", Kazanan.PlayerName, ToplamBahis, Math.Round(Convert.ToDouble((Bahis[Kazanan] * 100) / ToplamBahis), 2)]);


                foreach (var p in Utilities.GetPlayers())
                    if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV && Bahis[p] > 0)
                    {
                        if (p == Kazanan)
                        {
                            Server.ExecuteCommand($"css_krediver #{p.UserId} {ToplamBahis}");
                            p.PrintToChat(Localizer["player_win", Bahis[p], ToplamBahis]);
                        }
                        else p.PrintToChat(Localizer["player_lose", Bahis[p]]);

                        BiletBaslangic[p] = 0;
                        BiletBitis[p] = 0;
                        Bahis[p] = 0;
                    }

                ToplamBahis = 0; ToplamBahisci = 0;
                JackpotTimer = null;
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }
        
        return HookResult.Continue;
    }

    private void OnClientConnected(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null) return;

        BiletBaslangic[player] = 0;
        BiletBitis[player] = 0;
        Bahis[player] = 0;
    }

    private void OnClientDisconnect(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null) return;

        if (Bahis[player] > 0)
        {
            Server.ExecuteCommand($"css_krediver #{player.UserId} +{Bahis[player]}");
            ToplamBahis -= Bahis[player];
            ToplamBahisci--;
        }

        BiletBaslangic[player] = 0;
        BiletBitis[player] = 0;
        Bahis[player] = 0;
    }

    public int KrediMiktari(CCSPlayerController? player)
    {
        int iKrediMiktari = 0;
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();
            var playerData = connection.QueryFirstOrDefault($"SELECT * FROM marketi_sistemi WHERE steamid = '{player!.SteamID}'");

            if (playerData != null)
                iKrediMiktari = playerData.kredi_miktari;

            connection.Close();
        }

        return iKrediMiktari;
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