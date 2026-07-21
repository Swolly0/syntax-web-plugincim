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
using CounterStrikeSharp.API.Modules.Menu;
using CSSTargetResult = CounterStrikeSharp.API.Modules.Commands.Targeting.TargetResult;

using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace MarketSistemi;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;


    [JsonPropertyName("database")]
    public Dictionary<string, string> Database { get; set; } = new Dictionary<string, string>() {
        { "db_host", string.Empty },
        { "db_port", "3306" },
        { "db_user", string.Empty },
        { "db_pass", string.Empty },
        { "db_name", string.Empty }
    };



    [JsonPropertyName("TopLimit")]
    public int? TopLimit { get; set; } = 15;

    [JsonPropertyName("SpawnModelDelay")]
    public float SpawnModelDelay { get; set; } = 0.75f;

    [JsonPropertyName("StartCredit")]
    public int? StartCredit { get; set; } = 1;

    [JsonPropertyName("AutoCredit")]
    public int AutoCredit { get; set; } = 1;


    [JsonPropertyName("Products")]
    public StoreData StoreData { get; set; } = new StoreData();
}

public class StoreData
{
    public Product[]? Products { get; set; }
}

public class Product
{
    [JsonPropertyName("name")]
    public string? name { get; set; } = "TEST";

    [JsonPropertyName("model_path")]
    public string? model_path { get; set; } = "";

    [JsonPropertyName("price")]
    public int? price { get; set; } = 10;

    [JsonPropertyName("team")]
    public int? team { get; set; } = 0;

    [JsonPropertyName("type")]
    public string? type { get; set; } = "playermodel";

    [JsonPropertyName("product_time")]
    public int? product_time { get; set; } = 1;

    [JsonPropertyName("status")]
    public int? status { get; set; } = 0;
}
public class Purchased
{
    public string? steamid { get; set; }
    public string? name { get; set; }
    public int? start_date { get; set; }
    public int? end_date { get; set; }
}

