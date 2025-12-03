using Economy.Config;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;

namespace Economy.Commands;

/// <summary>
/// Handles the set subcommand: !eco set &lt;target&gt; &lt;amount&gt; &lt;wallet&gt;
/// </summary>
public sealed class EcoSetCommand : CommandBase
{
	public static void Handle(
		Economy plugin,
		ICommandContext ctx,
		IPlayer sender,
		ILocalizer localizer,
		PluginConfig config)
	{
		// Usage: eco set <target> <amount> <wallet>
		if (ctx.Args.Length < 4)
		{
			ShowUsageWithHint(ctx, localizer, config, "economy.usage.set", config.Commands.Set.Name);
			return;
		}

		var targets = FindTargets(plugin, sender, ctx.Args[1], ctx, localizer);
		if (targets.Count == 0)
			return;

		if (!TryParseAmount(ctx.Args[2], out var amount))
		{
			ctx.Reply($"{localizer["economy.prefix"]} {localizer["economy.error.invalid_amount"]}");
			return;
		}

		var walletKind = ctx.Args[3];

		if (!ValidateWallet(plugin, walletKind, ctx, localizer, config))
			return;

		foreach (var target in targets)
		{
			plugin.EconomyService.SetPlayerBalance(target.SteamID, walletKind, amount);
			ctx.Reply($"{localizer["economy.prefix"]} {localizer["economy.set.success", target.GetDisplayName(), amount, walletKind]}");

			// Notify online target
			if (target.IsOnline && target.SteamID != sender.SteamID)
			{
				var targetLocalizer = plugin.Core.Translation.GetPlayerLocalizer(target.Player!);
				target.Player!.SendChat($"{targetLocalizer["economy.prefix"]} {targetLocalizer["economy.set.changed", amount, walletKind, sender.Controller?.PlayerName ?? "Admin"]}");
			}
		}
	}
}
