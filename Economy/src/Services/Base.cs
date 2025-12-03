using System.Collections.Concurrent;
using Economy.Config;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace Economy.Services;

/// <summary>
/// Core economy service handling player balances with caching
/// </summary>
public partial class EconomyService(ISwiftlyCore core, PluginConfig config)
{
	private readonly ISwiftlyCore _core = core;
	private readonly PluginConfig _config = config;

	// Registered wallet types
	private readonly ConcurrentDictionary<string, bool> _walletKinds = new();

	// Online player balances (steamid -> walletKind -> balance)
	private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, int>> _playerBalances = new();

	// Dirty tracking - players with unsaved changes
	private readonly ConcurrentDictionary<ulong, bool> _dirtyPlayers = new();

	// Player tracking
	private readonly ConcurrentDictionary<ulong, IPlayer> _onlinePlayers = new();

	// Save queue for batched writes - using HashSet for O(1) contains check
	private readonly HashSet<ulong> _saveQueue = [];
	private readonly object _saveQueueLock = new();

	// Thread-safe locks per player (for online sync operations)
	private readonly ConcurrentDictionary<ulong, object> _playerLocks = new();

	// Async locks per player (for offline DB operations to prevent race conditions)
	private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _asyncPlayerLocks = new();

	// Events
	public event Action<ulong, string, long, long>? OnPlayerBalanceChanged;
	public event Action<ulong, ulong, string, long>? OnPlayerFundsTransferred;
	public event Action<IPlayer>? OnPlayerLoad;
	public event Action<IPlayer>? OnPlayerSave;

	/* ==================== Wallet Kind Management ==================== */

	public void EnsureWalletKind(string kindName)
	{
		_walletKinds.TryAdd(kindName, true);
	}

	public bool WalletKindExists(string kindName)
	{
		return _walletKinds.ContainsKey(kindName);
	}

	/* ==================== Internal Helpers ==================== */

	private object GetPlayerLock(ulong steamId) => _playerLocks.GetOrAdd(steamId, _ => new object());

	private SemaphoreSlim GetAsyncPlayerLock(ulong steamId) => _asyncPlayerLocks.GetOrAdd(steamId, _ => new SemaphoreSlim(1, 1));

	private bool IsOnline(ulong steamId) => _onlinePlayers.ContainsKey(steamId);

	/* ==================== Dirty Tracking ==================== */

	private void MarkDirty(ulong steamId) => _dirtyPlayers[steamId] = true;

	private void ClearDirty(ulong steamId) => _dirtyPlayers.TryRemove(steamId, out _);

	internal bool IsDirty(ulong steamId) => _dirtyPlayers.ContainsKey(steamId);

	/* ==================== Save Queue ==================== */

	internal void EnqueueSave(ulong steamId)
	{
		lock (_saveQueueLock)
		{
			_saveQueue.Add(steamId); // HashSet.Add is idempotent - O(1)
		}
	}

	internal bool TryDequeueSave(out ulong steamId)
	{
		lock (_saveQueueLock)
		{
			if (_saveQueue.Count == 0)
			{
				steamId = 0;
				return false;
			}

			steamId = _saveQueue.First();
			_saveQueue.Remove(steamId);
			return true;
		}
	}

	/// <summary>
	/// Returns a snapshot of all online player SteamIds to avoid iteration issues
	/// </summary>
	internal List<ulong> GetAllOnlineSteamIds() => [.. _onlinePlayers.Keys];
}
