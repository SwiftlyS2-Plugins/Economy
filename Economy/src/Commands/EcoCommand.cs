using Economy.Config;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;

namespace Economy.Commands;

/// <summary>
/// Main economy command handler that routes to subcommands
/// </summary>
public sealed class EcoCommand : CommandBase
{
	public static void OnCommand(Economy plugin, ICommandContext ctx)
	{
		var sender = ctx.Sender;
		if (sender == null || !sender.IsValid)
			return;

		var localizer = plugin.Core.Translation.GetPlayerLocalizer(sender);
		var config = plugin.Config;
		var commands = config.Commands;

		// No args = show all balances + help
		if (ctx.Args.Length == 0)
		{
			ShowBalancesAndHelp(plugin, ctx, sender, localizer, commands);
			return;
		}

		var subCommand = ctx.Args[0];

		// Give subcommand
		if (string.Equals(subCommand, commands.Give.Name, StringComparison.OrdinalIgnoreCase))
		{
			if (!HasPermission(plugin, sender, commands.Give.Permission, ctx, localizer))
				return;

			EcoGiveCommand.Handle(plugin, ctx, sender, localizer, config);
			return;
		}

		// Take subcommand
		if (string.Equals(subCommand, commands.Take.Name, StringComparison.OrdinalIgnoreCase))
		{
			if (!HasPermission(plugin, sender, commands.Take.Permission, ctx, localizer))
				return;

			EcoTakeCommand.Handle(plugin, ctx, sender, localizer, config);
			return;
		}

		// Set subcommand
		if (string.Equals(subCommand, commands.Set.Name, StringComparison.OrdinalIgnoreCase))
		{
			if (!HasPermission(plugin, sender, commands.Set.Permission, ctx, localizer))
				return;

			EcoSetCommand.Handle(plugin, ctx, sender, localizer, config);
			return;
		}

		// Pay subcommand
		if (string.Equals(subCommand, commands.Pay.Name, StringComparison.OrdinalIgnoreCase))
		{
			if (!HasPermission(plugin, sender, commands.Pay.Permission, ctx, localizer))
				return;

			EcoPayCommand.Handle(plugin, ctx, sender, localizer, config);
			return;
		}

		// Unknown subcommand - show help
		ShowBalancesAndHelp(plugin, ctx, sender, localizer, commands);
	}

	private static void ShowBalancesAndHelp(
		Economy plugin,
		ICommandContext ctx,
		IPlayer sender,
		ILocalizer localizer,
		CommandSettings commands)
	{
		var wallets = plugin.EconomyService.GetWalletKinds();
		var mainCmd = commands.MainCommand;

		// Show balances for all wallets
		if (wallets.Count > 0)
		{
			ctx.Reply($"{localizer["economy.prefix"]} {localizer["economy.balance.header"]}");
			foreach (var wallet in wallets)
			{
				var balance = plugin.EconomyService.GetPlayerBalance(sender.SteamID, wallet);
				ctx.Reply(localizer["economy.balance.item", wallet, balance]);
			}
		}
		else
		{
			ctx.Reply($"{localizer["economy.prefix"]} {localizer["economy.wallets.empty"]}");
		}

		// Show available commands based on permissions
		var availableCommands = new List<string>();

		if (HasPermissionSilent(plugin, sender, commands.Give.Permission))
			availableCommands.Add($"!{mainCmd} {commands.Give.Name}");

		if (HasPermissionSilent(plugin, sender, commands.Take.Permission))
			availableCommands.Add($"!{mainCmd} {commands.Take.Name}");

		if (HasPermissionSilent(plugin, sender, commands.Set.Permission))
			availableCommands.Add($"!{mainCmd} {commands.Set.Name}");

		if (HasPermissionSilent(plugin, sender, commands.Pay.Permission))
			availableCommands.Add($"!{mainCmd} {commands.Pay.Name}");

		if (availableCommands.Count > 0)
		{
			ctx.Reply($"{localizer["economy.prefix"]} {localizer["economy.help.available", string.Join(", ", availableCommands)]}");
		}
	}
}
