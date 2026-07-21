using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Localization;
using System.Drawing;
using System.Text.Json.Serialization;

namespace JBTakimOlustur;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}

public class JBTakimOlustur : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "Team Glow";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleDescription => "T takımındaki mahkumları iki takıma ayırır ve glow efekti verir.";
    public override string ModuleAuthor => "plugincim";

    private List<CCSPlayerController> redTeam = new();
    private List<CCSPlayerController> blueTeam = new();
    internal static IStringLocalizer? Stringlocalizer;
    public required Config Config { get; set; }

    public void OnConfigParsed(Config config)
    {
        Config = config;
        Stringlocalizer = Localizer;
    }

    public override void Load(bool hotReload)
    {
        AddCommand("css_takimo", "Takım oluşturur.", (player, command) => CreateTeams(player));
        AddCommand("css_takim0", "Takımları kapatır ve renkleri kaldırır.", (player, command) => ClearTeams(player));
    }

    private void CreateTeams(CCSPlayerController? player)
    {
        if (!Lib.IsJailbreakMap())
            return;

        if (!AdminManager.PlayerHasPermissions(player, "@css/generic") && player.is_t())
        {
            player?.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Stringlocalizer?["permission"]);
            return;
        }

        redTeam.Clear();
        blueTeam.Clear();

        var tPlayers = Utilities.GetPlayers().Where(p => p.is_valid_alive() && p.is_t()).ToList();
        if (tPlayers.Count < 2)
        {
            player?.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Stringlocalizer?["min_player"]);
            return;
        }

        ShuffleAndAssignTeams(tPlayers);
        ApplyGlowEffects();

        player?.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Stringlocalizer?["teams_created"]);
    }

    private void ClearTeams(CCSPlayerController? player)
    {
        if (!Lib.IsJailbreakMap())
            return;

        if (!AdminManager.PlayerHasPermissions(player, "@css/generic") && player.is_t())
        {
            player?.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Stringlocalizer?["permission"]);
            return;
        }

        redTeam.ForEach(RemoveGlow);
        blueTeam.ForEach(RemoveGlow);

        player?.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Stringlocalizer?["teams_cleared"]);
    }

    private void ShuffleAndAssignTeams(List<CCSPlayerController> players)
    {
        var rng = new Random();
        players = players.OrderBy(p => rng.Next()).ToList();

        for (int i = 0; i < players.Count; i++)
        {
            if (i % 2 == 0)
            {
                players[i]?.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Stringlocalizer?["your_team_red"]);
                redTeam.Add(players[i]);
            }
            else
            {
                players[i]?.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Stringlocalizer?["your_team_blue"]);
                blueTeam.Add(players[i]);
            }
        }
    }

    private void ApplyGlowEffects()
    {
        foreach (var player in redTeam)
            if (player.is_valid_alive())
                Lib.Glow(player.PlayerPawn.Value, Color.Red); // Kırmızı

        foreach (var player in blueTeam)
            if (player.is_valid_alive())
                Lib.Glow(player.PlayerPawn.Value, Color.Blue); // Mavi
    }

    private void RemoveGlow(CCSPlayerController player)
    {
        if (player.is_valid_alive())
            Lib.Glow(player.PlayerPawn.Value, Color.White); // Beyaz
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
}

public static class Lib
{
    public static bool IsJailbreakMap()
    {
        string mapName = Server.MapName;
        return mapName.StartsWith("jb_", System.StringComparison.OrdinalIgnoreCase);
    }

    public static bool is_valid(this CCSPlayerController? player)
    {
        return player != null && player.IsValid && player.PlayerPawn.IsValid;
    }

    public static bool is_t(this CCSPlayerController? player)
    {
        return player != null && is_valid(player) && player.TeamNum == 2;
    }

    public static bool is_ct(this CCSPlayerController? player)
    {
        return player != null && is_valid(player) && player.TeamNum == 3;
    }

    public static bool is_valid_alive(this CCSPlayerController? player)
    {
        return player != null && player.is_valid() && player.PawnIsAlive && player.get_health() > 0;
    }

    public static int get_health(this CCSPlayerController? player)
    {
        var pawn = player?.PlayerPawn.Value;
        return pawn?.Health ?? 100;
    }

    public static void Glow(this CBasePlayerPawn playerPawn, Color color)
    {
        playerPawn.RenderMode = RenderMode_t.kRenderTransColor;
        playerPawn.Render = color;
        Utilities.SetStateChanged(playerPawn, "CBaseModelEntity", "m_clrRender");
    }
}