using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBRedbull;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;

	[JsonPropertyName("redbull_time")]
    public float redbull_sure { get; set; }
	
	[JsonPropertyName("redbull_speed")]
    public float redbull_hiz { get; set; }
}

public class JBRedbull : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Redbull";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "JB | !redbull komutu ile oyuncular belirli bir sureligine hizlanir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    private readonly Dictionary<CCSPlayerController, bool> Kullandi = new();

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
            Console.WriteLine($"[Redbull] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
            AddCommand("css_redbull", "Redbull", (player, command) => Redbull(player, command));

            RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);


            foreach (var player in Utilities.GetPlayers())
                if (player != null)
                    Kullandi[player] = false;
        }
    }	
	
	public void Redbull(CCSPlayerController? player, CommandInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_")){
			if (player.is_valid_alive())
            {
                if (player.is_t())
                {
                    if (!Kullandi[player])
                    {
                        player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["redbull_use"]);
                        Kullandi[player] = true;

                        player.PlayerPawn.Value.VelocityModifier = Config.redbull_hiz;

                        AddTimer(Config.redbull_sure, () =>
                        {
                            if (player.is_valid_alive())
                                player.PlayerPawn.Value.VelocityModifier = 1.0f;
                        }, TimerFlags.STOP_ON_MAPCHANGE);


                    } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["redbull_used"]);	
				} else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["redbull_only_prisoner"]);			
			} else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["redbull_only_alive_prisoner"]);			
		}
    }

    HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
		if(NativeAPI.GetMapName().Contains("jb_")){
			foreach (var p in Utilities.GetPlayers()){
				if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
					Kullandi[p] = false;
            }
		}

        return HookResult.Continue;
    }

    private void OnClientConnected(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if(player == null) return;
        Kullandi[player] = false;
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


public static class Lib{

    static public bool is_valid(this CCSPlayerController? player)
    {
        return player != null && player.IsValid &&  player.PlayerPawn.IsValid;
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
        if(player == null || !player.is_valid())
        {
            return null;
        }

        CCSPlayerPawn? pawn = player.PlayerPawn.Value;

        return pawn;
    }	
	
    static public int get_health(this CCSPlayerController? player)
    {
        CCSPlayerPawn? pawn = player.pawn();

        if(pawn == null)
        {
            return 100;
        }

        return pawn.Health;
    }	
}