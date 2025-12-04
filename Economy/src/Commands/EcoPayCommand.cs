using Economy.Config;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;

namespace Economy.Commands;

/// <summary>
/// Handles the pay subcommand: !eco pay &lt;target&gt; &lt;amount&gt; &lt;wallet&gt;
/// Player-to-player transfers (online players only)
/// </summary>
public sealed class EcoPayCommand : CommandBase
{
	public static void Handle(
		Economy plugin,
		ICommandContext ctx,
		IPlayer sender,
		ILocalizer localizer,
		PluginConfig config)
	{
		// Usage: eco pay <target> <amount> <wallet>
		if (ctx.Args.Length < 4)
		{
			ShowUsageWithHint(ctx, localizer, config, "economy.usage.pay", config.Commands.Pay.Name);
			return;
		}

		// Pay only works for online players (exclude bots)
		var onlinePlayers = plugin.Core.PlayerManager.FindTargettedPlayers(
			sender,
			ctx.Args[1],
			TargetSearchMode.NoMultipleTargets);

		var target = onlinePlayers.FirstOrDefault(p => !p.IsFakeClient);
		if (target == null)
		{
			ctx.Reply($"{localizer["economy.prefix"]} {localizer["economy.error.no_target"]}");
			return;
		}

		// Cannot pay yourself
		if (target.SteamID == sender.SteamID)
		{
			ctx.Reply($"{localizer["economy.prefix"]} {localizer["economy.error.pay_self"]}");
			return;
		}

		if (!TryParsePositiveAmount(ctx.Args[2], out var amount))
		{
			ctx.Reply($"{localizer["economy.prefix"]} {localizer["economy.error.invalid_amount"]}");
			return;
		}

		var walletKind = ctx.Args[3];

		if (!ValidateWallet(plugin, walletKind, ctx, localizer, config))
			return;

		// Check if sender has enough funds
		if (!plugin.EconomyService.HasSufficientFunds(sender.SteamID, walletKind, amount))
		{
			ctx.Reply($"{localizer["economy.prefix"]} {localizer["economy.error.insufficient_funds"]}");
			return;
		}

		// Perform transfer
		plugin.EconomyService.SubtractPlayerBalance(sender.SteamID, walletKind, amount);
		plugin.EconomyService.AddPlayerBalance(target.SteamID, walletKind, amount);

		// Notify sender
		var targetName = target.Controller?.PlayerName ?? target.SteamID.ToString();
		ctx.Reply($"{localizer["economy.prefix"]} {localizer["economy.pay.success", amount, walletKind, targetName]}");

		// Notify target
		var targetLocalizer = plugin.Core.Translation.GetPlayerLocalizer(target);
		var senderName = sender.Controller?.PlayerName ?? sender.SteamID.ToString();
		target.SendChat($"{targetLocalizer["economy.prefix"]} {targetLocalizer["economy.pay.received", amount, walletKind, senderName]}");
	}
}
