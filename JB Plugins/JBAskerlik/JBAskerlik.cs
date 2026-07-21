using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Memory;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBAskerlik;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}

public class JBAskerlik : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Askerlik";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!askerlik komutu ile askerlik oyunu başlatılır.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    public readonly Dictionary<CCSPlayerController, CounterStrikeSharp.API.Modules.Utils.Vector> Konum = new();
    public readonly Dictionary<CCSPlayerController, int?> Geri_Sayim = new();
    public readonly Dictionary<CCSPlayerController, Timer?> timer_ex = new();
    public readonly Dictionary<CCSPlayerController, nint?> OyuncuEntity = new();

    public CounterStrikeSharp.API.Modules.Timers.Timer? OyunTimer;
    public bool OyunAktif = false;
    public int SonKac = 0;

    public static List<CDynamicProp?> Helikopterler = new List<CDynamicProp?>();

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
            Console.WriteLine($"[Askerlik] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
            AddCommand("css_askerlik", "Askerlik oyununu baslat.", (player, command) => Askerlik(player, command));
            AddCommand("css_askerlik0", "Askerlik oyununu durdur.", (player, command) => Askerlik0(player, command));

            RegisterEventHandler<EventRoundStart>(OnRoundEvent);
            RegisterEventHandler<EventRoundEnd>(OnRoundEvent);
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Pre);

            RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
            RegisterListener<Listeners.OnServerPrecacheResources>((manifest) =>
            {
                manifest.AddResource("models/hybridphysx/news_helicoptor_map1_intro_v1.vmdl");
            });
        }
    }

    public void Askerlik(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.TeamNum == 3)
            {
                if (!OyunAktif)
                {
                    var Arg1 = command.ArgByIndex(1);

                    int canlit = 0;
                    foreach (var p in Utilities.GetPlayers())
                        if (p.is_valid_alive() && p.is_t())
                            canlit++;

                    if (Arg1 != null && Arg1 != "" && IsInt(Arg1) && canlit > Convert.ToInt32(Arg1))
                    {
                        SonKac = Convert.ToInt32(Arg1);

                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_start", player.PlayerName]);
                        OyunAktif = true;

                        if (OyunTimer != null)
                            OyunTimer!.Kill();

                        foreach (var p in Utilities.GetPlayers())
                            if (p.is_valid())
                            {
                                timer_ex[p] = null;
                                OyuncuEntity[p] = 0;
                            }

                        OyunTimer = AddTimer(1.0f, () =>
                        {
                            if (!OyunAktif)
                            {
                                OyunTimer!.Kill();
                                OyunTimer = null;

                                return;
                            }


                            if (SonKac >= canlit)
                            {
                                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_end"]);

                                Sifirla();
                                return;
                            }

                            CCSPlayerController? randomplayer = null;
                            do
                            {
                                int random = new Random().Next(1, Utilities.GetPlayers().Count());

                                foreach (var p in Utilities.GetPlayers())
                                    if (p.is_valid_alive() && p.is_t() && timer_ex[p] == null && p.UserId == random)
                                    {
                                        randomplayer = p;
                                        break;
                                    }
                            }
                            while (randomplayer == null);

                            if (SonKac == canlit - 1)
                                randomplayer.CommitSuicide(true, false);
                            else
                                OyuncuCek(randomplayer);

                        }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);

                    }
                    else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage", canlit - 1]);
                }
                else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_started"]);

            }
            else
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
                return;
            }
    }

    public void Askerlik0(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || player.TeamNum == 3)
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_stop", player!.PlayerName]);
                Sifirla();
            }
            else
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
                return;
            }
    }

    public HookResult OnRoundEvent(EventRoundStart @event, GameEventInfo info)
    {
        if (OyunAktif)
            Sifirla();

        return HookResult.Continue;
    }

    public HookResult OnRoundEvent(EventRoundEnd @event, GameEventInfo info)
    {
        if (OyunAktif)
            Sifirla();

        return HookResult.Continue;
    }
    private void OnClientConnected(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null) return;
        timer_ex[player] = null;
        OyuncuEntity[player] = null;
    }

    HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (OyunAktif && NativeAPI.GetMapName().Contains("jb_"))
        {
            CCSPlayerController player = @event.Userid;

            if (player.is_valid())
            {
                HelikopterSil(player);

                if (timer_ex[player] != null)
                    timer_ex[player]!.Kill();

                timer_ex[player] = null;
            }

            int canlit = 0;
            foreach (var p in Utilities.GetPlayers())
                if (p.is_valid_alive() && p.is_t())
                    canlit++;

            if (SonKac >= canlit)
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["game_end"]);
                Sifirla();
            }
        }

        return HookResult.Continue;
    }

    public void OyuncuCek(CCSPlayerController? player)
    {
        if (player.is_valid())
        {
            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_selected", player!.PlayerName]);

            Freeze(player);
            Konum[player] = player.PlayerPawn.Value.AbsOrigin!;
            HelikopterOlustur(player);

            Geri_Sayim[player] = 0;

            if (timer_ex[player] != null)
                timer_ex[player]!.Kill();

            timer_ex[player] = AddTimer(0.1f, () =>
            {
                Geri_Sayim[player]++;

                if (Geri_Sayim[player] == 20)
                    player.CommitSuicide(true, false);
                else
                    player.PlayerPawn.Value!.Teleport(new CounterStrikeSharp.API.Modules.Utils.Vector(Konum[player].X, Konum[player].Y, Konum[player].Z + 20.0f), new QAngle(), new CounterStrikeSharp.API.Modules.Utils.Vector());

            }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
        }
    }

    public void Sifirla()
    {
        OyunAktif = false;

        foreach (var p in Utilities.GetPlayers())
            if (p.is_valid())
            {
                if (p.is_valid_alive() && p.is_t())
                    UnFreeze(p);

                if (timer_ex[p] != null)
                    timer_ex[p]!.Kill();

                timer_ex[p] = null;
            }

        HelikopterleriTemizle();
    }




    public void HelikopterOlustur(CCSPlayerController? player)
    {
        if (player.is_valid_alive())
        {
            var entity = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic_override");
            if (entity is null || !entity.IsValid)
                return;

            entity.SetModel("models/hybridphysx/news_helicoptor_map1_intro_v1.vmdl");
            entity.IdleAnim = "hover";
            entity.Entity.Name = "Helikopter " + Helikopterler.Count() + 1;
            entity.DispatchSpawn();

            entity.Teleport(new CounterStrikeSharp.API.Modules.Utils.Vector(Konum[player].X, Konum[player].Y, Konum[player].Z + 400.0f), new QAngle(), new CounterStrikeSharp.API.Modules.Utils.Vector());

            Helikopterler.Add(entity);
            OyuncuEntity[player] = entity.Handle;
        }
    }

    public void HelikopterSil(CCSPlayerController? player)
    {
        if (player.is_valid() && OyuncuEntity[player] != null)
        {
            for (var i = Helikopterler.Count - 1; i > -1; i--)
            {
                var entity = Helikopterler[i];
                if (entity != null && entity.IsValid && entity.Handle == OyuncuEntity[player])
                {
                    entity.AcceptInput("Kill");
                    OyuncuEntity[player] = null;

                    Helikopterler.Remove(entity);
                    break;
                }
            }
        }
    }

    public void HelikopterleriTemizle()
    {
        for (var i = Helikopterler.Count - 1; i > -1; i--)
        {
            var entity = Helikopterler[i];
            if (entity != null && entity.IsValid)
                entity.AcceptInput("Kill");
        }

        Helikopterler.Clear();
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