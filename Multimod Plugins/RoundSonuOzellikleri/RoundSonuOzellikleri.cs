using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Localization;

namespace RoundSonuOzellikleri;
public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}
public class RoundSonuOzellikleri : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "Round Sonu Ozellikleri";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!rso komutu ile round sonu ozellikleri acilip katilabilir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    private static readonly bool?[] bNoclip = new bool?[65];
    private static readonly bool?[] bSpeed = new bool?[65];
    private static readonly bool?[] bGravity = new bool?[65];

    // LISANS
    public int lisans_bitis_yil = 2025; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 30; // Lisansin bitecegi gun
    // LISANS

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[RSO] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
            AddCommand("css_rso", "Round Sonu Ozellikleri", (player, command) => RSO(player, command));

            RegisterEventHandler<EventPlayerConnect>(OnPlayerConnect);
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        }
    }

    public void RSO(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null && player.IsValid)
        {
            var menu = new CenterHtmlMenu(Localizer["MenuTitle"]);

            if (bNoclip[player.Index] == null || bNoclip[player.Index] == true)
                menu.AddMenuOption("Noclip: " + Localizer["active"], (player, option) => RSO_(player, 1));
            else
                menu.AddMenuOption("Noclip: " + Localizer["inactive"], (player, option) => RSO_(player, 1));

            if (bSpeed[player.Index] == null || bSpeed[player.Index] == true)
                menu.AddMenuOption("Speed: " + Localizer["active"], (player, option) => RSO_(player, 2));
            else
                menu.AddMenuOption("Speed: " + Localizer["inactive"], (player, option) => RSO_(player, 2));

            if(bGravity[player.Index] == null || bGravity[player.Index] == true)
                menu.AddMenuOption("Gravity: " + Localizer["active"], (player, option) => RSO_(player, 3));
            else
                menu.AddMenuOption("Gravity: " + Localizer["inactive"], (player, option) => RSO_(player, 3));


            MenuManager.OpenCenterHtmlMenu(this, player, menu);
        }
    }

    public void RSO_(CCSPlayerController player, int option)
    {
        if (player != null && player.IsValid)
        {
            if (option == 1)
            {
                if (bNoclip[player.Index] != null && bNoclip[player.Index] == true)
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["noclip_inactived"]);
                    bNoclip[player.Index] = false;
                }
                else
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["noclip_inactived"]);
                    bNoclip[player.Index] = true;
                }
            }

            if (option == 2)
            {
                if (bSpeed[player.Index] != null && bSpeed[player.Index] == true)
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["speed_inactived"]);
                    bSpeed[player.Index] = false;
                }
                else
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["speed_actived"]);
                    bSpeed[player.Index] = true;
                }
            }

            if (option == 3)
            {
                if (bGravity[player.Index] != null && bGravity[player.Index] == true)
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["gravity_inactived"]);
                    bGravity[player.Index] = false;
                }
                else
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["gravity_actived"]);
                    bGravity[player.Index] = true;
                }
            }
        }
    }

    HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        foreach (var p in Utilities.GetPlayers())
        {
            if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV && p.is_valid_alive())
            {
                if (bGravity[p.Index] != null && bNoclip[p.Index] == true)
                    p.PlayerPawn.Value.MoveType = MoveType_t.MOVETYPE_NOCLIP;

                if (bSpeed[p.Index] != null && bSpeed[p.Index] == true)
                    p.PlayerPawn.Value.VelocityModifier = 3.0f;

                if (bGravity[p.Index] != null && bGravity[p.Index] == true)
                    p.PlayerPawn.Value.GravityScale = 0.35f;
            }
        }
        
        return HookResult.Continue;
    }

    HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        bNoclip[player.Index] = false;
        bSpeed[player.Index] = false;
        bGravity[player.Index] = false;

        return HookResult.Continue;
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
            if(!remove)
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