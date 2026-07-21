
﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Memory;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Utils;

namespace JBTekMermi;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}

public class JBTekMermi : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Tek Mermi";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "JB | Mahkumlar ist";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    public int Geri_Sayim = 0;
    public bool OyunAktif = false;
    public CounterStrikeSharp.API.Modules.Timers.Timer? timer_ex = null;

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
            Console.WriteLine($"[Tek Mermi] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
            AddCommand("css_tekm", "Tek Mermi", (player, command) => TekMermi(player, command));
            AddCommand("css_tekmermi", "Tek Mermi", (player, command) => TekMermi(player, command));
            AddCommand("css_tekm0", "Tek Mermi İptal", (player, command) => TekMermi0(player, command));
            AddCommand("css_tekmermi0", "Tek Mermi İptal", (player, command) => TekMermi0(player, command));

            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Pre);
            RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt, HookMode.Pre);
            RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
            RegisterEventHandler<EventRoundStart>(OnRoundEvent);
            RegisterEventHandler<EventRoundEnd>(OnRoundEvent);

            RegisterListener<Listeners.OnTick>(() =>
            {
                if (Geri_Sayim >= 1)
                {
                    foreach (var p in Utilities.GetPlayers())
                        if (p.is_valid())
                            if (Geri_Sayim <= 10)
                                p.PrintToCenterHtml($"<img src='http://cyberrulz.com/img/plugin-banner.png'/><br/><br/><p style='font-weight: 700; color: red; font-size: 20px;'" + Localizer["menu_title1"] + $"</p><img src='http://cyberrulz.com/img/numbers/{Geri_Sayim}.png' width='64px' height='64px'/><br/><br/>");
                            else
                                p.PrintToCenterHtml($"<img src='http://cyberrulz.com/img/plugin-banner.png'/><br/><br/><p style='font-weight: 700; color: red; font-size: 20px;'>" + Localizer["menu_title2", Geri_Sayim] + "</p><br/><br/>");
                }
            });
        }
    }
	
	public void TekMermi(CCSPlayerController? player, CommandInfo info)
    {	
		if(NativeAPI.GetMapName().Contains("jb_")){
			if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct()){
				
				var Sure = info.ArgByIndex(1);
				
				if (Sure != null && Sure != "" && IsInt(Sure)){
                    if(player != null)
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_start", player.PlayerName, Sure]);

                    Geri_Sayim = Convert.ToInt32(Sure);
                    OyunAktif = false;

                    Server.ExecuteCommand("mp_death_drop_gun 0");
                    SilahlariSil(false);

                    AddTimer(0.3f, () =>
                    {
                        foreach (var p in Utilities.GetPlayers())
                            if (p.is_valid_alive() && p.is_t())
                            {
                                p.GiveNamedItem("weapon_ak47");

                                AddTimer(0.2f, () =>
                                {
                                    var weapon = new CBasePlayerWeapon((nint)(p.PlayerPawn.Value.WeaponServices.ActiveWeapon.Value.Handle));
                                    if (weapon == null || !weapon.IsValid) return;

                                    Server.NextFrame(() =>
                                    {
                                        weapon.Clip1 = 1;
                                        weapon.ReserveAmmo[0] = 0;
                                        Schema.SetSchemaValue<int>(weapon.Handle, "CBasePlayerWeapon", "m_iClip1", 1);
                                        Schema.SetSchemaValue<int>(weapon.Handle, "CBasePlayerWeapon", "m_pReserveAmmo", 0);
                                    });
                                });

                                SetHP(p, 100);
                            }
                    }, TimerFlags.STOP_ON_MAPCHANGE);

					if(timer_ex != null){ timer_ex?.Kill(); }
					timer_ex = AddTimer(1.0f, () =>
					{
						if(Geri_Sayim == 0){

                            Server.ExecuteCommand("mp_teammates_are_enemies true");

                            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["ff_active"]);

                            OyunAktif = true;
                            timer_ex?.Kill();

                            timer_ex = AddTimer(0.5f, () =>
                            {
                                if (OyunAktif)
                                {
                                    foreach (var p in Utilities.GetPlayers())
                                        if (p.is_valid_alive() && p.is_t())
                                        {
                                            var weapon = new CBasePlayerWeapon((nint)(p.PlayerPawn.Value.WeaponServices.ActiveWeapon.Value.Handle));
                                            if (weapon == null || !weapon.IsValid) return;

                                            Server.NextFrame(() =>
                                            {
                                                if (weapon.Clip1 > 1)
                                                {
                                                    weapon.Clip1 = 1;
                                                    Schema.SetSchemaValue<int>(weapon.Handle, "CBasePlayerWeapon", "m_iClip1", 1);
                                                }

                                                if (weapon.Clip1 != 0 && weapon.ReserveAmmo[0] >= 1)
                                                {
                                                    weapon.ReserveAmmo[0] = 0;
                                                    Schema.SetSchemaValue<int>(weapon.Handle, "CBasePlayerWeapon", "m_pReserveAmmo", 0);
                                                }
                                            });
                                        }
                                }
                                else
                                {
                                    timer_ex?.Kill();
                                    timer_ex = null;
                                    return;
                                }
                            }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);

                            return;
						} else
							Geri_Sayim--;

					}, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);						
				} else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage"]);		
				
			} else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);			
		}
    }

	public void TekMermi0(CCSPlayerController? player, CommandInfo info)
    {	
		if(player != null && NativeAPI.GetMapName().Contains("jb_")){
			if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct()){
				Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_stop", player.PlayerName]);
                Reset();
            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);			
		}
    }

    public void Reset()
    {
        Server.ExecuteCommand("mp_teammates_are_enemies false");
        Server.ExecuteCommand("mp_death_drop_gun 1");
        SilahlariSil(true);

        OyunAktif = false;
        Geri_Sayim = 0;

        if (timer_ex != null)
            timer_ex?.Kill();

        timer_ex = null;
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_") && OyunAktif)
        {
            if (!@event.Userid.IsValid || @event.Hitgroup == 1)
                return HookResult.Continue;

            CCSPlayerController player = @event.Userid;

            if (!player.is_valid_alive() || !player.is_t())
                return HookResult.Continue;

            player.PlayerPawn.Value.ArmorValue += @event.DmgArmor;

            SetHP(player, player.Health + @event.DmgHealth);
            @event.Userid.PlayerPawn.Value.VelocityModifier = 1;
        }

        return HookResult.Continue;
    }

    HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_") && OyunAktif)
        {
            int alivet = 0;
            foreach (var p in Utilities.GetPlayers())
                if (p.is_valid_alive() && p.is_t())
                    alivet++;

            if (alivet >= 2)
            {
                CCSPlayerController? killer = @event.Attacker;

                if (killer.is_valid_alive() && killer.is_t())
                {
                    var weapon = new CBasePlayerWeapon((nint)(killer.PlayerPawn.Value.WeaponServices.ActiveWeapon.Value.Handle));
                    if (weapon == null || !weapon.IsValid) return HookResult.Continue;

                    Server.NextFrame(() =>
                    {
                        if (weapon.Clip1 == 0)
                        {
                            weapon.Clip1 = 1;
                            Schema.SetSchemaValue<int>(weapon.Handle, "CBasePlayerWeapon", "m_iClip1", 1);
                            weapon.ReserveAmmo[0] = 0;
                            Schema.SetSchemaValue<int>(weapon.Handle, "CBasePlayerWeapon", "m_pReserveAmmo", 0);
                        }
                    });
                }
            }
            else
                Reset();
        }

        return HookResult.Continue;
    }

    HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_") && OyunAktif)
        {
            var player = @event.Userid;

            if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV && player.is_valid_alive())
            {
                var weapon = new CBasePlayerWeapon((nint)(player.PlayerPawn.Value.WeaponServices.ActiveWeapon.Value.Handle));
                if (weapon == null || !weapon.IsValid) return HookResult.Continue;

                Server.NextFrame(() =>
                {
                    if (weapon.Clip1 == 0)
                    {
                        weapon.ReserveAmmo[0] = 1;
                        Schema.SetSchemaValue<int>(weapon.Handle, "CBasePlayerWeapon", "m_pReserveAmmo", 1);
                    }
                });
            }
        }

        return HookResult.Continue;
    }

    public HookResult OnRoundEvent(EventRoundStart @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            if (timer_ex != null) { timer_ex?.Kill(); }
            timer_ex = null;

            Server.ExecuteCommand("mp_teammates_are_enemies false");
            Server.ExecuteCommand("mp_death_drop_gun 1");
        }

        return HookResult.Continue;
    }

    public HookResult OnRoundEvent(EventRoundEnd @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            if (timer_ex != null) { timer_ex?.Kill(); }
            timer_ex = null;

            Server.ExecuteCommand("mp_teammates_are_enemies false");
            Server.ExecuteCommand("mp_death_drop_gun 1");
        }

        return HookResult.Continue;
    }
    public void SilahlariSil(bool giveknife = false)
    {
        foreach (var entity in Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("weapon"))
        {
            if (entity == null) continue;
            if (entity.Entity == null) continue;
            if (!entity.DesignerName.StartsWith("weapon_")) continue;
            if (entity.OwnerEntity == null || !entity.OwnerEntity.IsValid)
            {
                entity.Remove();
            }
        }

        AddTimer(0.5f, () =>
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (player.is_valid_alive() && player.is_t())
                {
                    var playerPawn = player.pawn();
                    if (playerPawn?.WeaponServices?.MyWeapons == null)
                        continue;

                    var weapons = playerPawn.WeaponServices.MyWeapons.ToList();
                    if (weapons.Count == 0)
                        continue;

                    var activeWeapon = playerPawn.WeaponServices.ActiveWeapon.Value;
                    bool removeActiveWeapon = false;

                    foreach (var weapon in weapons.Select(w => w.Value))
                    {
                        if (weapon == null || !weapon.IsValid)
                            continue;

                        // Aktif silahsa daha sonra işlem yapılacak
                        if (weapon == activeWeapon)
                        {
                            removeActiveWeapon = true;
                            continue;
                        }

                        // Silahı öldür
                        weapon.AddEntityIOEvent("Kill", weapon);
                    }

                    // Eğer aktif silah silinmesi gerekiyorsa, kısa bir gecikmeyle düşürüp öldür
                    if (removeActiveWeapon)
                    {
                        AddTimer(0.2f, () =>
                        {
                            if (activeWeapon != null && activeWeapon.IsValid)
                            {
                                VirtualFunction.CreateVoid<nint, nint>(
                                    playerPawn.ItemServices.Handle,
                                    GameData.GetOffset("CCSPlayer_ItemServices_DropActivePlayerWeapon")
                                )(playerPawn.ItemServices.Handle, activeWeapon.Handle);

                                activeWeapon.AddEntityIOEvent("Kill", activeWeapon, delay: 0.1f);
                            }

                            if (giveknife)
                                player.GiveNamedItem("weapon_knife");
                        });
                    }
                }
            }
        });
    }

    public void SetHP(CCSPlayerController? p, int health)
    {
        if (health > 0 && p.is_valid_alive())
        {
            p.Health = health;
            p.pawn()!.Health = health;

            if (health > 100)
            {
                p.MaxHealth = health;
                p.pawn()!.MaxHealth = health;
            }

            Utilities.SetStateChanged(p.pawn()!, "CBaseEntity", "m_iHealth");
        }
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
