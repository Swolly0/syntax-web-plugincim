using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Localization;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using System.Drawing;
using CounterStrikeSharp.API.Modules.Memory;

namespace JBDaire;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}

public class JBDaire : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Daire";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!daire komutu ile t takimini daire yapar.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[JB Daire] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
        }

        Config = config;
        Stringlocalizer = Localizer;
    }

    // LISANS
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
            AddCommand("css_daire", "Daire olusturur", (player, command) => Daire(player, command));
	}

    private void Daire(CCSPlayerController? player, CommandInfo info)
    {
        if (!NativeAPI.GetMapName().Contains("jb_"))
            return;

        if (!player.is_valid())
            return;

        if (!AdminManager.PlayerHasPermissions(player, "@css/generic") && !player.is_ct())
        {
            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permission"]);
            return;
        }

        if (!player.is_valid_alive())
        {
            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["alive"]);
            return;
        }

        var tPlayers = Utilities.GetPlayers().Where(p => p.is_valid_alive() && p.is_t()).ToList();
        if (tPlayers.Count < 5)
        {
            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["min_player"]);
            return;
        }

        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["circle_created", player.PlayerName]);

        float radius = 300.0f; // Başlangıç yarıçapını biraz daha büyük tutalım.
        string arg1 = info.GetArg(1);

        if (!string.IsNullOrEmpty(arg1) && float.TryParse(arg1, out float radiusArg))
            radius = radiusArg;

        var playerPos = player.PlayerPawn.Value.AbsOrigin;

        double angleStep = 2 * Math.PI / tPlayers.Count;

        for (int i = 0; i < tPlayers.Count; i++)
        {
            var tPlayer = tPlayers[i];
            if (!tPlayer.is_valid_alive()) continue;

            var playerPawn = tPlayer.PlayerPawn.Value;

            double angle = i * angleStep;

            float xOffset = (float)(radius * Math.Cos(angle));
            float yOffset = (float)(radius * Math.Sin(angle));

            Vector newPos = new Vector(playerPos.X + xOffset, playerPos.Y + yOffset, playerPos.Z);
            playerPawn.Teleport(newPos, playerPawn.EyeAngles, playerPawn.AbsVelocity);

            ChangeMovetype(playerPawn, MoveType_t.MOVETYPE_OBSOLETE, Color.White);
        }
    }




    static void ChangeMovetype(CBasePlayerPawn pawn, MoveType_t movetype, Color? color)
    {
        pawn.MoveType = movetype;
        Schema.SetSchemaValue(pawn.Handle, "CBaseEntity", "m_nActualMoveType", movetype);
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");

        if (color != null)
        {
            pawn.RenderMode = RenderMode_t.kRenderTransColor;
            pawn.Render = color.Value;

            Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");
        }
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