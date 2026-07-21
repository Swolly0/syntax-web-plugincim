﻿using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBFFZaman;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}

public class JBFFZaman : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "FF Zaman";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "JB | !ffz komutu ile ff geri sayimi baslatilabilir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    public float Geri_Sayim;
    public CounterStrikeSharp.API.Modules.Timers.Timer? timer_ex;

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
            Console.WriteLine($"[FF Zaman] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
			AddCommand("css_ffz", "FF Zaman", (player, command) => FF_Zaman(player, command));
			AddCommand("css_ffzaman", "FF Zaman", (player, command) => FF_Zaman(player, command));
			
			AddCommand("css_ffz0", "FF Zaman Iptal", (player, command) => FF_Zaman0(player, command));
			AddCommand("css_ffzaman0", "FF Zaman Iptal", (player, command) => FF_Zaman0(player, command));

            RegisterEventHandler<EventRoundStart>(OnRoundEvent);
			RegisterEventHandler<EventRoundEnd>(OnRoundEvent);

            RegisterListener<Listeners.OnTick>(() =>
            {
                if (Geri_Sayim >= 1)
                {
                    foreach (var p in Utilities.GetPlayers())
                        if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
                            if (Geri_Sayim <= 10)
                                p.PrintToCenterHtml($"<img src='https://www.plugincim.com/assets/images/plugin-banner.png'/><br/><br/><p style='font-weight: 700; color: red; font-size: 20px;'>" + Localizer["center_countdown"] + $"</p><img src='https://www.plugincim.com/assets/images/numbers/{Convert.ToInt32(Geri_Sayim)}.png' width='64px' height='64px'/><br/><br/>");
                            else
                                p.PrintToCenterHtml($"<img src='https://www.plugincim.com/assets/images/plugin-banner.png'/><br/><br/><p style='font-weight: 700; color: red; font-size: 20px;'>" + Localizer["center_countdown"] + $"{Convert.ToInt32(Geri_Sayim)} " + Localizer["second"] + ".</p><br/><br/>");
                }
            });

        }
	}	
	
	public void FF_Zaman(CCSPlayerController? player, CommandInfo info)
    {	
		if(NativeAPI.GetMapName().Contains("jb_")){
			if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct()){
				
				var Sure = info.ArgByIndex(1);
				
				if (Sure != null && Sure != "" && IsInt(Sure)){
                    if(player != null)
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_used", player.PlayerName, Sure]);

                    Geri_Sayim = Convert.ToInt32(Sure);	
					
					if(timer_ex != null){ timer_ex?.Kill(); }
					
					timer_ex = AddTimer(1.0f, () =>
					{
						if(Geri_Sayim == 0.0){

                            Server.ExecuteCommand("mp_teammates_are_enemies true");
                            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ff_open"]);
							
							timer_ex?.Kill();
							timer_ex = null;
							return;
						} else {
							Geri_Sayim -= 1.0f;
							
							if(Geri_Sayim <= 10.0){ Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["chat_countdown", Geri_Sayim]); }
						}
					}, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);						
				} else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage"]);		
				
			} else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);			
		}
    }
	
	public void FF_Zaman0(CCSPlayerController? player, CommandInfo info)
    {	
		if(player != null && NativeAPI.GetMapName().Contains("jb_")){
			if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct()){

                Server.ExecuteCommand("mp_teammates_are_enemies false");

                if (timer_ex != null){ 
					timer_ex?.Kill(); 
				}
				
				Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ff_close", player.PlayerName]);
                Geri_Sayim = 0.0f;
                timer_ex = null;
				
			} else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);			
		}
    }	
	
    public HookResult OnRoundEvent(EventRoundStart @event, GameEventInfo info)
    {
		if(timer_ex != null){ timer_ex?.Kill(); }
		timer_ex = null;

        Server.ExecuteCommand("mp_teammates_are_enemies false");

        return HookResult.Continue;
    }

    public HookResult OnRoundEvent(EventRoundEnd @event, GameEventInfo info)
    {
        if (timer_ex != null) { timer_ex?.Kill(); }
        timer_ex = null;

        Server.ExecuteCommand("mp_teammates_are_enemies false");

        return HookResult.Continue;
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