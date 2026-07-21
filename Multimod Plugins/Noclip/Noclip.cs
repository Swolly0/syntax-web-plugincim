using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;

using CSSTargetResult = CounterStrikeSharp.API.Modules.Commands.Targeting.TargetResult;
using CounterStrikeSharp.API.Modules.Memory;

namespace Noclip;

public class Noclip : BasePlugin
{
    public override string ModuleName { get; } = "Noclip";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!noclip";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";

    // LISANS
    public string EklentiTagi = "[www.plugincim.com]";
    public int lisans_bitis_yil = 2024; // Lisansin bitecegi yil
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
			AddCommand("css_noclip", "Noclip", (player, command) => cNoclip(player, command));
		}
	}

    public void cNoclip(CCSPlayerController? player, CommandInfo command)
    {
        if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            if (command.ArgCount <= 1)
            {
                if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
                    player.PrintToChat($" \x0b{EklentiTagi}\x01 Komut kullanımı: !noclip <hedef>");

                return;
            }
            else
            {
                var target = GetTarget(command);
                target?.Players.ForEach(p =>
                {
                    if (p.is_valid_alive())
                        if(p.PlayerPawn.Value.MoveType == MoveType_t.MOVETYPE_NOCLIP)
                            p!.PlayerPawn.Value!.Noclip(false);
                        else
                            p!.PlayerPawn.Value!.Noclip(true);
                });

                if (player != null)
                    Server.PrintToChatAll($" \x0b{EklentiTagi}\x04 {player.PlayerName} \x01isimli yetkili \x04!noclip {command.GetArg(1)} \x01komutunu uyguladı.");
                else
                    Server.PrintToChatAll($" \x0b{EklentiTagi}\x04 Console \x01isimli yetkili \x04!noclip {command.GetArg(1)} \x01komutunu uyguladı.");
            }
        }
        else
        {
            command.ReplyToCommand($"{EklentiTagi} Bu komutu yetkililer kullanabilir.");
            return;
        }
    }

    private CSSTargetResult? GetTarget(CommandInfo info, bool allowMultiple = true, bool noError = false)
    {
        var matches = info.GetArgTargetResult(1);

        if (!matches.Any())
        {
            if (!noError)
                info.ReplyToCommand($"{EklentiTagi} Komut kullanımı: !respawn <hedef>");
            return null;
        }

        if (!(matches.Count() > 1) || (info.GetArg(1).StartsWith('@') && allowMultiple))
            return matches;

        return null;
    }
}


public static class Lib
{
    static public void Noclip(this CBasePlayerPawn pawn, bool noclip)
    {
        if (noclip)
        {
            pawn.MoveType = MoveType_t.MOVETYPE_NOCLIP;

            Schema.SetSchemaValue(pawn.Handle, "CBaseEntity", "m_nActualMoveType", 8);
        }
        else
        {
            pawn.MoveType = MoveType_t.MOVETYPE_WALK;

            Schema.SetSchemaValue(pawn.Handle, "CBaseEntity", "m_nActualMoveType", 2);
        }

        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");
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