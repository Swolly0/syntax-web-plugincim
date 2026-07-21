using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBSonaKalan;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string PluginTag { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("KrediMiktari")]
    public int KrediMiktari { get; set; } = 100;

    [JsonPropertyName("CpMiktari")]
    public int CpMiktari { get; set; } = 10;
}

public class JBSonaKalan : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "Sona Kalan Menu";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleDescription => "Sona kalan T oyuncusuna menü gönderir.";
    public override string ModuleAuthor => "plugincim.com";

    internal static IStringLocalizer? Stringlocalizer;
    public required Config Config { get; set; }
    public bool SonaKalanAktif = false;
    private CCSPlayerController? iWarden;

    public void OnConfigParsed(Config config)
    {
        Config = config;
        Stringlocalizer = Localizer;
    }

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventPlayerTeam>(OnPlayerTeam);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);

        AddCommand("css_w", "Komutçu olarak atanır.", (player, command) => Warden(player));
        AddCommand("css_uw", "Komutçuluk bırakılır.", (player, command) => UnWarden(player));
        AddCommand("css_sonakalan", "Sona kalan T oyuncusuna menü gönderir.", (player, command) => HandleLastTCommand(player));
    }

    private HookResult OnPlayerDeath(EventPlayerDeath ev, GameEventInfo info)
    {
        if (!Lib.IsJailbreakMap()) return HookResult.Continue;

        if (Utilities.GetPlayers().Count(p => p.is_valid_alive() && p.is_t()) == 1 && iWarden?.is_valid() == true)
            iWarden.PrintToChat(ReplaceTags($"{Config.PluginTag} \x01Sona kalan menü göndermek ister misiniz? \x0b(!sonakalan)"));

        return HookResult.Continue;
    }

    private void HandleLastTCommand(CCSPlayerController? player)
    {
        if (!Lib.IsJailbreakMap()) return;

        if (iWarden != player)
        {
            player?.PrintToChat(ReplaceTags($"{Config.PluginTag}\x01 Bu komutu sadece komutçu kullanabilir!"));
            return;
        }

        if(Utilities.GetPlayers().Count(p => p.is_valid_alive() && p.is_t()) != 1)
        {
            player?.PrintToChat(ReplaceTags($"{Config.PluginTag}\x01 Bu komutu sadece hayatta 1 mahkum kaldığında kullanabilirsin."));
            return;
        }

        var lastTPlayer = Utilities.GetPlayers().FirstOrDefault(p => p.is_valid_alive() && p.is_t());
        if (lastTPlayer != null)
        {
            ShowMenuToLastT(lastTPlayer);
            player?.PrintToChat(ReplaceTags($"{Config.PluginTag} \x01Sona kalan menüsü gönderildi."));
        }
        else
            player?.PrintToChat(ReplaceTags($"{Config.PluginTag} \x01Sona kalan T oyuncusu yok!"));
    }

    private void ShowMenuToLastT(CCSPlayerController lastT)
    {
        if (!Lib.IsJailbreakMap()) return;

        var totalTPlayers = Utilities.GetPlayers().Count(p => p.is_t());
        var menu = new CenterHtmlMenu(ReplaceTags("Sona Kalan Seçenekleri"));

        menu.AddMenuOption("LR'yi kazan ve koruma ol (!lr)", (player, option) => HandleMenuSelection(player, "lr"));

        if (totalTPlayers >= 10)
        {
            menu.AddMenuOption($"{Config.KrediMiktari} Kredi ve {Config.CpMiktari} JB",
                (player, option) => HandleMenuSelection(player, $"kredi"));
        }

        MenuManager.OpenCenterHtmlMenu(this, lastT, menu);
        SonaKalanAktif = true;
    }

    private void HandleMenuSelection(CCSPlayerController player, string selection)
    {
        MenuManager.CloseActiveMenu(player);
        player.PrintToCenterHtml("", 0);
        
        if (!Lib.IsJailbreakMap() || !SonaKalanAktif) return;
        if (!player.is_valid_alive() || !player.is_t() || Utilities.GetPlayers().Count(p => p.is_valid_alive() && p.is_t()) != 1)
            return;

        if(selection == "lr")
            player.ExecuteClientCommandFromServer("css_lr");
        else
        if (selection == "kredi")
        {
            Server.ExecuteCommand($"css_givecredits #{player.UserId} {Config.KrediMiktari}");
            Server.ExecuteCommand($"css_cpver #{player.UserId} {Config.CpMiktari}");
            Server.PrintToChatAll(ReplaceTags($"{Config.PluginTag} \x04{player.PlayerName} \x01sona kalan menüden \x0bkredi ve jb'yi seçti!"));
            Server.PrintToChatAll(ReplaceTags($"{Config.PluginTag} \x04{player.PlayerName} \x01sona kalan menüden \x0bkredi ve jb'yi seçti!"));
            player.CommitSuicide(true, true);
        }

        SonaKalanAktif = false;
    }

    private HookResult OnRoundStart(EventRoundStart ev, GameEventInfo info)
    {
        if (!Lib.IsJailbreakMap()) return HookResult.Continue;
        SonaKalanAktif = false;
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd ev, GameEventInfo info)
    {
        if (!Lib.IsJailbreakMap()) return HookResult.Continue;
        SonaKalanAktif = false;
        return HookResult.Continue;
    }

    private void Warden(CCSPlayerController? player)
    {
        if (!Lib.IsJailbreakMap()) return;

        if (iWarden == null && player?.is_ct() == true)
            iWarden = player;
    }

    private void UnWarden(CCSPlayerController? player)
    {
        if (!Lib.IsJailbreakMap()) return;

        if (iWarden == player)
            iWarden = null;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (iWarden == @event.Userid)
            iWarden = null;

        return HookResult.Continue;
    }

    private HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        if (iWarden == @event.Userid && @event.Team != 3)
            iWarden = null;
        
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
        {
            message = remove ? message.Replace(colorPatterns[i], "") : "\u200e" + message.Replace(colorPatterns[i], colorReplacements[i]);
        }
        return message;
    }
}

public static class Lib
{
    public static bool IsJailbreakMap() => Server.MapName.StartsWith("jb_", System.StringComparison.OrdinalIgnoreCase);

    public static bool is_valid(this CCSPlayerController? player) => player?.IsValid == true && player.PlayerPawn.IsValid;

    public static bool is_t(this CCSPlayerController? player) => player.is_valid() && player.TeamNum == 2;

    public static bool is_ct(this CCSPlayerController? player) => player.is_valid() && player.TeamNum == 3;

    public static bool is_valid_alive(this CCSPlayerController? player) => player.is_valid() && player.PawnIsAlive && player.get_health() > 0;

    public static int get_health(this CCSPlayerController? player) => player?.PlayerPawn.Value?.Health ?? 100;
}
