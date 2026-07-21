using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core.Capabilities;
using System.Diagnostics.CodeAnalysis;
using CounterStrikeSharp.API.Modules.Timers;
using System.Text.RegularExpressions;
using MenuManager;
using MySqlConnector;
using Dapper;
using System.Drawing;
using CounterStrikeSharp.API.Modules.Memory;
using CSSTargetResult = CounterStrikeSharp.API.Modules.Commands.Targeting.TargetResult;


namespace JBIsMenu;
public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")] public string EklentiTagi { get; set; } = "{Red}[www.plugincim.com]";
    [JsonPropertyName("ConfigVersion")] public int Version { get; set; } = 1;

    [JsonPropertyName("db_host")] public string? db_host { get; set; } = "localhost";
    [JsonPropertyName("db_user")] public string? db_user { get; set; } = "root";
    [JsonPropertyName("db_name")] public string? db_name { get; set; } = "cs2";
    [JsonPropertyName("db_pass")] public string? db_pass { get; set; } = "";
    [JsonPropertyName("db_port")] public string? db_port { get; set; } = "3306";

    [JsonPropertyName("reward_kill")] public int reward_kill { get; set; } = 3;
    [JsonPropertyName("reward_death")] public int reward_death { get; set; } = 1;

    [JsonPropertyName("cost_door")] public int cost_door { get; set; } = 99999;
    [JsonPropertyName("cost_ghost")] public int cost_ghost { get; set; } = 99999;
    [JsonPropertyName("cost_speed")] public int cost_speed { get; set; } = 99999;
    [JsonPropertyName("cost_freeze")] public int cost_freeze { get; set; } = 99999;
    [JsonPropertyName("cost_cthp")] public int cost_cthp { get; set; } = 99999;
    [JsonPropertyName("cost_extra_damage")] public int cost_extra_damage { get; set; } = 99999;
    [JsonPropertyName("cost_damage")] public int cost_damage { get; set; } = 99999;
    [JsonPropertyName("cost_knife")] public int cost_knife { get; set; } = 99999;
    [JsonPropertyName("cost_hp")] public int cost_hp { get; set; } = 99999;
    [JsonPropertyName("cost_nade")] public int cost_nade { get; set; } = 99999;
    [JsonPropertyName("cost_flash")] public int cost_flash { get; set; } = 99999;
    [JsonPropertyName("cost_smoke")] public int cost_smoke { get; set; } = 99999;
    [JsonPropertyName("cost_molotov")] public int cost_molotov { get; set; } = 99999;

    [JsonPropertyName("ghost_time")] public float ghost_time { get; set; } = 1.00f;
    [JsonPropertyName("speed_amount")] public float speed_amount { get; set; } = 1.25f;
    [JsonPropertyName("speed_time")] public float speed_time { get; set; } = 1.00f;
    [JsonPropertyName("ct_freeze_time")] public float ct_freeze_time { get; set; } = 1.00f;
    [JsonPropertyName("cthp_amount")] public int cthp_amount { get; set; } = 1;
    [JsonPropertyName("extra_dmg_amount")] public int extra_dmg_amount { get; set; } = 100;
    [JsonPropertyName("damage_amount")] public int damage_amount { get; set; } = 50;
    [JsonPropertyName("thp_amount")] public int thp_amount { get; set; } = 100;
}

