using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json.Serialization;
using CSQAngle = CounterStrikeSharp.API.Modules.Utils.QAngle;
using CSVector = CounterStrikeSharp.API.Modules.Utils.Vector;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace Arena1v1;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")] public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("MinPlayer")] public int MinPlayer { get; set; } = 3;
    [JsonPropertyName("StartHeal")] public int StartHeal { get; set; } = 100;

    [JsonPropertyName("StartWeapons")]
    public string[] StartWeapons { get; set; } = new string[]
    {
        "weapon_ak47", "weapon_m4a4", "weapon_deagle", "weapon_awp", "weapon_revolver", "weapon_usp", "weapon_p90"
    };

    [JsonPropertyName("StartPosT")] public string StartPosT { get; set; } = "-1291.077515, 432.044830, -234.968750";
    [JsonPropertyName("StartAngT")] public string StartAngT { get; set; } = "2.913744, -88.497375, 0";
  
    [JsonPropertyName("StartPosCT")] public string StartPosCT { get; set; } = "-1217.399536, -419.593140, -229.299805";
    [JsonPropertyName("StartAngCT")] public string StartAngCT { get; set; } = "4.607902, 88.140671, 0";

    [JsonPropertyName("ConfigVersion")] public int Version { get; set; } = 1;
}

public static class ConfigParser
{
    private static float ParseFloat(string raw)
    {
        // trims spaces and optional trailing 'f' that often appears in copied coords
        var s = raw.Trim();
        if (s.EndsWith("f", StringComparison.OrdinalIgnoreCase)) s = s[..^1];
        return float.Parse(s, CultureInfo.InvariantCulture);
    }

    public static CSVector ParseVector(string input)
    {
        var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3) throw new FormatException($"Vector must have 3 components: '{input}'");
        return new CSVector(ParseFloat(parts[0]), ParseFloat(parts[1]), ParseFloat(parts[2]));
    }

    public static CSQAngle ParseQAngle(string input)
    {
        var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3) throw new FormatException($"QAngle must have 3 components: '{input}'");
        return new CSQAngle(ParseFloat(parts[0]), ParseFloat(parts[1]), ParseFloat(parts[2]));
    }
}

