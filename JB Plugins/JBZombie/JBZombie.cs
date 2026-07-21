using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Numerics;
using CounterStrikeSharp.API.Core.Capabilities;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using MenuManager;
using System.ComponentModel;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Entities.Constants;


public class JBZombie : BasePlugin
{
    public override string ModuleName => "Zombie Mod";
    public override string ModuleVersion => "1.0.1";
    public override string ModuleDescription => "Geliştirilmiş Zombie modu eklentisi";
    public override string ModuleAuthor => "Custom Developer";

    private static bool ZombieAktif = false;
    private static Dictionary<CCSPlayerController, bool> ZombieOldu = new();
    private static Dictionary<CCSPlayerController, Vector3> PlayerDeathLocations = new();
    private static CounterStrikeSharp.API.Modules.Timers.Timer? countdownTimer;
    private static int GeriSayim;

    private IMenuApi? _api;
    private readonly PluginCapability<IMenuApi?> _pluginCapability = new("menu:nfcore");

    public override void Load(bool hotReload)
    {
        AddCommand("css_zombie", "Zombie modu başlat", (player, command) => StartZombieMode(player, command));
        AddCommand("css_zombie0", "Zombie modunu sıfırla", (player, command) => EndZombieMode(player));
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
    }

    [GameEventHandler(mode: HookMode.Pre)]
    private HookResult OnEventItemPickupPost(EventItemPickup @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (!player.is_valid_alive() || !player.is_ct() || !ZombieAktif)
            return HookResult.Continue;

        player.RemoveWeapons(); // Oyuncunun tüm silahlarını kaldır
        player.GiveNamedItem("weapon_knife"); // Bıçağı geri ver

        return HookResult.Continue; // Oyun akışını sürdür
    }



    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = _pluginCapability.Get();
        if (_api == null) Console.WriteLine("MenuManager Core not found...");
    }

    private void StartZombieMode(CCSPlayerController? player, CommandInfo command)
    {
        if (!IsJBMap()) return;
        if (ZombieAktif)
        {
            player?.PrintToChat(ReplaceTags(" {Red}[CyberRulz] {White}Zombie modu şu anda {Green}aktif. {Blue}!zombie0"));
            return;
        }

        if (command.ArgCount < 2 || !int.TryParse(command.ArgString, out int time))
        {
            player?.PrintToChat(ReplaceTags(" {Red}[CyberRulz] {White}Geçerli bir süre girin: !zombie <süre>"));
            return;
        }

        Server.PrintToChatAll(ReplaceTags($" {{Red}}[CyberRulz] {{Green}}{player.PlayerName}, {{White}}zombie oyununu başlattı."));
        Server.PrintToChatAll(ReplaceTags($" {{Red}}[CyberRulz] {{White}}zombie oyununu başlamasına {{Red}}son {time} saniye."));

        RemoveWeaponsOnTheGround();
        ZombieOldu.Clear();

        foreach (var p in Utilities.GetPlayers())
        {
            if (p.is_valid())
            {
                ZombieOldu.Add(p, false);

                if (p.is_valid_alive())
                    if (p.is_t())
                        SilahMenu(p);
                    else
                    if (p.is_ct())
                    {
                        player.RemoveWeapons();
                        player.GiveNamedItem("weapon_knife");
                    }

                    SetHP(p, p.is_t() ? 100 : 1000);
            }
        }

        ZombieAktif = true;
        GeriSayim = time;
        countdownTimer = AddTimer(1.0f, TimerGeriSayim, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void EndZombieMode(CCSPlayerController? player)
    {
        if (!IsJBMap()) return;

        Server.PrintToChatAll(ReplaceTags(" {Red}[CyberRulz] {White}Zombie modu kapatıldı."));
        countdownTimer?.Kill();
        countdownTimer = null;
        ZombieAktif = false;

        foreach (var p in Utilities.GetPlayers())
        {
            if (p.is_valid())
            {
                if (p.is_ct() && ZombieOldu.ContainsKey(p) && ZombieOldu[p] == true)
                {
                    if (p.is_valid_alive())
                        p.CommitSuicide(true, true);

                    p.ChangeTeam(CsTeam.Terrorist);
                }
                else if (p.is_t() && p.is_valid_alive())
                {
                    p.RemoveWeapons();
                }
                SetHP(p, 100);
            }
        }

        RemoveWeaponsOnTheGround();
        ZombieOldu.Clear();
        PlayerDeathLocations.Clear();
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (!IsJBMap() || !ZombieAktif || GeriSayim > 0) return HookResult.Continue;

        var victim = @event.Userid;
        var attacker = @event.Attacker;

        if (victim.is_valid())
        {
            ZombieUtils.CopyLastCoord(victim);

            if (victim.is_t())
            {
                int aliveTCount = Utilities.GetPlayers().FindAll(p => p.is_valid_alive() && p.is_t()).Count;
                if (aliveTCount <= 1)
                {
                    EndZombieMode(null);
                    return HookResult.Continue;
                }
            }
        }

        if (attacker.is_valid() && attacker.is_ct() && victim.is_t())
            ZombieYap(victim);

        return HookResult.Continue;
    }

    private void ZombieYap(CCSPlayerController player)
    {
        Server.PrintToChatAll(ReplaceTags($" {{Red}}[CyberRulz] {{Green}}{player.PlayerName}, {{White}}enfekte oldu."));
        player.ChangeTeam(CsTeam.CounterTerrorist);

        ZombieOldu[player] = true;

        AddTimer(0.2f, () => RespawnAtDeathLocation(player));
    }

    private void RespawnAtDeathLocation(CCSPlayerController target)
    {
        Vector lastCoord = target.GetLastCoord();

        CCSPlayerPawn? targetPawn = target.PlayerPawn.Value;

        if (targetPawn == null || targetPawn.AbsRotation == null)
        {
            return;
        }

        target.Respawn();
        targetPawn.Teleport(lastCoord, targetPawn.AbsRotation, targetPawn.AbsVelocity);

        SetHP(target, 500);
    }

    private void TimerGeriSayim()
    {
        if (!IsJBMap()) return;

        if (GeriSayim <= 10 || GeriSayim % 5 == 0)
            Server.PrintToChatAll(ReplaceTags($" {{Red}}[CyberRulz] {{White}}zombie oyununu başlamasına {{Red}}son {GeriSayim} saniye."));

        GeriSayim--;

        if (GeriSayim <= 0)
        {
            foreach (var p in Utilities.GetPlayers())
                if (p.is_valid())
                {
                    _api.CloseMenu(p);
                    p.PrintToCenterHtml("", 0);
                }

            Server.PrintToChatAll(ReplaceTags(" {Red}[CyberRulz] {White}Zombie modu başladı."));

            countdownTimer?.Kill();
            countdownTimer = null;
            ZombieAktif = true;
        }
    }

    private void SilahMenu(CCSPlayerController player)
    {
        player.RemoveWeapons();
        player.GiveNamedItem("weapon_ak47");
        player.GiveNamedItem("weapon_deagle");
        player.GiveNamedItem("weapon_knife");

        var menu = _api!.GetMenu("Zombie Silah Menüsü");
        menu.AddMenuOption("AK47", (p, option) => SilahMenu_(p, "weapon_ak47"));
        menu.AddMenuOption("M4A1", (p, option) => SilahMenu_(p, "weapon_m4a1"));
        menu.AddMenuOption("AWP", (p, option) => SilahMenu_(p, "weapon_awp"));
        menu.Open(player);
    }

    private void SilahMenu_(CCSPlayerController player, string option)
    {
        if (!ZombieAktif || !player.is_t() || !player.is_valid_alive()) return;

        player.RemoveWeapons();
        player.GiveNamedItem(option);
        player.GiveNamedItem("weapon_deagle");
        player.GiveNamedItem("weapon_knife");
        player.GiveNamedItem("weapon_hegrenade");
        player.GiveNamedItem("weapon_incgrenade");
    }

    public void SetHP(CCSPlayerController? p, int health)
    {
        if (p.is_valid_alive())
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
    public static void RemoveWeaponsOnTheGround()
    {
        IEnumerable<CCSWeaponBaseGun> entities = Utilities.FindAllEntitiesByDesignerName<CCSWeaponBaseGun>("weapon_");

        foreach (CCSWeaponBaseGun entity in entities)
        {
            if (!entity.IsValid)
            {
                continue;
            }

            if (entity.State != CSWeaponState_t.WEAPON_NOT_CARRIED)
            {
                continue;
            }

            if (entity.DesignerName.StartsWith("weapon_") == false)
            {
                continue;
            }

            entity.Remove();
        }
    }

    private bool IsJBMap()
    {
        return NativeAPI.GetMapName().StartsWith("jb_");
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


public static class ZombieUtils
{
    public static Dictionary<CCSPlayerController, (float X, float Y, float Z)> PlayerDeathLocations { get; set; } = [];

    public static void CopyLastCoord(this CCSPlayerController player)
    {
        Vector? absOrigin = player.PlayerPawn.Value?.AbsOrigin;

        if (absOrigin == null)
        {
            return;
        }

        if (PlayerDeathLocations.ContainsKey(player))
        {
            PlayerDeathLocations[player] = (absOrigin.X, absOrigin.Y, absOrigin.Z);
        }
        else
        {
            PlayerDeathLocations.Add(player, (absOrigin.X, absOrigin.Y, absOrigin.Z));
        }
    }

    public static Vector GetLastCoord(this CCSPlayerController player)
    {
        (float X, float Y, float Z) = PlayerDeathLocations.First(p => p.Key == player).Value;

        return new Vector(X, Y, Z);
    }
}


public static class Lib
{

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