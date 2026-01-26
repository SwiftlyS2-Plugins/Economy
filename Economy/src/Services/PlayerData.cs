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
			var balanceRecords = repository.GetAllBalances((long)steamId);

			if (balanceRecords.Count > 0)
			{
				foreach (var record in balanceRecords)
				{
					if (_walletKinds.ContainsKey(record.WalletKind))
						balances[record.WalletKind] = record.BalanceAmount;
				}
			}
			else
			{
				// New player - create initial balance records with 0
				foreach (var walletKind in _walletKinds.Keys)
				{
					repository.InsertBalance((long)steamId, walletKind, 0);
					balances[walletKind] = 0;
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

			var repository = CreateBalanceRepository();

			foreach (var (walletKind, balance) in balances)
			{
				repository.UpsertBalance((long)steamId, walletKind, balance);
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
		_onlinePlayers.TryRemove(steamId, out _);
		_playerLocks.TryRemove(steamId, out _);
		ClearDirty(steamId);
	}
}
