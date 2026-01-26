using Economy.Config;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SteamAPI;
using SwiftlyS2.Shared.Translation;

namespace Economy.Commands;

/// <summary>
/// Represents a target that can be either an online player or an offline SteamID
/// </summary>
public readonly struct TargetInfo
{
	public ulong SteamID { get; }
	public IPlayer? Player { get; }
	public bool IsOnline => Player != null;

	public TargetInfo(IPlayer player)
	{
		SteamID = player.SteamID;
		Player = player;
	}

	public TargetInfo(ulong steamId)
	{
		SteamID = steamId;
		Player = null;
	}

	public string GetDisplayName() => Player?.Controller?.PlayerName ?? SteamID.ToString();
}

/// <summary>
/// Base class for economy subcommands with common utilities
/// </summary>
public abstract class CommandBase
{
	protected static bool HasPermission(Economy plugin, IPlayer sender, string permission, ICommandContext ctx, ILocalizer localizer)
	{
		if (string.IsNullOrEmpty(permission))
			return true;

		if (plugin.Core.Permission.PlayerHasPermission(sender.SteamID, permission))
			return true;

		ctx.Reply($"{localizer["economy.prefix"]} {localizer["economy.error.no_permission"]}");
		return false;
	}

	protected static bool HasPermissionSilent(Economy plugin, IPlayer sender, string permission)
	{
		if (string.IsNullOrEmpty(permission))
			return true;

		return plugin.Core.Permission.PlayerHasPermission(sender.SteamID, permission);
	}

	/// <summary>
	/// Finds targets - first tries online players (excluding bots), then falls back to SteamID parsing for offline
	/// </summary>
	protected static List<TargetInfo> FindTargets(Economy plugin, IPlayer sender, string target, ICommandContext ctx, ILocalizer localizer)
	{
		var results = new List<TargetInfo>();

		// Try online players first (exclude bots)
		var onlinePlayers = plugin.Core.PlayerManager.FindTargettedPlayers(
			sender,
			target,
			TargetSearchMode.IncludeSelf);

		foreach (var player in onlinePlayers)
		{
			// Skip bots - they don't have economy
			if (player.IsFakeClient)
				continue;

			results.Add(new TargetInfo(player));
		}

		// If no online players found, try parsing as SteamID (for offline players)
		if (results.Count == 0)
		{
			var steamId = SteamIdParser.ParseToSteamId64(target);
			if (steamId.HasValue)
			{
				results.Add(new TargetInfo(steamId.Value));
			}
		}

		if (results.Count == 0)
		{
			ctx.Reply($"{localizer["economy.prefix"]} {localizer["economy.error.no_target"]}");
		}

		return results;
	}

	protected static bool TryParsePositiveAmount(string input, out decimal amount)
	{
		return decimal.TryParse(input, out amount) && amount > 0;
	}

	protected static bool TryParseAmount(string input, out decimal amount)
	{
		return decimal.TryParse(input, out amount);
	}

	protected static bool ValidateWallet(Economy plugin, string walletKind, ICommandContext ctx, ILocalizer localizer, PluginConfig config)
	{
		if (plugin.EconomyService.WalletKindExists(walletKind))
			return true;

		ctx.Reply($"{localizer["economy.prefix"]} {localizer["economy.error.invalid_wallet", walletKind]}");
		ctx.Reply($"{localizer["economy.prefix"]} {localizer["economy.usage.wallet_hint", config.Commands.MainCommand]}");
		return false;
	}

	protected static void ShowUsageWithHint(ICommandContext ctx, ILocalizer localizer, PluginConfig config, string usageKey, string subCmdName)
	{
		ctx.Reply($"{localizer["economy.prefix"]} {localizer[usageKey, config.Commands.MainCommand, subCmdName]}");
		ctx.Reply($"{localizer["economy.prefix"]} {localizer["economy.usage.wallet_hint", config.Commands.MainCommand]}");
	}
}
