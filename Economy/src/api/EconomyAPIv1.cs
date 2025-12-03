using System.Collections.Concurrent;
using Dommel;
using Economy.Contract;
using Economy.Database.Models;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

using static Economy.Economy;

namespace Economy.API;

public class EconomyAPIv1 : IEconomyAPIv1
{
    private readonly ISwiftlyCore _core;
    private readonly PluginConfig _config;
    private readonly ConcurrentDictionary<string, bool> _walletKinds;
    private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, int>> _playerBalances;
    private readonly ConcurrentQueue<IPlayer> _playerSaveQueue;
    private readonly ConcurrentDictionary<ulong, IPlayer> _playerBySteamId;

    // Lock objects for thread-safe balance operations
    private readonly ConcurrentDictionary<ulong, object> _playerLocks = new();

    public event Action<ulong, string, long, long>? OnPlayerBalanceChanged;
    public event Action<ulong, ulong, string, long>? OnPlayerFundsTransferred;
    public event Action<IPlayer>? OnPlayerLoad;
    public event Action<IPlayer>? OnPlayerSave;

    public EconomyAPIv1(
        ISwiftlyCore core,
        PluginConfig config,
        ref ConcurrentDictionary<string, bool> walletKinds,
        ref ConcurrentDictionary<ulong, ConcurrentDictionary<string, int>> playerBalances,
        ref ConcurrentQueue<IPlayer> playerSaveQueue,
        ref ConcurrentDictionary<ulong, IPlayer> playerBySteamId
    )
    {
        _core = core;
        _config = config;
        _walletKinds = walletKinds;
        _playerBalances = playerBalances;
        _playerSaveQueue = playerSaveQueue;
        _playerBySteamId = playerBySteamId;
    }

    private object GetPlayerLock(ulong steamid) => _playerLocks.GetOrAdd(steamid, _ => new object());

    public void AddPlayerBalance(IPlayer player, string walletKind, int amount)
    {
        AddPlayerBalance(player.SteamID, walletKind, amount);
    }

    public void AddPlayerBalance(int playerid, string walletKind, int amount)
    {
        var player = _core.PlayerManager.GetPlayer(playerid);
        if (player == null) return;

        AddPlayerBalance(player.SteamID, walletKind, amount);
    }

    public void AddPlayerBalance(ulong steamid, string walletKind, int amount)
    {
        if (!_walletKinds.ContainsKey(walletKind))
            throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");

        var playerLock = GetPlayerLock(steamid);

        if (!_playerBySteamId.TryGetValue(steamid, out IPlayer? player))
        {
            // Offline player - update DB directly
            Task.Run(async () =>
            {
                try
                {
                    var connection = _core.Database.GetConnection(_config.DatabaseConnection);
                    var users = await connection.SelectAsync<EconomyPlayer>(u => u.SteamId64 == (long)steamid);
                    var user = users.FirstOrDefault();

                    if (user == null)
                    {
                        user = new EconomyPlayer
                        {
                            SteamId64 = (long)steamid,
                            Balance = []
                        };
                        var id = await connection.InsertAsync(user);
                        user.Id = (ulong)id;
                    }

                    lock (playerLock)
                    {
                        user.Balance.TryGetValue(walletKind, out var currentBalance);
                        var oldBalance = currentBalance;
                        currentBalance += amount;
                        user.Balance[walletKind] = currentBalance;

                        connection.Update(user);
                        OnPlayerBalanceChanged?.Invoke(steamid, walletKind, currentBalance, oldBalance);
                    }
                }
                catch (Exception ex)
                {
                    _core.Logger.LogError(ex, "Failed to add balance for offline player {SteamId}", steamid);
                }
            });
        }
        else
        {
            // Online player - update cache
            lock (playerLock)
            {
                if (!_playerBalances.TryGetValue(steamid, out var balances))
                {
                    balances = new ConcurrentDictionary<string, int>();
                    _playerBalances[steamid] = balances;
                }

                balances.TryGetValue(walletKind, out var currentBalance);
                var oldBalance = currentBalance;
                currentBalance += amount;
                balances[walletKind] = currentBalance;

                _playerSaveQueue.Enqueue(player);
                OnPlayerBalanceChanged?.Invoke(steamid, walletKind, currentBalance, oldBalance);
            }
        }
    }

    public void EnsureWalletKind(string kindName)
    {
        _walletKinds.TryAdd(kindName, true);
    }

    public int GetPlayerBalance(IPlayer player, string walletKind)
    {
        return GetPlayerBalance(player.SteamID, walletKind);
    }

    public int GetPlayerBalance(int playerid, string walletKind)
    {
        var player = _core.PlayerManager.GetPlayer(playerid);
        if (player == null) return 0;

        return GetPlayerBalance(player.SteamID, walletKind);
    }

