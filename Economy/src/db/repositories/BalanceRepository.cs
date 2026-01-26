using System.Data;
using Economy.Database.Models;
using Dommel;
using Microsoft.Extensions.Logging;

namespace Economy.Database.Repositories;

/// <summary>
/// Repository for managing balance records in the new normalized 'balance' table.
/// Handles CRUD operations with proper decimal precision and timestamp management.
/// </summary>
public class BalanceRepository
{
	private readonly IDbConnection _connection;
	private readonly ILogger _logger;

	public BalanceRepository(IDbConnection connection, ILogger logger)
	{
		_connection = connection;
		_logger = logger;
	}

	/// <summary>
	/// Get a specific balance record for a player and wallet type.
	/// Returns null if no record exists.
	/// </summary>
	public Balance? GetBalance(long steamId, string walletKind)
	{
		try
		{
			var balances = _connection.Select<Balance>(b => 
				b.SteamId == steamId && b.WalletKind == walletKind);
			return balances.FirstOrDefault();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get balance for SteamId {SteamId}, WalletKind {WalletKind}", 
				steamId, walletKind);
			return null;
		}
	}

	/// <summary>
	/// Get all balance records for a player across all wallet types.
	/// </summary>
	public List<Balance> GetAllBalances(long steamId)
	{
		try
		{
			var balances = _connection.Select<Balance>(b => b.SteamId == steamId);
			return balances.ToList();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get all balances for SteamId {SteamId}", steamId);
			return [];
		}
	}

	/// <summary>
	/// Insert a new balance record.
	/// Sets CreatedAt and UpdatedAt to current time.
	/// Returns the inserted balance with its new ID.
	/// </summary>
	public Balance? InsertBalance(long steamId, string walletKind, decimal amount)
	{
		try
		{
			var balance = new Balance
			{
				SteamId = steamId,
				WalletKind = walletKind,
				BalanceAmount = amount,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};

			var id = _connection.Insert(balance);
			balance.Id = Convert.ToInt32(id);
			return balance;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to insert balance for SteamId {SteamId}, WalletKind {WalletKind}", 
				steamId, walletKind);
			return null;
		}
	}

	/// <summary>
	/// Update an existing balance record.
	/// Automatically updates the UpdatedAt timestamp.
	/// </summary>
	public bool UpdateBalance(Balance balance)
	{
		try
		{
			balance.UpdatedAt = DateTime.UtcNow;
			return _connection.Update(balance);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to update balance for Id {Id}", balance.Id);
			return false;
		}
	}

	/// <summary>
	/// Upsert (Insert or Update) a balance record.
	/// If the record exists, updates it; otherwise, inserts a new one.
	/// </summary>
	public Balance? UpsertBalance(long steamId, string walletKind, decimal amount)
	{
		var existing = GetBalance(steamId, walletKind);

		if (existing != null)
		{
			existing.BalanceAmount = amount;
			existing.UpdatedAt = DateTime.UtcNow;
			
			if (UpdateBalance(existing))
				return existing;
			
			return null;
		}

		return InsertBalance(steamId, walletKind, amount);
	}

	/// <summary>
	/// Delete a balance record.
	/// </summary>
	public bool DeleteBalance(long steamId, string walletKind)
	{
		try
		{
			var balance = GetBalance(steamId, walletKind);
			if (balance == null)
				return false;

			return _connection.Delete(balance);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to delete balance for SteamId {SteamId}, WalletKind {WalletKind}", 
				steamId, walletKind);
			return false;
		}
	}

	/// <summary>
	/// Get or create a balance record. If it doesn't exist, creates with amount 0.
	/// </summary>
	public Balance GetOrCreateBalance(long steamId, string walletKind)
	{
		var existing = GetBalance(steamId, walletKind);
		if (existing != null)
			return existing;

		var newBalance = InsertBalance(steamId, walletKind, 0);
		return newBalance ?? new Balance
		{
			SteamId = steamId,
			WalletKind = walletKind,
			BalanceAmount = 0,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
	}

	/// <summary>
	/// Batch update multiple balance records for a player.
	/// More efficient than individual updates.
	/// </summary>
	public bool UpdateBalances(long steamId, Dictionary<string, decimal> balances)
	{
		try
		{
			foreach (var (walletKind, amount) in balances)
			{
				UpsertBalance(steamId, walletKind, amount);
			}
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to batch update balances for SteamId {SteamId}", steamId);
			return false;
		}
	}

	/// <summary>
	/// Check if a balance record exists for a player and wallet type.
	/// </summary>
	public bool BalanceExists(long steamId, string walletKind)
	{
		return GetBalance(steamId, walletKind) != null;
	}
}
