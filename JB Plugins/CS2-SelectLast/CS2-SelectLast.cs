using System.Net;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CS2SelectLast;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
    
    [JsonPropertyName("LicenseKey")] 
    public string TmGXnexhnN { get; set; } = "YOUR_LICENSE_KEY";   
}

public class CS2SelectLast : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "CS2-SelectLast";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleDescription => "";
    public override string ModuleAuthor => "Plugincim.com";

    private int ModuleConfigVersion => 1;
    
    internal static IStringLocalizer? Stringlocalizer;

    public static List<int?> Oyuncular = new List<int?>();
    private readonly Dictionary<CCSPlayerController, bool> Canlandir = new();

    private MemoryFunctionVoid<CBasePlayerController, CCSPlayerPawn, bool, bool> CBasePlayerController_SetPawnFunc;
	

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Logger.LogInformation($"[{ModuleName}] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
        }

        Config = config;
        Stringlocalizer = Localizer;
    }
	
    public override void Load(bool hotReload)
    {

		AddCommand("css_sonsec", "Son Sec", (player, command) => SonSec(player, command));

		RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Pre);
		RegisterEventHandler<EventPlayerConnect>(OnPlayerConnect);
		RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

		CBasePlayerController_SetPawnFunc =
			new MemoryFunctionVoid<CBasePlayerController, CCSPlayerPawn, bool, bool>(GetSignature());

		Oyuncular.Clear();
    }

    public void SonSec(CCSPlayerController? player, CommandInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct())
            {
                var arg1 = info.ArgByIndex(1);

                if (arg1 != null && arg1 != "" && IsInt(arg1))
                {
                    int OyuncuSayisi = Convert.ToInt32(arg1);
                    if (Oyuncular.Count >= OyuncuSayisi)
                    {
                        if (player != null)
                            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_use", player.PlayerName, arg1]);

                        foreach (var p in Utilities.GetPlayers())
                            Canlandir[p] = false;

                        for (var i = Oyuncular.Count - 1; i > -1; i--)
                        {
                            foreach (var p in Utilities.GetPlayers())
                                if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV && p.is_t() && Oyuncular[i] == p.UserId)
                                {
                                    Canlandir[p] = true;
                                    OyuncuSayisi--;
                                }

                            if (OyuncuSayisi == 0)
                                break;
                        }

                        foreach (var p in Utilities.GetPlayers())
                            if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV && p.is_t())
                            {
                                if (!Canlandir[p] && p.is_valid_alive())
                                    p.CommitSuicide(false, true);

                                if (Canlandir[p] && !p.is_valid_alive())
                                {
                                    var playerPawn = p.PlayerPawn.Value;
                                    if (playerPawn == null) return;

                                    CBasePlayerController_SetPawnFunc.Invoke(p, playerPawn, true, false);
                                    VirtualFunction.CreateVoid<CCSPlayerController>(p.Handle,
                                        GameData.GetOffset("CCSPlayerController_Respawn"))(p);
                                }
                            }
                    } 
                    else player?.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_invalid_count"]);
                }
                else player?.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage"]);
            }
            else player?.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }



    HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            CCSPlayerController? player = @event.Userid;

            if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV && player.is_t())
                Oyuncular.Add(player.UserId);
        }

        return HookResult.Continue;
    }

    HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            CCSPlayerController? player = @event.Userid;

            if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
                Oyuncular.Remove(player.UserId);
        }

        return HookResult.Continue;
    }

    HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            CCSPlayerController? player = @event.Userid;

            if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
                Oyuncular.Remove(player.UserId);
        }

        return HookResult.Continue;
    }

    private static string GetSignature()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"\x44\x88\x4C\x24\x2A\x55\x57"
            : @"\x55\x48\x89\xE5\x41\x57\x41\x56\x41\x55\x41\x54\x49\x89\xFC\x53\x48\x89\xF3\x48\x81\xEC\xC8\x00\x00\x00";
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
    
    private static string PPujhEuVig(string url)
    {

        var request = (HttpWebRequest)WebRequest.Create(url);
        request.Method = "GET";
        using var response = (HttpWebResponse)request.GetResponse();
        using var streamReader = new StreamReader(response.GetResponseStream());
        return streamReader.ReadToEnd();
        
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