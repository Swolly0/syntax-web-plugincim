using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using System.Numerics;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Localization;
using System.Runtime.InteropServices;

namespace AfkChecker;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("FirstCheckTime")]
    public float FirstCheckTime { get; set; } = 30.0f;

    [JsonPropertyName("LastCheckTime")]
    public float LastCheckTime { get; set; } = 10.0f;

    [JsonPropertyName("MoveSpec")]
    public int MoveSpec { get; set; } = 3;

    [JsonPropertyName("KickFromFullServer")]
    public float KickFromFullServer { get; set; } = 0.95f;

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}

public class AfkChecker : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "Afk Checker";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "Afk olan oyuncular uzerinde islem yapar";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[Afk Checker] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
        }

        Config = config;
        Stringlocalizer = Localizer;
    }

    private Dictionary<CCSPlayerController, Timer> playerTimers = new();
    private Dictionary<CCSPlayerController, int> afkCounts = new();

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.SteamID == 0 || player.TeamNum == 0)
            return HookResult.Continue;

        StopAFKTimer(player);
        AfkUtils.CopyLastCoord(player);
        StartAFKTimer(player);

        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid)
            StopAFKTimer(player);

        return HookResult.Continue;
    }

    private void StartAFKTimer(CCSPlayerController player)
    {
        playerTimers[player] = AddTimer(Config.FirstCheckTime, () => CheckAFK(player), TimerFlags.REPEAT|TimerFlags.STOP_ON_MAPCHANGE);
    }





    private void CheckAFK(CCSPlayerController player)
    {
        if (player == null || !player.is_valid_alive() || player.TeamNum < 2)
        {
            StopAFKTimer(player);
            return;
        }

        QAngle currentPos = player.PlayerPawn?.Value?.EyeAngles;
        Vector lastPos = AfkUtils.GetLastCoord(player);

        Vector3 currentPos3 = new Vector3(currentPos.X, currentPos.Y, currentPos.Z);
        Vector3 lastPos3 = new Vector3(lastPos.X, lastPos.Y, lastPos.Z);


        if (Vector3.Distance(currentPos3, lastPos3) > 0.01f)
        {
            AfkUtils.CopyLastCoord(player);
            return;
        }

        player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["afk_warning", Config.LastCheckTime]);
        AddTimer(Config.LastCheckTime, () => HandleAFK(player, lastPos3));
    }

    private void HandleAFK(CCSPlayerController player, Vector3 oldAngles)
    {
        if (player == null || !player.is_valid_alive() || player.TeamNum < 2)
        {
            StopAFKTimer(player);
            return;
        }

        QAngle currentAngles = player.PlayerPawn?.Value?.EyeAngles;

        if (Math.Abs(currentAngles.X - oldAngles.X) > 1.0f ||
           Math.Abs(currentAngles.Y - oldAngles.Y) > 1.0f ||
           Math.Abs(currentAngles.Z - oldAngles.Z) > 1.0f)
        {
            AfkUtils.CopyLastCoord(player);
            return;
        }

        int maxPlayers = Server.MaxPlayers;
        int currentPlayers = Utilities.GetPlayers().Count;
        float serverFullThreshold = maxPlayers * Config.KickFromFullServer;

        if (currentPlayers >= serverFullThreshold)
            player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
        else
        {
            afkCounts[player] = afkCounts.ContainsKey(player) ? afkCounts[player] + 1 : 1;
            player.CommitSuicide(false, true);

            if (afkCounts[player] >= Config.MoveSpec)
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["afk_move_spec"]);
                player.ChangeTeam(CsTeam.Spectator);
                afkCounts[player] = 0;
                StopAFKTimer(player);
            }
            else
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["afk_slay"]);
        }
    }




    private void OnClientDisconnect(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null) return;

        StopAFKTimer(player);
        afkCounts.Remove(player);
    }

    private void StopAFKTimer(CCSPlayerController player)
    {
        if (playerTimers.ContainsKey(player))
        {
            playerTimers[player].Kill();
            playerTimers.Remove(player);
        }
        AfkUtils.PlayerLastLoc.Remove(player);
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





public static class AfkUtils
{
    public static Dictionary<CCSPlayerController, (float X, float Y, float Z)> PlayerLastLoc { get; set; } = [];

    public static void CopyLastCoord(this CCSPlayerController player)
    {
        QAngle? EyeAngles = player.PlayerPawn.Value?.EyeAngles;
        if (EyeAngles == null)
            return;

        PlayerLastLoc[player] = (EyeAngles.X, EyeAngles.Y, EyeAngles.Z);
    }

    public static Vector GetLastCoord(this CCSPlayerController player)
    {
        (float X, float Y, float Z) = PlayerLastLoc.First(p => p.Key == player).Value;

        return new Vector(X, Y, Z);
    }
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