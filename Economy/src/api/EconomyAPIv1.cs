using System.Collections.Concurrent;
using System.Text.Json;
using Dommel;
using Economy.Contract;
using Economy.Database.Models;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace Economy.API;

public class EconomyAPIv1 : IEconomyAPIv1
{
    private readonly ISwiftlyCore swiftlyCore;
    private readonly ConcurrentDictionary<string, bool> walletKinds;
    private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, int>> playerBalances;
    private readonly ConcurrentQueue<IPlayer> playerSaveQueue;
    private readonly ConcurrentDictionary<ulong, IPlayer> playerBySteamId;

    public event Action<ulong, string, long, long>? OnPlayerBalanceChanged;
    public event Action<ulong, ulong, string, long>? OnPlayerFundsTransferred;
    public event Action<IPlayer>? OnPlayerLoad;
    public event Action<IPlayer>? OnPlayerSave;

    public EconomyAPIv1(
        ISwiftlyCore core,
        ref ConcurrentDictionary<string, bool> walletKinds,
        ref ConcurrentDictionary<ulong, ConcurrentDictionary<string, int>> playerBalances,
        ref ConcurrentQueue<IPlayer> playerSaveQueue,
        ref ConcurrentDictionary<ulong, IPlayer> playerBySteamId
    )
    {
        swiftlyCore = core;
        this.walletKinds = walletKinds;
        this.playerBalances = playerBalances;
        this.playerSaveQueue = playerSaveQueue;
        this.playerBySteamId = playerBySteamId;
    }

    public void AddPlayerBalance(IPlayer player, string walletKind, int amount)
    {
        AddPlayerBalance(player.SteamID, walletKind, amount);
    }

    public void AddPlayerBalance(int playerid, string walletKind, int amount)
    {
        var player = swiftlyCore.PlayerManager.GetPlayer(playerid);
        if (player == null) return;

        AddPlayerBalance(player.SteamID, walletKind, amount);
    }