public class Arena1v1 : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "1v1 arena";
    public override string ModuleVersion => "1.0.1";
    public override string ModuleDescription => "1v1 kaldiginda oyuncular arenaya isinlanir (silah strip + gecikmeli give).";
    public override string ModuleAuthor => "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    private Timer? timer_ex;
    private bool isActive;
    private string selectedWeapon = "weapon_deagle";
    private ConVar? _mpInfiniteAmmo;

    // LISANS
    public int lisans_bitis_yil = 2025; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12;    // Lisansin bitecegi ay
    public int lisans_bitis_gun = 30;   // Lisansin bitecegi gun
    // LISANS

    public required Config Config { get; set; }

    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
            Console.WriteLine($"[1v1-arena] Old config file. Using:{config.Version} - Required:{ModuleConfigVersion}");

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
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Post); // Post: sayim dogru olsun
            RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt, HookMode.Pre);
            RegisterEventHandler<EventWeaponZoom>(WeaponZoom);

            _mpInfiniteAmmo = ConVar.Find("sv_infinite_ammo");
        }
    }

    // === Events ===
    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (!IsSupportedMap()) return HookResult.Continue;

        isActive = false;
        timer_ex?.Kill();
        timer_ex = AddTimer(1.0f, TickLogic, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (IsSupportedMap())
        {
            _mpInfiniteAmmo?.SetValue(0);
            StopTimer();
        }
        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (!isActive) return HookResult.Continue;

        var (aliveT, aliveCT) = GetPlayerCounts();
        if (aliveT + aliveCT == 1)
        {
            var winner = Utilities.GetPlayers().FirstOrDefault(p => p.is_valid_alive());
            if (winner != null)
            {
                if(winner.is_valid_alive()) winner.RemoveWeapons();
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["end_1v1", winner.PlayerName]);
            }

            foreach (var entity in Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("weapon"))
            {
                if (entity.Entity == null) continue;
                if (!entity.DesignerName.StartsWith("weapon_")) continue;

                entity.Remove();
            }

            isActive = false; // arena bitti
        }
        return HookResult.Continue;
    }

    // === Timer Tick ===
    private void TickLogic()
    {
        if (isActive) return; // zaten kurulu

        var (aliveT, aliveCT) = GetPlayerCounts();
        var (t, ct) = GetPlayerCounts(false);
        var total = t + ct;
        if (total >= Config.MinPlayer && aliveT == 1 && aliveCT == 1)
        {
            AddTimer(1.0f, () =>
            {
                (aliveT, aliveCT) = GetPlayerCounts();
                if (aliveT == 1 && aliveCT == 1)
                    StartArena();
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }
    }

    // === Core ===
    private void StartArena()
    {
        var tPlayer = Utilities.GetPlayers().FirstOrDefault(p => p.is_valid_alive() && p.is_t());
        var ctPlayer = Utilities.GetPlayers().FirstOrDefault(p => p.is_valid_alive() && p.is_ct());
        if (tPlayer == null || ctPlayer == null) return;

        isActive = true;

        // Config’den rastgele silah seç
        if (Config.StartWeapons.Length == 0)
            Config.StartWeapons = new string[] { "weapon_deagle" }; // fallback

        var rnd = new Random();
        selectedWeapon = Config.StartWeapons[rnd.Next(Config.StartWeapons.Length)];

        PrepareFighter(tPlayer, ConfigParser.ParseVector(Config.StartPosT), ConfigParser.ParseQAngle(Config.StartAngT), selectedWeapon);
        PrepareFighter(ctPlayer, ConfigParser.ParseVector(Config.StartPosCT), ConfigParser.ParseQAngle(Config.StartAngCT), selectedWeapon);

        if (selectedWeapon.Contains("deagle") || selectedWeapon.Contains("revolver")) _mpInfiniteAmmo?.SetValue(1);

        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["start_1v1", selectedWeapon.Replace("weapon_", "")]);
        Server.ExecuteCommand("css_resetcd");
        StopTimer();
    }

    private void PrepareFighter(CCSPlayerController player, CSVector pos, CSQAngle ang, string weapon)
    {
        AddTimer(0.5f, () =>
        {
            var (aliveT, aliveCT) = GetPlayerCounts();
            if (aliveT != 1 || aliveCT != 1) return;
            if (!player.is_valid_alive()) return;


            if (Config.StartHeal >= 1)
                player.Health(Config.StartHeal);

            player.RemoveWeapons();

            player.GiveNamedItem("weapon_knife");
            player.GiveNamedItem(weapon);

            var pawn = player.pawn();
            if (pawn != null && pawn.IsValid)
            {
                var vel = new CSVector(0f, 0f, 0f);
                pawn.Teleport(pos, ang, vel);
            }

        }, TimerFlags.STOP_ON_MAPCHANGE);
    }

    // === Helpers ===
    private (int T, int CT) GetPlayerCounts(bool Alive = true)
    {
        int t = 0, ct = 0;
        foreach (var p in Utilities.GetPlayers())
        {
            if (Alive && !p.is_valid_alive()) continue;
            if (p.is_t()) t++;
            else if (p.is_ct()) ct++;
        }
        return (t, ct);
    }

    private void StopTimer()
    {
        timer_ex?.Kill();
        timer_ex = null;
    }

    private static bool IsSupportedMap()
    {
        string map = NativeAPI.GetMapName();
        return map.Contains("awp_") || map.Contains("de_") || map.Contains("cs_");
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (!IsSupportedMap()) return HookResult.Continue;

        if (isActive)
        {
            CCSPlayerController player = @event.Userid;
            if (player.is_valid_alive())
            {
                if (@event.Weapon.Contains("knife") || @event.Weapon.Contains("bayonet") || ((@event.Weapon.Contains("deagle") || @event.Weapon.Contains("revolver")) && @event.Hitgroup != 1))
                {
                    player.PlayerPawn.Value.Health += @event.DmgHealth;
                    player.PlayerPawn.Value.ArmorValue += @event.DmgArmor;

                    player.Health += @event.DmgHealth;

                    Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_iHealth");

                    @event.Userid.PlayerPawn.Value.VelocityModifier = 1;
                }
            }
        }

        return HookResult.Continue;
    }
    HookResult WeaponZoom(EventWeaponZoom @event, GameEventInfo info)
    {
        if (!isActive || selectedWeapon != "weapon_awp") return HookResult.Continue;
        var player = @event.Userid;
        var currentWeapon = player.PlayerPawn.Value?.WeaponServices!.ActiveWeapon.Value?.DesignerName;
        player.PlayerPawn.Value?.WeaponServices!.ActiveWeapon.Value?.Remove();
        if (currentWeapon != null) player.GiveNamedItem(currentWeapon);

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
            message = "\u200e" + (remove ? message.Replace(colorPatterns[i], "") : message.Replace(colorPatterns[i], colorReplacements[i]));
        return message;
    }
}

public static class Lib
{
    public static void Health(this CCSPlayerController player, int health)
    {
        if (player.PlayerPawn == null || player.PlayerPawn.Value == null) return;

        player.Health = health;
        player.PlayerPawn.Value.Health = health;

        if (health > 100)
        {
            player.MaxHealth = health;
            player.PlayerPawn.Value.MaxHealth = health;
        }
        Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_iHealth");
    }

    public static void Freeze(this CBasePlayerPawn pawn) => pawn.MoveType = MoveType_t.MOVETYPE_OBSOLETE;

    public static bool is_valid(this CCSPlayerController? player)
        => player != null && player.IsValid && player.PlayerPawn.IsValid;

    public static bool is_t(this CCSPlayerController? player)
        => player != null && is_valid(player) && player.TeamNum == 2;

    public static bool is_ct(this CCSPlayerController? player)
        => player != null && is_valid(player) && player.TeamNum == 3;

    public static bool is_valid_alive(this CCSPlayerController? player)
        => player != null && player.is_valid() && player.PawnIsAlive && player.get_health() > 0;

    public static CCSPlayerPawn? pawn(this CCSPlayerController? player)
        => (player == null || !player.is_valid()) ? null : player.PlayerPawn.Value;

    public static int get_health(this CCSPlayerController? player)
    {
        var pawn = player.pawn();
        return pawn?.Health ?? 100;
    }
}
