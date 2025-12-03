using Economy.Config;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;

namespace Economy.Commands;

/// <summary>
/// Handles the take subcommand: !eco take &lt;target&gt; &lt;amount&gt; &lt;wallet&gt;
/// </summary>
public sealed class EcoTakeCommand : CommandBase
{
	public static void Handle(
		Economy plugin,
		ICommandContext ctx,
		IPlayer sender,
		ILocalizer localizer,
		PluginConfig config)
	{
		// Usage: eco take <target> <amount> <wallet>
		if (ctx.Args.Length < 4)
		{
			ShowUsageWithHint(ctx, localizer, config, "economy.usage.take", config.Commands.Take.Name);
			return;
		}

		var targets = FindTargets(plugin, sender, ctx.Args[1], ctx, localizer);
		if (targets.Count == 0)
			return;

		if (!TryParsePositiveAmount(ctx.Args[2], out var amount))
		{
			ctx.Reply($"{localizer["economy.prefix"]} {localizer["economy.error.invalid_amount"]}");
			return;
		}

		var walletKind = ctx.Args[3];

		if (!ValidateWallet(plugin, walletKind, ctx, localizer, config))
			return;

		foreach (var target in targets)
		{
			plugin.EconomyService.SubtractPlayerBalance(target.SteamID, walletKind, amount);
			ctx.Reply($"{localizer["economy.prefix"]} {localizer["economy.take.success", amount, walletKind, target.GetDisplayName()]}");

			// Notify online target
			if (target.IsOnline && target.SteamID != sender.SteamID)
			{
				var targetLocalizer = plugin.Core.Translation.GetPlayerLocalizer(target.Player!);
				target.Player!.SendChat($"{targetLocalizer["economy.prefix"]} {targetLocalizer["economy.take.removed", amount, walletKind, sender.Controller?.PlayerName ?? "Admin"]}");
			}
		}
	}
}
