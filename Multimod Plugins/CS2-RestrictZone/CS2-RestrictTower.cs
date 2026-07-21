using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Menu;
using System.Net;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonSerializer = System.Text.Json.JsonSerializer;
using CounterStrikeSharp.API.Modules.Timers;
using System.Drawing;

namespace Cs2RestrictZone;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
    
    [JsonPropertyName("LicenseKey")] 
    public string TmGXnexhnN { get; set; } = "YOUR_LICENSE_KEY";   

    [JsonPropertyName("detect_range")]
    public float AlgilamaMesafesi { get; set; } = 30.0f;
}

public class HaritaKonumlari
{
    public string? TowerPos { get; set; }
}

public class Cs2RestrictZone: BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "CS2-RestrictZone";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleDescription => "";
    public override string ModuleAuthor => "Plugincim.com";
    private string SbZlSxBgmc => $"{Convert.ToChar(104)}{Convert.ToChar(116)}{Convert.ToChar(116)}{Convert.ToChar(112)}{Convert.ToChar(115)}{Convert.ToChar(58)}{Convert.ToChar(47)}{Convert.ToChar(47)}{Convert.ToChar(112)}{Convert.ToChar(108)}{Convert.ToChar(117)}{Convert.ToChar(103)}{Convert.ToChar(105)}{Convert.ToChar(110)}{Convert.ToChar(99)}{Convert.ToChar(105)}{Convert.ToChar(109)}{Convert.ToChar(46)}{Convert.ToChar(99)}{Convert.ToChar(111)}{Convert.ToChar(109)}";

    private int ModuleConfigVersion => 1;
    private string ModuleProduct => "XXXX";
    
    internal static IStringLocalizer? Stringlocalizer;


    CDynamicProp? _entity;
    private HaritaKonumlari? _cfgHaritaKonumlari = new HaritaKonumlari();
    private string _configPath = "";

    int[]? _marker;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _timerEx;


    public required Config Config { get; set; }
    
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Logger.LogInformation($"[{ModuleName}] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
        }

        Config = config;
        Stringlocalizer = Localizer;
    }
	
    public override void Load(bool hotReload)
    {

        Server.NextFrame(() =>
        {
            
            if (!string.IsNullOrEmpty(Config.TmGXnexhnN))
            {
                
                string vzizzBPxaG = PPujhEuVig($"{SbZlSxBgmc}{Convert.ToChar(47)}{Convert.ToChar(97)}{Convert.ToChar(112)}{Convert.ToChar(105)}{Convert.ToChar(47)}{Convert.ToChar(108)}{Convert.ToChar(105)}{Convert.ToChar(99)}{Convert.ToChar(101)}{Convert.ToChar(110)}{Convert.ToChar(115)}{Convert.ToChar(101)}{Convert.ToChar(63)}{Convert.ToChar(107)}{Convert.ToChar(101)}{Convert.ToChar(121)}{Convert.ToChar(61)}{Config.TmGXnexhnN}{Convert.ToChar(38)}{Convert.ToChar(112)}{Convert.ToChar(114)}{Convert.ToChar(111)}{Convert.ToChar(100)}{Convert.ToChar(117)}{Convert.ToChar(99)}{Convert.ToChar(116)}{Convert.ToChar(61)}{ModuleProduct}");
                dynamic? mBbRTqtlvM = JsonConvert.DeserializeObject<dynamic>(vzizzBPxaG);
                
                if (mBbRTqtlvM?.status == "error")
                {
                        
                    Logger.LogError($"{Convert.ToChar(66)}{Convert.ToChar(117)}{Convert.ToChar(32)}{Convert.ToChar(101)}{Convert.ToChar(107)}{Convert.ToChar(108)}{Convert.ToChar(101)}{Convert.ToChar(110)}{Convert.ToChar(116)}{Convert.ToChar(105)}{Convert.ToChar(121)}{Convert.ToChar(101)}{Convert.ToChar(32)}{Convert.ToChar(97)}{Convert.ToChar(105)}{Convert.ToChar(116)}{Convert.ToChar(32)}{Convert.ToChar(108)}{Convert.ToChar(105)}{Convert.ToChar(115)}{Convert.ToChar(97)}{Convert.ToChar(110)}{Convert.ToChar(115)}{Convert.ToChar(32)}{Convert.ToChar(98)}{Convert.ToChar(117)}{Convert.ToChar(108)}{Convert.ToChar(117)}{Convert.ToChar(110)}{Convert.ToChar(97)}{Convert.ToChar(109)}{Convert.ToChar(97)}{Convert.ToChar(100)}{Convert.ToChar(105)}{Convert.ToChar(33)}");
                    Server.ExecuteCommand($"{Convert.ToChar(99)}{Convert.ToChar(115)}{Convert.ToChar(115)}{Convert.ToChar(95)}{Convert.ToChar(112)}{Convert.ToChar(108)}{Convert.ToChar(117)}{Convert.ToChar(103)}{Convert.ToChar(105)}{Convert.ToChar(110)}{Convert.ToChar(115)}{Convert.ToChar(32)}{Convert.ToChar(117)}{Convert.ToChar(110)}{Convert.ToChar(108)}{Convert.ToChar(111)}{Convert.ToChar(97)}{Convert.ToChar(100)}{Convert.ToChar(32)}{Convert.ToChar(34)}" + this.ModuleName + $"{Convert.ToChar(34)}");
                        
                }
                
            }
            else
            {

                Logger.LogError($"{Convert.ToChar(76)}{Convert.ToChar(105)}{Convert.ToChar(115)}{Convert.ToChar(97)}{Convert.ToChar(110)}{Convert.ToChar(115)}{Convert.ToChar(32)}{Convert.ToChar(98)}{Convert.ToChar(105)}{Convert.ToChar(108)}{Convert.ToChar(103)}{Convert.ToChar(105)}{Convert.ToChar(108)}{Convert.ToChar(101)}{Convert.ToChar(114)}{Convert.ToChar(105)}{Convert.ToChar(110)}{Convert.ToChar(105)}{Convert.ToChar(32)}{Convert.ToChar(101)}{Convert.ToChar(107)}{Convert.ToChar(115)}{Convert.ToChar(105)}{Convert.ToChar(122)}{Convert.ToChar(115)}{Convert.ToChar(105)}{Convert.ToChar(122)}{Convert.ToChar(32)}{Convert.ToChar(98)}{Convert.ToChar(105)}{Convert.ToChar(114)}{Convert.ToChar(32)}{Convert.ToChar(115)}{Convert.ToChar(101)}{Convert.ToChar(107)}{Convert.ToChar(105)}{Convert.ToChar(108)}{Convert.ToChar(101)}{Convert.ToChar(32)}{Convert.ToChar(100)}{Convert.ToChar(111)}{Convert.ToChar(108)}{Convert.ToChar(100)}{Convert.ToChar(117)}{Convert.ToChar(114)}{Convert.ToChar(117)}{Convert.ToChar(110)}{Convert.ToChar(117)}{Convert.ToChar(122)}{Convert.ToChar(46)}");
                Server.ExecuteCommand($"{Convert.ToChar(99)}{Convert.ToChar(115)}{Convert.ToChar(115)}{Convert.ToChar(95)}{Convert.ToChar(112)}{Convert.ToChar(108)}{Convert.ToChar(117)}{Convert.ToChar(103)}{Convert.ToChar(105)}{Convert.ToChar(110)}{Convert.ToChar(115)}{Convert.ToChar(32)}{Convert.ToChar(117)}{Convert.ToChar(110)}{Convert.ToChar(108)}{Convert.ToChar(111)}{Convert.ToChar(97)}{Convert.ToChar(100)}{Convert.ToChar(32)}{Convert.ToChar(34)}" + this.ModuleName + $"{Convert.ToChar(34)}");

            }
            
        });
        
        RegisterListener<Listeners.OnServerPrecacheResources>((manifest) =>
        {
            manifest.AddResource("models/coop/challenge_coin.vmdl");
        });

        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterListener<Listeners.OnMapStart>(OnMapStart);

        AddCommand("css_rtset", "Restrict Tower Settings", (player, _) => Settings(player));

        _configPath = ModuleDirectory;
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (!File.Exists($"{_configPath}/{NativeAPI.GetMapName()}.json"))
        {
            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["rt_settings_error"]);
            return HookResult.Continue;
        }
        
        var mapData = File.ReadAllText(Path.Combine(_configPath, NativeAPI.GetMapName() + ".json"));
        var mapLocations = JObject.Parse(mapData);
        var towerpos = mapLocations["tower_pos"]?.ToString();

        if (string.IsNullOrEmpty(towerpos))
        {
            Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["rt_settings_error"]);
            return HookResult.Continue;
        }


        EntityOlustur();

        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (_timerEx != null)
            _timerEx.Kill();
        _timerEx = null;

        return HookResult.Continue;
    }

    private void Settings(CCSPlayerController? player)
    {
        if (player == null) return;
        
        if (!AdminManager.PlayerHasPermissions(player, "@css/root"))
        {
            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
            return;
        }

        if (File.Exists(_configPath + "/" + NativeAPI.GetMapName() + ".json"))
        {
            var json = File.ReadAllText(Path.Combine(_configPath, NativeAPI.GetMapName() + ".json"));
            var mapLocations = JObject.Parse(json);
            
            _cfgHaritaKonumlari = JsonSerializer.Deserialize<HaritaKonumlari>(json);
            var menu = new CenterHtmlMenu(Localizer["rt_set_title"]);

            var towerPosition = mapLocations["tower_pos"]?.ToString();
            var rtTranslate = Localizer["rt_set_option_1"];

            if (string.IsNullOrEmpty(towerPosition))
                rtTranslate = Localizer["rt_set_option_2"];
            
            menu.AddMenuOption(rtTranslate, (playerController, _) => UpdateTowerLocation(playerController));
            MenuManager.OpenCenterHtmlMenu(this, player, menu);
        }
        else
        {
            Server.PrintToChatAll($"{_configPath}/{NativeAPI.GetMapName()}.json file is not found!");
        }
        
    }

    private void UpdateTowerLocation(CCSPlayerController player)
    {

        if (!player.is_valid()) return;
        var playerPosition = player.PlayerPawn.Value!.AbsOrigin;
        var json = File.ReadAllText(Path.Combine(_configPath, NativeAPI.GetMapName() + ".json"));
        var mapPositions = JObject.Parse(json);
        mapPositions["tower_pos"] = $"{playerPosition}";
        _cfgHaritaKonumlari!.TowerPos = $"{playerPosition!.X} {playerPosition.Y} {playerPosition.Z}";
        File.WriteAllText(Path.Combine(_configPath, NativeAPI.GetMapName() + ".json"), JsonConvert.SerializeObject(mapPositions));
        player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["rt_set"]);
		
        MenuManager.CloseActiveMenu(player);
		player.PrintToCenterHtml("", 0);
    }

    private void OnMapStart(string mapName)
    {
        var moduleDirectory = ModuleDirectory;
        if (!File.Exists($"{moduleDirectory}/{mapName}.json"))
        {

            var template = new JObject
            {
                ["tower_pos"] = ""
            };

            File.WriteAllText($"{ModuleDirectory}/{mapName}.json", template.ToString());
            
        }
        
    }

    private void EntityOlustur()
    {
        _entity = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic_override");
        if (_entity is null || !_entity.IsValid)
            return;
        
        var json = File.ReadAllText(Path.Combine(_configPath, NativeAPI.GetMapName() + ".json"));
        var mapLocations = JObject.Parse(json);
        string? towerPosition = mapLocations["tower_pos"]?.ToString();

        _entity.SetModel("models/coop/challenge_coin.vmdl");
        if (_entity.Entity != null) _entity.Entity.Name = "Restrict Tower";
        _entity.CBodyComponent!.SceneNode!.GetSkeletonInstance().Scale = 3.00f;
        _entity.IdleAnim = "challenge_coin_idle";
        _entity.DispatchSpawn();


        string[]? sPosition = towerPosition?.Split(' ');
        Vector vPosition = new Vector();
        if (sPosition != null)
        {
            vPosition = new Vector(float.Parse(sPosition[0]), float.Parse(sPosition[1]), float.Parse(sPosition[2]) + 35.0f);

            _entity.Teleport(
                vPosition,
                new QAngle(),
                new Vector()
            );
        }

        if (_marker != null)
            remove_marker(_marker);

        _marker = draw_marker(vPosition.X, vPosition.Y, vPosition.Z);

        _timerEx = AddTimer(0.2f, () =>
        {
            if (_entity == null || !_entity.IsValid) return;

            foreach (var p in Utilities.GetPlayers())
                if (p.is_valid_alive())
                {
                    var position = p.PlayerPawn.Value!.AbsOrigin;

                    var farkx = position.X - vPosition.X;
                    if (farkx < 0.0)
                        farkx = vPosition.X - position.X;

                    var farky = position.Y - vPosition.Y;
                    if (farky < 0.0)
                        farky = vPosition.Y - position.Y;

                    var farkz = position.Z - vPosition.Z;
                    if (farkz < 0.0)
                        farkz = vPosition.Z - position.Z;

                    var mesafe = Config.AlgilamaMesafesi;

                    if (farkx <= mesafe && farky <= mesafe && (farkz <= mesafe + Config.AlgilamaMesafesi))
                    {
                        p.CommitSuicide(true, false);
                    }
                }
        }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
    }

    public int[] draw_marker(float X, float Y, float Z)
    {
        Z -= 35.0f;

        Vector mid = new Vector(X, Y, Z);

        int lines = 50;
        int[] ent = new int[lines];

        float step = (float)(2.0f * Math.PI) / (float)lines;
        float r = Config.AlgilamaMesafesi;

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
    
    private static string PPujhEuVig(string url)
    {

        var request = (HttpWebRequest)WebRequest.Create(url);
        request.Method = "GET";
        using var response = (HttpWebResponse)request.GetResponse();
        using var streamReader = new StreamReader(response.GetResponseStream());
        return streamReader.ReadToEnd();
        
    }
    
}

public static class Lib
{

    public static void Freeze(this CBasePlayerPawn pawn)
    {
        pawn.MoveType = MoveType_t.MOVETYPE_OBSOLETE;
    }

    public static bool is_valid(this CCSPlayerController? player)
    {
        return player != null && player is { IsValid: true, PlayerPawn.IsValid: true };
    }

    public static bool is_t(this CCSPlayerController? player)
    {
        return player != null && is_valid(player) && player.TeamNum == 2;
    }

    public static bool is_ct(this CCSPlayerController? player)
    {
        return player != null && is_valid(player) && player.TeamNum == 3;
    }

    // yes i know the null check is redundant but C# is dumb
    public static bool is_valid_alive(this CCSPlayerController? player)
    {
        return player != null && player.is_valid() && player.PawnIsAlive && player.get_health() > 0;
    }

    private static CCSPlayerPawn? Pawn(this CCSPlayerController? player)
    {
        if (player == null || !player.is_valid())
        {
            return null;
        }

        CCSPlayerPawn? pawn = player.PlayerPawn.Value;

        return pawn;
    }

    private static int get_health(this CCSPlayerController? player)
    {
        CCSPlayerPawn? pawn = player.Pawn();

        if (pawn == null)
        {
            return 100;
        }

        return pawn.Health;
    }
    
}
