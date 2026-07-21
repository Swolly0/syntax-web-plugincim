using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using System.Drawing;
using System.Text.Json.Serialization;

namespace JBKomutcuMarker;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}

public class JBKomutcuMarker : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Komutcu Marker";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "Komutcu mouse orta tusu ile marker koyabilir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;

    CCSPlayerController iWarden = null;
    int[]? marker = null;

    // LISANS
    public int lisans_bitis_yil = 2024; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 30; // Lisansin bitecegi gun
    // LISANS

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[Warden Marker] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
        }

        Config = config;
    }
	
    public override void Load(bool hotReload)
    {
        var dateTime = new DateTime(lisans_bitis_yil, lisans_bitis_ay, lisans_bitis_gun, 0, 0, 0, DateTimeKind.Utc);
        var dateWithOffset = new DateTimeOffset(dateTime).ToUniversalTime();
        long timestamp = dateWithOffset.ToUnixTimeSeconds();

        if (timestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
			// KOMUTÇU SİSTEMİ
			AddCommand("css_w", "", (player, command) => Warden(player, command));
			AddCommand("css_k", "", (player, command) => Warden(player, command));
			AddCommand("css_uw", "", (player, command) => UnWarden(player, command));
			AddCommand("css_kcik", "", (player, command) => UnWarden(player, command));
			AddCommand("css_rw", "", (player, command) => RemoveWarden(player, command));
			AddCommand("css_ksil", "", (player, command) => RemoveWarden(player, command));

			RegisterEventHandler<EventPlayerTeam>(OnPlayerTeam);
			RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
			
			AddTimer(1.0f, () =>
			{
				if (iWarden != null && (!iWarden.is_valid() || !iWarden.is_ct()))
					iWarden = null;
			}, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);		
			// KOMUTÇU SİSTEMİ

			RegisterEventHandler<EventPlayerPing>(OnPlayerPing);
		}
    }
    public void Warden(CCSPlayerController? player, CommandInfo command)
    {
        if(player.IsValid && player.is_ct())
            if ((iWarden == null || !iWarden.is_valid() || !iWarden.is_ct()) && player.is_valid() && player.is_ct())
                iWarden = player;

        return;
    }

    public void UnWarden(CCSPlayerController? player, CommandInfo command)
    {
        if (iWarden == player)
            iWarden = null;

        return;
    }


    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            CCSPlayerController player = @event.Userid;

            if (player != null && iWarden == player)
                iWarden = null;

            return HookResult.Continue;
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            CCSPlayerController player = @event.Userid;

            if (player != null && iWarden == player)
            {
                if (@event.Team != 3)
                    iWarden = null;

                return HookResult.Continue;
            }
        }

        return HookResult.Continue;
    }















    HookResult OnPlayerPing(EventPlayerPing @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            var player = @event.Userid;

            if (player.is_valid() && iWarden == player)
            {
                remove_marker();
                marker = draw_marker(@event.X, @event.Y, @event.Z, 60.0f, Color.FromArgb(new Random().Next(1, 255), new Random().Next(1, 255), new Random().Next(1, 255), 255));
            }
        }

        return HookResult.Handled;
    }

    static public int[] draw_marker(float X, float Y, float Z, float time, Color colour)
    {
        Vector mid = new Vector(X, Y, Z);

        int lines = 50;
        int[] ent = new int[lines];

        float step = (float)(2.0f * Math.PI) / (float)lines;
        float r = 75.0f;

        float angle_old = 0.0f;
        float angle_cur = step;


        for (int i = 0; i < lines; i++)
        {
            Vector start = angle_on_circle(angle_old, r, mid);
            Vector end = angle_on_circle(angle_cur, r, mid);

            ent[i] = draw_laser(start, end, time, 2.0f, colour);

            angle_old = angle_cur;
            angle_cur += step;
        }

        return ent;
    }
    static public int draw_laser(Vector start, Vector end, float life, float width, Color colour)
    {
        CEnvBeam? laser = Utilities.CreateEntityByName<CEnvBeam>("env_beam");

        if (laser == null)
        {
            return -1;
        }

        // setup looks
        laser.set_colour(colour);
        laser.Width = 2.0f;

        // circle not working?
        //laser.Flags |= 8;

        laser.move(start, end);

        // start spawn
        laser.DispatchSpawn();

        // create a timer to remove it
        if (life != 0.0f)
        {
            remove_ent_delay(laser, life, "env_beam");
        }

        return (int)laser.Index;
    }

    void remove_marker()
    {
        if (marker != null)
        {
            destroy_beam_group(marker);
            marker = null;
        }
    }

    static public void destroy_beam_group(int[] ent)
    {
        foreach (int laser in ent)
        {
            remove_ent(laser, "env_beam");
        }
    }

    static public void remove_ent(int index, String name)
    {
        CBaseEntity? ent = Utilities.GetEntityFromIndex<CBaseEntity>(index);

        if (ent != null && ent.DesignerName == name)
        {
            ent.Remove();
        }
    }

    static public void remove_ent_delay(CEntityInstance entity, float delay, String name)
    {
        if (entity.DesignerName == name)
        {
            int index = (int)entity.Index;

            /*AddTimer(delay, () =>
            {
                remove_ent(index, name);
            }, TimerFlags.STOP_ON_MAPCHANGE);*/
        }
    }

    static Vector angle_on_circle(float angle, float r, Vector mid)
    {
        // {r * cos(x),r * sin(x)} + mid
        // NOTE: we offset Z so it doesn't clip into the ground
        return new Vector((float)(mid.X + (r * Math.Cos(angle))), (float)(mid.Y + (r * Math.Sin(angle))), mid.Z + 6.0f);
    }
}





public static class Lib
{
    static public void set_colour(this CEnvBeam? laser, Color colour)
    {
        if (laser != null)
        {
            laser.Render = colour;
        }
    }

    static Vector VEC_ZERO = new Vector(0.0f, 0.0f, 0.0f);
    static QAngle ANGLE_ZERO = new QAngle(0.0f, 0.0f, 0.0f);
    static public void move(this CEnvBeam? laser, Vector start, Vector end)
    {
        if (laser == null)
        {
            return;
        }

        // set pos
        laser.Teleport(start, ANGLE_ZERO, VEC_ZERO);

        // end pos
        // NOTE: we cant just move the whole vec
        laser.EndPos.X = end.X;
        laser.EndPos.Y = end.Y;
        laser.EndPos.Z = end.Z;

        Utilities.SetStateChanged(laser, "CBeam", "m_vecEndPos");
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