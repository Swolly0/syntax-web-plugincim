using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Core.Capabilities;
using MenuManager;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using CounterStrikeSharp.API.Modules.Timers;
using static CounterStrikeSharp.API.Core.Listeners;
using System.Drawing;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Cvars;

public class KurtAvi : BasePlugin
{
    public override string ModuleName => "Kurt Avı Modu";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleDescription => "Wolfteam Kurt Avı Modu";
    public override string ModuleAuthor => "https://plugincim.com/";

    private static bool KurtAviAktif = false;
    private static Dictionary<CCSPlayerController, int> CanlanmaHakki = new();
    private static Dictionary<CCSPlayerController, bool> MainKurt = new();
    private static readonly Random rnd = new Random();

    private IMenuApi? _api;
    private readonly PluginCapability<IMenuApi?> _pluginCapability = new("menu:nfcore");

    public CounterStrikeSharp.API.Modules.Timers.Timer? timer_ex;


    public int MainKurtHp = 1000;
    public int MainKurtMaxHp = 1500;
    public int EnfekteKurtHp = 750;
    public int EnfekteKurtMaxHp = 1000;
    public int InsanHp = 150;
    public int InsanMaxHp = 150;
    public float KurtCanCalma = 1.25f;
    public float InsanCanCalma = 0.10f;
    public static float MainKurtSpeed = 1.9f;
    public static float EnfekteKurtSpeed = 1.6f;
    public static float InsanSpeed = 1.15f;
    public static float MainKurtGravity = 0.60f;
    public static float EnfekteKurtGravity = 0.70f;
    public static float InsanGravity = 0.85f;

    public static int tickCount = 0;
    public const int tickInterval = 7;

    // ses efekti
    // model
    // ekran karart

