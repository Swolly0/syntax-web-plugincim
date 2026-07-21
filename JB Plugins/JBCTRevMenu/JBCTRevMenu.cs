using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Memory;
using System.Runtime.InteropServices;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Commands.Targeting;

namespace JBCTRevMenu;
public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
	
    [JsonPropertyName("respawn_limit")]
    public int ctrevmenu_respawn_hakki { get; set; } = 3;
	
	[JsonPropertyName("respawn_time")]
    public int ctrevmenu_respawn_suresi { get; set; } = 10;	
}
public class JBCTRevMenu : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB CT Rev Menu";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "JB | Komutcunun korumalarini belirli bir limit icerisinde canlandirabilir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    private static int Kalan_Hak = 0;
    public bool OtoRespawn;

    private readonly Dictionary<CCSPlayerController, int> Geri_Sayim = new();
    private readonly Dictionary<CCSPlayerController, Timer?> timer_ex = new();

    CCSPlayerController? iWarden = null;

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
            Console.WriteLine($"[CT Respawn Menu] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
				{
					iWarden = null;
					OtoRespawn = true;
				}
			}, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
			// KOMUTÇU SİSTEMİ



			AddCommand("css_ctrevmenu", "CT Rev Menu", (player, command) => Rev_Menu(player, command));
			AddCommand("css_ctr", "CT Rev Menu", (player, command) => Rev_Menu(player, command));
				
			AddCommand("css_haksifirla", "CT rev hakki sifirlama.", (player, command) => Hak_Sifirla(player, command));
			AddCommand("css_haksifir", "CT rev hakki sifirlama.", (player, command) => Hak_Sifirla(player, command));
								
			RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath,HookMode.Pre);	
			RegisterEventHandler<EventPlayerConnect>(OnPlayerConnect);
			RegisterEventHandler<EventRoundStart>(OnRoundStart);
		}
	}
	
	public void Rev_Menu(CCSPlayerController? player, CommandInfo commandInfo)
    {	
		if(player != null && NativeAPI.GetMapName().Contains("jb_")){
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || iWarden == player)
            {
                if (Kalan_Hak >= 1)
					RevMenu(player);
				else
					player.PrintToChat($"{Config.ctrevmenu_respawn_suresi}");
			} else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);			
		}
    }
	
	public void Hak_Sifirla(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if(player != null && NativeAPI.GetMapName().Contains("jb_"))
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || iWarden == player)
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["respawn_reset", player.PlayerName]);
				Kalan_Hak = Config.ctrevmenu_respawn_hakki;
            } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
    }	

    public void RevMenu(CCSPlayerController player)
    {
		if(player != null && NativeAPI.GetMapName().Contains("jb_"))
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || iWarden == player)
            {
                var menu = new CenterHtmlMenu(Localizer["menu_title"]);
                if(OtoRespawn)
                    menu.AddMenuOption(Localizer["auto_respawn_active"], (player, target) => RevMenu_(player, player));
                else
                    menu.AddMenuOption(Localizer["auto_respawn_inactive"], (player, target) => RevMenu_(player, player));

                foreach (var p in Utilities.GetPlayers())
                    if (!p.is_valid_alive() && p.is_ct())
                    {
                        string playerName = Localizer["menu_option", p.PlayerName, Geri_Sayim[p]];
                        menu.AddMenuOption(playerName, (player, target) => RevMenu_(player, p));
                    }

                MenuManager.OpenCenterHtmlMenu(this, player, menu);
            } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
    }
	
	public void RevMenu_(CCSPlayerController player, CCSPlayerController target)
	{
        if (player.is_valid() && target.is_valid() && player.is_ct())
        {
            if(player == target)
            {
                if (OtoRespawn)
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["auto_respawn_be_inactive"]);
                    OtoRespawn = false;
                }
                else
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["auto_respawn_be_active"]);
                    OtoRespawn = true;
                }
            }
            else
            if (Kalan_Hak >= 1)
            {
                if (Geri_Sayim[target] < 1)
                {
                    if (!target.PawnIsAlive && target.is_ct())
                    {
                        Kalan_Hak--;
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_respawn", target.PlayerName, Kalan_Hak]);

                        var playerPawn = target.PlayerPawn.Value;
                        if (playerPawn == null) return;
                        target.Respawn();
                    }

                }
                else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["respawn_for_time", target.PlayerName, Geri_Sayim[target]]);
            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["respawn_end"]);
        }
	}

    HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
		CCSPlayerController? player = @event.Userid;
		
		if(player != null && NativeAPI.GetMapName().Contains("jb_")){

			if(player.is_valid())
				if(player.is_ct())
					if(Kalan_Hak >= 1)
					{
						Geri_Sayim[player] = Config.ctrevmenu_respawn_suresi;
						
						timer_ex[player] = AddTimer(1.0f, () =>
						{
							if(!player.is_valid() || !player.is_ct() || Geri_Sayim[player] < 1 || player.PawnIsAlive)
							{
                                if (Kalan_Hak >= 1 && OtoRespawn && Geri_Sayim[player] < 1) {
                                    Kalan_Hak--;
                                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["auto_respawn", player.PlayerName, Kalan_Hak]);

                                    var playerPawn = player.PlayerPawn.Value;
                                    if (playerPawn == null) return;
                                    player.Respawn();
                                }


								timer_ex[player]?.Kill();
                                timer_ex[player] = null;

                                return;
							} else Geri_Sayim[player]--;
						}, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);		
					}	
					else
						Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["respawn_end"]);						
		}

        return HookResult.Continue;
    }		
	
    HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
    {
		CCSPlayerController? player = @event.Userid;
		
		if(player != null && NativeAPI.GetMapName().Contains("jb_"))
			if(player.is_valid())
				Geri_Sayim[player] = Config.ctrevmenu_respawn_suresi;

        return HookResult.Continue;
    }	
	
    HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
		if(NativeAPI.GetMapName().Contains("jb_"))
        {
			foreach (var p in Utilities.GetPlayers())
				if(p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
					Geri_Sayim[p] = Config.ctrevmenu_respawn_suresi;

			Kalan_Hak = Config.ctrevmenu_respawn_hakki;
		}

        return HookResult.Continue;
    }






    public void Warden(CCSPlayerController? player, CommandInfo command)
    {
        if ((iWarden == null || !iWarden.is_valid() || !iWarden.is_ct()) && player.is_valid() && player.is_ct())
            iWarden = player;

        return;
    }

    public void UnWarden(CCSPlayerController? player, CommandInfo command)
    {
        if (iWarden == player)
        {
            iWarden = null;
            OtoRespawn = true;
        }

        return;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            CCSPlayerController player = @event.Userid;

            if (player != null && iWarden == player)
            {
                iWarden = null;
                OtoRespawn = true;
            }
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
                {
                    iWarden = null;
                    OtoRespawn = true;
                }

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
            if(!remove)
                message = "\u200e" + message.Replace(colorPatterns[i], colorReplacements[i]);
            else
                message = "\u200e" + message.Replace(colorPatterns[i], "");

        return message;
    }
    private static bool IsValidConfigString(string value) => !string.IsNullOrEmpty(value) && value != "-"; // This is a "lambda expression body method"

    private static string GetSignature()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"\x44\x88\x4C\x24\x2A\x55\x57"
            : @"\x55\x48\x89\xE5\x41\x57\x41\x56\x41\x55\x41\x54\x49\x89\xFC\x53\x48\x89\xF3\x48\x81\xEC\xC8\x00\x00\x00";
    }
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