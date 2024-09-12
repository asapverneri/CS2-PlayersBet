﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace PlayersBet;

public class PlayersBet: BasePlugin
{
	public override string ModuleName => "PlayersBet";
	public override string ModuleVersion => "1.1.1";
	public override string ModuleAuthor => "Dliix66";
	public override string ModuleDescription => "Allow players to bet money on a team for the round.";

	private Dictionary<ulong, BetData> _currentBets = new Dictionary<ulong, BetData>();

	private bool _isInRound = false;

	public override void Load(bool hotReload)
	{
		base.Load(hotReload);

		AddCommand("bet", "bet", CommandBet);

		Server.NextFrame(() => { Logger.LogInformation($"Loaded! (version {ModuleVersion})"); });
    }

	[GameEventHandler]
	public HookResult OnRoundStart(EventRoundStart evnt, GameEventInfo info)
	{
		_isInRound = true;
		_currentBets.Clear();
		return HookResult.Continue;
	}

	[GameEventHandler]
	public HookResult OnRoundEnd(EventRoundEnd evnt, GameEventInfo info)
	{
		CsTeam winningTeam = (CsTeam)evnt.Winner;
		if (winningTeam == CsTeam.Spectator || winningTeam == CsTeam.None)
			return HookResult.Continue;

		foreach (KeyValuePair<ulong, BetData> currentBet in _currentBets)
		{
			CCSPlayerController player = currentBet.Value.player;
			if (player.IsValid == false)
				continue;

			if (currentBet.Value.team == winningTeam)
			{
				int total = currentBet.Value.earnings + currentBet.Value.amountBet;
				player.AddMoney(total);
                player.PrintToChat($"{Localizer["bet.win", currentBet.Value.earnings, currentBet.Value.amountBet]}");

            }
			else
			{
                player.PrintToChat($"{Localizer["bet.lost"]}");
            }
		}

		_isInRound = false;

		return HookResult.Continue;
	}

	private void CommandBet(CCSPlayerController player, CommandInfo commandInfo)
	{
		if (player.IsValid == false || player.InGameMoneyServices == null)
			return;

		string usage = $"{Localizer["bet.usage", commandInfo.GetArg(0)]}";


        if (!IsCommandValid(player, commandInfo, 2, usage))
			return;

		string teamArg = commandInfo.GetArg(1).ToLower();
		CsTeam team;
		if (teamArg == "t")
		{
			team = CsTeam.Terrorist;
		}
		else if (teamArg == "ct")
		{
			team = CsTeam.CounterTerrorist;
		}
		else
		{
			player.PrintToChat(usage);
			return;
		}

		string amountArg = commandInfo.GetArg(2).ToLower();
		int amount;
		if (amountArg == "all")
		{
			amount = player.InGameMoneyServices.Account;
		}
		else if (amountArg == "half")
		{
			amount = player.InGameMoneyServices.Account / 2;
		}
		else if (Int32.TryParse(amountArg, out amount) == false)
		{
			player.PrintToChat(usage);
			return;
		}

		if (player.TeamNum == (byte)CsTeam.Spectator || player.TeamNum == (byte)CsTeam.None)
		{
			player.PrintToChat($"{Localizer["bet.spectate"]}");
			return;
		}

		if (player.PawnIsAlive)
		{
            player.PrintToChat($"{Localizer["bet.alive"]}");
            return;
		}

		if (_isInRound == false)
		{
            player.PrintToChat($"{Localizer["bet.round"]}");
            return;
		}

		if (amount > player.InGameMoneyServices.Account)
		{
            player.PrintToChat($"{Localizer["bet.toomuch"]}");
            return;
		}

		ulong steamId = player.SteamID;
		if (_currentBets.ContainsKey(steamId))
		{
            player.PrintToChat($"{Localizer["bet.alreadybet"]}");
            return;
		}

		int earnings = CalculateBet(amount, team);
		if (earnings <= 0)
		{
            player.PrintToChat($"{Localizer["bet.cantnow"]}");
            return;
		}

		player.AddMoney(-amount);
		_currentBets.Add(steamId, new BetData()
		{
			amountBet = amount,
			earnings = earnings,
			player = player,
			team = team
		});

        player.PrintToChat($"{Localizer["bet.estimated", earnings, amount]}");

    }

	private int CalculateBet(int amount, CsTeam teamBet)
	{
		List<CCSPlayerController> allPlayers = Utilities.GetPlayers().Where(p => p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected).ToList();
		int tAlive = 0;
		int ctAlive = 0;
		foreach (CCSPlayerController player in allPlayers)
		{
			// not valid or not playing
			if (player.IsValid == false || player.TeamNum == (byte)CsTeam.Spectator || player.TeamNum == (byte)CsTeam.None)
				continue;

			// dead
			if (player.PlayerPawn.IsValid == false || player.PlayerPawn.Value.Health <= 0)
				continue;

			if (player.TeamNum == (byte)CsTeam.Terrorist)
				tAlive++;
			else if (player.TeamNum == (byte)CsTeam.CounterTerrorist)
				ctAlive++;
		}

		if (tAlive == 0 || ctAlive == 0)
			return 0;

		float multiplier;
		if (teamBet == CsTeam.Terrorist)
		{
			multiplier = (float)ctAlive / (float)tAlive;
		}
		else
		{
			multiplier = (float)tAlive / (float)ctAlive;
		}

		return (int)(amount * multiplier);
	}

	private bool IsCommandValid(CCSPlayerController player, CommandInfo command, int argCount, string usage)
	{
		if (player == null)
		{
			return false;
		}

		if (player.IsValid == false)
			return false;

		if (command.ArgCount != argCount + 1)
		{
			player.PrintToConsole(usage);
			return false;
		}

		return true;
	}
}
