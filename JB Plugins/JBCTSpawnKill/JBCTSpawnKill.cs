﻿using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBCTSpawnKill;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("god_time")]
    public float god_suresi { get; set; }
}

public class JBCTSpawnKill : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB CT Spawn Kill";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "JB | CT dogdugunda belirli bir sure olumsuz olur.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    private static readonly int?[] LastSpawn = new int?[65];

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
            Console.WriteLine($"[CT Spawn Kill] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
			RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn, HookMode.Pre);
			RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt, HookMode.Pre);
			RegisterEventHandler<EventPlayerConnect>(OnPlayerConnect);
		}
    }


    HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        LastSpawn[player.Index] = 0;


        if (NativeAPI.GetMapName().Contains("jb_") && Config.god_suresi >= 1.0f)
        {
            if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV && player.is_ct())
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["spawn_protect_msg", Config.god_suresi]);
                LastSpawn[player.Index] = Convert.ToInt32(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
        }

        return HookResult.Continue;
    }

     private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            CCSPlayerController? player = @event.Userid;
            if (player.is_valid() && player.is_ct() && LastSpawn[player.Index] + 3 >= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                if (player.Connected != PlayerConnectedState.PlayerConnected)
                    return HookResult.Continue;

                if (!player.PlayerPawn.IsValid)
                    return HookResult.Continue;

				player.PlayerPawn.Value.Health += @event.DmgHealth;
				player.PlayerPawn.Value.ArmorValue += @event.DmgArmor;

				player.Health += @event.DmgHealth;

				Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_iHealth");

				@event.Userid.PlayerPawn.Value.VelocityModifier = 1;
            }
        }

        return HookResult.Continue;
    }

    HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        LastSpawn[player.Index] = 0;

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

    public static void Freeze(this CBasePlayerPawn pawn)
    {
        pawn.MoveType = MoveType_t.MOVETYPE_OBSOLETE;
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