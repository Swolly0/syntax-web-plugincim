using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBCTSilahMenu;
public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}
public class JBCTSilahMenu : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "CT Silah Menu";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!kit ile gardiyanlar silahlarini secebilir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    private readonly Dictionary<CCSPlayerController, string> iWeapon = new();

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
            Console.WriteLine($"[CT Weapon Menu] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
			AddCommand("css_kit", "CT Silah Menusu", (player, command) => Silah_Menusu(player, command));

			RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn, HookMode.Pre);
			RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
		}
    }

    public void Silah_Menusu(CCSPlayerController? player, CommandInfo command)
    {
        if(NativeAPI.GetMapName().Contains("jb_"))
            if (player.is_valid())
                if (player.TeamNum == 3)
                {
                    var menu = new CenterHtmlMenu(Localizer["menu_title"]);
                    menu.AddMenuOption("AK47", (player, option) => Silah_Menusu_(player, 1));
                    menu.AddMenuOption("M4A4", (player, option) => Silah_Menusu_(player, 2));
                    menu.AddMenuOption("M4A1-S", (player, option) => Silah_Menusu_(player, 3));
                    menu.AddMenuOption("AUG", (player, option) => Silah_Menusu_(player, 4));
                    menu.AddMenuOption("P90", (player, option) => Silah_Menusu_(player, 5));
                    menu.AddMenuOption("AWP", (player, option) => Silah_Menusu_(player, 6));

                    MenuManager.OpenCenterHtmlMenu(this, player, menu);
                }
                else
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
                    return;
                }
    }

    public void Silah_Menusu_(CCSPlayerController player, int option)
    {
        MenuManager.CloseActiveMenu(player);
        player.PrintToCenterHtml("", 0);

        if (NativeAPI.GetMapName().Contains("jb_"))
            if (player.is_valid() && player.TeamNum == 3)
            {
                if (option == 1)
                    iWeapon[player] = "weapon_ak47";
                else
                if (option == 2)
                    iWeapon[player] = "weapon_m4a1";
                else
                if (option == 3)
                    iWeapon[player] = "weapon_m4a1_silencer";
                else
                if (option == 4)
                    iWeapon[player] = "weapon_aug";
                else
                if (option == 5)
                    iWeapon[player] = "weapon_p90";
                else
                if (option == 6)
                    iWeapon[player] = "weapon_awp";

                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["weapon_saved", iWeapon[player]]);
            } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
    }

    HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            CCSPlayerController? player = @event.Userid;

            if (player.is_valid() && player.TeamNum == 3)
            {
			    AddTimer(1.0f, () =>
                {
                    if (player.is_valid() && player.TeamNum == 3)
                    {
                        player.RemoveWeapons();

                        if (!String.IsNullOrEmpty(iWeapon[player]))
                            player.GiveNamedItem(iWeapon[player]);
                        else
                        {
                            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage"]);
                            player.GiveNamedItem("weapon_m4a1");
                        }

                        player.GiveNamedItem("weapon_deagle");
                        player.GiveNamedItem("weapon_knife");
                        player.GiveNamedItem("weapon_hegrenade");
                        player.GiveNamedItem("weapon_incgrenade");
                    }
                }, TimerFlags.STOP_ON_MAPCHANGE);
            }
        }

        return HookResult.Continue;
    }

    private void OnClientConnected(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        iWeapon[player] = "";
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