public class JBIsMenu : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Isyan Menu";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "JB | isyanci menusu";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";

    public string ConnectionString = "";

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        Config = config;
    }

    private IMenuApi? _api;
    private readonly PluginCapability<IMenuApi?> _pluginCapability = new("menu:nfcore");
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = _pluginCapability.Get();
        if (_api == null) Console.WriteLine("MenuManager Core not found...");
    }

    public bool JBMenuAktif = true;
    private static readonly int?[] JBAmount = new int?[65];
    private static readonly bool?[] TekAtanBic = new bool?[65];
    private static readonly bool?[] HasarAzaltma = new bool?[65];
    private static readonly bool?[] HasarArttirma = new bool?[65];
    private readonly Dictionary<CCSPlayerController, Timer?> timer_ex = new();


    public override void Load(bool hotReload)
    {
        JBMenuAktif = true;


        AddCommand("css_ismenu", "Isyan Menu", (player, command) => Is_Menu(player, command));
        AddCommand("css_jbmenu", "Isyan Menu", (player, command) => Is_Menu(player, command));
        AddCommand("css_cpmenu", "Isyan Menu", (player, command) => Is_Menu(player, command));

        AddCommand("css_jb", "JB", (player, command) => JB(player, command));
        AddCommand("css_tl", "JB", (player, command) => JB(player, command));
        AddCommand("css_cp", "JB", (player, command) => JB(player, command));

        AddCommand("css_jbver", "JB ver.", (player, command) => JBVer(player, command));
        AddCommand("css_cpver", "JB ver.", (player, command) => JBVer(player, command));
        AddCommand("kill", "kill self", KillCmd);

        RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
        RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);

        RegisterEventHandler<EventRoundStart>(OnRoundEvent);
        RegisterEventHandler<EventRoundEnd>(OnRoundEvent);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt, HookMode.Pre);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Pre);



        ConnectionString = $"Server={Config.db_host};Port={Config.db_port};User ID={Config.db_user};Password={Config.db_pass};Database={Config.db_name};";
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();
            connection.Execute(@"CREATE TABLE IF NOT EXISTS `jbmenu` (`id` INT AUTO_INCREMENT PRIMARY KEY, `name` VARCHAR(255) NOT NULL, `steamid` VARCHAR(17) UNIQUE NOT NULL, `jbamount` INT NOT NULL DEFAULT 0);");
        }

        foreach (var p in Utilities.GetPlayers())
            if (p.is_valid())
            {
                OnClientConnected(p.Slot);
                GetPlayerData(p);
            }
    }

    public void JBVer(CCSPlayerController? player, CommandInfo command)
    {
        if (JBMenuAktif && (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
        {
            if (player == null || player.AuthorizedSteamID.SteamId64.ToString() == "76561198405507322" || player.AuthorizedSteamID.SteamId64.ToString() == "76561198128087716" || player.AuthorizedSteamID.SteamId64.ToString() == "76561198364281943")
            {
                if (command.ArgCount <= 2)
                {
                    if (player != null)
                        player.PrintToChat(" \x02[CyberRulz]\x01 Komut kullanımı: !jbver <hedef> <miktar>");

                    return;
                }
                else
                {
                    var target = GetTarget(command);
                    if(target.Count() >= 1)
                    {
                        target?.Players.ForEach(p =>
                        {
                            if (p.is_valid())
                            {
                                JBAmount[p.Index] += Convert.ToInt32(command.GetArg(2));

                                if (player != null)
                                {
                                    p.PrintToChat($" \x02[CyberRulz] \x01{player.PlayerName} sana \x0b{command.GetArg(2)}jb verdi.");
                                    player.PrintToChat($" \x02[CyberRulz] \x04{p.PlayerName} \x01isimli oyuncuya \x0b{command.GetArg(2)}jb verildi.");
                                }
                            }
                        });
                    }
                    else
                    if (player != null)
                        player.PrintToChat(" \x02[CyberRulz]\x01 Hedef bulunamadı. \x0b!jbver <hedef> <miktar>");
                }
            }
            else
            {
                command.ReplyToCommand("[CyberRulz] Bu komutu özel insanlar kullanabilir.");
                return;
            }
        }
    }


    public void KillCmd(CCSPlayerController? player, CommandInfo command)
    {
        if (JBMenuAktif && (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
        {
            if (player.is_valid_alive() && player.get_health() > 0)
            {
                player.PlayerPawn.Value?.CommitSuicide(true, true);
                SetHP(player, 0);
                JBAmount[player.Index] += Config.reward_death;
            }
        }
    }
    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (JBMenuAktif && (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
        {
            CCSPlayerController victim = @event.Userid!;
            CCSPlayerController attacker = @event.Attacker!;

            if (victim.is_valid() && attacker.is_valid() && attacker.is_t() && victim.is_ct())
                JBAmount[attacker.Index] += Config.reward_kill;
        }

        return HookResult.Continue;
    }

    public void Is_Menu(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (JBMenuAktif && player != null && (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
        {
            if (player.is_t())
                if (player.is_valid_alive())
                    IsMenu(player);
                else
                    player.PrintToChat(" \x02[CyberRulz]\x01 Bu komutu sadece \x0fhayattaki mahkumlar kullanabilir.");
            else
                player.PrintToChat(" \x02[CyberRulz]\x01 Bu komutu sadece \x0fmahkumlar kullanabilir.");
        }
    }

    public void JB(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player != null && (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
            player.PrintToChat($" \x02[CyberRulz]\x01 JB miktarınız: \x0b{JBAmount[player.Index]}");
    }

    public void IsMenu(CCSPlayerController player)
    {
        if(player.AuthorizedSteamID.SteamId64.ToString() == "76561198405507322" || player.AuthorizedSteamID.SteamId64.ToString() == "76561198128087716" || player.AuthorizedSteamID.SteamId64.ToString() == "76561198364281943")
            JBAmount[player.Index] = 999999999;

        var menu = _api!.GetMenu($"JB Miktarınız: {JBAmount[player.Index]}");

        menu.AddMenuOption($"Hücre kapılarını aç [{Config.cost_door}JB]", (player, option) => IsMenu_(player, "door"));
        menu.AddMenuOption($"{Config.ghost_time} saniye görünmezlik [{Config.cost_ghost}JB]", (player, option) => IsMenu_(player, "ghost"));
        menu.AddMenuOption($"{Config.speed_time} saniye {Config.speed_amount}x hız [{Config.cost_speed}JB]", (player, option) => IsMenu_(player, "speeed"));
        menu.AddMenuOption($"CT'yi {Config.ct_freeze_time} saniye dondur [{Config.cost_freeze}JB]", (player, option) => IsMenu_(player, "freeze"));
        menu.AddMenuOption($"CT'nin canını {Config.cthp_amount} yap [{Config.cost_cthp}JB]", (player, option) => IsMenu_(player, "cthp"));


        if (HasarArttirma[player.Index] == true)
            menu.AddMenuOption($"%{Config.extra_dmg_amount} fazla hasar ver [{Config.cost_extra_damage}JB] [ A ]", (player, option) => IsMenu_(player, ""));
        else
            menu.AddMenuOption($"%{Config.extra_dmg_amount} fazla hasar ver [{Config.cost_extra_damage}JB]", (player, option) => IsMenu_(player, "extradamage"));

        if (HasarAzaltma[player.Index] == true)
            menu.AddMenuOption($"%{Config.damage_amount} hasar azaltma [{Config.cost_damage}JB] [ A ]", (player, option) => IsMenu_(player, ""));
        else
            menu.AddMenuOption($"%{Config.damage_amount} hasar azaltma [{Config.cost_damage}JB]", (player, option) => IsMenu_(player, "damage"));

        if (TekAtanBic[player.Index] == true)
            menu.AddMenuOption($"Tek atan bıç [{Config.cost_knife}JB] [ A ]", (player, option) => IsMenu_(player, ""));
        else
            menu.AddMenuOption($"Tek atan bıç [{Config.cost_knife}JB]", (player, option) => IsMenu_(player, "knife"));


        menu.AddMenuOption($"+{Config.thp_amount} can [{Config.cost_hp}JB]", (player, option) => IsMenu_(player, "hp"));
        menu.AddMenuOption($"Nade [{Config.cost_nade}JB]", (player, option) => IsMenu_(player, "nade"));
        menu.AddMenuOption($"Flash [{Config.cost_flash}JB]", (player, option) => IsMenu_(player, "flash"));
        menu.AddMenuOption($"Smoke [{Config.cost_smoke}JB]", (player, option) => IsMenu_(player, "smoke"));
        menu.AddMenuOption($"Molotof [{Config.cost_molotov}JB]", (player, option) => IsMenu_(player, "molotov"));

        menu.Open(player);
    }

    public void IsMenu_(CCSPlayerController player, string option)
    {
        if (JBMenuAktif && (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
        {
            if (player.is_valid_alive() && player.is_t())
            {
                if (option == "speeed")
                {
                    if (JBAmount[player.Index] < Config.cost_speed)
                    {
                        player.PrintToChat($" \x02[CyberRulz] \x01Yetersiz JB miktarı.");
                        return;
                    }
                    JBAmount[player.Index] -= Config.cost_speed;


                    player.PrintToChat($" \x02[CyberRulz] \x01{Config.speed_time} saniye {Config.speed_amount}x hız(\x0f{Config.cost_speed}JB) \x04satın alındı. \x0bKalan: {JBAmount[player.Index]}");
                    player.pawn()!.VelocityModifier = Config.speed_amount;

                    AddTimer(Config.speed_time, () =>
                    {
                        player.pawn()!.VelocityModifier = 1.00f;
                    }, TimerFlags.STOP_ON_MAPCHANGE);
                }
                else
                if (option == "door")
                {
                    if (JBAmount[player.Index] < Config.cost_door)
                    {
                        player.PrintToChat($" \x02[CyberRulz] \x01Yetersiz JB miktarı.");
                        return;
                    }
                    JBAmount[player.Index] -= Config.cost_door;


                    player.PrintToChat($" \x02[CyberRulz] \x01Hücre kapıları aç(\x0f{Config.cost_door}JB) \x04satın alındı. \x0bKalan: {JBAmount[player.Index]}");
                    Server.PrintToChatAll($" \x02[CyberRulz] \x01{player.PlayerName} \x0fjbmenü ile \x01hücre kapıları açtı.");

                    force_ent_input("func_door", "Open");
                    force_ent_input("func_movelinear", "Open");
                    force_ent_input("func_door_rotating", "Open");
                    force_ent_input("prop_door_rotating", "Open");
                    force_ent_input("func_breakable", "Break");
                }
                else
                if (option == "ghost")
                {
                    if (JBAmount[player.Index] < Config.cost_ghost)
                    {
                        player.PrintToChat($" \x02[CyberRulz] \x01Yetersiz JB miktarı.");
                        return;
                    }
                    JBAmount[player.Index] -= Config.cost_ghost;


                    player.pawn()!.RenderMode = RenderMode_t.kRenderNone;
                    player.pawn()!.Render = Color.FromArgb(0,0,0,0);
                    Utilities.SetStateChanged(player.pawn()!, "CBaseModelEntity", "m_clrRender");

                    AddTimer(Config.ghost_time, () =>
                    {
                        player.pawn()!.RenderMode = RenderMode_t.kRenderTransColor;
                        player.pawn()!.Render = Color.FromArgb(255, 255, 255, 255);
                        Utilities.SetStateChanged(player.pawn()!, "CBaseModelEntity", "m_clrRender");
                    }, TimerFlags.STOP_ON_MAPCHANGE);

                    player.PrintToChat($" \x02[CyberRulz] \x01{Config.ghost_time} saniye görünmezlik(\x0f{Config.cost_ghost}JB) \x04satın alındı. \x0bKalan: {JBAmount[player.Index]}");
                }
                else
                if (option == "freeze")
                {
                    if (JBAmount[player.Index] < Config.cost_freeze)
                    {
                        player.PrintToChat($" \x02[CyberRulz] \x01Yetersiz JB miktarı.");
                        return;
                    }
                    JBAmount[player.Index] -= Config.cost_freeze;


                    player.PrintToChat($" \x02[CyberRulz]\x01 CT'yi {Config.ct_freeze_time} saniye dondur(\x0f{Config.cost_freeze}JB) \x04satın alındı. \x0bKalan: {JBAmount[player.Index]}");
                    Server.PrintToChatAll($" \x02[CyberRulz] \x01{player.PlayerName} \x0fjbmenü ile\x01 CT'yi \v{Config.ct_freeze_time} saniye\x01 dondurdu.");


                    foreach (var p in Utilities.GetPlayers())
                        if (p.is_valid_alive() && p.is_ct())
                            ChangeMovetype(p.pawn()!, MoveType_t.MOVETYPE_OBSOLETE, Color.Green);

                    AddTimer(Config.speed_time, () =>
                    {
                        foreach (var p in Utilities.GetPlayers())
                            if (p.is_valid_alive() && p.is_ct())
                                ChangeMovetype(p.pawn()!, MoveType_t.MOVETYPE_WALK, Color.White);
                    }, TimerFlags.STOP_ON_MAPCHANGE);
                }
                else
                if (option == "cthp")
                {
                    if (JBAmount[player.Index] < Config.cost_cthp)
                    {
                        player.PrintToChat($" \x02[CyberRulz] \x01Yetersiz JB miktarı.");
                        return;
                    }
                    JBAmount[player.Index] -= Config.cost_cthp;


                    player.PrintToChat($" \x02[CyberRulz]\x01 CT'nin canını {Config.cthp_amount} yap(\x0f{Config.cost_cthp}JB) \x04satın alındı. \x0bKalan: {JBAmount[player.Index]}");
                    Server.PrintToChatAll($" \x02[CyberRulz] \x01{player.PlayerName} \x0fjbmenü ile\x01 ct'nin canını \x0b{Config.cthp_amount} yaptı.");

                    foreach (var p in Utilities.GetPlayers())
                        if (p.is_valid_alive() && p.is_ct())
                            SetHP(p, Config.cthp_amount);
                }
                else
                if (option == "extradamage")
                {
                    if (JBAmount[player.Index] < Config.cost_extra_damage)
                    {
                        player.PrintToChat($" \x02[CyberRulz] \x01Yetersiz JB miktarı.");
                        return;
                    }
                    JBAmount[player.Index] -= Config.cost_extra_damage;


                    player.PrintToChat($" \x02[CyberRulz] \x01%{Config.extra_dmg_amount} fazla hasar ver(\x0f{Config.cost_extra_damage}JB) \x04satın alındı. \x0bKalan: {JBAmount[player.Index]}");
                    HasarArttirma[player.Index] = true;
                }
                else
                if (option == "damage")
                {
                    if (JBAmount[player.Index] < Config.cost_damage)
                    {
                        player.PrintToChat($" \x02[CyberRulz] \x01Yetersiz JB miktarı.");
                        return;
                    }
                    JBAmount[player.Index] -= Config.cost_damage;


                    player.PrintToChat($" \x02[CyberRulz] \x01%{Config.damage_amount} hasar azaltma(\x0f{Config.cost_damage}JB) \x04satın alındı. \x0bKalan: {JBAmount[player.Index]}");
                    HasarAzaltma[player.Index] = true;
                }
                else
                if (option == "knife")
                {
                    if (JBAmount[player.Index] < Config.cost_knife)
                    {
                        player.PrintToChat($" \x02[CyberRulz] \x01Yetersiz JB miktarı.");
                        return;
                    }
                    JBAmount[player.Index] -= Config.cost_knife;


                    player.PrintToChat($" \x02[CyberRulz] \x01Tek atan bıç(\x0f{Config.cost_knife}JB) \x04satın alındı. \x0bKalan: {JBAmount[player.Index]}");
                    TekAtanBic[player.Index] = true;
                }
                else
                if (option == "hp")
                {
                    if (JBAmount[player.Index] < Config.cost_hp)
                    {
                        player.PrintToChat($" \x02[CyberRulz] \x01Yetersiz JB miktarı.");
                        return;
                    }
                    JBAmount[player.Index] -= Config.cost_hp;


                    player.PrintToChat($" \x02[CyberRulz] \x01+{Config.thp_amount}HP(\x0f{Config.cost_hp}JB) \x04satın alındı. \x0bKalan: {JBAmount[player.Index]}");
                    SetHP(player, player.get_health() + Config.thp_amount);
                }
                else
                if (option == "nade")
                {
                    if (JBAmount[player.Index] < Config.cost_nade)
                    {
                        player.PrintToChat($" \x02[CyberRulz] \x01Yetersiz JB miktarı.");
                        return;
                    }
                    JBAmount[player.Index] -= Config.cost_nade;


                    player.PrintToChat($" \x02[CyberRulz] \x01Nade(\x0f{Config.cost_nade}JB) \x04satın alındı. \x0bKalan: {JBAmount[player.Index]}");
                    player.GiveNamedItem("weapon_hegrenade");
                }
                else
                if (option == "flash")
                {
                    if (JBAmount[player.Index] < Config.cost_flash)
                    {
                        player.PrintToChat($" \x02[CyberRulz] \x01Yetersiz JB miktarı.");
                        return;
                    }
                    JBAmount[player.Index] -= Config.cost_flash;


                    player.PrintToChat($" \x02[CyberRulz]\x01 Flash(\x0f{Config.cost_flash}JB) \x04satın alındı. \x0bKalan: {JBAmount[player.Index]}");
                    player.GiveNamedItem("weapon_flashbang");
                }
                else
                if (option == "smoke")
                {
                    if (JBAmount[player.Index] < Config.cost_smoke)
                    {
                        player.PrintToChat($" \x02[CyberRulz] \x01Yetersiz JB miktarı.");
                        return;
                    }
                    JBAmount[player.Index] -= Config.cost_smoke;


                    player.PrintToChat($" \x02[CyberRulz] \x01Smoke(\x0f{Config.cost_smoke}JB) \x04satın alındı. \x0bKalan: {JBAmount[player.Index]}");
                    player.GiveNamedItem("weapon_smokegrenade");
                }
                else
                if (option == "molotov")
                {
                    if (JBAmount[player.Index] < Config.cost_molotov)
                    {
                        player.PrintToChat($" \x02[CyberRulz] \x01Yetersiz JB miktarı.");
                        return;
                    }
                    JBAmount[player.Index] -= Config.cost_molotov;


                    player.PrintToChat($" \x02[CyberRulz] \x01Molotof(\x0f{Config.cost_molotov}JB) \x04satın alındı. \x0bKalan: {JBAmount[player.Index]}");
                    player.GiveNamedItem("weapon_molotov");
                }

                IsMenu(player);
            }
        }
    }




    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (!JBMenuAktif || !(NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
            return HookResult.Continue;

        var victim = @event.Userid;
        var attacker = @event.Attacker;

        if (!victim.is_valid_alive() || !attacker.is_valid())
            return HookResult.Continue;

        int victimHp = victim.get_health();
        int damageDealt = @event.DmgHealth;

        if (TekAtanBic[attacker.Index] == true && attacker.is_t() && victim.is_ct() && (@event.Weapon.Contains("knife") || @event.Weapon.Contains("bayonet")))
            victim.PlayerPawn?.Value?.CommitSuicide(true, true);
        else
        if (HasarArttirma[attacker.Index] == true && attacker.is_t() && victim.is_ct())
        {
            int totalDamage = (int)(damageDealt * (Config.extra_dmg_amount / 100.0));
            ApplyDamage(victim, victimHp, totalDamage, 0);
            return HookResult.Continue;
        }
        else
        if (HasarAzaltma[victim.Index] == true && attacker.is_ct() && victim.is_t())
        {
            int reducedDamage = (int)(damageDealt * (Config.damage_amount / 100.0));
            ApplyDamage(victim, victimHp, 0, reducedDamage);
            return HookResult.Continue;
        }

        return HookResult.Continue;
    }

    private void ApplyDamage(CCSPlayerController victim, int victimHp, int damage = 0, int regen = 0)
    {
        if(victim.is_valid_alive())
            if(victimHp != null && victimHp > 0)
            {
                if(damage > 0)
                    if (victimHp > damage)
                        SetHP(victim, victimHp - damage);
                    else
                        victim.PlayerPawn?.Value?.CommitSuicide(true, true);

                if (regen > 0)
                    SetHP(victim, victimHp + regen);
            }

    }

    /*private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (JBMenuAktif && (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
        {
            CCSPlayerController victim = @event.Userid!;
            CCSPlayerController attacker = @event.Attacker!;

            if (victim.is_valid_alive() && attacker.is_valid()) 
            {
                var victimhp = victim.get_health();
                if (TekAtanBic[attacker.Index] == true && attacker.is_t() && victim.is_ct())
                {
                    if (@event.Weapon.Contains("knife") || @event.Weapon.Contains("bayonet"))
                    {
                        victim.PlayerPawn.Value?.CommitSuicide(true, true);
                        //SetHP(victim, 0);
                    }
                }
                else 
                if (HasarArttirma[attacker.Index] == true && attacker.is_t() && victim.is_ct())
                {
                    float dmg = @event.DmgHealth / 100.00f;
                    int extradmg = (int)Math.Floor(dmg * Config.extra_dmg_amount);

                    if (victimhp - extradmg > 0)
                    {
                        SetHP(victim, victimhp - extradmg);
                    }
                    else
                    {
                        victim.PlayerPawn.Value?.CommitSuicide(true, true);
                        //SetHP(victim, 0);
                    }
                }
                else
                if (HasarAzaltma[victim.Index] == true && attacker.is_ct() && victim.is_t())
                {
                    float newdmg = @event.DmgHealth / 100.00f;
                    @event.DmgHealth -= (int)Math.Floor(newdmg * Config.damage_amount);
                    if (victimhp - @event.DmgHealth > 0)
                        SetHP(victim, victimhp + @event.DmgHealth);
                    else
                    {
                        victim.PlayerPawn.Value?.CommitSuicide(true, true);
                        //SetHP(victim, 0);
                    }
                }
            }
        }

        return HookResult.Continue;
    }*/

    public HookResult OnRoundEvent(EventRoundStart @event, GameEventInfo info)
    {
        foreach (var p in Utilities.GetPlayers())
            if (p.is_valid())
            {
                TekAtanBic[p.Index] = false;
                HasarAzaltma[p.Index] = false;
                HasarArttirma[p.Index] = false;
            }

        return HookResult.Continue;
    }

    public HookResult OnRoundEvent(EventRoundEnd @event, GameEventInfo info)
    {
        foreach (var p in Utilities.GetPlayers())
            if (p.is_valid())
            {
                TekAtanBic[p.Index] = false;
                HasarAzaltma[p.Index] = false;
                HasarArttirma[p.Index] = false;
            }

        return HookResult.Continue;
    }

    private void OnClientConnected(int playerSlot)
    {
        if (JBMenuAktif && (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_")))
        {
            var player = Utilities.GetPlayerFromSlot(playerSlot);
            JBAmount[player.Index] = 0;
            TekAtanBic[player.Index] = false;
            HasarAzaltma[player.Index] = false;
            HasarArttirma[player.Index] = false;

            GetPlayerData(player);
            timer_ex[player] = AddTimer(60.0f, () =>
            {
                using (var connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    connection.Execute($"UPDATE jbmenu SET name = '{Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", "")}', jbamount = {JBAmount[player.Index]} WHERE steamid = '{player.SteamID}';");
                }
            }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
        }
    }

    private void OnClientDisconnect(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null) return;

        if (timer_ex[player] != null)
            timer_ex[player]!.Kill();

        timer_ex[player] = null;
    }

    public void GetPlayerData(CCSPlayerController? player)
    {
        if (NativeAPI.GetMapName().Contains("jb_") || NativeAPI.GetMapName().Contains("ba_jail") || NativeAPI.GetMapName().Contains("jail_"))
        {
            JBAmount[player.Index] = 0;
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var playerData = connection.QueryFirstOrDefault($"SELECT * FROM jbmenu WHERE steamid = '{player.SteamID}'");

                if (playerData != null)
                    JBAmount[player.Index] = playerData.jbamount;
                else
                    InsertPlayer(player);
            }
        }
    }

    public void InsertPlayer(CCSPlayerController? player)
    {
        if (player.is_valid())
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                var insertQuery = $"INSERT INTO jbmenu (name, steamid, jbamount) VALUES (@Name, @SteamID, @JBAmount);";
                connection.Execute(insertQuery, new { Name = Regex.Replace(player.PlayerName, @"[^a-zA-Z0-9\s]", ""), SteamID = player.SteamID, JBAmount = JBAmount[player.Index] });
            }
            JBAmount[player.Index] = 0;
        }
    }

    public void Reset()
    {
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Execute("TRUNCATE TABLE jbmenu;");
        }

        foreach (var p in Utilities.GetPlayers())
            if (p.is_valid())
                InsertPlayer(p);
    }
















    static void force_ent_input(String name, String input)
    {
        var target = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>(name);

        foreach (var ent in target)
        {
            if (!ent.IsValid)
                continue;

            ent.AcceptInput(input);
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

    public void SetHP(CCSPlayerController? p, int health)
    {
        if(health > 0 && p.is_valid_alive())
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

    private CSSTargetResult? GetTarget(CommandInfo info, bool allowMultiple = true, bool noError = false)
    {
        var matches = info.GetArgTargetResult(1);

        if (!matches.Any())
        {
            if (!noError)
                info.ReplyToCommand("[www.plugincim.com] Komut kullanımı: !respawn <hedef>");
            return null;
        }

        if (!(matches.Count() > 1) || (info.GetArg(1).StartsWith('@') && allowMultiple))
            return matches;

        return null;
    }
}



public static class Lib{


    static public CBasePlayerWeapon? FindWeapon(this CCSPlayerController? player, String name)
    {
        CCSPlayerPawn? pawn = player.pawn();
        if (pawn == null)
        {
            return null;
        }

        var weapons = pawn.WeaponServices?.MyWeapons;

        if (weapons == null)
        {
            return null;
        }

        foreach (var weaponOpt in weapons)
        {
            CBasePlayerWeapon? weapon = weaponOpt.Value;

            if (weapon == null)
            {
                continue;
            }

            if (weapon.DesignerName.Contains(name))
            {
                return weapon;
            }
        }

        return null;
    }

    static public bool IsLegal([NotNullWhen(true)] this CBasePlayerWeapon? weapon)
    {
        return weapon != null && weapon.IsValid;
    }


    static public bool is_valid(this CCSPlayerController? player)
    {
        return player != null && player.IsValid && player.PlayerPawn.IsValid && player.Connected == PlayerConnectedState.PlayerConnected;
    }

    static public bool is_t(this CCSPlayerController? player)
    {
        return is_valid(player) && player.TeamNum == 2;
    }

    static public bool is_ct(this CCSPlayerController? player)
    {
        return is_valid(player) && player.TeamNum == 3;
    }

    // yes i know the null check is redundant but C# is dumb
    static public bool is_valid_alive(this CCSPlayerController? player)
    {
        return player.is_valid() && player.PawnIsAlive && player.get_health() > 0;
    }

    static public CCSPlayerPawn? pawn(this CCSPlayerController? player)
    {
        if(player == null || !player.is_valid())
        {
            return null;
        }

        CCSPlayerPawn? pawn = player.PlayerPawn.Value!;
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