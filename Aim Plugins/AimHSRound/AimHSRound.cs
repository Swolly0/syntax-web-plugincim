using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;

using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace AimHSRound;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("autovote_round")]
    public int rastgele_round { get; set; } = 6;
}

public class AimHSRound : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "Aim HS Round";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "AIM haritalarinda belirli bir round'da bir hs round oylanir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    private static int RoundCounter = 0, ActiveRound = 0;
    private static bool VoteActive = false;
    private static readonly int[] Votes = new int[32];
    private readonly Dictionary<CCSPlayerController, bool> Oy = new();
    public CounterStrikeSharp.API.Modules.Timers.Timer? VoteTimer;
    public CounterStrikeSharp.API.Modules.Timers.Timer? timer_ex;

    ConVar? mp_death_drop_gun = null!;

    private string AktifSilah = "";

    // LISANS
    public int lisans_bitis_yil = 2025; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 25; // Lisansin bitecegi gun
    // LISANS

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[AHR] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
            AddCommand("css_hsround", "HS Round", (player, command) => StartVote(player, command));
            AddCommand("css_hsr", "HS Round", (player, command) => StartVote(player, command));

            RegisterEventHandler<EventRoundStart>(OnRoundStart, HookMode.Pre);
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd, HookMode.Pre);
            RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt, HookMode.Pre);

            mp_death_drop_gun = ConVar.Find("mp_death_drop_gun");
        }
    }

    HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            mp_death_drop_gun.SetValue(1);
            RoundCounter++;

            if (VoteTimer != null)
                VoteTimer.Kill();

            VoteTimer = null;


            if (VoteActive)
            {
                mp_death_drop_gun.SetValue(1);
                RoundCounter = 0;
                VoteActive = false;

                int i = 0, MaxVote = 0;
                while (i != 7)
                {
                    i++;

                    if (Votes[i] >= MaxVote)
                    {
                        ActiveRound = i;
                        MaxVote = Votes[i];
                    }
                }


                if (ActiveRound == 1)
                    AktifSilah = "weapon_ak47";
                else
                if (ActiveRound == 2)
                    AktifSilah = "weapon_m4a1_silencer";
                else
                if (ActiveRound == 3)
                    AktifSilah = "weapon_m4a1";
                else
                if (ActiveRound == 4)
                    AktifSilah = "weapon_deagle";
                else
                if (ActiveRound == 5)
                    AktifSilah = "weapon_usp_silencer";
                else
                if (ActiveRound == 6)
                    AktifSilah = "weapon_elite";

                //SilahlariSil();
                foreach (var p in Utilities.GetPlayers())
                    if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
                    {
                        p.RemoveWeapons();
                        p.GiveNamedItem(AktifSilah);
                    }



                if (ActiveRound == 1)
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["playing_round", "AK47"]);
                else
                if (ActiveRound == 2)
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["playing_round", "M4A1-S"]);
                else
                if (ActiveRound == 3)
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["playing_round", "M4A1"]);
                else
                if (ActiveRound == 4)
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["playing_round", "DEAGLE"]);
                else
                if (ActiveRound == 5)
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["playing_round", "USP-S"]);
                else
                if (ActiveRound == 6)
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["playing_round", "ÇİFT BERETTA"]);



                if (timer_ex != null) timer_ex!.Kill();
                timer_ex = AddTimer(0.5f, () =>
                {
                    if (ActiveRound > 0 && AktifSilah != "")
                    {
                        foreach (var p in Utilities.GetPlayers())
                            if (p.is_valid_alive())
                            {
                                bool kontrol = false;
                                int silahsayisi = 0;

                                foreach (var weapon in p.PlayerPawn.Value.WeaponServices!.MyWeapons)
                                {
                                    if (weapon is { IsValid: true, Value.IsValid: true })
                                    {
                                        CCSWeaponBaseVData? weaponData = weapon.Value.As<CCSWeaponBase>().VData;

                                        if (weaponData?.Name == AktifSilah)
                                            kontrol = true;

                                        silahsayisi++;
                                    }
                                }

                                if (silahsayisi >= 2)
                                {
                                    p.RemoveWeapons();
                                    p.GiveNamedItem(AktifSilah);
                                }
                                else
                                if (!kontrol)
                                {
                                    p.RemoveWeapons();
                                    p.GiveNamedItem(AktifSilah);
                                }
                            }
                    }
                }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
            }
            else
            if (RoundCounter == Config.rastgele_round)
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["autovote_started"]);
                Start_Vote();
            }
        }

        return HookResult.Continue;
    }

    HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            if (ActiveRound > 0)
            {
                SilahlariSil();
                AddTimer(1.0f, () =>
                {
                    foreach (var p in Utilities.GetPlayers())
                        if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
                            p.GiveNamedItem("weapon_knife");
                }, TimerFlags.STOP_ON_MAPCHANGE);

                mp_death_drop_gun.SetValue(1);
                ActiveRound = 0;

                if (timer_ex != null) timer_ex!.Kill();
                timer_ex = null;
            }

            if (!VoteActive && RoundCounter == Config.rastgele_round)
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["autovote_started"]);
                Start_Vote();
            }
        }

        return HookResult.Continue;
    }

    public void StartVote(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_started", player.PlayerName]);
                Start_Vote();
            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }

    public void Start_Vote()
    {
        Votes[1] = 0; Votes[2] = 0; Votes[3] = 0; Votes[4] = 0; Votes[5] = 0; Votes[6] = 0;
        VoteActive = true;
        RoundCounter = 0;

        foreach (var p in Utilities.GetPlayers())
            if (p != null)
            {
                var menu = new CenterHtmlMenu(Localizer["vote_title"]);
                menu.AddMenuOption("AK47 Round", (player, option) => Vote_(p, 1));
                menu.AddMenuOption("M4A1-S Round", (player, option) => Vote_(p, 2));
                menu.AddMenuOption("M4A4 Round", (player, option) => Vote_(p, 3));
                menu.AddMenuOption("DEAGLE Round", (player, option) => Vote_(p, 4));
                menu.AddMenuOption("USP-S Round", (player, option) => Vote_(p, 5));
                menu.AddMenuOption("Çift Beretta Round", (player, option) => Vote_(p, 6));
                MenuManager.OpenCenterHtmlMenu(this, p, menu);

                Oy[p] = false;
            }


        if (VoteTimer != null)
            VoteTimer.Kill();

        VoteTimer = AddTimer(15.0f, () =>
        {
			foreach (CCSPlayerController target in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false })){
				MenuManager.CloseActiveMenu(target);
				target.PrintToCenterHtml("", 0);
			}

            VoteTimer = null;

            int i = 0, MaxVote = 0, WinRound = 0;
            while (i != 7)
            {
                i++;

                if (Votes[i] >= MaxVote)
                {
                    WinRound = i;
                    MaxVote = Votes[i];
                }
            }

            if (WinRound == 1)
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["win_round", "AK47"]);
            else
            if (WinRound == 2)
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["win_round", "M4A1-S"]);
            else
            if (WinRound == 3)
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["win_round", "M4A1"]);
            else
            if (WinRound == 4)
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["win_round", "DEAGLE"]);
            else
            if (WinRound == 5)
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["win_round", "USP-S"]);
            else
            if (WinRound == 6)
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["win_round", "ÇİFT BERETTA"]);

        }, TimerFlags.STOP_ON_MAPCHANGE);
    }

    public void Vote_(CCSPlayerController player, int option)
    {
        if (VoteTimer != null && !Oy[player])
        {
            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_gived"]);
            Oy[player] = true;
            Votes[option]++;
        }
    }


    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
            if (ActiveRound >= 1)
            {
                if (!@event.Userid.IsValid || @event.Hitgroup == 1)
                {
                    return HookResult.Continue;
                }

                CCSPlayerController player = @event.Userid;

                if (player.Connected != PlayerConnectedState.PlayerConnected)
                {
                    return HookResult.Continue;
                }

                if (!player.PlayerPawn.IsValid)
                {
                    return HookResult.Continue;
                }

                player.PlayerPawn.Value.Health += @event.DmgHealth;
                player.PlayerPawn.Value.ArmorValue += @event.DmgArmor;

                player.Health += @event.DmgHealth;

                Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_iHealth");

                @event.Userid.PlayerPawn.Value.VelocityModifier = 1;
            }

        return HookResult.Continue;
    }

    public void SilahlariSil()
    {
        foreach (var entity in Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("weapon"))
        {
            if (entity == null) continue;
            if (entity.Entity == null) continue;
            if (!entity.DesignerName.StartsWith("weapon_")) continue;

            entity.Remove();
        }
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