public class MarketSistemi : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "Market Sistemi";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!market komutu ile oyuncu modeli satisi.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    public string ConnectionString = "";

    private readonly Dictionary<CCSPlayerController, int> iKrediMiktari = new();
    private readonly Dictionary<CCSPlayerController, string> iCTModelPath = new();
    private readonly Dictionary<CCSPlayerController, string> iCTModelName = new();
    private readonly Dictionary<CCSPlayerController, string> iTModelPath = new();
    private readonly Dictionary<CCSPlayerController, string> iTModelName = new();

    private readonly Dictionary<CCSPlayerController, Timer?> timer_ex = new();

    public static List<Purchased> iPurchased = new List<Purchased>();


    // LISANS
    public int lisans_bitis_yil = 2024; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 30; // Lisansin bitecegi gun
    // LISANS

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[RSO] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
            AddCommand("css_market", "Market", (player, command) => Market(player, command));
            AddCommand("css_store", "Market", (player, command) => Market(player, command));
            AddCommand("css_envanter", "Envanter", (player, command) => Envanter(player, command));
            AddCommand("css_env", "Envanter", (player, command) => Envanter(player, command));
            AddCommand("css_inv", "Envanter", (player, command) => Envanter(player, command));
            AddCommand("css_inventory", "Envanter", (player, command) => Envanter(player, command));

            AddCommand("css_krediver", "Krediver", (player, command) => Krediver(player, command));
            AddCommand("css_givecredits", "Krediver", (player, command) => Krediver(player, command));
            AddCommand("css_kredi", "Kredi", (player, command) => Kredi(player, command));
            AddCommand("css_credits", "Kredi", (player, command) => Kredi(player, command));
            AddCommand("css_hediye", "Hediye", (player, command) => Hediye(player, command));
            AddCommand("css_gifts", "Hediye", (player, command) => Hediye(player, command));

            AddCommand("css_topkredi", "Top Kredi", (player, command) => TopKredi(player, command));
            AddCommand("css_zenginler", "Top Kredi", (player, command) => TopKredi(player, command));

            AddCommand("css_kredisifirla", "Kredileri sıfırlar.", (player, command) => Kredi0(player, command));
            AddCommand("css_resetcredits", "Kredileri sıfırlar.", (player, command) => Kredi0(player, command));

            AddCommand("css_marketisifirla", "Marketi sifirlar.", (player, command) => Market0(player, command));
            AddCommand("css_resetstore", "Reset store.", (player, command) => Market0(player, command));

            RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
            RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn, HookMode.Pre);

            ConnectionString = $"Server={Config.Database["db_host"]};Port={Config.Database["db_port"]};User ID={Config.Database["db_user"]};Password={Config.Database["db_pass"]};Database={Config.Database["db_name"]};";
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                connection.Execute(@"CREATE TABLE IF NOT EXISTS `marketi_sistemi` (`id` INT AUTO_INCREMENT PRIMARY KEY, `name` VARCHAR(32) NOT NULL, `steamid` VARCHAR(17) UNIQUE NOT NULL, `kredi_miktari` INT NOT NULL DEFAULT 0);");
                connection.Execute(@"CREATE TABLE IF NOT EXISTS `market_siparisler` (`id` INT AUTO_INCREMENT PRIMARY KEY, `steamid` VARCHAR(17) NOT NULL, `name` VARCHAR(64) NOT NULL, `used` INT NOT NULL DEFAULT 0, `start_date` INT NOT NULL DEFAULT 0, `end_date` INT NOT NULL DEFAULT 0);");
                connection.Execute(@"CREATE TABLE IF NOT EXISTS `market_log` (`id` INT AUTO_INCREMENT PRIMARY KEY, `name` VARCHAR(32) NOT NULL, `steamid` VARCHAR(17) NOT NULL, `detail` VARCHAR(255) NOT NULL, `createdate` INT NOT NULL DEFAULT 0);");
                connection.Close();
            }

            foreach (var player in Utilities.GetPlayers())
                if (player != null)
                    Connected(player);
        }
    }
    public void Market(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        player.PrintToChat($"------------------------------------");
        var menu = new CenterHtmlMenu(Localizer["store_title", iKrediMiktari[player]]);

        foreach (var product in Config.StoreData.Products)
        {
            if (product.status == 1)
                if (product.type == "playermodel")
                {
                    string menutext = "";
                    var checkproduct = iPurchased.Where(cust => cust.steamid == Convert.ToString(player.SteamID)).Where(cust => cust.name == product.name);

                    if (product.team == 1)
                        menutext = "(T-CT) ";
                    else
                    if (product.team == 2)
                        menutext = "(T) ";
                    else
                    if (product.team == 3)
                        menutext = "(CT) ";

                    if (checkproduct.Count() > 0)
                    {
                        if (iTModelName[player] == product.name || iCTModelName[player] == product.name)
                            menutext = menutext + Localizer["equiping"];
                        else
                            menutext = menutext + Localizer["equip"];
                    }
                    else
                        menutext = menutext + $"[{product.price} {Localizer["credit"]}] ";

                    menutext = menutext + product.name;
                    menu.AddMenuOption($"{menutext}", (player, option) => Market_(player, product.name));
                }
        }
        MenuManager.OpenCenterHtmlMenu(this, player, menu);
        player.PrintToChat($"------------------------------------");

    }

    public void Market_(CCSPlayerController player, string option)
    {
        var checkproduct = iPurchased.Where(cust => cust.steamid == Convert.ToString(player.SteamID)).Where(cust => cust.name == option);
        if (checkproduct.Count() > 0)
        {
            foreach (var product in Config.StoreData.Products)
            {
                if (product.status == 1)
                    if (product.name == option)
                    {
                        using (var connection = new MySqlConnection(ConnectionString))
                        {
                            connection.Open();

                            bool removed = false;
                            if (iTModelName[player] == product.name)
                            {
                                iTModelName[player] = "";
                                iTModelPath[player] = "";
                                removed = true;
                            }
                            
                            if (iCTModelName[player] == product.name)
                            {
                                iCTModelName[player] = "";
                                iCTModelPath[player] = "";
                                removed = true;
                            }

                            if (!removed)
                            {
                                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["item_equip_msg", product.name]);
                                connection.Execute($"UPDATE market_siparisler SET used = 1 WHERE name = '{product.name}' AND steamid = '{player.SteamID}';");

                                if (product.team == 1 || product.team == 2)
                                {
                                    iTModelName[player] = product.name;
                                    iTModelPath[player] = product.model_path;
                                }

                                if (product.team == 1 || product.team == 3)
                                {
                                    iCTModelName[player] = product.name;
                                    iCTModelPath[player] = product.model_path;
                                }

                                if (player.is_valid_alive())
                                {
                                    if (player.TeamNum == 2)
                                        player.PlayerPawn.Value!.SetModel(@"" + iTModelPath[player]);
                                    else
                                    if (player.TeamNum == 3)
                                        player.PlayerPawn.Value!.SetModel(@"" + iCTModelPath[player]);
                                }
                            }
                            else
                            {
                                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["item_unequip_msg", product.name]);
                                connection.Execute($"UPDATE market_siparisler SET used = '0' WHERE name = '{product.name}' AND steamid = '{player.SteamID}';");
                            }

                            connection.Close();
                        }

                        return;
                    }
            }
        }
        else
        {
            var custQuery2 = Config.StoreData.Products.Where(cust => cust.name == option);
            int unix = Convert.ToInt32(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            foreach (var product in custQuery2)
            {
                if (iKrediMiktari[player] >= product.price) {
                    iPurchased.Add(new Purchased() { steamid = Convert.ToString(player.SteamID), name = product.name, start_date = unix, end_date = unix + product.product_time });
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["item_buyed_msg", product.name]);

                    iKrediMiktari[player] -= Convert.ToInt32(product.price);

                    using (var connection = new MySqlConnection(ConnectionString))
                    {
                        connection.Open();
                        connection.Execute($"UPDATE marketi_sistemi SET name = '{Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", "")}', kredi_miktari = {iKrediMiktari[player]} WHERE steamid = '{player.SteamID}';");

                        var insertQuery = $"INSERT INTO market_siparisler (steamid, name, start_date, end_date) VALUES (@SteamID, @Name, @StartDate, @EndDate);";
                        connection.Execute(insertQuery, new { SteamID = player.SteamID, Name = product.name, StartDate = unix, EndDate = unix + product.product_time });
                        connection.Close();
                    }
                } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + $" \x04{product.name} \x01satın alabilmen için \x0e{product.price - iKrediMiktari[player]} kredi\x01 eksiğin var.");

                return;
            }
        }
    }

    public void Envanter(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        player.PrintToChat($"------------------------------------");
        var menu = new CenterHtmlMenu(Localizer["inventory_title", iKrediMiktari[player]]);

        var GetProducts = iPurchased.Where(cust => cust.steamid == Convert.ToString(player.SteamID));
        if (GetProducts.Count() > 0)
        {
            foreach (var products in GetProducts)
            {
                var custQuery2 = Config.StoreData.Products.Where(cust => cust.name == products.name);

                foreach (var product in custQuery2)
                {
                    if (product.status == 1)
                        if (product.type == "playermodel")
                        {
                            string menutext = "";
                            var checkproduct = iPurchased.Where(cust => cust.steamid == Convert.ToString(player.SteamID)).Where(cust => cust.name == product.name);

                            if (product.team == 1)
                                menutext = "(T-CT) ";
                            else
                            if (product.team == 2)
                                menutext = "(T) ";
                            else
                            if (product.team == 3)
                                menutext = "(CT) ";

                            if (checkproduct.Count() > 0)
                            {
                                if (iTModelName[player] == product.name || iCTModelName[player] == product.name)
                                    menutext = menutext + Localizer["equiping"];
                                else
                                    menutext = menutext + Localizer["equip"];
                            }
                            else
                                menutext = menutext + $"[{product.price} {Localizer["credit"]}] ";

                            menutext = menutext + product.name + Localizer["expiry"] + GetDate(Convert.ToInt32(products.end_date));
                            menu.AddMenuOption($"{menutext}", (player, option) => Market_(player, product.name));
                        }

                    break;
                }
            }
        }
        else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["inventory_empty"]);

        MenuManager.OpenCenterHtmlMenu(this, player, menu);
        player.PrintToChat($"------------------------------------");
    }






































    public void Krediver(CCSPlayerController? player, CommandInfo command)
    {
        if (AdminManager.PlayerHasPermissions(player, "@css/root"))
        {
            if (command.ArgCount == 3)
            {
                var KrediMiktari = command.ArgByIndex(2);
                if (KrediMiktari != null && KrediMiktari != "" && IsInt(KrediMiktari))
                {
                    var target = GetTarget(command);
                    if (target != null && target.Count() > 0)
                    {
                        using (var connection = new MySqlConnection(ConnectionString))
                        {
                            connection.Open();
                            target?.Players.ForEach(p =>
                            {
                                if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
                                {
                                    iKrediMiktari[p] += Convert.ToInt32(KrediMiktari);

                                    if (player != null)
                                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + $" \x04{player.PlayerName}, \x01{p.PlayerName} isimli oyuncuya \x0e{KrediMiktari} \x01kredi verdi.");

                                    connection.Execute($"UPDATE marketi_sistemi SET name = '{Regex.Replace(p.PlayerName, @"[^a-zA-Z0-9\s]", "")}', kredi_miktari = {iKrediMiktari[p]} WHERE steamid = '{p.SteamID}';");
                                }
                            });
                            connection.Close();
                        }
                    } else if (player != null) player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["target_not_found"]);
                } else if (player != null) player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["invalid_credit"]);
            } else if (player != null) player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage"]);

        }
        else if (player != null) player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
    }

    public void Kredi(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;
        player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["credit_msg", iKrediMiktari[player]]);
    }

    public void Hediye(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        if (command.ArgCount == 3)
        {
            var KrediMiktari = command.ArgByIndex(2);
            if (KrediMiktari != null && KrediMiktari != "" && Convert.ToInt32(KrediMiktari) > 0)
            {
                var target = GetTarget(command);
                if (target != null && target.Count() > 0)
                {
                    using (var connection = new MySqlConnection(ConnectionString))
                    {
                        connection.Open();

                        target?.Players.ForEach(p =>
                        {
                            if(iKrediMiktari[player] >= Convert.ToInt32(KrediMiktari))
                                if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
                                {
                                    iKrediMiktari[p] += Convert.ToInt32(KrediMiktari);
                                    iKrediMiktari[player] -= Convert.ToInt32(KrediMiktari);

                                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["gift_credit_sended", p.PlayerName, KrediMiktari]);
                                    p.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["gift_credit_received", p.PlayerName, KrediMiktari]);


                                    connection.Execute($"UPDATE marketi_sistemi SET name = '{Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", "")}', kredi_miktari = {iKrediMiktari[player]} WHERE steamid = '{player.SteamID}';");
                                    connection.Execute($"UPDATE marketi_sistemi SET name = '{Regex.Replace(p.PlayerName, @"[^a-zA-Z0-9\s]", "")}', kredi_miktari = {iKrediMiktari[p]} WHERE steamid = '{p.SteamID}';");
                                }
                        });

                        connection.Close();
                    }
                } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["gift_target_not_found"]);
            } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["gift_command_usage"]);
        } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["gift_command_usage"]);
    }

    public void Kredi0(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        if (AdminManager.PlayerHasPermissions(player, "@css/root"))
        {
            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["reset_credit_msg", player.PlayerName]);
            ResetCredits();
        }
        else
            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
    }

    public void Market0(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        if (AdminManager.PlayerHasPermissions(player, "@css/root"))
        {
            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["reset_store_msg", player.PlayerName]);
            ResetOrders();
            ResetCredits();
        }
        else
            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
    }

    public void TopKredi(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        try
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var topPlayersQuery = $@"
                        SELECT name, kredi_miktari
                        FROM marketi_sistemi
                        WHERE kredi_miktari > 0
                        ORDER BY kredi_miktari DESC
                        LIMIT {Config.TopLimit};";

                var topPlayers = connection.Query(topPlayersQuery).ToList();

                if (topPlayers.Any())
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["topcredit_title"]);
                    player.PrintToChat($"-----------------------------------------------");

                    for (int i = 0; i < topPlayers.Count; i++)
                    {
                        var topPlayerInfo = topPlayers[i];

                        player.PrintToChat($" \x0b{i + 1}. \x04{topPlayerInfo.name} \x01- {topPlayerInfo.kredi_miktari} {Localizer["credit"]}.");
                        player.PrintToConsole($"{i + 1}. {topPlayerInfo.name} - {topPlayerInfo.kredi_miktari} {Localizer["credit"]}.");
                    }

                    player.PrintToChat($"-----------------------------------------------");
                }
                else
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["topcredit_empty"]);

                connection.Close();
            }
        }
        catch (Exception ex)
        {
            player.PrintToChat(Localizer["topcredit_error"] + ex.Message);
        }
    }



    HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
        {
            AddTimer(Config.SpawnModelDelay, () =>
            {
                if (player.is_t() && iTModelPath[player] != "")
                    player.PlayerPawn.Value!.SetModel(@"" + iTModelPath[player]);
                else
                if (player.is_ct() && iCTModelPath[player] != "")
                    player.PlayerPawn.Value!.SetModel(@"" + iCTModelPath[player]);
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }

        return HookResult.Continue;
    }

    private void OnClientConnected(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null) return;

        Connected(player);
    }

    public void Connected(CCSPlayerController? player)
    {
        iKrediMiktari[player] = 0;
        iTModelPath[player] = "";
        iCTModelPath[player] = "";
        timer_ex[player] = null;

        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
        {
            GetPlayerData(player);
            GetPlayerProducts(player);

            if (timer_ex[player] != null) return;
            timer_ex[player] = AddTimer(60.0f, () =>
            {
                if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["auto_credit_msg", Config.AutoCredit]);
                    iKrediMiktari[player] += Config.AutoCredit;

                    using (var connection = new MySqlConnection(ConnectionString))
                    {
                        connection.Open();
                        connection.Execute($"UPDATE marketi_sistemi SET name = '{Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", "")}', kredi_miktari = {iKrediMiktari[player]} WHERE steamid = '{player.SteamID}';");
                        connection.Close();
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
            timer_ex[player]!.Kill();

        timer_ex[player] = null;

        iPurchased.RemoveAll(r => r.steamid == Convert.ToString(player.SteamID));
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();
            connection.Execute($"UPDATE marketi_sistemi SET name = '{Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", "")}', kredi_miktari = {iKrediMiktari[player]} WHERE steamid = '{player.SteamID}';");
            connection.Close();
        }
    }

    public void GetPlayerData(CCSPlayerController? player)
    {
        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
        {
            iKrediMiktari[player] = 0;
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var playerData = connection.QueryFirstOrDefault($"SELECT * FROM marketi_sistemi WHERE steamid = '{player.SteamID}'");

                if (playerData != null)
                    iKrediMiktari[player] = playerData.kredi_miktari;
                else
                    InsertPlayer(player);

                connection.Close();
            }
        }
    }

    public void GetPlayerProducts(CCSPlayerController? player)
    {
        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
        {
            int unix = Convert.ToInt32(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            iTModelPath[player] = "";
            iTModelName[player] = "";
            iCTModelPath[player] = "";
            iCTModelName[player] = "";

            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var player_products_sql = $@"SELECT * FROM market_siparisler WHERE steamid = {player.SteamID};";
                var GetProducts = connection.Query(player_products_sql).ToList();

                if (GetProducts.Any())
                {
                    for (int i = 0; i < GetProducts.Count; i++)
                    {
                        var products = GetProducts[i];
                        var custQuery2 = Config.StoreData.Products.Where(cust => cust.name == products.name);

                        foreach (var product in custQuery2)
                        {
                            if (products.end_date > unix)
                            {
                                if (products.used == 1)
                                {
                                    if (product.team == 1 || product.team == 2)
                                    {
                                        iTModelName[player] = product.name;
                                        iTModelPath[player] = product.model_path;
                                    }

                                    if (product.team == 1 || product.team == 3)
                                    {
                                        iCTModelName[player] = product.name;
                                        iCTModelPath[player] = product.model_path;
                                    }
                                }

                                iPurchased.Add(new Purchased() { steamid = Convert.ToString(player.SteamID), name = product.name, start_date = products.start_date, end_date = products.end_date });
                            }
                        }
                    }
                }

                connection.Close();
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
                var insertQuery = $"INSERT INTO marketi_sistemi (name, steamid, kredi_miktari) VALUES (@Name, @SteamID, @KrediMiktari);";
                connection.Execute(insertQuery, new { Name = Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", ""), SteamID = player.SteamID, KrediMiktari = Config.StartCredit });
            }

            iKrediMiktari[player] = 0;
        }
    }

    public void ResetCredits()
    {
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();
            connection.Execute("TRUNCATE TABLE marketi_sistemi;");
            connection.Close();
        }

        foreach (var p in Utilities.GetPlayers())
            if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
                InsertPlayer(p);
    }

    public void ResetOrders()
    {
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();
            connection.Execute("TRUNCATE TABLE market_siparisler;");
            connection.Close();
        }

        foreach (var p in Utilities.GetPlayers())
            if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
            {
                iTModelPath[p] = "";
                iTModelName[p] = "";
                iCTModelPath[p] = "";
                iCTModelName[p] = "";
            }

        iPurchased.Clear();
    }

    private CSSTargetResult? GetTarget(CommandInfo info, bool allowMultiple = true)
    {
        var matches = info.GetArgTargetResult(1);

        if (!matches.Any())
            return null;

        if (!(matches.Count() > 1) || (info.GetArg(1).StartsWith('@') && allowMultiple))
            return matches;

        return null;
    }

    public static string GetDate(int iResetTime)
    {
        System.DateTime dat_Time = new System.DateTime(1970, 1, 1, 0, 0, 0).ToLocalTime();
        dat_Time = dat_Time.AddSeconds(iResetTime);
        string print_the_Date = dat_Time.ToShortDateString() + " " + dat_Time.ToShortTimeString();

        return print_the_Date;
    }


    private bool IsInt(string sVal)
    {
        int i = 0;
        if (int.TryParse(sVal, out i))
            return true;
        else
            return false;
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