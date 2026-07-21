using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using System.Runtime.InteropServices;

using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBSustum;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("Words")]
    public string[] Kelimeler { get; set; } = { "cibo", "peniag", "swolly", "tedy", "kaan", "zeyno", "cyberrulz", "araba", "ev", "para", "ben", "katil", "dünya", "kafam", "güzel", "kötü", "kz", "buyukisyan", "swo", "türkiye", "izmir", "istanbul", "bmw", "mercedes", "zurna", "deniz", "bozkurt" };

}

public class JBSustum : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Sustum";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "JB | Sustum";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    CCSPlayerController iWarden = null;
    public float Geri_Sayim = 0.0f;
    public int Sustum_Turu = 0, Toplam = 0;
    public string Kelime = "";
    public bool Sustum;

    private MemoryFunctionVoid<CBasePlayerController, CCSPlayerPawn, bool, bool> CBasePlayerController_SetPawnFunc;
    private static readonly float[] DedPosX = new float[32];
    private static readonly float[] DedPosY = new float[32];
    private static readonly float[] DedPosZ = new float[32];
    private readonly Dictionary<CCSPlayerController, bool> Yazdi = new();
    private readonly Dictionary<CCSPlayerController, bool> DSustumAktif = new();

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
            Console.WriteLine($"[JB Sustum] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
					iWarden = null;
			}, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
			// KOMUTÇU SİSTEMİ




			AddCommand("css_tsustum", "T sustum'u baslatir.", (player, command) => TSustum(player, command));
			AddCommand("css_ctsustum", "Freeze Zaman", (player, command) => CTSustum(player, command));
			AddCommand("css_dsustum", "Freeze Zaman", (player, command) => DSustum(player, command));
			AddCommand("css_olusustum", "Freeze Zaman", (player, command) => OluSustum(player, command));
			AddCommand("css_sustum0", "Sustum'ları iptal eder.", (player, command) => Sustum0(player, command));


			AddCommandListener("say", OnPlayerSay, HookMode.Post);
			AddCommandListener("say_team", OnPlayerSay, HookMode.Post);
			RegisterEventHandler<EventWeaponFire>(OnWeaponFire, HookMode.Pre);
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Pre);


            RegisterListener<Listeners.OnTick>(() =>
			{
				if (Geri_Sayim >= 1 || Sustum)
				{
					foreach (var p in Utilities.GetPlayers())
						if (p.is_valid())
						{
							string SustumIsmi = "";
                            if (!Sustum)
                            {
                                if (Sustum_Turu == 1)
                                    SustumIsmi = $"<p style='font-weight: 700; color: red; font-size: 20px;'>" + Localizer["countdown_title_1", Convert.ToInt32(Geri_Sayim)] + "</p>";
                                else
                                if (Sustum_Turu == 2)
                                    SustumIsmi = $"<p style='font-weight: 700; color: red; font-size: 20px;'>" + Localizer["countdown_title_2", Convert.ToInt32(Geri_Sayim)] + "</p>";
                                else
                                if (Sustum_Turu == 3)
                                    SustumIsmi = $"<p style='font-weight: 700; color: red; font-size: 20px;'>" + Localizer["countdown_title_3", Convert.ToInt32(Geri_Sayim)] + "</p>";
                                else
                                if (Sustum_Turu == 4)
                                    SustumIsmi = $"<p style='font-weight: 700; color: red; font-size: 20px;'>" + Localizer["countdown_title_4", Convert.ToInt32(Geri_Sayim)] + "</p>";

                                if (Geri_Sayim <= 10)
                                    p.PrintToCenterHtml($"<img src='https://www.plugincim.com/assets/images/plugin-banner.png'/><br/><br/>{SustumIsmi}<img src='https://www.plugincim.com/assets/images/numbers/{Convert.ToInt32(Geri_Sayim)}.png' width='64px' height='64px'/><br/><br/>");
                                else
                                    p.PrintToCenterHtml($"<img src='https://www.plugincim.com/assets/images/plugin-banner.png'/><br/><br/>{SustumIsmi}<br/><br/>");
                            }
                            else
                            {
                                SustumIsmi = $"<p style='font-weight: 700; color: yellow; font-size: 24px;'>{Kelime}</p>"; ;
                                p.PrintToCenterHtml($"<img src='https://www.plugincim.com/assets/images/plugin-banner.png'/><br/><br/>{SustumIsmi}<br/><br/>");
                            }
                        }
				}
			});


			CBasePlayerController_SetPawnFunc =
				new MemoryFunctionVoid<CBasePlayerController, CCSPlayerPawn, bool, bool>(GetSignature());
		}
    }
    public void TSustum(CCSPlayerController? player, CommandInfo info)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct())
            {
                var Sure = info.ArgByIndex(1);

                if (Sure != null && Sure != "" && IsInt(Sure))
                {
                    if (player != null)
                    {
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_start_1", player.PlayerName, Sure]);
                        Geri_Sayim = Convert.ToInt32(Sure);
                        Sustum_Turu = 1;
                        Sustum = false;
                    }

                    SustumBaslat();
                }
                else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage_1"]);

            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }
    public void CTSustum(CCSPlayerController? player, CommandInfo info)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct())
            {
                var Sure = info.ArgByIndex(1);

                if (Sure != null && Sure != "" && IsInt(Sure))
                {
                    if (player != null)
                    {
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_start_2", player.PlayerName, Sure]);
                        Geri_Sayim = Convert.ToInt32(Sure);
                        Sustum_Turu = 2;
                        Sustum = false;
                    }

                    SustumBaslat();
                }
                else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage_2"]);

            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }
    public void DSustum(CCSPlayerController? player, CommandInfo info)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct())
            {
                var Sure = info.ArgByIndex(1);

                if (Sure != null && Sure != "" && IsInt(Sure))
                {
                    if (player != null)
                    {
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_start_3", player.PlayerName, Sure]);
                        Geri_Sayim = Convert.ToInt32(Sure);
                        Sustum_Turu = 3;
                        Sustum = false;
                    }

                    SustumBaslat();
                }
                else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage_3"]);

            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }

    public void OluSustum(CCSPlayerController? player, CommandInfo info)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct())
            {
                var Sure = info.ArgByIndex(1);

                if (Sure != null && Sure != "" && IsInt(Sure))
                {
                    if (player != null)
                    {
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_start_4", player.PlayerName, Sure]);
                        Geri_Sayim = Convert.ToInt32(Sure);
                        Sustum_Turu = 4;
                        Sustum = false;
                    }

                    SustumBaslat();
                }
                else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage_4"]);

            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }

    public void Sustum0(CCSPlayerController? player, CommandInfo info)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct())
            {
                if (timer_ex != null)
                    timer_ex?.Kill();

                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_stop", player.PlayerName]);
                Geri_Sayim = 0.0f;
                timer_ex = null;
                Sustum = false;

                foreach (var p in Utilities.GetPlayers())
                    if (p.is_valid())
                    {
                        Yazdi[p] = false;
                        DSustumAktif[p] = false;
                    }

            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }

    public void SustumBaslat()
    {
        Kelime = $"{Config.Kelimeler[new Random().Next(0, 17)]}{new Random().Next(1, 999)}";

        if (timer_ex != null) { timer_ex?.Kill(); }
        timer_ex = AddTimer(1.0f, () =>
        {
            if (Geri_Sayim == 0.0)
            {
                if (Sustum_Turu == 4)
                {
                    foreach (var p in Utilities.GetPlayers())
                        if (p.is_valid())
                        {
                            Yazdi[p] = false;
                            DSustumAktif[p] = false;
                        }

                    Toplam = 0;
                }

                Sustum = true;
                timer_ex?.Kill();

                return;
            }
            else
                Geri_Sayim -= 1.0f;
        }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
    }












    private HookResult OnPlayerSay(CCSPlayerController? player, CommandInfo info)
    {
        if (!Sustum || player == null || !player.IsValid || player.IsBot || player.IsHLTV) return HookResult.Continue;

        var message = info.GetArg(1);
        string trimmedMessage1 = message.TrimStart();
        string messages = trimmedMessage1.TrimEnd();
        
        if (NativeAPI.GetMapName().Contains("jb_") && messages == Kelime)
        {
            if (Sustum_Turu == 1 && player.TeamNum == 2)
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_win_game_1", player.PlayerName]);
                player.SwitchTeam(CsTeam.CounterTerrorist);
                Sustum = false;
            }
            else
            if (Sustum_Turu == 2 && player.TeamNum == 3 && !Yazdi[player])
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_win_game_2", player.PlayerName]);
                Yazdi[player] = true;

                int ToplamCT = 0;
                Toplam++;

                foreach (var p in Utilities.GetPlayers())
                    if (p.is_valid() && p.is_ct() && p != iWarden)
                        ToplamCT++;

                if(ToplamCT - 1 == Toplam)
                {
                    foreach (var p in Utilities.GetPlayers())
                        if (p.is_valid() && p.is_ct() && p != iWarden && !Yazdi[p])
                        {
                            p.SwitchTeam(CsTeam.Terrorist); 
                            break;
                        }

                    Sustum = false;
                }
            }
            else
            if (Sustum_Turu == 3 && player.is_t() && player.is_valid_alive())
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_win_game_3", player.PlayerName]);
                player.GiveNamedItem("weapon_deagle");
                Sustum = false;
                DSustumAktif[player] = true;
            }
            else
            if (Sustum_Turu == 4 && player.is_t() && !player.is_valid_alive())
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_win_game_4", player.PlayerName]);

                var playerPawn = player.PlayerPawn.Value;
                player.Respawn();

                var position = playerPawn.AbsOrigin;
                var angle = playerPawn.EyeAngles!;
                var velocity = playerPawn.AbsVelocity;

                position.X = DedPosX[player.Index];
                position.Y = DedPosY[player.Index];
                position.Z = DedPosZ[player.Index];

                player.Teleport(position, angle, velocity);

                Sustum = false;
            }
        }

        return HookResult.Continue;
    }
    HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if (player != null && player.is_valid_alive() && player.is_t() && DSustumAktif.ContainsKey(player) && DSustumAktif[player])
        {
            AddTimer(0.1f, () =>
            {
                player.RemoveWeapons();
            }, TimerFlags.STOP_ON_MAPCHANGE);

            AddTimer(0.3f, () =>
            {
                player.GiveNamedItem("weapon_knife");
            }, TimerFlags.STOP_ON_MAPCHANGE);

            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["weapon_fire", player.PlayerName]);
            DSustumAktif[player] = false;
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
            iWarden = null;

        return;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            CCSPlayerController player = @event.Userid;

            if (player != null && iWarden == player)
                iWarden = null;

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
                    iWarden = null;

                return HookResult.Continue;
            }
        }

        return HookResult.Continue;
    }

    HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if (player.is_valid())
        {
            var playerPawn = player.PlayerPawn.Value;
            if (playerPawn == null || !player.IsValid) return HookResult.Continue;

            var position = playerPawn.AbsOrigin;

            if (position != null)
            {
                DedPosX[player.Index] = position.X;
                DedPosY[player.Index] = position.Y;
                DedPosZ[player.Index] = position.Z;
            }
        }

        return HookResult.Continue;
    }


    private static string GetSignature()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"\x44\x88\x4C\x24\x2A\x55\x57"
            : @"\x55\x48\x89\xE5\x41\x57\x41\x56\x41\x55\x41\x54\x49\x89\xFC\x53\x48\x89\xF3\x48\x81\xEC\xC8\x00\x00\x00";
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