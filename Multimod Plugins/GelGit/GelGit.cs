using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;

namespace GelGit;

public class GelGit : BasePlugin
{
    public override string ModuleName { get; } = "Gel Git";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!gel !git komutlari.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";

    // LISANS
    public string EklentiTagi = "[BabunGang]";
    public int lisans_bitis_yil = 2025; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 20; // Lisansin bitecegi gun
    // LISANS

    public override void Load(bool hotReload)
    {
        var dateTime = new DateTime(lisans_bitis_yil, lisans_bitis_ay, lisans_bitis_gun, 0, 0, 0, DateTimeKind.Utc);
        var dateWithOffset = new DateTimeOffset(dateTime).ToUniversalTime();
        long timestamp = dateWithOffset.ToUnixTimeSeconds();

        if (timestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            AddCommand("css_git", "Oyuncuya isinlan.", (player, command) => Git(player, command));

            AddCommandListener("css_gel", (player, info) =>
            {
                if (player != null)
                {
                    if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.TeamNum == 3)
                    {
                        if (info.ArgCount <= 1)
                        {
                            player.PrintToChat($" \x0b{EklentiTagi}\x01 Komut kullanımı: !gel <hedef>");
                            return HookResult.Handled;
                        }

                        var target = info.GetArgTargetResult(1);

                        if (target.Players.Count <= 0)
                        {
                            player.PrintToChat($" \x0b{EklentiTagi}\x01 Geçersiz hedef.");
                            return HookResult.Handled;
                        }

                        var playerPawn = player.PlayerPawn.Value;
                        Vector? position = RayTrace.TraceRay(playerPawn.AbsOrigin, playerPawn.EyeAngles, Masks.laserMask, true);
                        var angle = playerPawn.EyeAngles!;
                        var velocity = playerPawn.AbsVelocity;

                        foreach (var targetPawn in target.Players.Select(p => p.PlayerPawn.Value))
                            targetPawn.Teleport(position, angle, velocity);

                        Server.PrintToChatAll($" \x0b{EklentiTagi}\x04 {player.PlayerName} \x01isimli yetkili \x04!gel {info.GetArg(1)} \x01komutunu uyguladı.");
                    }
                    else info.ReplyToCommand($"{EklentiTagi} Bu komutu yetkililer ve ct kullanabilir.");
                }
                else info.ReplyToCommand($"{EklentiTagi} Bu komutu oyuncular kullanabilir.");

                return HookResult.Handled;
            }, HookMode.Pre);


            AddCommandListener("css_gelt", (player, info) =>
            {
                if (player != null)
                {
                    if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.TeamNum == 3)
                    {
                        var playerPawn = player.PlayerPawn.Value;
                        Vector? position = RayTrace.TraceRay(playerPawn.AbsOrigin, playerPawn.EyeAngles, Masks.laserMask, true);
                        var angle = playerPawn.EyeAngles!;
                        var velocity = playerPawn.AbsVelocity;

                        foreach (var p in Utilities.GetPlayers())
                            if (p.is_valid_alive() && p.is_t())
                                p.PlayerPawn.Value.Teleport(position, angle, velocity);

                        Server.PrintToChatAll($" \x0b{EklentiTagi}\x04 {player.PlayerName} \x01isimli yetkili \x04!gelt \x01komutunu uyguladı.");
                    }
                    else info.ReplyToCommand($"{EklentiTagi} Bu komutu yetkililer ve ct kullanabilir.");
                }
                else info.ReplyToCommand($"{EklentiTagi} Bu komutu oyuncular kullanabilir.");

                return HookResult.Handled;
            }, HookMode.Pre);


