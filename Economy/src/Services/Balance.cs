using System.Collections.Concurrent;
using Economy.Database.Models;
using Dommel;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Players;

namespace Economy.Services;

public partial class EconomyService
{
	/* ==================== Get Balance ==================== */

	public int GetPlayerBalance(IPlayer player, string walletKind)
	{
		if (player.IsFakeClient) return 0;
		return GetPlayerBalance(player.SteamID, walletKind);
	}

	public int GetPlayerBalance(int playerId, string walletKind)
	{
		var player = _core.PlayerManager.GetPlayer(playerId);
		if (player == null || player.IsFakeClient) return 0;
		return GetPlayerBalance(player.SteamID, walletKind);
	}

	public int GetPlayerBalance(ulong steamId, string walletKind)
	{
		if (!_walletKinds.ContainsKey(walletKind))
			throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");

		// Try cache first (online players)
		if (_playerBalances.TryGetValue(steamId, out var balances) && balances.TryGetValue(walletKind, out var cachedBalance))
			return cachedBalance;

		// Fallback to DB for offline players
		var user = LoadFromDatabase(steamId);
		return user?.Balance.TryGetValue(walletKind, out var dbBal) == true ? (int)dbBal : 0;
	}

	/* ==================== Has Sufficient Funds ==================== */

	public bool HasSufficientFunds(IPlayer player, string walletKind, int amount)
	{
		if (player.IsFakeClient) return false;
		return HasSufficientFunds(player.SteamID, walletKind, amount);
	}

	public bool HasSufficientFunds(int playerId, string walletKind, int amount)
	{
		var player = _core.PlayerManager.GetPlayer(playerId);
		if (player == null || player.IsFakeClient) return false;
		return HasSufficientFunds(player.SteamID, walletKind, amount);
	}

	public bool HasSufficientFunds(ulong steamId, string walletKind, int amount)
	{
		if (!_walletKinds.ContainsKey(walletKind))
			throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");

		return GetPlayerBalance(steamId, walletKind) >= amount;
	}

	/* ==================== Set Balance ==================== */

	public void SetPlayerBalance(IPlayer player, string walletKind, int amount)
	{
		if (player.IsFakeClient) return;
		SetPlayerBalance(player.SteamID, walletKind, amount);
	}

	public void SetPlayerBalance(int playerId, string walletKind, int amount)
	{
		var player = _core.PlayerManager.GetPlayer(playerId);
		if (player == null || player.IsFakeClient) return;
		SetPlayerBalance(player.SteamID, walletKind, amount);
	}

	public void SetPlayerBalance(ulong steamId, string walletKind, int amount)
	{
		if (!_walletKinds.ContainsKey(walletKind))
			throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");

		// Clamp negative if not allowed
		if (!_config.AllowNegativeBalance && amount < 0)
			amount = 0;

		if (IsOnline(steamId))
		{
			// Online: update cache + queue save
			var playerLock = GetPlayerLock(steamId);
			lock (playerLock)
			{
				var balances = _playerBalances.GetOrAdd(steamId, _ => new ConcurrentDictionary<string, int>());
				balances.TryGetValue(walletKind, out var oldBalance);
				balances[walletKind] = amount;

				MarkDirty(steamId);
				EnqueueSave(steamId);
				OnPlayerBalanceChanged?.Invoke(steamId, walletKind, amount, oldBalance);
			}
		}
		else
		{
			// Offline: update DB directly with async lock to prevent race conditions
			Task.Run(async () =>
			{
				var asyncLock = GetAsyncPlayerLock(steamId);
				await asyncLock.WaitAsync();
				try
				{
					var user = await LoadOrCreateFromDatabaseAsync(steamId);
					user.Balance.TryGetValue(walletKind, out var oldBalance);
					user.Balance[walletKind] = amount;

					SaveToDatabase(user);
					OnPlayerBalanceChanged?.Invoke(steamId, walletKind, amount, oldBalance);
				}
				catch (Exception ex)
				{
					_core.Logger.LogError(ex, "Failed to set balance for offline player {SteamId}", steamId);
				}
				finally
				{
					asyncLock.Release();
				}
			});
		}
	}

	/* ==================== Add Balance ==================== */

	public void AddPlayerBalance(IPlayer player, string walletKind, int amount)
	{
		if (player.IsFakeClient) return;
		AddPlayerBalance(player.SteamID, walletKind, amount);
	}

