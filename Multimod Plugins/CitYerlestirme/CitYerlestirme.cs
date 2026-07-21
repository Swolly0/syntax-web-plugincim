using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace CitYerlestirme;
public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}
public class CitYerlestirme : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "Cit Yerlestirme";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!cit komutu ile haritaya cit koyabilirsiniz.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    private readonly Dictionary<CCSPlayerController, bool> FenceMode = new();
    private readonly Dictionary<CCSPlayerController, long> LastAdd = new();
    public static List<CDynamicProp?> Citler = new List<CDynamicProp?>();

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
            Console.WriteLine($"[Cit Yerlestirme] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
            AddCommand("css_cit", "Cit koymayi aktif/pasif yapar.", (player, command) => Cit(player, command));
            AddCommand("css_cit0", "Cit koymayi aktif/pasif yapar.", (player, command) => Cit0(player, command));

            RegisterEventHandler<EventBulletImpact>(OnBulletImpact);
            RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);

            RegisterListener<Listeners.OnServerPrecacheResources>((manifest) =>
            {
                manifest.AddResource("models/props_urban/fence_gate002_256.vmdl");
            });
        }
    }

    public void Cit(CCSPlayerController? player, CommandInfo info)
    {
        if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            if (player == null) return;
            if (Citler.Count() >= 32)
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["fence_limit"]);
                FenceMode[player] = false;

                return;
            }

            LastAdd[player] = 0;
            if (FenceMode[player] == true)
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["cant_put_fence"]);
                FenceMode[player] = false;
            }
            else
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["can_put_fence"]);
                FenceMode[player] = true;
            }
        }
        else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
    }

    public void Cit0(CCSPlayerController? player, CommandInfo info)
    {
        if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            if (player == null) return;

            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["fences_removed", player.PlayerName]);
            CitleriKaldir();

        }
        else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
    }

    HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        foreach (var p in Utilities.GetPlayers())
            FenceMode[p] = false;

        CitleriKaldir();

        return HookResult.Continue;
    }

    public void CitleriKaldir()
    {
        for (var i = Citler.Count - 1; i > -1; i--)
        {
            var entity = Citler[i];
            if (entity != null && entity.IsValid)
                entity.AcceptInput("Kill");
        }

        Citler.Clear();
    }

    HookResult OnBulletImpact(EventBulletImpact @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null) return HookResult.Continue;

        if (FenceMode[player] == true && DateTimeOffset.UtcNow.ToUnixTimeSeconds() > LastAdd[player] + 1)
        {
            var entity = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
            if (entity == null || !entity.IsValid)
                return HookResult.Continue;

            entity.SetModel("models/props_urban/fence_gate002_256.vmdl");
            entity.Entity.Name = "Cit " + Citler.Count() + 1;
            entity.Collision.CollisionGroup = 0;
            entity.Collision.SolidFlags = 0x0040;
            entity.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;

            entity.Teleport(new CounterStrikeSharp.API.Modules.Utils.Vector(@event.X, @event.Y, @event.Z - 25), player.PlayerPawn.Value.AbsRotation, player.PlayerPawn.Value.AbsVelocity);
            entity.DispatchSpawn();

            LastAdd[player] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Citler.Add(entity);
            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["fence_placed", player.PlayerName, Citler.Count()]);

            if (Citler.Count() == 32)
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["fence_limit"]);
                FenceMode[player] = false;
            }
        }

        return HookResult.Continue;
    }

    private void OnClientConnected(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null) return;
        FenceMode[player] = false;
        LastAdd[player] = 0;
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