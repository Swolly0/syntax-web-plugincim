using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;

namespace SpawnKill;

public class SpawnKill : BasePlugin
{
    public override string ModuleName { get; } = "Spawn Kill";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "JB | CT dogdugunda belirli bir sure olumsuz olur.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";

    private readonly Dictionary<CCSPlayerController, bool> God = new();

    ConVar? mp_freezetime = null!;

    public override void Load(bool hotReload)
    {
		RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn, HookMode.Pre);
		RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt, HookMode.Pre);
		RegisterEventHandler<EventPlayerConnect>(OnPlayerConnect);

        mp_freezetime = ConVar.Find("mp_freezetime");
    }

    HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        mp_freezetime.SetValue(0);

        CCSPlayerController? player = @event.Userid;
        God[player] = false;

        if (NativeAPI.GetMapName().Contains("aim_"))
        {
            God[player] = true;

            AddTimer(2.0f, () =>
            {
                God[player] = false;
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }

        return HookResult.Continue;
    }

     private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("aim_"))
        {
            CCSPlayerController? player = @event.Userid;
            if (player.is_valid() && God[player])
            {
                if (player.Connected != PlayerConnectedState.PlayerConnected)
                    return HookResult.Continue;

                if (!player.PlayerPawn.IsValid)
                    return HookResult.Continue;

				player.PlayerPawn.Value.Health += @event.DmgHealth;
				player.PlayerPawn.Value.ArmorValue += @event.DmgArmor;

				player.Health += @event.DmgHealth;

				Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_iHealth");

				@event.Userid.PlayerPawn.Value.VelocityModifier = 1;
            }
        }

        return HookResult.Continue;
    }

    HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        God[player] = false;

        return HookResult.Continue;
    }
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