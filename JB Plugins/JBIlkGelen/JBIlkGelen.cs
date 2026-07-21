using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBIlkGelen;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}

public class JBIlkGelen : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Ilk Gelen";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!ilk <sure> komutu ile oyun baslatilir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    public float fPosX, fPosY, fPosZ;
    CDynamicProp? entity = null;
    int[]? marker = null;

    public int Ilk = 0, Toplam = 0, UnixBaslangic = 0;
    private readonly Dictionary<CCSPlayerController, bool> Dokundu = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? timer_ex = null;

    // LISANS
    public int lisans_bitis_yil = 2025; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 9; // Lisansin bitecegi gun
    // LISANS

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[Ilk Gelen] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
            AddCommand("css_ilk", "Ilk gelen baslat", (player, command) => IlkGelen(player, command));
            AddCommand("css_ilk0", "Ilk gelen durdur", (player, command) => IlkGelen0(player, command));

            RegisterEventHandler<EventRoundStart>(OnRoundEvent);
            RegisterEventHandler<EventRoundEnd>(OnRoundEvent);
            RegisterListener<Listeners.OnClientConnected>(OnClientConnected);

            RegisterListener<Listeners.OnTick>(() =>
            {
                if (entity == null || !entity.IsValid || Ilk == 0 || Toplam == 0) return;

                if (Toplam >= Ilk)
                {
                    EntityKaldir();

                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["killed_fail_players", Toplam, Ilk]);

                    foreach (var p in Utilities.GetPlayers())
                        if (p.is_valid_alive() && p.TeamNum == 2 && !Dokundu[p])
                            p.CommitSuicide(false, true);

                    Toplam = 0;
                    Ilk = 0;
                }
            });

            RegisterListener<Listeners.OnServerPrecacheResources>((manifest) =>
            {
                manifest.AddResource("models/coop/challenge_coin.vmdl");
            });
        }
    }

    public void IlkGelen(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (player.is_ct())
            {
                var sIlk = command.ArgByIndex(1);

                if (sIlk != null && sIlk != "" && IsInt(sIlk) && Convert.ToInt32(sIlk) > 0)
                {
                    Ilk = Convert.ToInt32(sIlk);
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_start", player.PlayerName, sIlk]);

                    EntityKaldir();
                    EntityOlustur(player);

                    foreach (var p in Utilities.GetPlayers())
                        if (p.is_valid())
                            Dokundu[p] = false;

                    Toplam = 0;
                    UnixBaslangic = Convert.ToInt32(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage"]);

            } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }

    public void IlkGelen0(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct())
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_stop", player.PlayerName]);

                EntityKaldir();

                foreach (var p in Utilities.GetPlayers())
                    if (p.is_valid())
                        Dokundu[p] = false;

                Toplam = 0;
                Ilk = 0;
            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }
    public HookResult OnRoundEvent(EventRoundStart @event, GameEventInfo info)
    {
        EntityKaldir();
        return HookResult.Continue;
    }

    public HookResult OnRoundEvent(EventRoundEnd @event, GameEventInfo info)
    {
        EntityKaldir();
        return HookResult.Continue;
    }

    private void OnClientConnected(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null) return;
        Dokundu[player] = false;
    }

    public void EntityOlustur(CCSPlayerController? player)
    {
        entity = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic_override");
        if (entity is null || !entity.IsValid)
            return;

        entity.SetModel("models/coop/challenge_coin.vmdl");
        entity.Entity.Name = "Ilk Gelen";
        entity.CBodyComponent!.SceneNode!.GetSkeletonInstance().Scale = 3.00f;
        entity.IdleAnim = "challenge_coin_idle";
        entity.DispatchSpawn();

        var position = player.PlayerPawn.Value!.AbsOrigin;
        position.Z += 35.0f;
        fPosX = position.X;
        fPosY = position.Y;
        fPosZ = position.Z;

        entity.Teleport(
            position,
            player.PlayerPawn.Value.AbsRotation,
            player.PlayerPawn.Value.AbsVelocity
        );

        if (marker != null)
            remove_marker(marker);

        var vPosition = player.PlayerPawn.Value!.AbsOrigin;
        marker = draw_marker(vPosition.X, vPosition.Y, vPosition.Z);

        timer_ex = AddTimer(0.2f, () =>
        {
            if (entity == null || !entity.IsValid) return;

            if (Toplam >= Ilk)
                return;

            foreach (var p in Utilities.GetPlayers())
                if (p.is_valid_alive() && p.TeamNum == 2 && !Dokundu[p] && Ilk > Toplam)
                {
                    var position = p.PlayerPawn.Value!.AbsOrigin;

                    var farkx = position.X - fPosX;
                    if (farkx < 0.0)
                        farkx = fPosX - position.X;

                    var farky = position.Y - fPosY;
                    if (farky < 0.0)
                        farky = fPosY - position.Y;

                    var farkz = position.Z - fPosZ;
                    if (farkz < 0.0)
                        farkz = fPosZ - position.Z;

                    var mesafe = 20.0;

                    if (farkx <= mesafe && farky <= mesafe && (farkz <= mesafe + 20.0))
                    {
                        Toplam++;
                        Dokundu[p] = true;

                        var Fark = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - UnixBaslangic;
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_finish_game", p.PlayerName, Fark, Toplam, Toplam, Ilk]);
                    }
                }
        }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
    }

    public void EntityKaldir()
    {
        if (entity != null && entity.IsValid)
            entity.Remove();

        if (timer_ex != null)
            timer_ex.Kill();

        if (marker != null)
            remove_marker(marker);

        timer_ex = null;
        entity = null;
        marker = null;
    }


    public int[] draw_marker(float X, float Y, float Z)
    {
        Z -= 35.0f;

        Vector mid = new Vector(X, Y, Z);

        int lines = 50;
        int[] ent = new int[lines];

        float step = (float)(2.0f * Math.PI) / (float)lines;
        float r = 20.0f;

        float angle_old = 0.0f;
        float angle_cur = step;


        for (int i = 0; i < lines; i++)
        {
            Vector start = angle_on_circle(angle_old, r, mid);
            Vector end = angle_on_circle(angle_cur, r, mid);

            ent[i] = draw_laser(start, end, 2.0f);

            angle_old = angle_cur;
            angle_cur += step;
        }

        return ent;
    }

    static public int draw_laser(Vector start, Vector end, float width)
    {
        CEnvBeam? laser = Utilities.CreateEntityByName<CEnvBeam>("env_beam");

        if (laser == null)
            return -1;

        laser.Render = Color.FromArgb(new Random().Next(1, 255), new Random().Next(1, 255), new Random().Next(1, 255), 255);
        laser.Width = 5.0f;

        Vector VEC_ZERO = new Vector(0.0f, 0.0f, 0.0f);
        QAngle ANGLE_ZERO = new QAngle(0.0f, 0.0f, 0.0f);

        laser.Teleport(start, ANGLE_ZERO, VEC_ZERO);
        laser.EndPos.X = end.X;
        laser.EndPos.Y = end.Y;
        laser.EndPos.Z = end.Z;

        Utilities.SetStateChanged(laser, "CBeam", "m_vecEndPos");
        laser.DispatchSpawn();

        return (int)laser.Index;
    }

    static Vector angle_on_circle(float angle, float r, Vector mid)
    {
        return new Vector((float)(mid.X + (r * Math.Cos(angle))), (float)(mid.Y + (r * Math.Sin(angle))), mid.Z + 6.0f);
    }



    static public void remove_marker(int[] ent)
    {
        foreach (int laser in ent)
        {
            CBaseEntity? markerent = Utilities.GetEntityFromIndex<CBaseEntity>(laser);

            if (markerent != null && markerent.DesignerName == "env_beam")
            {
                markerent.Remove();
            }
        }
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