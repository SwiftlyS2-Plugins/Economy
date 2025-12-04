using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Players;

namespace Economy.Services;

public partial class EconomyService
{
	/* ==================== Transfer Funds ==================== */

	public bool TransferFunds(IPlayer fromPlayer, IPlayer toPlayer, string walletKind, int amount)
	{
		// Bots don't have economy
		if (fromPlayer.IsFakeClient || toPlayer.IsFakeClient) return false;
		return TransferFunds(fromPlayer.SteamID, toPlayer.SteamID, walletKind, amount);
	}

	public bool TransferFunds(int fromPlayerId, int toPlayerId, string walletKind, int amount)
	{
		var fromPlayer = _core.PlayerManager.GetPlayer(fromPlayerId);
		var toPlayer = _core.PlayerManager.GetPlayer(toPlayerId);
		// Bots don't have economy
		if (fromPlayer == null || toPlayer == null || fromPlayer.IsFakeClient || toPlayer.IsFakeClient) return false;

		return TransferFunds(fromPlayer.SteamID, toPlayer.SteamID, walletKind, amount);
	}

	/// <summary>
	/// Atomic transfer between two players with rollback on failure.
	/// Only works for online players. For offline, use SetPlayerBalance directly.
	/// </summary>
	public bool TransferFunds(ulong fromSteamId, ulong toSteamId, string walletKind, int amount)
	{
		if (!_walletKinds.ContainsKey(walletKind))
			throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");

		if (amount <= 0)
			throw new ArgumentException("Transfer amount must be positive.", nameof(amount));

		// Only support transfers between online players for atomicity
		if (!IsOnline(fromSteamId) || !IsOnline(toSteamId))
		{
			_core.Logger.LogWarning("Transfer attempted with offline player. From: {From}, To: {To}", fromSteamId, toSteamId);
			return false;
		}

		// Lock both players in consistent order to prevent deadlocks
		var (firstLock, secondLock) = fromSteamId < toSteamId
			? (GetPlayerLock(fromSteamId), GetPlayerLock(toSteamId))
			: (GetPlayerLock(toSteamId), GetPlayerLock(fromSteamId));

		lock (firstLock)
		{
			lock (secondLock)
			{
				// Get current balances
				var fromBalances = _playerBalances.GetOrAdd(fromSteamId, _ => new System.Collections.Concurrent.ConcurrentDictionary<string, int>());
				var toBalances = _playerBalances.GetOrAdd(toSteamId, _ => new System.Collections.Concurrent.ConcurrentDictionary<string, int>());

				fromBalances.TryGetValue(walletKind, out var fromBalance);
				toBalances.TryGetValue(walletKind, out var toBalance);

				// Check funds
				if (!_config.AllowNegativeBalance && fromBalance < amount)
				{
					return false; // Insufficient funds
				}

				// Calculate new balances
				var newFromBalance = fromBalance - amount;
				var newToBalance = toBalance + amount;

				// Clamp if needed
				if (!_config.AllowNegativeBalance && newFromBalance < 0)
					newFromBalance = 0;

				// Apply atomically (both under lock)
				fromBalances[walletKind] = newFromBalance;
				toBalances[walletKind] = newToBalance;

				// Mark dirty and enqueue saves
				MarkDirty(fromSteamId);
				MarkDirty(toSteamId);
				EnqueueSave(fromSteamId);
				EnqueueSave(toSteamId);

				// Fire events
				OnPlayerBalanceChanged?.Invoke(fromSteamId, walletKind, newFromBalance, fromBalance);
				OnPlayerBalanceChanged?.Invoke(toSteamId, walletKind, newToBalance, toBalance);
				OnPlayerFundsTransferred?.Invoke(fromSteamId, toSteamId, walletKind, amount);

				return true;
			}
		}
	}
}