            AddCommandListener("css_gelct", (player, info) =>
            {
                if (player != null)
                {
                    if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.TeamNum == 3)
                    {
                        var playerPawn = player.PlayerPawn.Value;
                        Vector? position = RayTrace.TraceRay(playerPawn.AbsOrigin, playerPawn.EyeAngles, Masks.laserMask, true);
                        var angle = playerPawn.EyeAngles!;
                        var velocity = playerPawn.AbsVelocity;

                        foreach (var p in Utilities.GetPlayers())
                            if (p.is_valid_alive() && p.is_ct())
                                p.PlayerPawn.Value.Teleport(position, angle, velocity);

                        Server.PrintToChatAll($" \x0b{EklentiTagi}\x04 {player.PlayerName} \x01isimli yetkili \x04!gelct \x01komutunu uyguladı.");
                    }
                    else info.ReplyToCommand($"{EklentiTagi} Bu komutu yetkililer ve ct kullanabilir.");
                }
                else info.ReplyToCommand($"{EklentiTagi} Bu komutu oyuncular kullanabilir.");

                return HookResult.Handled;
            }, HookMode.Pre);
        }
    }

    [RequiresPermissions("@css/generic")]
    public void Git(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null)
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.TeamNum == 3)
            {
                if (command.ArgCount >= 1)
                {
                    var targetArg = command.GetArgTargetResult(1);

                    if (targetArg.Players.Count <= 0)
                    {
                        player.PrintToChat($" \x0b{EklentiTagi}\x01 Geçersiz hedef.");
                        return;
                    }

                    var target = targetArg.Players.First();

                    var targetPawn = target.PlayerPawn.Value;
                    var clientPawn = player.PlayerPawn.Value;

                    var position = targetPawn.AbsOrigin!;
                    var angle = targetPawn.AbsRotation!;
                    var velocity = targetPawn.AbsVelocity;

                    clientPawn.Teleport(position, angle, velocity);
                    player.PrintToChat($" \x0b{EklentiTagi}\x04 {target.PlayerName} \x01isimli oyuncuya ışınlandın.");
                }
                else player.PrintToChat($" \x0b{EklentiTagi}\x01 Komut kullanımı: !git <nick>");
            }
            else command.ReplyToCommand($"{EklentiTagi} Bu komutu yetkililer ve ct kullanabilir.");
        }
        else command.ReplyToCommand($"{EklentiTagi} Bu komutu oyuncular kullanabilir.");
    }

    public void Gel(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null)
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.TeamNum == 3)
            {
                if (command.ArgCount <= 1)
                {
                    player.PrintToChat($" \x0b{EklentiTagi}\x01 Komut kullanımı: !gel <hedef>");
                    return;
                }
                
                
                var target = command.GetArgTargetResult(1);

                if (target.Players.Count <= 0)
                {
                    player.PrintToChat($" \x0b{EklentiTagi}\x01 Geçersiz hedef.");
                    return;
                }

                var playerPawn = player.PlayerPawn.Value;
                Vector? position = RayTrace.TraceRay(playerPawn.AbsOrigin, playerPawn.EyeAngles, Masks.laserMask, true);
                var angle = playerPawn.EyeAngles!;
                var velocity = playerPawn.AbsVelocity;

                foreach (var targetPawn in target.Players.Select(p => p.PlayerPawn.Value))
                    targetPawn.Teleport(position, angle, velocity);

                Server.PrintToChatAll($" \x0b{EklentiTagi}\x04 {player.PlayerName} \x01isimli yetkili \x04!gel {command.GetArg(1)} \x01komutunu uyguladı.");
            }
            else command.ReplyToCommand($"{EklentiTagi} Bu komutu yetkililer ve ct kullanabilir.");
        }
        else command.ReplyToCommand($"{EklentiTagi} Bu komutu oyuncular kullanabilir.");
    }

}





public static class Lib{

	public static void Freeze(this CBasePlayerPawn pawn)
	{
		pawn.MoveType = MoveType_t.MOVETYPE_OBSOLETE;
	}

    static public bool is_valid(this CCSPlayerController? player)
    {
        return player != null && player.IsValid &&  player.PlayerPawn.IsValid;
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
        if(player == null || !player.is_valid())
        {
            return null;
        }

        CCSPlayerPawn? pawn = player.PlayerPawn.Value;

        return pawn;
    }	
	
    static public int get_health(this CCSPlayerController? player)
    {
        CCSPlayerPawn? pawn = player.pawn();

        if(pawn == null)
        {
            return 100;
        }

        return pawn.Health;
    }	
}