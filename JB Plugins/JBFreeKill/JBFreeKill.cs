using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Events;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using CounterStrikeSharp.API.Modules.Entities;
using System.Numerics;
using CounterStrikeSharp.API.Modules.Commands.Targeting;
namespace JBFreeKill;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}

public class JBFreeKill : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "Free Kill";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "JB | !fk komutu ile yanlislikla oldurulen oyunculari oldukleri yerde canlandir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";

    private readonly Dictionary<int, List<int>> ctKillLog = new(); // CT oyuncularının öldürdüğü T oyuncularını saklar
    internal static IStringLocalizer? Stringlocalizer;

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        Config = config;
        Stringlocalizer = Localizer;
    }

    public override void Load(bool hotReload)
    {
        // Komutları kaydet
        AddCommand("css_fk", "Öldürdüğünüz T oyuncularını gösterir.", (player, command) => ShowKillList(player));

        // Eventler
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventRoundStart>(OnRoundReset);
        RegisterEventHandler<EventRoundEnd>(OnRoundReset);
    }

    // Oyuncu öldüğünde çalışır
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (!Lib.IsJailbreakMap())
            return HookResult.Continue;

        var attacker = @event.Attacker;
        var victim = @event.Userid;

        if (!attacker.is_valid() || !victim.is_valid())
            return HookResult.Continue;

        if (attacker.is_ct() && victim.is_t())
        {
            FreeKillUtils.CopyLastCoord(victim);

            int attackerId = attacker.UserId ?? -1;
            int victimId = victim.UserId ?? -1;

            if (attackerId == -1 || victimId == -1)
                return HookResult.Continue;

            if (!ctKillLog.ContainsKey(attackerId))
                ctKillLog[attackerId] = new List<int>();

            ctKillLog[attackerId].Insert(0, victimId); // En son öldürüleni başa ekle
        }

        return HookResult.Continue;
    }

    // Oyuncu spawn olduğunda çalışır, tüm listelerden kaldırır
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (!Lib.IsJailbreakMap())
            return HookResult.Continue;

        var player = @event.Userid;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        int spawnedPlayerId = player.UserId ?? -1;

        foreach (var ctList in ctKillLog.Values)
            ctList.Remove(spawnedPlayerId);

        return HookResult.Continue;
    }

    // Round başında ve sonunda listeleri sıfırlar
    public HookResult OnRoundReset(GameEvent @event, GameEventInfo info)
    {
        if (!Lib.IsJailbreakMap())
            return HookResult.Continue;

        ctKillLog.Clear();
        return HookResult.Continue;
    }

    // !fk komutu çalıştırıldığında oyuncuya menü gösterir
    public void ShowKillList(CCSPlayerController? player)
    {
        if (!Lib.IsJailbreakMap())
            return;

        if (!player.is_valid_alive())
            return;

        if (!player.is_ct())
        {
            player?.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["only_ct"]);
            return;
        }

        int playerId = player.UserId ?? -1;
        if (playerId == -1 || !ctKillLog.ContainsKey(playerId) || ctKillLog[playerId].Count == 0)
        {
            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["no_kills"]);
            return;
        }

        var menu = new ChatMenu(Localizer["kill_list_title"]);
        foreach (var victimId in ctKillLog[playerId])
        {
            var victim = Utilities.GetPlayers().FirstOrDefault(p => p.UserId == victimId);
            if (victim != null)
            {
                menu.AddMenuOption($"{victim.PlayerName}", (ply, option) =>
                {
                    if(!ply.is_valid_alive() || !ply.is_ct())
                        return;

                    ctKillLog[playerId].Remove(victimId);
                    ply.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_removed", victim.PlayerName]);


                    Vector lastCoord = victim.GetLastCoord();
                    CCSPlayerPawn? targetPawn = victim.PlayerPawn.Value;
                    if (targetPawn == null)
                        return;

                    victim.Respawn();
                    targetPawn.Teleport(lastCoord, targetPawn.AbsRotation, targetPawn.AbsVelocity);
                });
            }
        }

        MenuManager.OpenChatMenu(player, menu);
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



public static class FreeKillUtils
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
    public static bool IsJailbreakMap()
    {
        string mapName = Server.MapName;
        return mapName.StartsWith("jb_", StringComparison.OrdinalIgnoreCase);
    }
    public static bool is_valid(this CCSPlayerController? player)
    {
        return player != null && player.IsValid && player.PlayerPawn.IsValid;
    }

    public static bool is_t(this CCSPlayerController? player)
    {
        return player != null && is_valid(player) && player.TeamNum == 2;
    }

    public static bool is_ct(this CCSPlayerController? player)
    {
        return player != null && is_valid(player) && player.TeamNum == 3;
    }

    public static bool is_valid_alive(this CCSPlayerController? player)
    {
        return player != null && player.is_valid() && player.PawnIsAlive && player.get_health() > 0;
    }

    public static CCSPlayerPawn? pawn(this CCSPlayerController? player)
    {
        if (player == null || !player.is_valid())
        {
            return null;
        }

        return player.PlayerPawn.Value;
    }

    public static int get_health(this CCSPlayerController? player)
    {
        var pawn = player.pawn();
        return pawn?.Health ?? 100;
    } 
}
