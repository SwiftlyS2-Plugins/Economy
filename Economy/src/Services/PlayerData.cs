using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Players;

namespace Economy.Services;

public partial class EconomyService
{
	/* ==================== Load Player Data ==================== */

	public void LoadData(IPlayer player)
	{
		var steamId = player.SteamID;
		_onlinePlayers[steamId] = player;

		try
		{
			var repository = CreateBalanceRepository();
			var balances = _playerBalances.GetOrAdd(steamId, _ => new ConcurrentDictionary<string, decimal>());
			var initialBalances = _initialBalances.GetOrAdd(steamId, _ => new ConcurrentDictionary<string, decimal>());
			var balanceRecords = repository.GetAllBalances((long)steamId);

			if (balanceRecords.Count > 0)
			{
				foreach (var record in balanceRecords)
				{
					if (_walletKinds.ContainsKey(record.WalletKind))
					{
						balances[record.WalletKind] = record.BalanceAmount;
						initialBalances[record.WalletKind] = record.BalanceAmount;
					}
				}
			}
			else
			{
				// New player - create initial balance records with 0
				foreach (var walletKind in _walletKinds.Keys)
				{
					repository.InsertBalance((long)steamId, walletKind, 0);
					balances[walletKind] = 0;
					initialBalances[walletKind] = 0;
				}
			}

			OnPlayerLoad?.Invoke(player);
		}
		catch (Exception ex)
		{
			_core.Logger.LogError(ex, "Failed to load economy data for player {SteamId}", steamId);
		}
	}

	/* ==================== Save Player Data ==================== */

	public void SaveData(IPlayer player)
	{
		if (SaveDataInternal(player.SteamID))
		{
			OnPlayerSave?.Invoke(player);
		}
	}

	public void SaveData(int playerId)
	{
		var player = _core.PlayerManager.GetPlayer(playerId);
		if (player == null) return;

		if (SaveDataInternal(player.SteamID))
		{
			OnPlayerSave?.Invoke(player);
		}
	}

	public void SaveData(ulong steamId)
	{
		SaveDataInternal(steamId);
	}

	private bool SaveDataInternal(ulong steamId)
	{
		if (!IsDirty(steamId))
			return false;

		try
		{
			if (!_playerBalances.TryGetValue(steamId, out var balances))
				return false;

			if (!_initialBalances.TryGetValue(steamId, out var initialBalances))
				return false;

			var repository = CreateBalanceRepository();

			// Calculate deltas and apply them to current DB values
			foreach (var (walletKind, currentCachedBalance) in balances)
			{
				// Get the initial balance we loaded
				initialBalances.TryGetValue(walletKind, out var initialBalance);
				
				// Calculate the change that happened during the session
				var delta = currentCachedBalance - initialBalance;

				if (delta != 0)
				{
					// Read current DB value (may have been modified externally)
					var dbBalance = repository.GetBalance((long)steamId, walletKind);
					var currentDbBalance = dbBalance?.BalanceAmount ?? 0;

					// Apply delta to current DB value
					var newBalance = currentDbBalance + delta;

					// Apply negative balance constraint if configured
					if (!_config.AllowNegativeBalance && newBalance < 0)
						newBalance = 0;

					repository.UpsertBalance((long)steamId, walletKind, newBalance);

					// Update our cache and initial values to the new balance
					balances[walletKind] = newBalance;
					initialBalances[walletKind] = newBalance;
				}
			}

			ClearDirty(steamId);
			return true;
		}
		catch (Exception ex)
		{
			_core.Logger.LogError(ex, "Failed to save economy data for player {SteamId}", steamId);
			return false;
		}
	}

	/* ==================== Batch Save (Round End) ==================== */

	public void SaveAllOnlinePlayers()
	{
		var steamIds = GetAllOnlineSteamIds();

		foreach (var steamId in steamIds)
		{
			try
			{
				SaveDataInternal(steamId);
			}
			catch (Exception ex)
			{
				_core.Logger.LogError(ex, "Failed to save economy data for player {SteamId} during batch save", steamId);
			}
		}
	}

	/* ==================== Cleanup ==================== */

	public void RemovePlayer(ulong steamId)
	{
		_playerBalances.TryRemove(steamId, out _);
		_initialBalances.TryRemove(steamId, out _);
		_onlinePlayers.TryRemove(steamId, out _);
		_playerLocks.TryRemove(steamId, out _);
		ClearDirty(steamId);
	}
}
