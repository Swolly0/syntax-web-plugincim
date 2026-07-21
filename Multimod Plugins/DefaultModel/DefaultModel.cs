using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace DefaultModel;

public class DefaultModel : BasePlugin
{
    public override string ModuleName { get; } = "Default Model";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "Oyuncularin baslangic modellerini degistirir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn, HookMode.Pre);
    }

    HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if (player != null && player.IsValid)
        {
            AddTimer(0.5f, () =>
            {
                if (player.is_t())
                    player.PlayerPawn.Value!.SetModel(@"characters\models\tm_phoenix\tm_phoenix.vmdl");
                else
                if (player.is_ct())
                    player.PlayerPawn.Value!.SetModel(@"characters\models\ctm_fbi\ctm_fbi_varianta.vmdl");

            }, TimerFlags.STOP_ON_MAPCHANGE);
        }

        return HookResult.Continue;
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