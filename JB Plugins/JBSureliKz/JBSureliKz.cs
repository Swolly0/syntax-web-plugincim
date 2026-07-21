using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;
using CounterStrikeSharp.API.Modules.Menu;
using System.Drawing;
using CounterStrikeSharp.API.Modules.Memory;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBSureliKz;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("detect_range")]
    public float algilama_mesafesi { get; set; } = 75.0f;
}

public class HaritaKonumlari
{
    public string? skz_konumu { get; set; }
    public string? kulvar_konumu { get; set; }
    public string? hucre_konumu { get; set; }
}

public class JBSureliKz : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Sureli Kz";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!skz <sure> komutu ile oyun baslatilir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    CDynamicProp? entity = null;
    int[]? marker = null;

    public int? Sure = 0, GeriSayim = 0, Toplam = 0, UnixBaslangic = 0;
    private readonly Dictionary<CCSPlayerController, bool> Dokundu = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? timer_countdown = null;
    private CounterStrikeSharp.API.Modules.Timers.Timer? timer_icon = null;
    private static readonly HttpClient client = new HttpClient();

    public HaritaKonumlari cfg_harita_konumlari = new HaritaKonumlari();

    public string ConfigPath = "";


    // LISANS
    public int lisans_bitis_yil = 2025; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 20; // Lisansin bitecegi gun
    // LISANS

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[Sureli Kz] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
            AddCommand("css_skz", "Skz baslat", (player, command) => SureliKz(player, command));
            AddCommand("css_skz0", "Skz durdur", (player, command) => SureliKz0(player, command));
            AddCommand("css_skzayar", "Skz ayarlari", (player, command) => SkzAyar(player, command));

            RegisterEventHandler<EventRoundStart>(OnRoundEvent);
            RegisterEventHandler<EventRoundEnd>(OnRoundEvent);
            RegisterListener<Listeners.OnClientConnected>(OnClientConnected);

            ConfigPath = ModuleDirectory;

            RegisterListener<Listeners.OnTick>(() =>
            {
                if (Sure == -1 && timer_countdown != null)
                {
                    timer_countdown?.Kill();
                    timer_countdown = null;
                }
                else
                if(Sure > 0 && GeriSayim == -1)
                    if (entity != null && entity.IsValid)
                    {
                        foreach (var p in Utilities.GetPlayers())
                        if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
                            if (Sure <= 10)
                                p.PrintToCenterHtml($"<img src='https://www.plugincim.com/forum/data/assets/logo/yataybeyaz.png'/><br/><br/><p style='font-weight: 700; color: red; font-size: 20px;'>" + Localizer["menu_title1"] + $"</p><img src='https://www.plugincim.com/assets/images/cs2/numbers/{Convert.ToInt32(Sure)}.png' width='64px' height='64px'/><br/><br/>");
                            else
                                p.PrintToCenterHtml($"<img src='https://www.plugincim.com/forum/data/assets/logo/yataybeyaz.png'/><br/><br/><p style='font-weight: 700; color: red; font-size: 20px;'>" + Localizer["menu_title2", Sure] + "</p><br/><br/>");
                    }
            });

            RegisterListener<Listeners.OnServerPrecacheResources>((manifest) =>
            {
                manifest.AddResource("models/coop/challenge_coin.vmdl");
            });
        }
    }

    public void SureliKz(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct())
            {
                if(Sure > 0 || GeriSayim > 0)
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_active"]);
                    return;
                }

                if (!File.Exists(ConfigPath + "/" + NativeAPI.GetMapName() + ".json"))
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_set_error"]);
                    return;
                }

                var json = File.ReadAllText(Path.Combine(ConfigPath, NativeAPI.GetMapName() + ".json"));
                cfg_harita_konumlari = JsonSerializer.Deserialize<HaritaKonumlari>(json);

                if (cfg_harita_konumlari.skz_konumu == null)
                {
                    player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_settings_error"]);
                    return;
                }

                var sSure = command.ArgByIndex(1);

                if (sSure != null && sSure != "" && IsInt(sSure) && Convert.ToInt32(sSure) > 0)
                {
                    Sure = Convert.ToInt32(sSure);
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_start_command", player.PlayerName, sSure]);

                    var menu = new CenterHtmlMenu(Localizer["game_start_title"]);
                    menu.AddMenuOption(Localizer["game_start_option_1"], (player, option) => SureliKz_(player, 1));

                    if (cfg_harita_konumlari.kulvar_konumu == null || cfg_harita_konumlari.kulvar_konumu == "")
                        menu.AddMenuOption(Localizer["game_start_option_2"], (player, option) => SureliKz_(player, 0));
                    else
                        menu.AddMenuOption(Localizer["game_start_option_3"], (player, option) => SureliKz_(player, 2));

                    if (cfg_harita_konumlari.hucre_konumu == null || cfg_harita_konumlari.hucre_konumu == "")
                        menu.AddMenuOption(Localizer["game_start_option_4"], (player, option) => SureliKz_(player, 0));
                    else
                        menu.AddMenuOption(Localizer["game_start_option_5"], (player, option) => SureliKz_(player, 3));

                    MenuManager.OpenCenterHtmlMenu(this, player, menu);

                } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage"]);

            } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }

    public void SureliKz_(CCSPlayerController player, int option)
    {
        if (player.is_valid())
        {
            var position = new Vector();
            if (option == 1)
            {
                position = player.PlayerPawn.Value!.AbsOrigin;
                position.Z += 35.0f;
            }
            else
            if (option == 2)
            {
                string[] sPosition = cfg_harita_konumlari.kulvar_konumu.Split(' ');
                position = new Vector(float.Parse(sPosition[0]), float.Parse(sPosition[1]), float.Parse(sPosition[2]) + 35.0f);
            }
            else
            if (option == 3)
            {
                string[] sPosition = cfg_harita_konumlari.hucre_konumu.Split(' ');
                position = new Vector(float.Parse(sPosition[0]), float.Parse(sPosition[1]), float.Parse(sPosition[2]) + 35.0f);
            }

            foreach (var p in Utilities.GetPlayers())
                if(p.is_valid_alive() && p.is_t())
                {
                    Dokundu[p] = false;

                    p.PlayerPawn.Value.Teleport(
                        position,
                        p.PlayerPawn.Value.AbsRotation,
                        p.PlayerPawn.Value.AbsVelocity
                    );

                    Freeze(p);
                }

            EntityKaldir();
            EntityOlustur(player);



            GeriSayim = 3;
            Toplam = 0;

            if (timer_countdown != null)
                timer_countdown?.Kill();

            timer_countdown = AddTimer(1.0f, () =>
            {
                if (GeriSayim >= 1)
                {
                    if(GeriSayim <= 3)
                    {
                        foreach (var p in Utilities.GetPlayers())
                            if (p.is_valid())
                                p.ExecuteClientCommand($"play diger/geri_sayim/s{Sure}");
                    }

                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_countdown", GeriSayim]);
                    GeriSayim--;
                }
                else
                if (GeriSayim == 0)
                {
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_start", Sure]);
                    UnixBaslangic = Convert.ToInt32(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                    foreach (var p in Utilities.GetPlayers())
                        if (p.is_valid())
                        {
                            Dokundu[p] = false;
                            UnFreeze(p);
                        }

                    GeriSayim = -1;
                }
                else
                {
                    if (Sure <= 0)
                    {
                        int ToplamKontrol = 0;
                        foreach (var p in Utilities.GetPlayers())
                            if (p.is_valid() && p.is_valid_alive() && p.TeamNum == 2 && Dokundu[p])
                                ToplamKontrol++;

                        if (ToplamKontrol > 0)
                        {
                            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_end"]);
                            Server.PrintToChatAll(Localizer["game_win_count", ToplamKontrol]);

                            foreach (var p in Utilities.GetPlayers())
                                if (p.is_valid() && p.is_valid_alive() && p.TeamNum == 2 && !Dokundu[p])
                                    p.CommitSuicide(false, true);
                        }
                        else
                            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_no_win"]);

                        Sifirla();
                        return;
                    }
                    else
                    if (Sure % 10 == 0)
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_end_countdown", Sure]);
                    else
                    if (Sure < 10)
                    {
                        if(Sure <= 3)
                        {
                            foreach (var p in Utilities.GetPlayers())
                                if (p.is_valid())
                                    p.ExecuteClientCommand($"play diger/geri_sayim/s{Sure}");
                        }

                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_end_countdown", Sure]);
                    }

                    Sure--;
                }

            }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
        }
    }

    public void SureliKz0(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.is_ct())
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_stop"]);
                Sifirla();
            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }
    public void Sifirla()
    {
        EntityKaldir();

        foreach (var p in Utilities.GetPlayers())
            if (p.is_valid())
                Dokundu[p] = false;

        Toplam = 0;
        Sure = -1;
        GeriSayim = -1;

        if (marker != null)
            remove_marker(marker);

        marker = null;
    }

    public void SkzAyar(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/root"))
            {
                if (File.Exists(ConfigPath + "/" + NativeAPI.GetMapName() + ".json"))
                {
                    var json = File.ReadAllText(Path.Combine(ConfigPath, NativeAPI.GetMapName() + ".json"));
                    cfg_harita_konumlari = JsonSerializer.Deserialize<HaritaKonumlari>(json);
                }

                var menu = new CenterHtmlMenu(Localizer["game_set_title"]);

                if (cfg_harita_konumlari.skz_konumu == null || cfg_harita_konumlari.skz_konumu == "")
                    menu.AddMenuOption(Localizer["game_set_option_1"], (player, option) => SkzAyar_(player, 1));
                else
                    menu.AddMenuOption(Localizer["game_set_option_2"], (player, option) => SkzAyar_(player, 1));

                if (cfg_harita_konumlari.kulvar_konumu == null || cfg_harita_konumlari.kulvar_konumu == "")
                    menu.AddMenuOption(Localizer["game_set_option_3"], (player, option) => SkzAyar_(player, 2));
                else
                    menu.AddMenuOption(Localizer["game_set_option_4"], (player, option) => SkzAyar_(player, 2));

                if (cfg_harita_konumlari.hucre_konumu == null || cfg_harita_konumlari.hucre_konumu == "")
                    menu.AddMenuOption(Localizer["game_set_option_5"], (player, option) => SkzAyar_(player, 3));
                else
                    menu.AddMenuOption(Localizer["game_set_option_6"], (player, option) => SkzAyar_(player, 3));

                MenuManager.OpenCenterHtmlMenu(this, player, menu);
            }
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions2"]);
        }
    }

    public void SkzAyar_(CCSPlayerController player, int option)
    {
        if (player.is_valid())
        {
            var position = player.PlayerPawn.Value!.AbsOrigin;

            if (option == 1)
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_set_1"]);
                cfg_harita_konumlari.skz_konumu = $"{position.X} {position.Y} {position.Z}";
            }
            else
            if (option == 2)
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_set_2"]);
                cfg_harita_konumlari.kulvar_konumu = $"{position.X} {position.Y} {position.Z}";
            }
            else
            if (option == 3)
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_set_3"]);
                cfg_harita_konumlari.hucre_konumu = $"{position.X} {position.Y} {position.Z}";
            }

            var new_pos = $"\"skz_konumu\": \"{cfg_harita_konumlari.skz_konumu}\",\"kulvar_konumu\": \"{cfg_harita_konumlari.kulvar_konumu}\",\"hucre_konumu\": \"{cfg_harita_konumlari.hucre_konumu}\"";
            File.WriteAllText(Path.Combine(ConfigPath, NativeAPI.GetMapName() + ".json"), "{" + new_pos + "}");
        }
    }








    public HookResult OnRoundEvent(EventRoundStart @event, GameEventInfo info)
    {
        Sifirla();
        return HookResult.Continue;
    }

    public HookResult OnRoundEvent(EventRoundEnd @event, GameEventInfo info)
    {
        Sifirla();
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
        entity.Entity.Name = "Sureli KZ";
        entity.CBodyComponent!.SceneNode!.GetSkeletonInstance().Scale = 3.00f;
        entity.IdleAnim = "challenge_coin_idle";
        entity.DispatchSpawn();


        string[] sPosition = cfg_harita_konumlari.skz_konumu.Split(' ');
        Vector vPosition = new Vector(float.Parse(sPosition[0]), float.Parse(sPosition[1]), float.Parse(sPosition[2]) + 35.0f);

        entity.Teleport(
            vPosition,
            player.PlayerPawn.Value.AbsRotation,
            player.PlayerPawn.Value.AbsVelocity
        );

        if (marker != null)
            remove_marker(marker);

        marker = draw_marker(vPosition.X, vPosition.Y, vPosition.Z);

        timer_icon = AddTimer(0.2f, () =>
        {
            if (GeriSayim > 0) return;

            foreach (var p in Utilities.GetPlayers())
                if (p.is_valid_alive() && p.TeamNum == 2 && !Dokundu[p])
                {
                    var position = p.PlayerPawn.Value!.AbsOrigin;

                    var farkx = position.X - float.Parse(sPosition[0]);
                    if (farkx < 0.0)
                        farkx = float.Parse(sPosition[0]) - position.X;

                    var farky = position.Y - float.Parse(sPosition[1]);
                    if (farky < 0.0)
                        farky = float.Parse(sPosition[1]) - position.Y;

                    var farkz = position.Z - float.Parse(sPosition[2]);
                    if (farkz < 0.0)
                        farkz = float.Parse(sPosition[2]) - position.Z;

                    if (farkx <= Config.algilama_mesafesi && farky <= Config.algilama_mesafesi && (farkz <= Config.algilama_mesafesi + 20.0))
                    {
                        Toplam++;
                        Dokundu[p] = true;

                        var Fark = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - UnixBaslangic;
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_finish_game", p.PlayerName, Fark, Toplam]);
                    }
                }
        }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
    }

    public void EntityKaldir()
    {
        if (entity != null && entity.IsValid)
            entity.Remove();

        if (timer_icon != null)
            timer_icon?.Kill();

        timer_icon = null;
        entity = null;
    }

    public int[] draw_marker(float X, float Y, float Z)
    {
        Z -= 35.0f;

        Vector mid = new Vector(X, Y, Z);

        int lines = 50;
        int[] ent = new int[lines];

        float step = (float)(2.0f * Math.PI) / (float)lines;
        float r = Config.algilama_mesafesi;

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

    public void Freeze(CCSPlayerController? player)
    {
        if (player.is_valid_alive())
        {
            var playerpawn = player!.PlayerPawn.Value;
            if (playerpawn == null) return;

            playerpawn.MoveType = MoveType_t.MOVETYPE_NONE;
            Schema.SetSchemaValue(playerpawn.Handle, "CBaseEntity", "m_nActualMoveType", 0);
            Utilities.SetStateChanged(playerpawn, "CBaseEntity", "m_MoveType");
        }
    }

    public void UnFreeze(CCSPlayerController? player)
    {
        if (player.is_valid_alive())
        {
            var playerpawn = player!.PlayerPawn.Value;
            if (playerpawn == null) return;

            playerpawn.MoveType = MoveType_t.MOVETYPE_WALK;
            Schema.SetSchemaValue(playerpawn.Handle, "CBaseEntity", "m_nActualMoveType", 2);
            Utilities.SetStateChanged(playerpawn, "CBaseEntity", "m_MoveType");
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