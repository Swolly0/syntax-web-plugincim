using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;

namespace KacCm;

public class KacCm : BasePlugin
{
    public override string ModuleName { get; } = "Kac Cm";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "Oyuncularin pipisinin kac cm oldugunu gosterir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";

    private static readonly int?[] LastUse = new int?[65];

    // LISANS
    public string EklentiTagi = "[www.plugincim.com]";
    public int lisans_bitis_yil = 2024; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 30; // Lisansin bitecegi gun
    // LISANS

    public override void Load(bool hotReload)
    {
        var dateTime = new DateTime(lisans_bitis_yil, lisans_bitis_ay, lisans_bitis_gun, 0, 0, 0, DateTimeKind.Utc);
        var dateWithOffset = new DateTimeOffset(dateTime).ToUniversalTime();
        long timestamp = dateWithOffset.ToUnixTimeSeconds();

        if (timestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {			
			AddCommand("css_kaccm", "Kac cm oldugunu ogren.", (player, command) => Kac_Cm(player, command));
			RegisterEventHandler<EventPlayerConnect>(OnPlayerConnect);
			RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
		}
	}

    public void Kac_Cm(CCSPlayerController? player, CommandInfo command)
    {
        int unix = Convert.ToInt32(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        if (player != null && player.IsValid)
        {
            if (LastUse[player.Index] == null || unix - LastUse[player.Index] >= 30)
            {
                Server.PrintToChatAll($" \x0b{EklentiTagi}\x04 {player.PlayerName} \x01isimli oyuncunun \x04pipisi \x01{new Random().Next(1, 31)} cm.");
                LastUse[player.Index] = unix;
            }
            else
                player.PrintToChat($" \x0b{EklentiTagi} \x01pipinin kac cm olduğunu tekrar görmek için biraz beklemelisin.");
        }
    }

    HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        LastUse[player.Index] = Convert.ToInt32(DateTimeOffset.UtcNow.ToUnixTimeSeconds()) - 100;

        return HookResult.Continue;
    }

    HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        LastUse[player.Index] = Convert.ToInt32(DateTimeOffset.UtcNow.ToUnixTimeSeconds()) - 100;

        return HookResult.Continue;
    }
}