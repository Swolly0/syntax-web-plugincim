using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Memory;
using System.Runtime.InteropServices;
using CounterStrikeSharp.API.Modules.Admin;
using System.Text.Json.Serialization;
using MenuManager;
using CounterStrikeSharp.API.Core.Capabilities;
using System.Diagnostics.CodeAnalysis;
using CounterStrikeSharp.API.Modules.Entities;

namespace JBCTMenu;
public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("select_limit")]
    public int select_limit { get; set; } = 2;

    [JsonPropertyName("respawn_limit")]
    public int respawn_hakki { get; set; } = 3;
	
	[JsonPropertyName("respawn_time")]
    public int respawn_suresi { get; set; } = 3;




    [JsonPropertyName("warden_hp")]
    public int warden_hp { get; set; } = 300;

    [JsonPropertyName("guardian_hp")]
    public int guardian_hp { get; set; } = 150;

    [JsonPropertyName("warden_speed")]
    public float warden_speed { get; set; } = 1.25f;

    [JsonPropertyName("guardian_speed")]
    public float guardian_speed { get; set; } = 1.15f;

    [JsonPropertyName("warden_stealhp")]
    public int warden_stealhp { get; set; } = 50;

    [JsonPropertyName("guardian_stealhp")]
    public int guardian_stealhp { get; set; } = 25;

}

