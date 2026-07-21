using System.Net;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API.Modules.Commands.Targeting;

namespace RastgeleRound;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("autovote_round")]
    public int rastgele_round { get; set; } = 10;
}

public class CS2AwpRandomRounds : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "CS2-AwpRandomRounds";
    public override string ModuleVersion => "1.1.0";
    public override string ModuleAuthor => "Plugincim.com";

    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    private static int _roundCounter, _activeRound;
    private static readonly int[] Votes = new int[32];
    private static bool _voteActive;
    private readonly Dictionary<CCSPlayerController, bool> _oy = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? _voteTimer;

    private ConVar? _mpDeathDropGun;
    private ConVar? _mpInfiniteAmmo;
    private bool isActive => _activeRound > 0;

    public required Config Config { get; set; }

    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Logger.LogInformation($"[{ModuleName}] Old config version {config.Version}, required {ModuleConfigVersion}");
        }
        Config = config;
        Stringlocalizer = Localizer;
    }

    public override void Load(bool hotReload)
    {
        AddCommand("css_rastgeleround", "Rastgele Round", StartVote);
        AddCommand("css_cr", "Rastgele Round", StartVote);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventWeaponZoom>(WeaponZoom);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt, HookMode.Pre);

        _mpDeathDropGun = ConVar.Find("mp_death_drop_gun");
        _mpInfiniteAmmo = ConVar.Find("sv_infinite_ammo");
    }

    private bool IsSupportedMap() => NativeAPI.GetMapName().Contains("awp_");

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (!IsSupportedMap()) return HookResult.Continue;

        _mpDeathDropGun?.SetValue(1);
        _roundCounter++;

        _voteTimer?.Kill();
        _voteTimer = null;

        if (_voteActive)
        {
            _mpDeathDropGun?.SetValue(0);
            _roundCounter = 0;
            _voteActive = false;

            int maxVote = 0;
            for (int i = 1; i < 8; i++)
            {
                if (Votes[i] >= maxVote)
                {
                    _activeRound = i;
                    maxVote = Votes[i];
                }
            }

            GiveRoundWeapons();
            AnnounceRound();
        }
        else if (_roundCounter == Config.rastgele_round)
        {
            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["autovote_started"]);
            Start_Vote();
        }

        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (!IsSupportedMap()) return HookResult.Continue;

        if (_activeRound > 0)
        {
            RemoveWeaponsFromAll();
            GiveKnifeToAll();
            _activeRound = 0;
            Server.ExecuteCommand("bunny_enable 0");
            _mpInfiniteAmmo?.SetValue(0);
        }
        return HookResult.Continue;
    }

    public void StartVote(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !IsSupportedMap()) return;

        if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_started", player.PlayerName]);
            Start_Vote();
        }
        else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
    }

    public void Start_Vote()
    {
        Array.Clear(Votes, 0, Votes.Length);
        _voteActive = true;
        _roundCounter = 0;

        foreach (var p in Utilities.GetPlayers())
        {
            var menu = new CenterHtmlMenu(Localizer["vote_title"]);
            menu.AddMenuOption("No Scope Round", (_, _) => Vote_(p, 1));
            menu.AddMenuOption("Deagle Round", (_, _) => Vote_(p, 2));
            menu.AddMenuOption("AK47 Round", (_, _) => Vote_(p, 4));
            menu.AddMenuOption("Ssg08 Round", (_, _) => Vote_(p, 5));
            MenuManager.OpenCenterHtmlMenu(this, p, menu);
            _oy[p] = false;
        }

        _voteTimer?.Kill();
        _voteTimer = AddTimer(15.0f, EndVote, TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void EndVote()
    {
        foreach (var target in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false }))
            MenuManager.CloseActiveMenu(target);

        _voteTimer = null;

        int maxVote = 0, winRound = 0;
        for (int i = 1; i < 8; i++)
        {
            if (Votes[i] >= maxVote)
            {
                winRound = i;
                maxVote = Votes[i];
            }
        }

        string roundName = winRound switch
        {
            1 => "No Scope",
            2 => "Deagle",
            3 => "Bunny + Knife",
            4 => "Ak47",
            5 => "Ssg08",
            6 => "M4A1-S",
            7 => "USP-S",
            _ => "Unknown"
        };

        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["win_round", roundName]);
    }

    public void Vote_(CCSPlayerController player, int option)
    {
        if (_voteTimer != null && !_oy[player])
        {
            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_gived"]);
            _oy[player] = true;
            Votes[option]++;
        }
        MenuManager.CloseActiveMenu(player);
    }

    private HookResult WeaponZoom(EventWeaponZoom @event, GameEventInfo info)
    {
        if (_activeRound != 1) return HookResult.Continue;
        var player = @event.Userid;
        var currentWeapon = player.PlayerPawn.Value?.WeaponServices!.ActiveWeapon.Value?.DesignerName;
        player.PlayerPawn.Value?.WeaponServices!.ActiveWeapon.Value?.Remove();
        if (currentWeapon != null) player.GiveNamedItem(currentWeapon);
        return HookResult.Continue;
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (!IsSupportedMap() || !isActive) return HookResult.Continue;

        CCSPlayerController player = @event.Userid;
        if (player.is_valid_alive())
        {
            // Bıçak hasarı kapalı
            if (@event.Weapon.Contains("knife") || @event.Weapon.Contains("bayonet")
                || ((@event.Weapon.Contains("deagle") || @event.Weapon.Contains("revolver")) && @event.Hitgroup != 1))
            {
                // Hasarı tamamen iptal et
                int restoreHealth = @event.DmgHealth;
                int restoreArmor = @event.DmgArmor;

                @event.DmgHealth = 0;
                @event.DmgArmor = 0;

                if (player.PlayerPawn.Value != null)
                {
                    // Canı 100'ün üstüne çıkmasın
                    int newHealth = Math.Min(100, player.PlayerPawn.Value.Health + restoreHealth);
                    player.PlayerPawn.Value.Health = newHealth;

                    // Armor 100 üstüne çıkmasın
                    int newArmor = Math.Min(100, player.PlayerPawn.Value.ArmorValue + restoreArmor);
                    player.PlayerPawn.Value.ArmorValue = newArmor;

                    Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_iHealth");
                    player.PlayerPawn.Value.VelocityModifier = 1;
                }
            }
        }

        return HookResult.Continue;
    }

    private void GiveRoundWeapons()
    {
        RemoveWeaponsFromAll();
        AddTimer(0.5f, () =>
        {
            foreach (var p in Utilities.GetPlayers())
            {
                if (!p.is_valid_alive()) continue;

                switch (_activeRound)
                {
                    case 1: p.GiveNamedItem("weapon_awp"); break;
                    case 2: p.GiveNamedItem("weapon_deagle"); _mpInfiniteAmmo?.SetValue(1); break;
                    case 4: p.GiveNamedItem("weapon_ak47"); break;
                    case 5: p.GiveNamedItem("weapon_ssg08"); break;
                    case 6: p.GiveNamedItem("weapon_m4a1_silencer"); break;
                    case 7: p.GiveNamedItem("usp_silencer"); break;
                }
                p.GiveNamedItem("weapon_knife");
            }
        }, TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void AnnounceRound()
    {
        string roundName = _activeRound switch
        {
            1 => "AWP",
            2 => "Deagle",
            3 => "Bunny + Knife",
            4 => "AK47",
            5 => "Ssg08",
            6 => "M4A1-S",
            7 => "USP-S",
            _ => "Unknown"
        };

        if (_activeRound == 3) Server.ExecuteCommand("bunny_enable 1");

        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["playing_round", roundName]);
    }

    private void RemoveWeaponsFromAll()
    {
        AddTimer(0.5f, () =>
        {
            foreach (var p in Utilities.GetPlayers())
                if (p.is_valid_alive()) p.RemoveWeapons();
        });
    }

    private void GiveKnifeToAll()
    {
        AddTimer(0.5f, () =>
        {
            foreach (var p in Utilities.GetPlayers())
                if (p.is_valid_alive()) p.GiveNamedItem("weapon_knife");
        }, TimerFlags.STOP_ON_MAPCHANGE);
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
            message = remove ? message.Replace(colorPatterns[i], "") : "\u200e" + message.Replace(colorPatterns[i], colorReplacements[i]);

        return message;
    }
}

public static class Lib
{
    public static bool is_valid(this CCSPlayerController? player)
        => player != null && player.IsValid && player.PlayerPawn.IsValid;

    public static bool is_t(this CCSPlayerController? player)
        => player.is_valid() && player!.TeamNum == 2;

    public static bool is_ct(this CCSPlayerController? player)
        => player.is_valid() && player!.TeamNum == 3;

    public static bool is_valid_alive(this CCSPlayerController? player)
        => player.is_valid() && player!.PawnIsAlive && player.get_health() > 0;

    public static CCSPlayerPawn? Pawn(this CCSPlayerController? player)
        => player != null && player.is_valid() ? player.PlayerPawn.Value : null;

    public static int get_health(this CCSPlayerController? player)
    {
        var pawn = player.Pawn();
        return pawn?.Health ?? 100;
    }
}