    public override void Load(bool hotReload)
    {
        AddCommand("css_kurtavi", "Kurt Avı modunu başlat", (player, command) => StartKurtAvi(player));
        AddCommand("css_kurtavi0", "Kurt Avı modunu sıfırla", (player, command) => EndKurtAvi(player));
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt, HookMode.Pre);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnect);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        AddCommandListener("jointeam", OnPlayerChangeTeam);

        RegisterListener<OnServerPrecacheResources>(OnServerPrecacheResources);
        KurtAviAktif = false;
    }
    public static void OnServerPrecacheResources(ResourceManifest manifest)
    {
        manifest.AddResource(@"characters/models/nozb1/muscular_doge_player_model/muscular_doge_player_model.vmdl");
        manifest.AddResource(@"characters/models/nozb1/muscular_doge_player_model/muscular_doge_arm.vmdl");
        manifest.AddResource(@"characters/models/nozb1/kaiju_8_player_model/kaiju_8_player_model.vmdl");
    }

    public override void Unload(bool hotReload)
    {
        RemoveCommand("css_kurtavi", (player, command) => StartKurtAvi(player));
        RemoveCommand("css_kurtavi0", (player, command) => StartKurtAvi(player));
        RemoveCommandListener("jointeam", OnPlayerChangeTeam, HookMode.Pre);

        RemoveListener<OnServerPrecacheResources>(OnServerPrecacheResources);
        RemoveListener<OnTick>(OnTick);
    }

    public static void OnTick()
    {
        tickCount++;

        List<CCSPlayerController> players = [.. Utilities.GetPlayers().Where(p => p.PawnIsAlive)];
        foreach (CCSPlayerController? player in players)
        {
            CCSPlayerPawn? playerPawn = player.pawn();
            if (playerPawn != null)
            {
                if (player.is_ct())
                {
                    if (tickCount % tickInterval != 0)
                        continue;

                    player.PlayerPawn.Value.GravityScale = InsanGravity;
                    player.PlayerPawn.Value.VelocityModifier = InsanSpeed;
                    Utilities.SetStateChanged(player, "CCSPlayerPawn", "m_flVelocityModifier");
                }
                else
                if (player.is_t())
                {
                    PlayerFlags flags = (PlayerFlags)playerPawn.Flags;
                    PlayerButtons buttons = player.Buttons;

                    if (flags.HasFlag(PlayerFlags.FL_ONGROUND) && !playerPawn.MoveType.HasFlag(MoveType_t.MOVETYPE_LADDER))
                        if (buttons.HasFlag(PlayerButtons.Jump))
                            playerPawn.AbsVelocity.Z = 267.0f;
                        else
                        {
                            if (tickCount % tickInterval != 0)
                                continue;

                            float gravity = MainKurt.TryGetValue(player, out bool isMain1) && isMain1 ? MainKurtGravity: EnfekteKurtGravity;
                            float speed = MainKurt.TryGetValue(player, out bool isMain2) && isMain2 ? MainKurtSpeed : EnfekteKurtSpeed;

                            player.PlayerPawn.Value.GravityScale = gravity;
                            player.PlayerPawn.Value.VelocityModifier = speed;
                            Utilities.SetStateChanged(player, "CCSPlayerPawn", "m_flVelocityModifier");
                        }
                }
            }
        }
    }
    private bool IsWolfteamMap()
    {
        string mapName = NativeAPI.GetMapName();

        if (string.IsNullOrEmpty(mapName)) return false;

        return mapName.StartsWith("wolfteam_") || mapName.Contains("wt_") || mapName.Contains("wolf_");
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = _pluginCapability.Get();
        if (_api == null) Console.WriteLine("MenuManager Core not found...");
    }

    private void StartKurtAvi(CCSPlayerController? player)
    {
        /*if(player.AuthorizedSteamID.SteamId64.ToString() != "76561198405507322")
        {
            player?.PrintToChat(ReplaceTags("{Red}[CyberRulz] {White}Bu mod sadece {Red}SWOLLY'e {White}opsiyonlanmıştır."));
            return;
        }*/

        if (!IsWolfteamMap())
        {
            player?.PrintToChat(ReplaceTags("{Red}[CyberRulz] {White}Bu mod yalnızca 'wolfteam_' haritalarında çalışır."));
            return;
        }

        if (KurtAviAktif)
        {
            player?.PrintToChat(ReplaceTags("{Red}[CyberRulz] {White}Kurt Avı modu zaten aktif."));
            return;
        }

        RegisterListener<Listeners.OnTick>(OnTick);
        KurtAviAktif = true;

        // **Tüm oyuncuları CT takımına al**
        foreach (var p in Utilities.GetPlayers())
            if (p.is_valid())
            {
                if (p.TeamNum != (int)CsTeam.CounterTerrorist)
                    p.ChangeTeam(CsTeam.CounterTerrorist);

                if (p.is_valid_alive()) p.CommitSuicide(true, true);
            }

        Server.PrintToChatAll(ReplaceTags("{Red}[CyberRulz] {Green}Kurt Avı modu başladı!"));
        SetCvar(1);
    }

    private void EndKurtAvi(CCSPlayerController? player)
    {
        if (!KurtAviAktif) return;
        
        /*if (player.AuthorizedSteamID.SteamId64.ToString() != "76561198405507322")
        {
            player?.PrintToChat(ReplaceTags("{Red}[CyberRulz] {White}Bu mod sadece {Red}SWOLLY'e {White}opsiyonlanmıştır."));
            return;
        }*/

        KurtAviAktif = false;
        RemoveListener<Listeners.OnTick>(OnTick);

        CanlanmaHakki.Clear();
        MainKurt.Clear();

        // **Oyuncuları iki takıma böl**
        var players = Utilities.GetPlayers().Where(p => p.is_valid()).ToList();
        int halfCount = players.Count / 2;

        for (int i = 0; i < players.Count; i++)
        {
            if (i < halfCount)
            {
                players[i].ChangeTeam(CsTeam.Terrorist);
            }
            else
            {
                players[i].ChangeTeam(CsTeam.CounterTerrorist);
            }
        }

        if (timer_ex != null)
            timer_ex.Kill();
        timer_ex = null;

        Server.PrintToChatAll(ReplaceTags("{Red}[CyberRulz] {White}Kurt Avı modu kapatıldı."));
        SetCvar(0);
    }

    public void SetCvar(int value)
    {
        if(value == 1)
        {
            Server.ExecuteCommand("mp_death_drop_gun 0");
            Server.ExecuteCommand("mp_autoteambalance false");
            Server.ExecuteCommand("mp_teamname_1 \"INSANLAR\"");
            Server.ExecuteCommand("mp_teamname_2 \"KURTLAR\"");
            Server.ExecuteCommand("mp_t_default_primary \"\"");
            Server.ExecuteCommand("mp_t_default_secondary \"\"");
            Server.ExecuteCommand("mp_roundtime 6.0");
            Server.ExecuteCommand("mp_buytime 0.0");
            Server.ExecuteCommand("mp_freezetime 1");
            Server.ExecuteCommand("sv_alltalk true");
            Server.ExecuteCommand("mp_give_player_c4 false");
        }
        else
        {
            Server.ExecuteCommand("mp_death_drop_gun 1");
            Server.ExecuteCommand("mp_autoteambalance true");
            Server.ExecuteCommand("mp_teamname_1 CT");
            Server.ExecuteCommand("mp_teamname_2 T");
            Server.ExecuteCommand("mp_t_default_secondary weapon_glock");
            Server.ExecuteCommand("mp_roundtime 3.0");
            Server.ExecuteCommand("mp_buytime 30.0");
            Server.ExecuteCommand("mp_freezetime 5");
            Server.ExecuteCommand("sv_alltalk true");
            Server.ExecuteCommand("mp_give_player_c4 true");
        }
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (!KurtAviAktif || !IsWolfteamMap()) return HookResult.Continue;

        var players = Utilities.GetPlayers().Where(p => p.is_valid()).ToList();
        var tPlayers = players.Where(p => p.is_t()).ToList();
        var ctPlayers = players.Where(p => p.is_ct()).ToList();


        MainKurt.Clear();
        foreach (var player in tPlayers)
            player.ChangeTeam(CsTeam.CounterTerrorist);


        AddTimer(0.5f, () => {
            int kurtsNeeded = Math.Max(1, ctPlayers.Count / 5); // En az 1 kurt seç
            var selectedKurts = ctPlayers.OrderBy(x => rnd.Next()).Take(kurtsNeeded).ToList();

            foreach (var kurt in selectedKurts)
            {
                kurt.CommitSuicide(true, true);
                kurt.ChangeTeam(CsTeam.Terrorist);
                AddTimer(0.5f, () => {
                    kurt.Respawn();
                    CanlanmaHakki[kurt] = 2;
                    MainKurt[kurt] = true;
                });
            }
        });

        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (!KurtAviAktif || !IsWolfteamMap()) return HookResult.Continue;

        var CTSayisi = Utilities.GetPlayers().Where(p => p.is_valid_alive() && p.is_ct()).ToList();
        var TSayisi = Utilities.GetPlayers().Where(p => p.is_valid() && p.is_t()).ToList();
        if (CTSayisi.Count == 0)
        {
            foreach (var player in TSayisi)
                player.SwitchTeam(CsTeam.CounterTerrorist);

            return HookResult.Continue;
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (!IsWolfteamMap() || !KurtAviAktif) return HookResult.Continue;

        var player = @event.Userid;
        if (!player.is_valid()) return HookResult.Continue;

        AddTimer(0.5f, () =>
        {
            if (player.is_ct())
            {
                player.RemoveWeapons();
                AddTimer(0.65f, () => SetHumanAttributes(player, true, true, true));
            }
            else if (player.is_t())
            {
                player.RemoveWeapons();
                player.GiveNamedItem("weapon_knife");
                player.GiveNamedItem("item_assaultsuit");

                AddTimer(0.05f, () => SetWolfAttributes(player, true, true));
                AddTimer(0.25f, () => SetWolfAttributes(player, true, true));
                AddTimer(0.65f, () => SetWolfAttributes(player, true, true));
            }
        });

        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (!KurtAviAktif || !IsWolfteamMap()) return HookResult.Continue;

        var victim = @event.Userid;
        if (victim == null || !victim.is_valid()) return HookResult.Continue;

        if (victim.is_t())
            if (CanlanmaHakki.TryGetValue(victim, out int canlanma) && canlanma > 0)
            {
                CanlanmaHakki[victim]--;
                AddTimer(3.0f, () => {
                    if (victim.is_valid()) victim.Respawn();
                });

                return HookResult.Continue;
            }

        var attacker = @event.Attacker;
        if (attacker == null || !attacker.is_valid()) return HookResult.Continue;

        var CTSayisi = Utilities.GetPlayers().Where(p => p.is_valid_alive() && p.is_ct()).ToList();
        if (CTSayisi.Count == 0)
            return HookResult.Continue;

        if (attacker.is_t() && victim.is_ct())
        {
            victim.ChangeTeam(CsTeam.Terrorist);
            AddTimer(0.5f, () => {
                victim.Respawn();
                CanlanmaHakki[victim] = 1;
            });
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (!IsWolfteamMap() || !KurtAviAktif) return HookResult.Continue;

        var victim = @event.Userid;
        var attacker = @event.Attacker;

        if (!victim.is_valid() || !attacker.is_valid()) return HookResult.Continue;

        float damage = @event.DmgHealth;

        // İnsan (CT) vs Kurt (T)
        if (attacker.is_ct() && victim.is_t())
        {
            int healAmount = (int)Math.Floor(damage * InsanCanCalma);
            int newHealth = Math.Min(attacker.pawn().Health + healAmount, InsanMaxHp);
            SetHP(attacker, newHealth);


            int regenhp = (int)Math.Floor(damage * 0.25f);
            SetHP(victim, victim.pawn().Health + regenhp);
            victim.pawn().ArmorValue = 100;
        }
        // Kurt (T) vs İnsan (CT)
        else if (attacker.is_t() && victim.is_ct())
        {
            int maxHP = MainKurt.TryGetValue(attacker, out bool isMain) && isMain ? MainKurtMaxHp : EnfekteKurtMaxHp;
            int healAmount = (int)Math.Floor(damage * KurtCanCalma);
            int newHealth = Math.Min(attacker.pawn().Health + healAmount, maxHP);
            SetHP(attacker, newHealth);

            /*if (victim.Health > 75)
                SetHP(victim, victim.Health - 75);
            else
            if(victim.Health - damage > 1)
                SetHP(victim, 1);*/
        }
        return HookResult.Continue;
    }




    private void SetHumanAttributes(CCSPlayerController player, bool weapomenu = false, bool hp = false, bool model = false)
    {
        if (!player.is_valid_alive() || !player.is_ct()) return;

        if (weapomenu)
            SilahMenu(player);

        if (hp == true)
            SetHP(player, InsanHp);
    }

    private void SetWolfAttributes(CCSPlayerController player, bool hp = false, bool model = false)
    {
        if (!player.is_valid_alive() || !player.is_t()) return;

        //player.ColorScreen(Color.FromArgb(255, 0, 0, 50), 0.1f, 0.1f, Lib.FadeFlags.FADE_IN, false);
        //player.ColorScreen(Color.FromArgb(255, 0, 0, 50), 0.05f, 0.05f, Lib.FadeFlags.FADE_IN, false);

        if (hp == true)
            SetHP(player, MainKurt.TryGetValue(player, out bool isMain3) && isMain3 ? MainKurtHp : EnfekteKurtHp);

        if(model == true)
        {
            AddTimer(0.7f, () =>
            {
                if (player.is_valid_alive() && player.is_t())
                {
                    var pawn = player.pawn();
                    if (pawn == null)
                        return;


                    pawn!.SetModel(MainKurt.TryGetValue(player, out bool isMain2) && isMain2
                        ? "characters/models/nozb1/muscular_doge_player_model/muscular_doge_player_model.vmdl"
                        : "characters/models/nozb1/kaiju_8_player_model/kaiju_8_player_model.vmdl");
                }
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }
    }

    private void SilahMenu(CCSPlayerController player)
    {
        player.RemoveWeapons();

        var menu = _api!.GetMenu("Kurt Avı Silah Menüsü");
        menu.AddMenuOption("AK47", (p, option) => SilahMenu_(p, "weapon_ak47"));
        menu.AddMenuOption("M4A1", (p, option) => SilahMenu_(p, "weapon_m4a1"));
        menu.AddMenuOption("P90", (p, option) => SilahMenu_(p, "weapon_p90"));
        menu.AddMenuOption("AUG", (p, option) => SilahMenu_(p, "weapon_aug"));
        menu.AddMenuOption("AWP", (p, option) => SilahMenu_(p, "weapon_awp"));
        menu.AddMenuOption("XM1014", (p, option) => SilahMenu_(p, "weapon_xm1014"));
        menu.AddMenuOption("M249", (p, option) => SilahMenu_(p, "weapon_m249"));
        menu.Open(player);
    }

    private void SilahMenu_(CCSPlayerController player, string option)
    {
        if (!KurtAviAktif || !player.is_ct() || !player.is_valid_alive()) return;

        player.RemoveWeapons();
        player.GiveNamedItem(option);
        player.GiveNamedItem("weapon_knife");
        player.GiveNamedItem("weapon_deagle");
        player.GiveNamedItem("weapon_hegrenade");
        player.GiveNamedItem("weapon_incgrenade");
        player.GiveNamedItem("item_assaultsuit");
    }

    public void SetHP(CCSPlayerController? p, int health)
    {
        if (p.is_valid_alive())
        {
            p.Health = health;
            p.pawn()!.Health = health;
            if (health > 100)
            {
                p.MaxHealth = health;
                p.pawn()!.MaxHealth = health;
            }
            Utilities.SetStateChanged(p.pawn()!, "CBaseEntity", "m_iHealth");
        }
    }

    [GameEventHandler]
    public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        if (!IsWolfteamMap() || !KurtAviAktif) return HookResult.Continue;

        CCSPlayerController player = @event.Userid;
        if (!player.is_valid()) return HookResult.Continue;

        AddTimer(1.0f, () =>
        {
            if (!player.is_valid()) return;
            player.SwitchTeam(CsTeam.CounterTerrorist);
        });

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerChangeTeam(CCSPlayerController? player, CommandInfo command)
    {
        if (!IsWolfteamMap() || !KurtAviAktif) return HookResult.Continue;
        if (!player.is_valid()) return HookResult.Continue;

        if (!Int32.TryParse(command.ArgByIndex(1), out int team_switch))
            return HookResult.Continue;

        // Eğer oyuncu T takımında ve başka bir takıma geçmeye çalışıyorsa, T'ye geri atıyoruz.
        if (player.TeamNum == 2 && team_switch != 2)
        {
            player.SwitchTeam(CsTeam.Terrorist);
            return HookResult.Stop;
        }
        // Eğer oyuncu CT takımında ve T'ye geçmeye çalışıyorsa, CT'ye geri atıyoruz.
        else if (player.TeamNum == 3 && team_switch == 2)
        {
            player.SwitchTeam(CsTeam.CounterTerrorist);
            return HookResult.Stop;
        }
        // Eğer oyuncu izleyicideyse ve T'ye geçmeye çalışıyorsa, izleyiciye geri atıyoruz.
        else if (player.TeamNum == 1 && team_switch == 2)
        { 
            player.SwitchTeam(CsTeam.Spectator);
            return HookResult.Stop;
        }

        return HookResult.Continue;
    }

    public static void RemoveWeaponsOnTheGround()
    {
        IEnumerable<CCSWeaponBaseGun> entities = Utilities.FindAllEntitiesByDesignerName<CCSWeaponBaseGun>("weapon_");

        foreach (CCSWeaponBaseGun entity in entities)
        {
            if (!entity.IsValid)
            {
                continue;
            }

            if (entity.State != CSWeaponState_t.WEAPON_NOT_CARRIED)
            {
                continue;
            }

            if (entity.DesignerName.StartsWith("weapon_") == false)
            {
                continue;
            }
            entity.Remove();
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


public static class Lib
{
    public enum FadeFlags
    {
        FADE_IN,
        FADE_OUT,
        FADE_STAYOUT
    }

    static public void ColorScreen(this CCSPlayerController player, Color color, float hold = 0.1f, float fade = 0.2f, FadeFlags flags = FadeFlags.FADE_IN, bool withPurge = true)
    {
        var fadeMsg = UserMessage.FromPartialName("Fade");

        fadeMsg.SetInt("duration", Convert.ToInt32(fade * 512));
        fadeMsg.SetInt("hold_time", Convert.ToInt32(hold * 512));

        var flag = flags switch
        {
            FadeFlags.FADE_IN => 0x0001,
            FadeFlags.FADE_OUT => 0x0002,
            FadeFlags.FADE_STAYOUT => 0x0008,
            _ => 0x0001
        };

        if (withPurge)
        {
            flag |= 0x0010;
        }

        fadeMsg.SetInt("flags", flag);
        fadeMsg.SetInt("color", color.R | color.G << 8 | color.B << 16 | color.A << 24);
        fadeMsg.Send(player);
    }
    static public bool is_valid(this CCSPlayerController? player)
    {
        return player != null && player.IsValid && player.PlayerPawn.IsValid && player.Connected == PlayerConnectedState.PlayerConnected;
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