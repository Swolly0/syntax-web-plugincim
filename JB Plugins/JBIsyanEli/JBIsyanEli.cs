using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBIsyanEli;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}

public class JBIsyanEli : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Isyan Eli";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!iseli ile isyan eli baslatilabilir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    public CounterStrikeSharp.API.Modules.Timers.Timer? timer_ex;
    public float Geri_Sayim = 0.0f;
    private MemoryFunctionVoid<CBasePlayerController, CCSPlayerPawn, bool, bool> CBasePlayerController_SetPawnFunc;


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
            Console.WriteLine($"[Isyan Eli] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
			AddCommand("css_iseli", "Isyan eli baslatilir.", (player, command) => IsEli(player, command));
			CBasePlayerController_SetPawnFunc =
				new MemoryFunctionVoid<CBasePlayerController, CCSPlayerPawn, bool, bool>(GetSignature());

			RegisterListener<Listeners.OnTick>(() =>
			{
                if (Geri_Sayim >= 1)
                {
                    foreach (var p in Utilities.GetPlayers())
                        if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
                            if (Geri_Sayim <= 10)
                                p.PrintToCenterHtml($"<img src='https://www.plugincim.com/assets/images/plugin-banner.png'/><br/><br/><p style='font-weight: 700; color: red; font-size: 20px;'>" + Localizer["center_game_countdown2"] + $"</p><img src='https://www.plugincim.com/assets/images/numbers/{Convert.ToInt32(Geri_Sayim)}.png' width='64px' height='64px'/><br/><br/>");
                            else
                                p.PrintToCenterHtml($"<img src='https://www.plugincim.com/assets/images/plugin-banner.png'/><br/><br/><p style='font-weight: 700; color: red; font-size: 20px;'>" + Localizer["center_game_countdown1", Convert.ToInt32(Geri_Sayim)] + "</p><br/><br/>");
				}
			});
		}
    }
	
    public void IsEli(CCSPlayerController? player, CommandInfo info)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.TeamNum == 3)
            {
                var Sure = info.ArgByIndex(1);

                if (Sure != null && Sure != "" && IsInt(Sure))
                {
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_used", player.PlayerName, Sure]);

                    foreach (var p in Utilities.GetPlayers())
                        if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
                        {
                            if(p.is_valid_alive())
                            {
                                player.MaxHealth = 100;
                                player.PlayerPawn.Value.MaxHealth = 100;
                                player.Health = 100;
                                player.PlayerPawn.Value.Health = 100;
                            }

                            if (p.is_t() || (p.is_ct() && !p.is_valid_alive()))
                            {
                                var targetPawn = p.PlayerPawn.Value;
                                if (targetPawn == null) return;

                                CBasePlayerController_SetPawnFunc.Invoke(p, targetPawn, true, false);
                                VirtualFunction.CreateVoid<CCSPlayerController>(p.Handle,
                                    GameData.GetOffset("CCSPlayerController_Respawn"))(p);
                            }

                            if (p.is_t())
                            {
                                var weapons = p.PlayerPawn.Value.WeaponServices.MyWeapons;
                                foreach (var weapon in weapons)
                                    if(weapon != null && weapon.IsValid)
                                        weapon.Value.Remove();

                                p.GiveNamedItem("weapon_knife");
                            }
                        }


                    Geri_Sayim = Convert.ToInt32(Sure);
                    if (timer_ex != null) { timer_ex?.Kill(); }
                    timer_ex = AddTimer(1.0f, () =>
                    {
                        if (Geri_Sayim == 0.0)
                        {
                            foreach (var p in Utilities.GetPlayers())
                                if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV && p.is_valid_alive())
                                {
                                    player.MaxHealth = 100;
                                    player.PlayerPawn.Value.MaxHealth = 100;
                                    player.Health = 100;
                                    player.PlayerPawn.Value.Health = 100;
                                }

                            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_started"]);
                            force_ent_input("func_door", "Open");
                            force_ent_input("func_movelinear", "Open");
                            force_ent_input("func_door_rotating", "Open");
                            force_ent_input("prop_door_rotating", "Open");
                            force_ent_input("func_breakable", "Break");

                            timer_ex?.Kill();
                            return;
                        }
                        else
                        {
                            Geri_Sayim -= 1.0f;

                            if (Geri_Sayim <= 10.0) { Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_countdown", Geri_Sayim]); }
                        }
                    }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);

                } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage"]);
            }
            else info.ReplyToCommand(ReplaceTags($"{Config.EklentiTagi} ", true) + Localizer["permissions"]);
    }

    private static string GetSignature()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"\x44\x88\x4C\x24\x2A\x55\x57"
            : @"\x55\x48\x89\xE5\x41\x57\x41\x56\x41\x55\x41\x54\x49\x89\xFC\x53\x48\x89\xF3\x48\x81\xEC\xC8\x00\x00\x00";
    }

    static void force_ent_input(String name, String input)
    {
        var target = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>(name);

        foreach (var ent in target)
        {
            if (!ent.IsValid)
                continue;

            ent.AcceptInput(input);
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