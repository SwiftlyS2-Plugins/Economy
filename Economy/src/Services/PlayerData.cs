using System.Collections.Concurrent;
using Economy.Database.Models;
using Dommel;
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
			var user = LoadFromDatabase(steamId);
			var balances = _playerBalances.GetOrAdd(steamId, _ => new ConcurrentDictionary<string, int>());

			if (user != null)
			{
				foreach (var (walletKind, balance) in user.Balance)
				{
					if (_walletKinds.ContainsKey(walletKind))
						balances[walletKind] = (int)balance;
				}
			}
			else
			{
				// New player - create DB record
				var newUser = new EconomyPlayer
				{
					SteamId64 = (long)steamId,
					Balance = []
				};
				InsertToDatabase(newUser);
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
		// Note: No OnPlayerSave event here since we don't have IPlayer reference
	}

	/// <summary>
	/// Internal save method that returns true if data was actually saved
	/// </summary>
	private bool SaveDataInternal(ulong steamId)
	{
		// Skip if not dirty
		if (!IsDirty(steamId))
			return false;

		try
		{
			if (!_playerBalances.TryGetValue(steamId, out var balances))
				return false;

			var user = LoadFromDatabase(steamId);

			if (user == null)
			{
				user = new EconomyPlayer
				{
					SteamId64 = (long)steamId,
					Balance = []
				};
				InsertToDatabase(user);
			}

			foreach (var (walletKind, balance) in balances)
			{
				user.Balance[walletKind] = balance;
			}

			SaveToDatabase(user);
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
		// Take a snapshot to avoid iteration issues if players join/leave during save
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

	/* ==================== Database Helpers ==================== */

	private EconomyPlayer? LoadFromDatabase(ulong steamId)
	{
		var connection = _core.Database.GetConnection(_config.DatabaseConnection);
		var users = connection.Select<EconomyPlayer>(u => u.SteamId64 == (long)steamId);
		return users.FirstOrDefault();
	}

	private async Task<EconomyPlayer> LoadOrCreateFromDatabaseAsync(ulong steamId)
	{
		var connection = _core.Database.GetConnection(_config.DatabaseConnection);
		var users = await connection.SelectAsync<EconomyPlayer>(u => u.SteamId64 == (long)steamId);
		var user = users.FirstOrDefault();

		if (user == null)
		{
			user = new EconomyPlayer
			{
				SteamId64 = (long)steamId,
				Balance = []
			};
			var id = await connection.InsertAsync(user);
			user.Id = (ulong)id;
		}

		return user;
	}

	private void InsertToDatabase(EconomyPlayer user)
	{
		var connection = _core.Database.GetConnection(_config.DatabaseConnection);
		connection.Insert(user);
	}

	private void SaveToDatabase(EconomyPlayer user)
	{
		var connection = _core.Database.GetConnection(_config.DatabaseConnection);
		connection.Update(user);
	}
}
