using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Commands;
namespace TeamBugFix;

public class TeamBugFix : BasePlugin
{
    public override string ModuleName { get; } = "Team Bug Fix";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "Team Bug Fix";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/ - Swolly";

    public enum JoinTeamReason
    {
        OneTeamChange = 1,
        TeamsFull = 2,
        TerroristTeamFull = 7,
        CTTeamFull = 8
    }
    public Dictionary<CCSPlayerController, int> SelectedTeam = [];

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnClientConnected>((slot) =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);

            if (player == null)
                return;

            SelectedTeam[player] = 0;
        });


        AddCommandListener("jointeam", Command_JoinTeam);
    }

    [GameEventHandler]
    public HookResult TeamJoinFailed(EventJointeamFailed @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if (player == null || !player.IsValid)
            return HookResult.Continue;

        if (!SelectedTeam.ContainsKey(player))
            SelectedTeam[player] = 0;

        player.ChangeTeam((CsTeam)SelectedTeam[player]);
        player.PlayerPawn.Value!.TeamNum = (byte)SelectedTeam[player];

        return HookResult.Handled;
    }

    private HookResult Command_JoinTeam(CCSPlayerController? player, CommandInfo info)
    {
        if (player != null && player.IsValid)
        {
            int startIndex = 0;
            if (info.ArgCount > 0 && info.ArgByIndex(0).ToLower() == "jointeam")
            {
                startIndex = 1;
            }

            if (info.ArgCount > startIndex)
            {
                string teamArg = info.ArgByIndex(startIndex);

                if (int.TryParse(teamArg, out int teamId))
                {
                    if (teamId >= (int)CsTeam.Spectator && teamId <= (int)CsTeam.CounterTerrorist)
                    {
                        SelectedTeam[player] = teamId;
                    }
                }
                else
                {
                    Console.WriteLine("Failed to parse team ID.");
                }
            }
        }

        return HookResult.Continue;
    }

    public static int GetTeamPlayerCount(CsTeam team)
    {
        return Utilities.GetPlayers().Count(p => p.Team == team);
    }



    [GameEventHandler]
    public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid!;

        AddTimer(0.1f, () =>
        {
            player.ChangeTeam(CsTeam.Spectator);
            player.Respawn();
            
            AddTimer(0.3f, () =>
            {
                if (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("jail_") || NativeAPI.GetMapName().Contains("ba_jail"))
                {
                    player.ChangeTeam(CsTeam.Terrorist);
                    player.PlayerPawn.Value!.TeamNum = 2;
                }
                else
                {
                    int tcount = GetTeamPlayerCount(CsTeam.Terrorist), ctcount = GetTeamPlayerCount(CsTeam.CounterTerrorist);

                    if (tcount > ctcount)
                    {
                        player.ChangeTeam(CsTeam.CounterTerrorist);
                        player.PlayerPawn.Value!.TeamNum = 3;
                    }
                    else
                    if (ctcount > tcount)
                    {
                        player.ChangeTeam(CsTeam.Terrorist);
                        player.PlayerPawn.Value!.TeamNum = 2;
                    }
                    else
                    {
                        Random rnd = new Random();
                        int random = rnd.Next(1, 2);

                        if (random == 1)
                        {
                            player.ChangeTeam(CsTeam.Terrorist);
                            player.PlayerPawn.Value!.TeamNum = 2;
                        }
                        else
                        {
                            player.ChangeTeam(CsTeam.CounterTerrorist);
                            player.PlayerPawn.Value!.TeamNum = 3;
                        }
                    }
                }

                player.CommitSuicide(true, true);
            }, TimerFlags.STOP_ON_MAPCHANGE);
        });
       
        return HookResult.Continue;
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