	public void AddPlayerBalance(int playerId, string walletKind, int amount)
	{
		var player = _core.PlayerManager.GetPlayer(playerId);
		if (player == null || player.IsFakeClient) return;
		AddPlayerBalance(player.SteamID, walletKind, amount);
	}

	public void AddPlayerBalance(ulong steamId, string walletKind, int amount)
	{
		if (!_walletKinds.ContainsKey(walletKind))
			throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");

		if (IsOnline(steamId))
		{
			var playerLock = GetPlayerLock(steamId);
			lock (playerLock)
			{
				var balances = _playerBalances.GetOrAdd(steamId, _ => new ConcurrentDictionary<string, int>());
				balances.TryGetValue(walletKind, out var currentBalance);
				var oldBalance = currentBalance;
				currentBalance += amount;
				balances[walletKind] = currentBalance;

				MarkDirty(steamId);
				EnqueueSave(steamId);
				OnPlayerBalanceChanged?.Invoke(steamId, walletKind, currentBalance, oldBalance);
			}
		}
		else
		{
			// Offline: update DB directly with async lock to prevent race conditions
			Task.Run(async () =>
			{
				var asyncLock = GetAsyncPlayerLock(steamId);
				await asyncLock.WaitAsync();
				try
				{
					var user = await LoadOrCreateFromDatabaseAsync(steamId);
					user.Balance.TryGetValue(walletKind, out var currentBalance);
					var oldBalance = currentBalance;
					currentBalance += amount;
					user.Balance[walletKind] = currentBalance;

					SaveToDatabase(user);
					OnPlayerBalanceChanged?.Invoke(steamId, walletKind, currentBalance, oldBalance);
				}
				catch (Exception ex)
				{
					_core.Logger.LogError(ex, "Failed to add balance for offline player {SteamId}", steamId);
				}
				finally
				{
					asyncLock.Release();
				}
			});
		}
	}

	/* ==================== Subtract Balance ==================== */

	public void SubtractPlayerBalance(IPlayer player, string walletKind, int amount)
	{
		if (player.IsFakeClient) return;
		SubtractPlayerBalance(player.SteamID, walletKind, amount);
	}

	public void SubtractPlayerBalance(int playerId, string walletKind, int amount)
	{
		var player = _core.PlayerManager.GetPlayer(playerId);
		if (player == null || player.IsFakeClient) return;
		SubtractPlayerBalance(player.SteamID, walletKind, amount);
	}

	public void SubtractPlayerBalance(ulong steamId, string walletKind, int amount)
	{
		if (!_walletKinds.ContainsKey(walletKind))
			throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");

		if (IsOnline(steamId))
		{
			var playerLock = GetPlayerLock(steamId);
			lock (playerLock)
			{
				var balances = _playerBalances.GetOrAdd(steamId, _ => new ConcurrentDictionary<string, int>());
				balances.TryGetValue(walletKind, out var currentBalance);
				var oldBalance = currentBalance;
				currentBalance -= amount;

				if (!_config.AllowNegativeBalance && currentBalance < 0)
					currentBalance = 0;

				balances[walletKind] = currentBalance;

				MarkDirty(steamId);
				EnqueueSave(steamId);
				OnPlayerBalanceChanged?.Invoke(steamId, walletKind, currentBalance, oldBalance);
			}
		}
		else
		{
			// Offline: update DB directly with async lock to prevent race conditions
			Task.Run(async () =>
			{
				var asyncLock = GetAsyncPlayerLock(steamId);
				await asyncLock.WaitAsync();
				try
				{
					var user = await LoadOrCreateFromDatabaseAsync(steamId);
					user.Balance.TryGetValue(walletKind, out var currentBalance);
					var oldBalance = currentBalance;
					currentBalance -= amount;

					if (!_config.AllowNegativeBalance && currentBalance < 0)
						currentBalance = 0;

					user.Balance[walletKind] = currentBalance;

					SaveToDatabase(user);
					OnPlayerBalanceChanged?.Invoke(steamId, walletKind, currentBalance, oldBalance);
				}
				catch (Exception ex)
				{
					_core.Logger.LogError(ex, "Failed to subtract balance for offline player {SteamId}", steamId);
				}
				finally
				{
					asyncLock.Release();
				}
			});
		}
	}
}
