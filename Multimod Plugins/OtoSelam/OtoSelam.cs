using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;

namespace OtoSelam;

public class OtoSelam : BasePlugin
{
    public override string ModuleName { get; } = "Oto Selam";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "Otomatik selam verir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";

    // LISANS
    public string EklentiTagi = "[www.plugincim.com]";
    public int lisans_bitis_yil = 2024; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 1; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 20; // Lisansin bitecegi gun
    // LISANS

    public override void Load(bool hotReload)
    {
        var dateTime = new DateTime(lisans_bitis_yil, lisans_bitis_ay, lisans_bitis_gun, 0, 0, 0, DateTimeKind.Utc);
        var dateWithOffset = new DateTimeOffset(dateTime).ToUniversalTime();
        long timestamp = dateWithOffset.ToUnixTimeSeconds();

        if (timestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
			AddCommandListener("say", OnPlayerSay, HookMode.Post);
			AddCommandListener("say_team", OnPlayerSay, HookMode.Post);
		}
	}

    private HookResult OnPlayerSay(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid || player.IsBot || player.IsHLTV) return HookResult.Continue;

        var message = info.GetArg(1);
        if(message == "sa")
            player.PrintToChat($" \x0b{EklentiTagi}\x01 Aleyküm selam.");

        return HookResult.Continue;
    }

}