    public int GetPlayerBalance(ulong steamid, string walletKind)
    {
        if (!_walletKinds.ContainsKey(walletKind))
            throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");

        // Try cache first
        if (_playerBalances.TryGetValue(steamid, out var balances) && balances.TryGetValue(walletKind, out var cachedBalance))
            return cachedBalance;

        // Fallback to DB for offline players
        var connection = _core.Database.GetConnection(_config.DatabaseConnection);
        var users = connection.Select<EconomyPlayer>(u => u.SteamId64 == (long)steamid);
        var user = users.FirstOrDefault();

        if (user == null) return 0;

        return user.Balance.TryGetValue(walletKind, out var balance) ? (int)balance : 0;
    }

    public bool HasSufficientFunds(IPlayer player, string walletKind, int amount)
    {
        return HasSufficientFunds(player.SteamID, walletKind, amount);
    }

    public bool HasSufficientFunds(int playerid, string walletKind, int amount)
    {
        var player = _core.PlayerManager.GetPlayer(playerid);
        if (player == null) return false;

        return HasSufficientFunds(player.SteamID, walletKind, amount);
    }

    public bool HasSufficientFunds(ulong steamid, string walletKind, int amount)
    {
        if (!_walletKinds.ContainsKey(walletKind))
            throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");

        var balance = GetPlayerBalance(steamid, walletKind);
        return balance >= amount;
    }

    public void SetPlayerBalance(IPlayer player, string walletKind, int amount)
    {
        SetPlayerBalance(player.SteamID, walletKind, amount);
    }

    public void SetPlayerBalance(int playerid, string walletKind, int amount)
    {
        var player = _core.PlayerManager.GetPlayer(playerid);
        if (player == null) return;

        SetPlayerBalance(player.SteamID, walletKind, amount);
    }

    public void SetPlayerBalance(ulong steamid, string walletKind, int amount)
    {
        if (!_walletKinds.ContainsKey(walletKind))
            throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");

        // Block negative if not allowed
        if (!_config.AllowNegativeBalance && amount < 0)
            amount = 0;

        var playerLock = GetPlayerLock(steamid);

        if (!_playerBySteamId.TryGetValue(steamid, out IPlayer? player))
        {
            Task.Run(async () =>
            {
                try
                {
                    var connection = _core.Database.GetConnection(_config.DatabaseConnection);
                    var users = await connection.SelectAsync<EconomyPlayer>(u => u.SteamId64 == (long)steamid);
                    var user = users.FirstOrDefault();

                    if (user == null)
                    {
                        user = new EconomyPlayer
                        {
                            SteamId64 = (long)steamid,
                            Balance = []
                        };
                        var id = await connection.InsertAsync(user);
                        user.Id = (ulong)id;
                    }

                    lock (playerLock)
                    {
                        user.Balance.TryGetValue(walletKind, out var oldBalance);
                        user.Balance[walletKind] = amount;

                        connection.Update(user);
                        OnPlayerBalanceChanged?.Invoke(steamid, walletKind, amount, oldBalance);
                    }
                }
                catch (Exception ex)
                {
                    _core.Logger.LogError(ex, "Failed to set balance for offline player {SteamId}", steamid);
                }
            });
        }
        else
        {
            lock (playerLock)
            {
                if (!_playerBalances.TryGetValue(steamid, out var balances))
                {
                    balances = new ConcurrentDictionary<string, int>();
                    _playerBalances[steamid] = balances;
                }

                balances.TryGetValue(walletKind, out var oldBalance);
                balances[walletKind] = amount;

                _playerSaveQueue.Enqueue(player);
                OnPlayerBalanceChanged?.Invoke(steamid, walletKind, amount, oldBalance);
            }
        }
    }

    public void SubtractPlayerBalance(IPlayer player, string walletKind, int amount)
    {
        SubtractPlayerBalance(player.SteamID, walletKind, amount);
    }

    public void SubtractPlayerBalance(int playerid, string walletKind, int amount)
    {
        var player = _core.PlayerManager.GetPlayer(playerid);
        if (player == null) return;

        SubtractPlayerBalance(player.SteamID, walletKind, amount);
    }

