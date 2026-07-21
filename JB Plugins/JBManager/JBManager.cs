using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Commands.Targeting;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json.Serialization;

namespace JBStrip;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Red}[CyberRulz]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}

public class JBManager : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Manager";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "JB Manager.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[JB Manager] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
        }

        Config = config;
    }
	
    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnect);
        AddCommandListener("jointeam", OnPlayerChangeTeam);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
	}

    HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            foreach (var p in Utilities.GetPlayers())
            {
                if (p.is_valid())
                {
                    if (p.is_ct())
                    {
                        p.VoiceFlags = VoiceFlags.Normal;
                    } 
                    else
                    {
                        if (p.is_valid_alive() && p.is_t())
                        {
                            /*var weapons = p.PlayerPawn.Value.WeaponServices.MyWeapons;
                            foreach (var weapon in weapons)
                                if (weapon != null && weapon.IsValid)
                                    weapon.Value.Remove();*/

                            p.RemoveWeapons();
                            p.GiveNamedItem("weapon_knife");
                        }

                        p.VoiceFlags = VoiceFlags.Muted;
                    }
                }
            }
        }

        return HookResult.Continue;
    }


    [GameEventHandler]
    public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            CCSPlayerController player = @event.Userid;
            if (player != null && NativeAPI.GetMapName().Contains("jb_"))
            {
                if (player == null || !player.IsValid)
                    return HookResult.Continue;

                player.SwitchTeam(CsTeam.Terrorist);
                player.VoiceFlags = VoiceFlags.Muted;
            }
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerChangeTeam(CCSPlayerController? player, CommandInfo command)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            if (!Int32.TryParse(command.ArgByIndex(1), out int team_switch))
                return HookResult.Continue;

            if (player == null || !player.IsValid)
                return HookResult.Continue;

            CCSPlayerPawn? playerpawn = player.PlayerPawn.Value;
            var player_team = team_switch;

            if (player_team != 2)
            {
                player.SwitchTeam(CsTeam.Terrorist);
                return HookResult.Stop;
            }
        }

        return HookResult.Continue;
    }

    private bool IsInt(string sVal)
    {
        foreach (char c in sVal)
        {
            int iN = (int)c;
            if ((iN > 57) || (iN < 48))
                return false;
        }
        return true;
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