using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands.Targeting;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using System.Runtime.InteropServices;

using CSSTargetResult = CounterStrikeSharp.API.Modules.Commands.Targeting.TargetResult;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;


namespace RespawnFix;

public class RespawnFix : BasePlugin
{
    public override string ModuleName { get; } = "RespawnFix";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!respawn komutu fix.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";

    private MemoryFunctionVoid<CBasePlayerController, CCSPlayerPawn, bool, bool> CBasePlayerController_SetPawnFunc;
    private readonly Dictionary<CCSPlayerController, float> DedPosX = new();
    private readonly Dictionary<CCSPlayerController, float> DedPosY = new();
    private readonly Dictionary<CCSPlayerController, float> DedPosZ = new();

    public override void Load(bool hotReload)
    {
        CBasePlayerController_SetPawnFunc =
            new MemoryFunctionVoid<CBasePlayerController, CCSPlayerPawn, bool, bool>(GetSignature());

        AddCommand("css_respawn", "Oyuncu(lari) canlandir.", (player, command) => Respawn(player, command));
        AddCommand("css_rev", "Oyuncu(lari) canlandir.", (player, command) => Respawn(player, command));
        AddCommand("css_hrespawn", "Oyuncu(lari) canlandir.", (player, command) => HRespawn(player, command));
        AddCommand("css_hrev", "Oyuncu(lari) canlandir.", (player, command) => HRespawn(player, command));
        AddCommand("css_1up", "Oyuncu(lari) canlandir.", (player, command) => HRespawn(player, command));

        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn, HookMode.Pre);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Pre);
    }
    public void Respawn(CCSPlayerController? player, CommandInfo command)
    {
        if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            if (command.ArgCount <= 1)
            {
                if(player != null)
                    player.PrintToChat(" \x0b[www.plugincim.com]\x01 Komut kullanımı: !respawn <hedef>");

                return;
            }
            else
            {
                var target = GetTarget(command);
                target?.Players.ForEach(p =>
                {
                    if (!p.is_valid_alive())
                    {
                        var targetPawn = p.PlayerPawn.Value;
                        if (targetPawn == null) return;

                        CBasePlayerController_SetPawnFunc.Invoke(p, targetPawn, true, false);
                        VirtualFunction.CreateVoid<CCSPlayerController>(p.Handle,
                            GameData.GetOffset("CCSPlayerController_Respawn"))(p);
                    }
                });

                if(player != null)
                    Server.PrintToChatAll($" \x0b[www.plugincim.com]\x04 {player.PlayerName} \x01isimli yetkili \x04!respawn {command.GetArg(1)} \x01komutunu uyguladı.");
                else
                    Server.PrintToChatAll($" \x0b[www.plugincim.com]\x04 Console \x01isimli yetkili \x04!respawn {command.GetArg(1)} \x01komutunu uyguladı.");
            }
        }
        else
        {
            command.ReplyToCommand("[www.plugincim.com] Bu komutu yetkililer kullanabilir.");
            return;
        }
    }

    HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if (player.is_valid())
        {
            DedPosX[player] = 0.0f;
            DedPosY[player] = 0.0f;
            DedPosZ[player] = 0.0f;
        }

        return HookResult.Continue;
    }

    HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
        {
            var playerPawn = player.PlayerPawn.Value;
            if (playerPawn == null || !player.IsValid) return HookResult.Continue;

            var position = playerPawn.AbsOrigin;

            if (position != null)
            {
                DedPosX[player] = position.X;
                DedPosY[player] = position.Y;
                DedPosZ[player] = position.Z;
            }
        }

        return HookResult.Continue;
    }

    public void HRespawn(CCSPlayerController? player, CommandInfo command)
    {
        if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            if (command.ArgCount <= 1)
            {
                if (player != null)
                    player.PrintToChat(" \x0b[www.plugincim.com]\x01 Komut kullanımı: !hrev <hedef>");

                return;
            }
            else
            {
                var target = GetTarget(command);
                target?.Players.ForEach(p =>
                {
                    if (!p.is_valid_alive() && DedPosX[p] != 0.0f)
                    {
                        var playerPawn = p.PlayerPawn.Value;
                        if (playerPawn == null || !p.IsValid) return;

                        CBasePlayerController_SetPawnFunc.Invoke(p, playerPawn, true, false);
                        VirtualFunction.CreateVoid<CCSPlayerController>(p.Handle,
                            GameData.GetOffset("CCSPlayerController_Respawn"))(p);

                        var position = playerPawn.AbsOrigin;
                        var angle = playerPawn.EyeAngles!;
                        var velocity = playerPawn.AbsVelocity;

                        if (position == null) return;
                        position.X = DedPosX[p];
                        position.Y = DedPosY[p];
                        position.Z = DedPosZ[p];

                        p.Teleport(position, angle, velocity);
                    }
                });

                if (player != null)
                    Server.PrintToChatAll($" \x0b[www.plugincim.com]\x04 {player.PlayerName} \x01isimli yetkili \x04!hrev {command.GetArg(1)} \x01komutunu uyguladı.");
                else
                    Server.PrintToChatAll($" \x0b[www.plugincim.com]\x04 Console \x01isimli yetkili \x04!hrev {command.GetArg(1)} \x01komutunu uyguladı.");
            }
        }
        else
        {
            command.ReplyToCommand("[www.plugincim.com] Bu komutu yetkililer kullanabilir.");
            return;
        }
    }

    private CSSTargetResult? GetTarget(CommandInfo info, bool allowMultiple = true, bool noError = false)
    {
        var matches = info.GetArgTargetResult(1);

        if (!matches.Any())
        {
            if (!noError)
                info.ReplyToCommand("[www.plugincim.com] Komut kullanımı: !respawn <hedef>");
            return null;
        }

        if (!(matches.Count() > 1) || (info.GetArg(1).StartsWith('@') && allowMultiple))
            return matches;

        return null;
    }


    private static string GetSignature()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"\x44\x88\x4C\x24\x2A\x55\x57"
            : @"\x55\x48\x89\xE5\x41\x57\x41\x56\x41\x55\x41\x54\x49\x89\xFC\x53\x48\x89\xF3\x48\x81\xEC\xC8\x00\x00\x00";
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