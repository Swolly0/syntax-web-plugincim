using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Admin;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;
using System.Drawing;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.UserMessages;

namespace JBSaklambac;
public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Red}[CyberRulz]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}

public class JBSaklambac : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Saklambac";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "JB | !saklambac komutu ile saklambac geri sayimi baslatilabilir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";

    public float Geri_Sayim = 0;
    private Timer? timer_ex = null;
    internal static IStringLocalizer? Stringlocalizer;
    public bool SaklambacAktif = false;

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        Config = config;
        Stringlocalizer = Localizer;
    }

    public override void Load(bool hotReload)
    {
        AddCommand("css_saklambac", "Saklambac", (player, command) => Saklambac(player, command));
        AddCommand("css_saklambac0", "Saklambac0", (player, command) => Saklambac0(player, command));

        AddCommandListener("say", OnPlayerChat);

        RegisterEventHandler<EventRoundStart>(OnRoundEvent);
        RegisterEventHandler<EventRoundEnd>(OnRoundEvent);

        RegisterListener<Listeners.OnTick>(() =>
        {
            if (Geri_Sayim >= 1)
            {
                foreach (var p in Utilities.GetPlayers())
                    if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
                        if (Geri_Sayim <= 10)
                            p.PrintToCenterHtml($"<img src='http://cyberrulz.com/img/plugin-banner.png'/><br/><br/><p style='font-weight: 700; color: red; font-size: 20px;'>" + Localizer["center_countdown"] + $"</p><img src='http://cyberrulz.com/img/numbers/{Convert.ToInt32(Geri_Sayim)}.png' width='64px' height='64px'/><br/><br/>");
                        else
                            p.PrintToCenterHtml($"<img src='http://cyberrulz.com/img/plugin-banner.png'/><br/><br/><p style='font-weight: 700; color: red; font-size: 20px;'>" + Localizer["center_countdown"] + $" {Convert.ToInt32(Geri_Sayim)} " + Localizer["second"] + ".</p><br/><br/>");
            }
        });
    }

    public void Saklambac(CCSPlayerController? player, CommandInfo info)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct())
            {
                var Sure = info.ArgByIndex(1);

                if (Sure != null && Sure != "" && IsInt(Sure))
                {
                    if (player != null)
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_used", player.PlayerName, Sure]);

                    Geri_Sayim = Convert.ToInt32(Sure);
                    SaklambacAktif = false;

                    foreach (var p in Utilities.GetPlayers())
                        if (p.is_valid_alive())
                            if(p.is_ct())
                            {
                                ChangeMovetype(p.pawn()!, MoveType_t.MOVETYPE_OBSOLETE, Color.White);
                                p.Blind(999f);
                            }
                            else
                            if (p.is_t())
                                ChangeMovetype(p.pawn()!, MoveType_t.MOVETYPE_WALK, Color.White);


                    if (timer_ex != null) { timer_ex?.Kill(); }
                    timer_ex = AddTimer(1.0f, () =>
                    {
                        if (Geri_Sayim == 0.0)
                        {
                            foreach (var p in Utilities.GetPlayers())
                                if (p.is_valid_alive())
                                    if (p.is_ct())
                                    {
                                        ChangeMovetype(p.pawn()!, MoveType_t.MOVETYPE_WALK, Color.White);
                                        p.UnBlind();
                                    }
                                    else
                                    if (p.is_t())
                                        ChangeMovetype(p.pawn()!, MoveType_t.MOVETYPE_OBSOLETE, Color.White);

                            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["freeze_open"]);
                            SaklambacAktif = true;

                            timer_ex?.Kill();
                            timer_ex = null;
                            return;
                        }
                        else
                        {
                            Geri_Sayim -= 1.0f;
                            if (Geri_Sayim <= 10.0) { Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["chat_countdown", Geri_Sayim]); }
                        }

                    }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
                }
                else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage"]);

            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }

    public void Saklambac0(CCSPlayerController? player, CommandInfo info)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct())
            {
                foreach (var p in Utilities.GetPlayers())
                    if (p.is_valid_alive())
                        if (p.is_ct())
                        {
                            ChangeMovetype(p.pawn()!, MoveType_t.MOVETYPE_WALK, Color.White);
                            p.UnBlind();
                        }
                        else
                        if (p.is_t())
                            ChangeMovetype(p.pawn()!, MoveType_t.MOVETYPE_WALK, Color.White);

                if (timer_ex != null)
                    timer_ex?.Kill();

                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["freeze_close", player.PlayerName]);
                Geri_Sayim = 0.0f;
                timer_ex = null;
                SaklambacAktif = false;
            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }


    private HookResult OnPlayerChat(CCSPlayerController? player, CommandInfo info)
    {
        if (SaklambacAktif)
            if (!player.is_valid_alive() && player.is_t() && !AdminManager.PlayerHasPermissions(player, "@css/generic"))
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + " \x01saklambaç oynatılırken mesaj atamazsın.");
                return HookResult.Handled;
            }

        return HookResult.Continue;
    }

    public HookResult OnRoundEvent(EventRoundStart @event, GameEventInfo info)
    {
        if (timer_ex != null) { timer_ex?.Kill(); }
        timer_ex = null;
        SaklambacAktif = false;

        return HookResult.Continue;
    }

    public HookResult OnRoundEvent(EventRoundEnd @event, GameEventInfo info)
    {
        if (timer_ex != null) { timer_ex?.Kill(); }
        timer_ex = null;
        SaklambacAktif = false;

        return HookResult.Continue;
    }

    static void ChangeMovetype(CBasePlayerPawn pawn, MoveType_t movetype, Color? color)
    {
        pawn.MoveType = movetype;
        Schema.SetSchemaValue(pawn.Handle, "CBaseEntity", "m_nActualMoveType", movetype);
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");

        if (color != null)
        {
            pawn.RenderMode = RenderMode_t.kRenderTransColor;
            pawn.Render = color.Value;

            Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");
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
    public enum FadeFlags
    {
        FADE_IN,
        FADE_OUT,
        FADE_STAYOUT
    }

    public static void Blind(this CCSPlayerController player, float value) => player.ColorScreen(Color.Black, value);
    public static void UnBlind(this CCSPlayerController player) => player.ColorScreen(Color.Black, 0, 0);
    private static void ColorScreen(this CCSPlayerController player, Color color, float hold = 0.1f, float fade = 0.2f, FadeFlags flags = FadeFlags.FADE_IN, bool withPurge = true)
    {
        var fadeMsg = UserMessage.FromPartialName("Fade");

        fadeMsg.SetInt("duration", Convert.ToInt32(fade * 512));
        fadeMsg.SetInt("hold_time", Convert.ToInt32(hold * 512));

        var flag = flags switch
        {
            FadeFlags.FADE_IN => 0x0001,
            FadeFlags.FADE_OUT => 0x0002,
            FadeFlags.FADE_STAYOUT => 0x0008,
            _ => 0x0001
        };

        if (withPurge)
        {
            flag |= 0x0010;
        }

        fadeMsg.SetInt("flags", flag);
        fadeMsg.SetInt("color", color.R | color.G << 8 | color.B << 16 | color.A << 24);
        fadeMsg.Send(player);
    }



    static public bool is_valid(this CCSPlayerController? player)
    {
        return player != null && player.IsValid && player.PlayerPawn.IsValid && player.Connected == PlayerConnectedState.PlayerConnected;
    }

    static public bool is_t(this CCSPlayerController? player)
    {
        return is_valid(player) && player.TeamNum == 2;
    }

    static public bool is_ct(this CCSPlayerController? player)
    {
        return is_valid(player) && player.TeamNum == 3;
    }

    // yes i know the null check is redundant but C# is dumb
    static public bool is_valid_alive(this CCSPlayerController? player)
    {
        return player.is_valid() && player.PawnIsAlive && player.get_health() > 0;
    }

    static public CCSPlayerPawn? pawn(this CCSPlayerController? player)
    {
        if (player == null || !player.is_valid())
        {
            return null;
        }

        CCSPlayerPawn? pawn = player.PlayerPawn.Value!;
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