using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using CSSTargetResult = CounterStrikeSharp.API.Modules.Commands.Targeting.TargetResult;
using System.Drawing;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace Hide;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}


public class Hide : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "Hide";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!hide komutu ile oyunculari gizlemenize yardimci olur.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    // LISANS
    public int lisans_bitis_yil = 2024; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 6; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 20; // Lisansin bitecegi gun
    // LISANS

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[HIDE] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
        }

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
            AddCommand("css_hide", "Oyunculari gizle.", (player, command) => cmdHide(player, command));
        }
    }

    public void cmdHide(CCSPlayerController? player, CommandInfo command)
    {
        if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            if (command.ArgCount <= 2)
            {
                if (player != null)
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage"]);

                return;
            }
            else
            {
                var target = GetTarget(command);
                target?.Players.ForEach(p =>
                {
                    if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV && p.is_valid_alive())
                    {
                        var targetPawn = p.PlayerPawn.Value;
                        if (targetPawn == null || !p.IsValid || !targetPawn.IsValid) return;

                        if (Convert.ToInt32(command.GetArg(2)) == 1)
                        {
                            Color render = targetPawn.Render;
                            render = Color.FromArgb(0, render);
                            targetPawn.Render = render;

                            foreach (var weapon in targetPawn.WeaponServices!.MyWeapons)
                            {
                                if (weapon is { IsValid: true, Value.IsValid: true })
                                {
                                    CCSWeaponBaseVData? weaponData = weapon.Value.As<CCSWeaponBase>().VData;

                                    if (weaponData?.WeaponType == CSWeaponType.WEAPONTYPE_KNIFE)
                                        weapon.Value.Remove();
                                }
                            }


                            AddTimer(0.2f, () =>
                            {
                                p.GiveNamedItem("weapon_knife");
                                p.ExecuteClientCommand("slot3");
                            });

                        }
                        else
                        {
                            Color render = targetPawn.Render;
                            render = Color.FromArgb(255, render);
                            targetPawn.Render = render;

                            foreach (var weapon in targetPawn.WeaponServices!.MyWeapons)
                            {
                                if (weapon is { IsValid: true, Value.IsValid: true })
                                {
                                    CCSWeaponBaseVData? weaponData = weapon.Value.As<CCSWeaponBase>().VData;

                                    if (weaponData?.WeaponType == CSWeaponType.WEAPONTYPE_KNIFE)
                                        weapon.Value.Remove();
                                }
                            }


                            AddTimer(0.2f, () =>
                            {
                                p.GiveNamedItem("weapon_knife");
                                p.ExecuteClientCommand("slot3");
                            });
                        }
                    }
                });

                if (player != null)
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_used", player.PlayerName, command.GetArg(1), command.GetArg(2)]);
            }
        }
        else
        {
            command.ReplyToCommand(ReplaceTags($"{Config.EklentiTagi} ", true) + Localizer["permissions"]);
            return;
        }
    }


    private CSSTargetResult? GetTarget(CommandInfo info, bool allowMultiple = true, bool noError = false)
    {
        var matches = info.GetArgTargetResult(1);

        if (!matches.Any())
        {
            return null;
        }

        if (!(matches.Count() > 1) || (info.GetArg(1).StartsWith('@') && allowMultiple))
            return matches;

        return null;
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