public class JBCTMenu : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB CT Menu";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "JB | Komutcunun secebilecegi ozelliklerin oldugu bir menu.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";


    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        Config = config;
    }

    private readonly Dictionary<CCSPlayerController, int> Geri_Sayim = new();
    private readonly Dictionary<CCSPlayerController, Timer?> timer_ex = new();

    CCSPlayerController? iWarden = null;
    private MemoryFunctionVoid<CBasePlayerController, CCSPlayerPawn, bool, bool> CBasePlayerController_SetPawnFunc;


    private IMenuApi? _api;
    private readonly PluginCapability<IMenuApi?> _pluginCapability = new("menu:nfcore");

    public static int KalanSecim = 0;
    public bool SinirsizMermi;
    public bool CTRev;
    public bool CTHp;
    public bool CTHiz;
    public bool CTCanCalma;



    private static int KalanRev = 0;
    public bool OtoRespawn;





	
    public override void Load(bool hotReload)
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

        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn, HookMode.Pre);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Pre);
        RegisterEventHandler<EventPlayerConnect>(OnPlayerConnect);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt, HookMode.Pre);




        AddCommand("css_ctm", "CT Menu", (player, command) => CT_Menu(player, command));
        AddCommand("css_ctmenu", "CT Menu", (player, command) => CT_Menu(player, command));


        AddCommand("css_ctrevmenu", "CT Rev Menu", (player, command) => Rev_Menu(player, command));
		AddCommand("css_ctr", "CT Rev Menu", (player, command) => Rev_Menu(player, command));
				
		AddCommand("css_haksifirla", "CT rev hakki sifirlama.", (player, command) => Hak_Sifirla(player, command));
		AddCommand("css_haksifir", "CT rev hakki sifirlama.", (player, command) => Hak_Sifirla(player, command));
	}

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = _pluginCapability.Get();
        if (_api == null) Console.WriteLine("MenuManager Core not found...");
    }

    public void CT_Menu(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player != null && (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
        {
            if (iWarden == player)
                CTMenu(player);
            else 
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }

    public void CTMenu(CCSPlayerController player)
    {
        var menu = _api!.GetMenu(Localizer["ctmenu_title", KalanSecim]);
        if(SinirsizMermi)
            menu.AddMenuOption(Localizer["ctm_infinite_ammo"] + " [ AKTİF ]", (player, option) => handleeMenu(player, "ammo"));
        else
            menu.AddMenuOption(Localizer["ctm_infinite_ammo"], (player, option) => handleeMenu(player, "ammo"));

        if (CTRev)
            menu.AddMenuOption(Localizer["ctm_rev", Config.respawn_hakki] + " [ AKTİF ]", (player, option) => handleeMenu(player, "rev"));
        else
            menu.AddMenuOption(Localizer["ctm_rev", Config.respawn_hakki], (player, option) => handleeMenu(player, "rev"));

        if (CTHp)
            menu.AddMenuOption(Localizer["ctm_hp", Config.warden_hp, Config.guardian_hp] + " [ AKTİF ]", (player, option) => handleeMenu(player, "hp"));
        else
            menu.AddMenuOption(Localizer["ctm_hp", Config.warden_hp, Config.guardian_hp], (player, option) => handleeMenu(player, "hp"));

        if (CTHiz)
            menu.AddMenuOption(Localizer["ctm_speed", Config.warden_speed, Config.guardian_speed] + " [ AKTİF ]", (player, option) => handleeMenu(player, "speed"));
        else
            menu.AddMenuOption(Localizer["ctm_speed", Config.warden_speed, Config.guardian_speed], (player, option) => handleeMenu(player, "speed"));

        if (CTCanCalma)
            menu.AddMenuOption(Localizer["ctm_stealhp", Config.warden_stealhp, Config.guardian_stealhp] + " [ AKTİF ]", (player, option) => handleeMenu(player, "stealhp"));
        else
            menu.AddMenuOption(Localizer["ctm_stealhp", Config.warden_stealhp, Config.guardian_stealhp], (player, option) => handleeMenu(player, "stealhp"));

        menu.Open(player);
    }

    public void handleeMenu(CCSPlayerController player, string option)
    {
        if (option == "ammo")
        {
            if(!SinirsizMermi)
            {
                if (KalanSecim <= 0)
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ctm_limit"]);
                    return;
                }

                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ctm_ammo_enable"]);
                SinirsizMermi = true;
                KalanSecim--;
            }
            else
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ctm_ammo_disable"]);
                SinirsizMermi = false;
                KalanSecim++;
            }        
        }
        else
        if (option == "rev")
        {
            if (!CTRev)
            {
                if (KalanSecim <= 0)
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ctm_limit"]);
                    return;
                }

                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ctm_rev_enable"]);
                CTRev = true;
                KalanSecim--;
            }
            else
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ctm_rev_disable"]);
                CTRev = false;
                KalanSecim++;
            }
        }
        else
        if (option == "hp")
        {
            if (!CTHp)
            {
                if (KalanSecim <= 0)
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ctm_limit"]);
                    return;
                }

                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ctm_hp_enable"]);
                CTHp = true;
                KalanSecim--;

                foreach (var p in Utilities.GetPlayers())
                    if (p.is_valid_alive() && p.is_ct())
                    {
                        var health = 100;
                        if (iWarden == p)
                            health = Config.warden_hp;
                        else
                            health = Config.guardian_hp;


                        p.Health = health;
                        p.PlayerPawn.Value!.Health = health;

                        if (health > 100)
                        {
                            p.MaxHealth = health;
                            p.PlayerPawn.Value!.MaxHealth = health;
                        }

                        Utilities.SetStateChanged(p.PlayerPawn.Value!, "CBaseEntity", "m_iHealth");
                    }
            }
            else
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ctm_hp_disable"]);
                CTHp = false;
                KalanSecim++;

                foreach (var p in Utilities.GetPlayers())
                    if (p.is_valid_alive() && p.is_ct() && p.PlayerPawn.Value!.Health > 100)
                    {
                        var health = 100;

                        p.Health = health;
                        p.PlayerPawn.Value!.Health = health;

                        Utilities.SetStateChanged(p.PlayerPawn.Value!, "CBaseEntity", "m_iHealth");
                    }
            }
        }
        else
        if (option == "speed")
        {
            if (!CTHiz)
            {
                if (KalanSecim <= 0)
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ctm_limit"]);
                    return;
                }

                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ctm_speed_enable"]);
                CTHiz = true;
                KalanSecim--;

                foreach (var p in Utilities.GetPlayers())
                    if (p.is_valid_alive() && p.is_ct())
                    {
                        if (iWarden == p)
                            p.PlayerPawn.Value!.VelocityModifier = Config.warden_speed;
                        else
                            p.PlayerPawn.Value!.VelocityModifier = Config.guardian_speed;
                    }
            }
            else
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ctm_speed_disable"]);
                CTHiz = false;
                KalanSecim++;

                foreach (var p in Utilities.GetPlayers())
                    if (p.is_valid_alive() && p.is_ct())
                    {
                        if (iWarden == p)
                            p.PlayerPawn.Value!.VelocityModifier = 1.00f;
                        else
                            p.PlayerPawn.Value!.VelocityModifier = 1.00f;
                    }
            }
        }
        else
        if (option == "stealhp")
        {
            if (!CTCanCalma)
            {
                if (KalanSecim <= 0)
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ctm_limit"]);
                    return;
                }

                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ctm_stealhp_enable"]);
                CTCanCalma = true;
                KalanSecim--;
            }
            else
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ctm_stealhp_disable"]);
                CTCanCalma = false;
                KalanSecim++;
            }
        }

        CTMenu(player);
    }



















    public void Rev_Menu(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (CTRev && player != null && (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || iWarden == player)
            {
                if (KalanRev >= 1)
                    RevMenu(player);
                else
                    player.PrintToChat($"{Config.respawn_suresi}");
            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }
	
	public void Hak_Sifirla(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if(CTRev && player != null && (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || iWarden == player)
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["respawn_reset", player.PlayerName]);
				KalanRev = Config.respawn_hakki;
            } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
    }	

    public void RevMenu(CCSPlayerController player)
    {
		if(CTRev && player != null && (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || iWarden == player)
            {
                var menu = _api!.GetMenu(Localizer["revmenu_title"]);
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

                menu.Open(player);;
            } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
    }
	
	public void RevMenu_(CCSPlayerController player, CCSPlayerController target)
	{
        if (CTRev && player.is_valid() && target.is_valid() && player.is_ct())
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
            if (KalanRev >= 1)
            {
                if (Geri_Sayim[target] < 1)
                {
                    if (!target.PawnIsAlive && target.is_ct())
                    {
                        KalanRev--;
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_respawn", target.PlayerName, KalanRev]);

                        var playerPawn = target.PlayerPawn.Value;
                        if (playerPawn == null) return;

                        CBasePlayerController_SetPawnFunc.Invoke(target, playerPawn, true, false);
                        VirtualFunction.CreateVoid<CCSPlayerController>(target.Handle,
                            GameData.GetOffset("CCSPlayerController_Respawn"))(target);
                    }

                }
                else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["respawn_for_time", target.PlayerName, Geri_Sayim[target]]);
            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["respawn_end"]);
        }
	}

    HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        CCSPlayerController? p = @event.Userid;

        if (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_"))
        {
            if (p.is_valid() && p.is_ct())
            {
                AddTimer(1.0f, () =>
                {
                    if (p.is_valid_alive() && p.is_ct())
                    {
                        if (CTHp)
                        {
                            var health = 100;
                            if (iWarden == p)
                                health = Config.warden_hp;
                            else
                                health = Config.guardian_hp;

                            p.Health = health;
                            p.PlayerPawn.Value!.Health = health;

                            if (health > 100)
                            {
                                p.MaxHealth = health;
                                p.PlayerPawn.Value!.MaxHealth = health;
                            }

                            Utilities.SetStateChanged(p.PlayerPawn.Value!, "CBaseEntity", "m_iHealth");
                        }

                        if (CTHiz)
                        {
                            if (iWarden == p)
                                p.PlayerPawn.Value!.VelocityModifier = Config.warden_speed;
                            else
                                p.PlayerPawn.Value!.VelocityModifier = Config.guardian_speed;
                        }
                    }
                }, TimerFlags.STOP_ON_MAPCHANGE);
            }

        }

        return HookResult.Continue;
    }

    HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
		CCSPlayerController? player = @event.Userid;
		
		if(CTRev && player != null && (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_"))){

			if(player.is_valid())
				if(player.is_ct())
					if(KalanRev >= 1)
					{
						Geri_Sayim[player] = Config.respawn_suresi;
						
						timer_ex[player] = AddTimer(1.0f, () =>
						{
							if(!player.is_valid() || !player.is_ct() || Geri_Sayim[player] < 1 || player.PawnIsAlive)
							{
                                if (KalanRev >= 1 && OtoRespawn && Geri_Sayim[player] < 1) {
                                    KalanRev--;
                                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["auto_respawn", player.PlayerName, KalanRev]);
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
		
		if(player != null && (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
			if(player.is_valid())
				Geri_Sayim[player] = Config.respawn_suresi;

        return HookResult.Continue;
    }	

    HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
		if((NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
        {
			foreach (var p in Utilities.GetPlayers())
				if(p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
					Geri_Sayim[p] = Config.respawn_suresi;

			KalanRev = Config.respawn_hakki;
        }

        return HookResult.Continue;
    }

    public void Warden(CCSPlayerController? player, CommandInfo command)
    {
        if ((iWarden == null || !iWarden.is_valid() || !iWarden.is_ct()) && player.is_valid() && player.is_ct())
        {
            iWarden = player;

            KalanSecim = Config.select_limit;

            SinirsizMermi = false;
            CTRev = false;
            CTHp = false;
            CTHiz = false;
            CTCanCalma = false;
            OtoRespawn = true;
        }

        return;
    }

    public void UnWarden(CCSPlayerController? player, CommandInfo command)
    {
        if (iWarden == player)
            iWarden = null;

        return;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if ((NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
        {
            CCSPlayerController player = @event.Userid!;
            if (player != null && iWarden == player)
                iWarden = null;

            return HookResult.Continue;
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        if ((NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
        {
            CCSPlayerController player = @event.Userid!;

            if (player != null && iWarden == player)
            {
                if (@event.Team != 3)
                    iWarden = null;

                return HookResult.Continue;
            }
        }

        return HookResult.Continue;
    }
    
    HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        if (SinirsizMermi && (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
        {
            var player = @event.Userid;
            var weaponname = @event.Weapon;

            if (!player.is_valid() || !player.is_ct())
                return HookResult.Continue;

            var weapon = player.FindWeapon(weaponname);
            if (!weapon.IsLegal())
                return HookResult.Continue;

            if (!checkIfWeapon(weaponname, weapon.AttributeManager.Item.ItemDefinitionIndex))
                return HookResult.Continue;


            CCSWeaponBase _weapon = weapon.As<CCSWeaponBase>();
            if (_weapon != null)
            {
                if (_weapon.VData != null)
                {
                    _weapon.VData.MaxClip1 = 32;
                    _weapon.VData.DefaultClip1 = 32;
                }

                _weapon.Clip1 = 32;
                Utilities.SetStateChanged(weapon.As<CCSWeaponBase>(), "CBasePlayerWeapon", "m_iClip1");
            }
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (CTCanCalma && (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
        {
            CCSPlayerController victim = @event.Userid!;
            CCSPlayerController attacker = @event.Attacker!;

            if (victim.is_valid() && attacker.is_valid() && victim.is_t() && attacker.is_ct())
            {
                var pawn = attacker.PlayerPawn.Value!;

                float regenhp = @event.DmgHealth / 100.00f;
                int health = 0;

                if (iWarden == attacker)
                    health = (int)Math.Floor(regenhp * Config.warden_stealhp);
                else
                    health = (int)Math.Floor(regenhp * Config.guardian_stealhp);

                health += pawn.Health;

                if (health > 100)
                    if (CTHp)
                    {
                        if (iWarden == attacker && health > Config.warden_hp)
                            health = Config.warden_hp;
                        else
                        if (iWarden != attacker && health > Config.guardian_hp)
                            health = Config.guardian_hp;
                    }
                    else health = 100;




                attacker.Health = health;
                pawn!.Health = health;

                if (attacker.PlayerPawn.Value!.Health + health > 100)
                {
                    attacker.MaxHealth = health;
                    pawn!.MaxHealth = health;
                }

                Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
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


    static bool checkIfWeapon(string weaponName, int weaponDefIndex)
    {
        Dictionary<int, string> WeaponDefindex = new()
      {
        { 1, "weapon_deagle" },
        { 2, "weapon_elite" },
        { 3, "weapon_fiveseven" },
        { 4, "weapon_glock" },
        { 7, "weapon_ak47" },
        { 8, "weapon_aug" },
        { 9, "weapon_awp" },
        { 10, "weapon_famas" },
        { 11, "weapon_g3sg1" },
        { 13, "weapon_galilar" },
        { 14, "weapon_m249" },
        { 16, "weapon_m4a1" },
        { 17, "weapon_mac10" },
        { 19, "weapon_p90" },
        { 23, "weapon_mp5sd" },
        { 24, "weapon_ump45" },
        { 25, "weapon_xm1014" },
        { 26, "weapon_bizon" },
        { 27, "weapon_mag7" },
        { 28, "weapon_negev" },
        { 29, "weapon_sawedoff" },
        { 30, "weapon_tec9" },
        { 32, "weapon_hkp2000" },
        { 33, "weapon_mp7" },
        { 34, "weapon_mp9" },
        { 35, "weapon_nova" },
        { 36, "weapon_p250" },
        { 38, "weapon_scar20" },
        { 39, "weapon_sg556" },
        { 40, "weapon_ssg08" },
        { 60, "weapon_m4a1_silencer" },
        { 61, "weapon_usp_silencer" },
        { 63, "weapon_cz75a" },
        { 64, "weapon_revolver" },
      };

        if (WeaponDefindex.TryGetValue(weaponDefIndex, out string? value) && value == weaponName) return true;

        return false;
    }
}



public static class Lib{

    static public CBasePlayerWeapon? FindWeapon(this CCSPlayerController? player, String name)
    {
        CCSPlayerPawn? pawn = player.PlayerPawn.Value;
        if (pawn == null)
        {
            return null;
        }

        var weapons = pawn.WeaponServices?.MyWeapons;

        if (weapons == null)
        {
            return null;
        }

        foreach (var weaponOpt in weapons)
        {
            CBasePlayerWeapon? weapon = weaponOpt.Value;

            if (weapon == null)
            {
                continue;
            }

            if (weapon.DesignerName.Contains(name))
            {
                return weapon;
            }
        }

        return null;
    }

    static public bool IsLegal([NotNullWhen(true)] this CBasePlayerWeapon? weapon)
    {
        return weapon != null && weapon.IsValid;
    }


    static public bool is_valid(this CCSPlayerController? player)
    {
        return player != null && player.IsValid && player.PlayerPawn.IsValid && player.Connected == PlayerConnectedState.PlayerConnected;
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