using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Menu;
using System.Text;
using System.Collections.Concurrent;
using CounterStrikeSharp.API.Modules.Commands.Targeting;

namespace Vote;

public class Vote : BasePlugin
{
    public override string ModuleName { get; } = "Vote";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!vote";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";

    public static Dictionary<string, int> voteAnswers = new Dictionary<string, int>();
    public static bool voteInProgress = false;
    public static bool[] VoteUsed = new bool[Server.MaxPlayers + 1];

    [ConsoleCommand("css_vote")]
    [RequiresPermissions("@css/generic")]
    [CommandHelper(minArgs: 2, usage: "<soru> [... cevaplar ...]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnVoteCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (voteInProgress)
        {
            caller.PrintToChat($" \x01 Aktif bir oylama var. \x04(!vote0)");
            return;
        }

        if (command.GetArg(1) == null || command.GetArg(1).Length < 0 || command.ArgCount < 2)
            return;

        voteAnswers.Clear();

        string question = command.GetArg(1);
        int answersCount = command.ArgCount;

        if (caller == null || caller != null)
        {
            var voteMenu = new CenterHtmlMenu("Oylama: " + question);

            for (int i = 2; i <= answersCount - 1; i++)
            {
                voteAnswers.Add(command.GetArg(i), 0);
                voteMenu.AddMenuOption(command.GetArg(i), handleVotes);
            }

            foreach (CCSPlayerController _player in Utilities.GetPlayers().Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && !p.IsBot && !p.IsHLTV))
            {
                VoteUsed[_player.Slot] = false;
                MenuManager.OpenCenterHtmlMenu(this, _player, voteMenu);
            }

            Server.PrintToChatAll($" \x04{caller.PlayerName} \x01isimli yetkili tarafından \x0e{question} \x0fiçin oylama başlatıldı!");
            voteInProgress = true;

            AddTimer(15, () =>
            {
                Server.PrintToChatAll($" \x04{question} \x01için oylama sonuçları!");

                foreach (KeyValuePair<string, int> kvp in voteAnswers)
                    Server.PrintToChatAll($"{kvp.Key} - {kvp.Value}");

                foreach (CCSPlayerController target in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false })){
                    MenuManager.CloseActiveMenu(target);
					target.PrintToCenterHtml("", 0);
				}

                voteInProgress = false;

            }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.STOP_ON_MAPCHANGE);
        }
    }


    [ConsoleCommand("css_vote0")]
    [RequiresPermissions("@css/generic")]
    public void OnVote0Command(CCSPlayerController? caller, CommandInfo command)
    {
        if (voteInProgress)
        {
            Server.PrintToChatAll($" \x04{caller.PlayerName} \x01oylamayı iptal etti.");

			foreach (CCSPlayerController target in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false })){
				MenuManager.CloseActiveMenu(target);
				target.PrintToCenterHtml("", 0);
			}

            voteInProgress = false;
        }
        else
            caller.PrintToChat($" \x04 Aktif bir oylama yok.");
    }


    internal static void handleVotes(CCSPlayerController player, ChatMenuOption option)
    {
        if (voteInProgress && !VoteUsed[player.Slot])
        {
            player.PrintToChat($" \x04Oy kullanıldı..");

            VoteUsed[player.Slot] = true;
            voteAnswers[option.Text]++;
        }
    }
}