using System.IO;
using System;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBMeslekMenu;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;

	[JsonPropertyName("flash_speed_time")]
    public float cflash_sure { get; set; } = 10.0f;
	
	[JsonPropertyName("flash_speed")]
    public float cflash_hiz { get; set; } = 2.0f;
	
	[JsonPropertyName("rambo_hp")]
    public int crambo_can { get; set; } = 125;
	
	[JsonPropertyName("rambo_armor")]
    public int crambo_zirh { get; set; } = 200;
}

public class JBMeslekMenu : BasePlugin, IPluginConfig<Config>
{
    // PLUGIN START
    public override string ModuleName { get; } = "Meslek Menu";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "JB | !meslek komutu ile oyuncular her round bir meslek secebilirler.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    private static readonly bool?[] Kullandi = new bool?[65];

    // LISANS
    public int lisans_bitis_yil = 2025; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 9; // Lisansin bitecegi gun
    // LISANS

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[Jobs] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
            AddCommand("css_meslekmenu", "Meslek Menu", (player, command) => MeslekMenu_(player, command));
            AddCommand("css_meslek", "Meslek Menu", (player, command) => MeslekMenu_(player, command));

            RegisterEventHandler<EventRoundStart>(OnRoundStart);
        }
    }

    public void MeslekMenu_(CCSPlayerController? player, CommandInfo info)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (player.is_valid_alive())
            {
                if (player.is_t())
                {
                    if (Kullandi[player.Index] == null || Kullandi[player.Index] == false)
                    {
                        var menu = new CenterHtmlMenu(Localizer["menu_title"]);
                        menu.AddMenuOption(Localizer["job_doctor"], (player, option) => MeslekMenu__(player, 1));
                        menu.AddMenuOption(Localizer["job_flash", Config.cflash_sure], (player, option) => MeslekMenu__(player, 2));
                        menu.AddMenuOption(Localizer["job_bomber"], (player, option) => MeslekMenu__(player, 3));
                        menu.AddMenuOption(Localizer["job_rambo", Config.crambo_can, Config.crambo_zirh], (player, option) => MeslekMenu__(player, 4));

                        MenuManager.OpenCenterHtmlMenu(this, player, menu);
                    }
                    else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_use_job"]);
                }
                else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["job_only_prisoner"]);
            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["job_only_alive_prinoser"]);
        }
    }

    public void MeslekMenu__(CCSPlayerController player, int option)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (player.is_valid_alive())
            {
                if (player.is_t())
                {
                    if (Kullandi[player.Index] == null || Kullandi[player.Index] == false)
                    {
                        if (option == 1)
                        {
                            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_chose_doctor"]);
                            player.GiveNamedItem("weapon_healthshot");
                        }
                        else if (option == 2)
                        {
                            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_chose_flash"]);

                            CCSPlayerPawn? playerPawnValue = player.PlayerPawn.Value;
                            if (playerPawnValue == null) return;

                            playerPawnValue.VelocityModifier = Config.cflash_hiz;

                            AddTimer(Config.cflash_sure, () =>
                            {
                                if (player.is_valid_alive())
                                    playerPawnValue.VelocityModifier = 1.0f;
                            }, TimerFlags.STOP_ON_MAPCHANGE);

                        }
                        else if (option == 3)
                        {
                            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_chose_bomber"]);

                            int random = new Random().Next(1, 3);

                            if (random == 1)
                            {
                                player.GiveNamedItem("weapon_flashbang");
                            }
                            else if (random == 2)
                            {
                                player.GiveNamedItem("weapon_smokegrenade");
                            }
                            else
                            {
                                player.GiveNamedItem("weapon_hegrenade");
                            }
                        }
                        else if (option == 4)
                        {
                            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_chose_rambo"]);

                            player.Health(Config.crambo_can);
                            player.Armor(Config.crambo_zirh);
                        }

                        Kullandi[player.Index] = true;
                    }
                    else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_use_job"]);
                }
                else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["job_only_prisoner"]);
            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["job_only_alive_prisoner"]);
        }
    }

    HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            foreach (var p in Utilities.GetPlayers())
            {
                if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
                    Kullandi[p.Index] = false;
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
    static public void Health(this CCSPlayerController player, int health)
    {
        if (player.PlayerPawn == null || player.PlayerPawn.Value == null)
        {
            return;
        }

        player.Health = health;
        player.PlayerPawn.Value.Health = health;

        if (health > 100)
        {
            player.MaxHealth = health;
            player.PlayerPawn.Value.MaxHealth = health;
        }

        Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_iHealth");
    }

    static public void Armor(this CCSPlayerController player, int armor)
    {
        if (player.PlayerPawn == null || player.PlayerPawn.Value == null)
        {
            return;
        }

        player.PlayerPawn.Value.ArmorValue = armor;
        Utilities.SetStateChanged(player.PlayerPawn.Value, "CCSPlayerPawnBase", "m_ArmorValue");
    }

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