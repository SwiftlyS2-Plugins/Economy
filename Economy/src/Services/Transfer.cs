using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Players;

namespace Economy.Services;

public partial class EconomyService
{
	/* ==================== Transfer Funds ==================== */

	public bool TransferFunds(IPlayer fromPlayer, IPlayer toPlayer, string walletKind, decimal amount)
	{
		if (fromPlayer.IsFakeClient || toPlayer.IsFakeClient) return false;
		return TransferFunds(fromPlayer.SteamID, toPlayer.SteamID, walletKind, amount);
	}

	public bool TransferFunds(int fromPlayerId, int toPlayerId, string walletKind, decimal amount)
	{
		var fromPlayer = _core.PlayerManager.GetPlayer(fromPlayerId);
		var toPlayer = _core.PlayerManager.GetPlayer(toPlayerId);
		if (fromPlayer == null || toPlayer == null || fromPlayer.IsFakeClient || toPlayer.IsFakeClient) return false;

		return TransferFunds(fromPlayer.SteamID, toPlayer.SteamID, walletKind, amount);
	}

	public bool TransferFunds(ulong fromSteamId, ulong toSteamId, string walletKind, decimal amount)
	{
		if (!_walletKinds.ContainsKey(walletKind))
			throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");

		if (amount <= 0)
			throw new ArgumentException("Transfer amount must be positive.", nameof(amount));

		if (!IsOnline(fromSteamId) || !IsOnline(toSteamId))
		{
			_core.Logger.LogWarning("Transfer attempted with offline player. From: {From}, To: {To}", fromSteamId, toSteamId);
			return false;
		}

		var (firstLock, secondLock) = fromSteamId < toSteamId
			? (GetPlayerLock(fromSteamId), GetPlayerLock(toSteamId))
			: (GetPlayerLock(toSteamId), GetPlayerLock(fromSteamId));

		lock (firstLock)
		{
			lock (secondLock)
			{
				var fromBalances = _playerBalances.GetOrAdd(fromSteamId, _ => new System.Collections.Concurrent.ConcurrentDictionary<string, decimal>());
				var toBalances = _playerBalances.GetOrAdd(toSteamId, _ => new System.Collections.Concurrent.ConcurrentDictionary<string, decimal>());

				fromBalances.TryGetValue(walletKind, out var fromBalance);
				toBalances.TryGetValue(walletKind, out var toBalance);

				if (!_config.AllowNegativeBalance && fromBalance < amount)
				{
					return false;
				}

				var newFromBalance = fromBalance - amount;
				var newToBalance = toBalance + amount;

				if (!_config.AllowNegativeBalance && newFromBalance < 0)
					newFromBalance = 0;

				fromBalances[walletKind] = newFromBalance;
				toBalances[walletKind] = newToBalance;

				MarkDirty(fromSteamId);
				MarkDirty(toSteamId);
				EnqueueSave(fromSteamId);
				EnqueueSave(toSteamId);

				OnPlayerBalanceChanged?.Invoke(fromSteamId, walletKind, newFromBalance, fromBalance);
				OnPlayerBalanceChanged?.Invoke(toSteamId, walletKind, newToBalance, toBalance);
				OnPlayerFundsTransferred?.Invoke(fromSteamId, toSteamId, walletKind, amount);

				return true;
			}
		}
	}
}