    public void AddPlayerBalance(ulong steamid, string walletKind, int amount)
    {
        if (!walletKinds.ContainsKey(walletKind))
        {
            throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");
        }

        if (!playerBySteamId.TryGetValue(steamid, out IPlayer? value))
        {
            Task.Run(async () =>
            {
                var connection = swiftlyCore.Database.GetConnection("economyapi");

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
                    user.Id = (int)id;
                }

                user.Balance.TryGetValue(walletKind, out var currentBalance);
                currentBalance += amount;

                user.Balance[walletKind] = currentBalance;
                await connection.UpdateAsync(user);

                OnPlayerBalanceChanged?.Invoke(steamid, walletKind, currentBalance, currentBalance - amount);
            });
        }
        else
        {
            if (!playerBalances[steamid].ContainsKey(walletKind))
            {
                playerBalances[steamid][walletKind] = 0;
            }
            playerBalances[steamid][walletKind] += amount;

            playerSaveQueue.Enqueue(value);
            OnPlayerBalanceChanged?.Invoke(steamid, walletKind, playerBalances[steamid][walletKind], playerBalances[steamid][walletKind] - amount);
        }
    }

    public void EnsureWalletKind(string kindName)
    {
        if (!walletKinds.ContainsKey(kindName))
        {
            walletKinds[kindName] = true;
        }
    }

    public int GetPlayerBalance(IPlayer player, string walletKind)
    {
        return GetPlayerBalance(player.SteamID, walletKind);
    }

    public int GetPlayerBalance(int playerid, string walletKind)
    {
        var player = swiftlyCore.PlayerManager.GetPlayer(playerid);
        if (player == null) return 0;

        return GetPlayerBalance(player.SteamID, walletKind);
    }

    public int GetPlayerBalance(ulong steamid, string walletKind)
    {
        if (!walletKinds.ContainsKey(walletKind))
        {
            throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");
        }

        if (!playerBalances.ContainsKey(steamid))
        {
            var connection = swiftlyCore.Database.GetConnection("economyapi");

            var users = connection.Select<EconomyPlayer>(u => u.SteamId64 == (long)steamid);
            var user = users.FirstOrDefault();

            if (user == null) return 0;

            return user.Balance.TryGetValue(walletKind, out var balance) ? (int)balance : 0;
        }
        else
        {
            if (playerBalances[steamid].ContainsKey(walletKind))
            {
                return playerBalances[steamid][walletKind];
            }

            return 0;
        }
    }

    public bool HasSufficientFunds(IPlayer player, string walletKind, int amount)
    {
        return HasSufficientFunds(player.SteamID, walletKind, amount);
    }

    public bool HasSufficientFunds(int playerid, string walletKind, int amount)
    {
        var player = swiftlyCore.PlayerManager.GetPlayer(playerid);
        if (player == null) return false;

        return HasSufficientFunds(player.SteamID, walletKind, amount);
    }

    public bool HasSufficientFunds(ulong steamid, string walletKind, int amount)
    {
        if (!walletKinds.ContainsKey(walletKind))
        {
            throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");
        }

        var balance = GetPlayerBalance(steamid, walletKind);
        return balance >= amount;
    }

    public void SetPlayerBalance(IPlayer player, string walletKind, int amount)
    {
        SetPlayerBalance(player.SteamID, walletKind, amount);
    }

    public void SetPlayerBalance(int playerid, string walletKind, int amount)
    {
        var player = swiftlyCore.PlayerManager.GetPlayer(playerid);
        if (player == null) return;

        SetPlayerBalance(player.SteamID, walletKind, amount);
    }

    public void SetPlayerBalance(ulong steamid, string walletKind, int amount)
    {
        if (!walletKinds.ContainsKey(walletKind))
        {
            throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");
        }

        if (!playerBySteamId.TryGetValue(steamid, out IPlayer? value))
        {
            Task.Run(async () =>
            {
                var connection = swiftlyCore.Database.GetConnection("economyapi");

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
                    user.Id = (int)id;
                }

                user.Balance.TryGetValue(walletKind, out var currentBalance);
                var oldBalance = currentBalance;
                currentBalance = amount;

                user.Balance[walletKind] = currentBalance;

                OnPlayerBalanceChanged?.Invoke(steamid, walletKind, amount, oldBalance);

                await connection.UpdateAsync(user);
            });
        }
        else
        {
            if (!playerBalances[steamid].ContainsKey(walletKind))
            {
                playerBalances[steamid][walletKind] = 0;
            }

            var currentBalance = playerBalances[steamid][walletKind];

            playerBalances[steamid][walletKind] = amount;

            playerSaveQueue.Enqueue(value);
            OnPlayerBalanceChanged?.Invoke(steamid, walletKind, amount, currentBalance);
        }
    }

    public void SubtractPlayerBalance(IPlayer player, string walletKind, int amount)
    {
        SubtractPlayerBalance(player.SteamID, walletKind, amount);
    }

    public void SubtractPlayerBalance(int playerid, string walletKind, int amount)
    {
        var player = swiftlyCore.PlayerManager.GetPlayer(playerid);
        if (player == null) return;

        SubtractPlayerBalance(player.SteamID, walletKind, amount);
    }

    public void SubtractPlayerBalance(ulong steamid, string walletKind, int amount)
    {
        if (!walletKinds.ContainsKey(walletKind))
        {
            throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");
        }

        if (!playerBySteamId.TryGetValue(steamid, out IPlayer? value))
        {
            Task.Run(async () =>
            {
                var connection = swiftlyCore.Database.GetConnection("economyapi");

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
                    user.Id = (int)id;
                }

                user.Balance.TryGetValue(walletKind, out var currentBalance);
                currentBalance -= amount;

                user.Balance[walletKind] = currentBalance;
                OnPlayerBalanceChanged?.Invoke(steamid, walletKind, currentBalance, currentBalance + amount);
                await connection.UpdateAsync(user);
            });
        }
        else
        {
            if (!playerBalances[steamid].ContainsKey(walletKind))
            {
                playerBalances[steamid][walletKind] = 0;
            }

            playerBalances[steamid][walletKind] -= amount;
            OnPlayerBalanceChanged?.Invoke(steamid, walletKind, playerBalances[steamid][walletKind], playerBalances[steamid][walletKind] + amount);

            playerSaveQueue.Enqueue(value);
        }
    }

    public void TransferFunds(IPlayer fromPlayer, IPlayer toPlayer, string walletKind, int amount)
    {
        TransferFunds(fromPlayer.SteamID, toPlayer.SteamID, walletKind, amount);
    }

    public void TransferFunds(int fromPlayerid, int toPlayerid, string walletKind, int amount)
    {
        var fromPlayer = swiftlyCore.PlayerManager.GetPlayer(fromPlayerid);
        var toPlayer = swiftlyCore.PlayerManager.GetPlayer(toPlayerid);
        if (fromPlayer == null || toPlayer == null) return;

        TransferFunds(fromPlayer.SteamID, toPlayer.SteamID, walletKind, amount);
    }

    public void TransferFunds(ulong fromSteamid, ulong toSteamid, string walletKind, int amount)
    {
        if (!walletKinds.ContainsKey(walletKind))
        {
            throw new KeyNotFoundException($"Wallet kind '{walletKind}' does not exist.");
        }

        SubtractPlayerBalance(fromSteamid, walletKind, amount);
        AddPlayerBalance(toSteamid, walletKind, amount);

        OnPlayerFundsTransferred?.Invoke(fromSteamid, toSteamid, walletKind, amount);
    }

    public bool WalletKindExists(string kindName)
    {
        return walletKinds.ContainsKey(kindName);
    }

    public void LoadData(IPlayer player)
    {
        playerBySteamId[player.SteamID] = player;

        var database = swiftlyCore.Database.GetConnection("economyapi");
        var users = database.Select<EconomyPlayer>(p => p.SteamId64 == (long)player.SteamID);
        var user = users.FirstOrDefault();

        if (user != null)
        {
            foreach (var (walletKind, balance) in user.Balance)
            {
                try
                {
                    SetPlayerBalance(player.SteamID, walletKind, (int)balance);
                }
                catch { }
            }
        }
        else
        {
            var newUser = new EconomyPlayer
            {
                SteamId64 = (long)player.SteamID,
                Balance = []
            };
            database.Insert(newUser);
        }

        OnPlayerLoad?.Invoke(player);
    }

    public void SaveData(IPlayer player)
    {
        SaveData(player.SteamID);
        OnPlayerSave?.Invoke(player);
    }
    public void SaveData(int playerid)
    {
        var player = swiftlyCore.PlayerManager.GetPlayer(playerid);
        if (player == null) return;

        SaveData(player.SteamID);
        OnPlayerSave?.Invoke(player);
    }

    public void SaveData(ulong steamid)
    {
        var connection = swiftlyCore.Database.GetConnection("economyapi");

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
            user.Id = (int)id;
        }

        if (playerBalances.TryGetValue(steamid, out var balances))
        {
            foreach (var (walletKind, balance) in balances)
            {
                user.Balance[walletKind] = balance;
            }
        }

        connection.Update(user);
    }
}