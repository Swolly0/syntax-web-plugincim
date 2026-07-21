using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Admin;

using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBFFDondur;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}
public class JBFFDondur : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "FF Dondur";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "JB | !ffdondur komutu ile freeze ve ff kapanma suresi belirlenir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    public float FreezeGeriSayim;
    public float FFGeriSayim;
    public CounterStrikeSharp.API.Modules.Timers.Timer? timer_ex1;
    public CounterStrikeSharp.API.Modules.Timers.Timer? timer_ex2;

    ConVar? ff_command1 = null!;
    ConVar? ff_command2 = null!;

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
            Console.WriteLine($"[FF Dondur] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
			AddCommand("css_ffdondur", "FF Dondur", (player, command) => FF_Dondur(player, command));
			AddCommand("css_ffd", "FF Dondur", (player, command) => FF_Dondur(player, command));

			AddCommand("css_ffdondur0", "FF Dondur Iptal", (player, command) => FF_Dondur0(player, command));
			AddCommand("css_ffd0", "FF Dondur Iptal", (player, command) => FF_Dondur0(player, command));

			RegisterEventHandler<EventRoundStart>(OnRoundEvent);
			RegisterEventHandler<EventRoundEnd>(OnRoundEvent);

			RegisterListener<Listeners.OnTick>(() =>
			{
				if (FreezeGeriSayim >= 1|| FFGeriSayim >= 1)
				{
					var countdowntext = "<img src='https://www.plugincim.com/forum/data/assets/logo/yataybeyaz.png'/><br/>";
                    if (FreezeGeriSayim >= 1)
                        countdowntext = countdowntext + Localizer["center_freeze_countdown", Convert.ToInt32(FreezeGeriSayim)];
                    else
                        countdowntext = countdowntext + Localizer["center_frozen_t"];

					if (FFGeriSayim >= 1)
						countdowntext = countdowntext + Localizer["center_ff_countdown", Convert.ToInt32(FFGeriSayim)];
					else
						countdowntext = countdowntext + Localizer["center_ff_closed"];

					/*if (FreezeGeriSayim <= 10 || FFGeriSayim <= 10)
						if (FreezeGeriSayim <= 10)
							countdowntext = countdowntext + $"<br/><img src='https://www.plugincim.com/assets/images/cs2/numbers/{Convert.ToInt32(FreezeGeriSayim)}.png' width='64px' height='64px'/>";
						else
							countdowntext = countdowntext + $"<br/><img src='https://www.plugincim.com/assets/images/cs2/numbers/{Convert.ToInt32(FFGeriSayim)}.png' width='64px' height='64px'/>";
					*/

					countdowntext = countdowntext + "<br/><br/>";

					foreach (var p in Utilities.GetPlayers())
						if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
							p.PrintToCenterHtml(countdowntext);
				}
			});


			ff_command1 = ConVar.Find("mp_teammates_are_enemies");
			ff_command2 = ConVar.Find("mp_friendlyfire");
		}
	}
	
    public void FF_Dondur(CCSPlayerController? player, CommandInfo info)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct())
            {

                var FreezeSure = info.ArgByIndex(1);
                var FFSure = info.ArgByIndex(2);

                if (FreezeSure != null && FreezeSure != "" && IsInt(FreezeSure) && FFSure != null && FFSure != "" && IsInt(FFSure))
                {
                    if (player != null)
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_used", player.PlayerName, FreezeSure, FFSure]);

                    FreezeGeriSayim = Convert.ToInt32(FreezeSure);
                    FFGeriSayim = Convert.ToInt32(FFSure);

                    if (timer_ex1 != null) { timer_ex1?.Kill(); }
                    timer_ex1 = AddTimer(1.0f, () =>
                    {
                        if (FreezeGeriSayim == 0.0)
                        {
                            foreach (var p in Utilities.GetPlayers())
                                if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV && p.is_valid_alive() && p.is_t())
                                    p.Pawn.Value!.Freeze();

                            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["chat_frozen_t"]);

                            timer_ex1?.Kill();
                            timer_ex1 = null;
                            return;
                        }
                        else
                            FreezeGeriSayim -= 1.0f;
                    }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);

                    if (timer_ex2 != null) { timer_ex2?.Kill(); }
                    timer_ex2 = AddTimer(1.0f, () =>
                    {
                        if (FFGeriSayim == 0.0)
                        {
                            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["chat_ff_closed"]);
                            if (ff_command1 != null) { ff_command1.SetValue(false); }
                            if (ff_command2 != null) { ff_command2.SetValue(false); }

                            timer_ex2?.Kill();
                            timer_ex2 = null;
                            return;
                        }
                        else
                            FFGeriSayim -= 1.0f;
                    }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
                }
                else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage"]);

            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }

    public void FF_Dondur0(CCSPlayerController? player, CommandInfo info)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct())
            {
                if (timer_ex1 != null)
                    timer_ex1?.Kill();

                if (timer_ex2 != null)
                    timer_ex2?.Kill();

                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_cancel", player.PlayerName]);
                FreezeGeriSayim = 0.0f;
                FFGeriSayim = 0.0f;
                timer_ex1 = null;
                timer_ex2 = null;

            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }

    public HookResult OnRoundEvent(EventRoundStart @event, GameEventInfo info)
    {
        if (timer_ex1 != null) { timer_ex1?.Kill(); }
        timer_ex1 = null;

        if (timer_ex2 != null) { timer_ex2?.Kill(); }
        timer_ex2 = null;

        return HookResult.Continue;
    }

    public HookResult OnRoundEvent(EventRoundEnd @event, GameEventInfo info)
    {
        if (timer_ex1 != null) { timer_ex1?.Kill(); }
        timer_ex1 = null;

        if (timer_ex2 != null) { timer_ex2?.Kill(); }
        timer_ex2 = null;

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