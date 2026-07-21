using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using MenuManager;
using CSSTargetResult = CounterStrikeSharp.API.Modules.Commands.Targeting.TargetResult;

namespace Noclip;

public class Noclip : BasePlugin
{
    public override string ModuleName { get; } = "Uyar";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!tw - !uyar";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";

    // LISANS
    public string EklentiTagi = "[VAGA]";
    public int lisans_bitis_yil = 2025; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 20; // Lisansin bitecegi gun
                                      // LISANS

    private IMenuApi? _api;
    private readonly PluginCapability<IMenuApi?> _pluginCapability = new("menu:nfcore");
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = _pluginCapability.Get();
        if (_api == null) Console.WriteLine("MenuManager Core not found...");
    }

    public override void Load(bool hotReload)
    {
        var dateTime = new DateTime(lisans_bitis_yil, lisans_bitis_ay, lisans_bitis_gun, 0, 0, 0, DateTimeKind.Utc);
        var dateWithOffset = new DateTimeOffset(dateTime).ToUniversalTime();
        long timestamp = dateWithOffset.ToUnixTimeSeconds();

        if (timestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            AddCommand("css_uyar", "Uyar", (player, command) => cUyar(player, command));
            AddCommand("css_tw", "TW", (player, command) => cTW(player, command));
        }
    }

    public void cUyar(CCSPlayerController? player, CommandInfo command)
    {
        if (!player.is_valid() || player == null) return;
        if (AdminManager.PlayerHasPermissions(player, "@css/ban"))
        {
            if (command.ArgCount <= 1)
            {
                if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
                    player.PrintToChat($" \x0b{EklentiTagi}\x01 Komut kullanımı: !uyar <hedef>");

                return;
            }
            else
            {
                var target = GetTarget(command);
                target?.Players.ForEach(p =>
                {
                    if (!p.is_valid())
                        return;

                    p.PrintToChat($" \x0b[UYARI]\x0f Admin seni izleyicilere'e taşıdı.");
                    p.PrintToChat($" \x0b[UYARI]\x0f Fotoğrafın veya ismin uygun değil.");
                    p.PrintToChat($" \x0b[UYARI]\x0f Sunucu kurallarına aykırı.");
                    p.PrintToChat($" \x0b[UYARI]\x0f Uymazsan yasaklanabilirsin.");
                    p.PrintToChat($" \x0b[UYARI]\x0f Değiştir ve yeniden bağlan.");

                    var menu = _api!.GetMenu("UYARI!");
                    menu.AddMenuOption("Admin seni izleyicilere'e taşıdı.", (player, option) => Menu_(p, ""));
                    menu.AddMenuOption("Fotoğrafın veya ismin uygun değil.", (player, option) => Menu_(p, ""));
                    menu.AddMenuOption("Sunucu kurallarına aykırı.", (player, option) => Menu_(p, ""));
                    menu.AddMenuOption("Uymazsan yasaklanabilirsin.", (player, option) => Menu_(p, ""));
                    menu.AddMenuOption("Değiştir ve yeniden bağlan.", (player, option) => Menu_(p, ""));
                    menu.Open(player);



                    p.CommitSuicide(false, true);
                    p.ChangeTeam(CsTeam.Spectator);

                    Server.PrintToChatAll($" \x0b{EklentiTagi}\x04 {p.PlayerName} \x01isimli oyuncu \x07{player.PlayerName} tarafından \x0fuyarıldı.");
                });
            }
        }
        else
        {
            command.ReplyToCommand($"{EklentiTagi} Bu komutu yetkililer kullanabilir.");
            return;
        }
    }

    public void cTW(CCSPlayerController? player, CommandInfo command)
    {
        if (!player.is_valid() || player == null) return;
        if (AdminManager.PlayerHasPermissions(player, "@css/ban"))
        {
            if (command.ArgCount <= 1)
            {
                if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
                    player.PrintToChat($" \x0b{EklentiTagi}\x01 Komut kullanımı: !tw <hedef>");

                return;
            }
            else
            {
                var target = GetTarget(command);
                target?.Players.ForEach(p =>
                {
                    if (!p.is_valid())
                        return;

                    p.PrintToChat($" \x0b[UYARI]\x0f Admin seni izleyicilere'e taşıdı.");
                    p.PrintToChat($" \x0b[UYARI]\x0f Oyunu kapatma, yasaklanırsın.");
                    p.PrintToChat($" \x0b[UYARI]\x0f !dc yaz, bağlantıı ile dc'ye gel.");
                    p.PrintToChat($" \x0b[UYARI]\x0f #twsesodası'na bağlan.");
                    p.PrintToChat($" \x0b[UYARI]\x0f Süren 5 dakika.");

                    var menu = _api!.GetMenu("UYARI!");
                    menu.AddMenuOption("Admin seni izleyicilere'e taşıdı.", (player, option) => Menu_(p, ""));
                    menu.AddMenuOption("Oyunu kapatma, yasaklanırsın.", (player, option) => Menu_(p, ""));
                    menu.AddMenuOption("!dc yaz, bağlantıı ile dc'ye gel.", (player, option) => Menu_(p, ""));
                    menu.AddMenuOption("#twsesodası'na bağlan.", (player, option) => Menu_(p, ""));
                    menu.AddMenuOption("Süren 5 dakika.", (player, option) => Menu_(p, ""));
                    menu.Open(player);

                    p.CommitSuicide(false, true);
                    p.ChangeTeam(CsTeam.Spectator);

                    Server.PrintToChatAll($" \x0b{EklentiTagi}\x04 {p.PlayerName} \x01isimli oyuncu \x07{player.PlayerName} tarafından \x0fuyarıldı.");
                });
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

    public void Menu_(CCSPlayerController player, string option)
    {

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