    public void SubtractPlayerBalance(ulong steamid, string walletKind, int amount)
    {
        if (!_walletKinds.ContainsKey(walletKind))
            throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");

        var playerLock = GetPlayerLock(steamid);

        if (!_playerBySteamId.TryGetValue(steamid, out IPlayer? player))
        {
            Task.Run(async () =>
            {
                try
                {
                    var connection = _core.Database.GetConnection(_config.DatabaseConnection);
                    var users = await connection.SelectAsync<EconomyPlayer>(u => u.SteamId64 == (long)steamid);
                    var user = users.FirstOrDefault();

                    if (user == null)
                    {
                        user = new EconomyPlayer
                        {
                            SteamId64 = (long)steamid,
                            Balance = []
                        };
                        var id = await connection.InsertAsync(user);
                        user.Id = (ulong)id;
                    }

                    lock (playerLock)
                    {
                        user.Balance.TryGetValue(walletKind, out var currentBalance);
                        var oldBalance = currentBalance;
                        currentBalance -= amount;

                        // Clamp to 0 if negative not allowed
                        if (!_config.AllowNegativeBalance && currentBalance < 0)
                            currentBalance = 0;

                        user.Balance[walletKind] = currentBalance;
                        connection.Update(user);
                        OnPlayerBalanceChanged?.Invoke(steamid, walletKind, currentBalance, oldBalance);
                    }
                }
                catch (Exception ex)
                {
                    _core.Logger.LogError(ex, "Failed to subtract balance for offline player {SteamId}", steamid);
                }
            });
        }
        else
        {
            lock (playerLock)
            {
                if (!_playerBalances.TryGetValue(steamid, out var balances))
                {
                    balances = new ConcurrentDictionary<string, int>();
                    _playerBalances[steamid] = balances;
                }

                balances.TryGetValue(walletKind, out var currentBalance);
                var oldBalance = currentBalance;
                currentBalance -= amount;

                // Clamp to 0 if negative not allowed
                if (!_config.AllowNegativeBalance && currentBalance < 0)
                    currentBalance = 0;

                balances[walletKind] = currentBalance;

                _playerSaveQueue.Enqueue(player);
                OnPlayerBalanceChanged?.Invoke(steamid, walletKind, currentBalance, oldBalance);
            }
        }
    }

    public void TransferFunds(IPlayer fromPlayer, IPlayer toPlayer, string walletKind, int amount)
    {
        TransferFunds(fromPlayer.SteamID, toPlayer.SteamID, walletKind, amount);
    }

    public void TransferFunds(int fromPlayerid, int toPlayerid, string walletKind, int amount)
    {
        var fromPlayer = _core.PlayerManager.GetPlayer(fromPlayerid);
        var toPlayer = _core.PlayerManager.GetPlayer(toPlayerid);
        if (fromPlayer == null || toPlayer == null) return;

        TransferFunds(fromPlayer.SteamID, toPlayer.SteamID, walletKind, amount);
    }

    public void TransferFunds(ulong fromSteamid, ulong toSteamid, string walletKind, int amount)
    {
        if (!_walletKinds.ContainsKey(walletKind))
            throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");

        // Check funds before transfer (unless negative allowed)
        if (!_config.AllowNegativeBalance && !HasSufficientFunds(fromSteamid, walletKind, amount))
            throw new InvalidOperationException($"Insufficient funds for transfer. Required: {amount}");

        SubtractPlayerBalance(fromSteamid, walletKind, amount);
        AddPlayerBalance(toSteamid, walletKind, amount);

        OnPlayerFundsTransferred?.Invoke(fromSteamid, toSteamid, walletKind, amount);
    }

    public bool WalletKindExists(string kindName)
    {
        return _walletKinds.ContainsKey(kindName);
    }

    public void LoadData(IPlayer player)
    {
        _playerBySteamId[player.SteamID] = player;

        try
        {
            var database = _core.Database.GetConnection(_config.DatabaseConnection);
            var users = database.Select<EconomyPlayer>(p => p.SteamId64 == (long)player.SteamID);
            var user = users.FirstOrDefault();

            // Init player balance dict
            var balances = _playerBalances.GetOrAdd(player.SteamID, _ => new ConcurrentDictionary<string, int>());

            if (user != null)
            {
                foreach (var (walletKind, balance) in user.Balance)
                {
                    // Only load known wallet kinds
                    if (_walletKinds.ContainsKey(walletKind))
                        balances[walletKind] = (int)balance;
                }
            }
            else
            {
                // New player - create DB record
                var newUser = new EconomyPlayer
                {
                    SteamId64 = (long)player.SteamID,
                    Balance = []
                };
                database.Insert(newUser);
            }

            OnPlayerLoad?.Invoke(player);
        }
        catch (Exception ex)
        {
            _core.Logger.LogError(ex, "Failed to load economy data for player {SteamId}", player.SteamID);
        }
    }

    public void SaveData(IPlayer player)
    {
        SaveData(player.SteamID);
        OnPlayerSave?.Invoke(player);
    }

    public void SaveData(int playerid)
    {
        var player = _core.PlayerManager.GetPlayer(playerid);
        if (player == null) return;

        SaveData(player.SteamID);
        OnPlayerSave?.Invoke(player);
    }

    public void SaveData(ulong steamid)
    {
        try
        {
            var connection = _core.Database.GetConnection(_config.DatabaseConnection);
            var users = connection.Select<EconomyPlayer>(u => u.SteamId64 == (long)steamid);
            var user = users.FirstOrDefault();

            if (user == null)
            {
                user = new EconomyPlayer
                {
                    SteamId64 = (long)steamid,
                    Balance = []
                };
                var id = connection.Insert(user);
                user.Id = (ulong)id;
            }

            if (_playerBalances.TryGetValue(steamid, out var balances))
            {
                foreach (var (walletKind, balance) in balances)
                {
                    user.Balance[walletKind] = balance;
                }
            }

            connection.Update(user);
        }
        catch (Exception ex)
        {
            _core.Logger.LogError(ex, "Failed to save economy data for player {SteamId}", steamid);
        }
    }
}
