using SwiftlyS2.Shared.Players;

namespace Economy.Contract;

public interface IEconomyAPIv1
{
    void EnsureWalletKind(string kindName);

    decimal GetPlayerBalance(IPlayer player, string walletKind);
    decimal GetPlayerBalance(int playerid, string walletKind);
    decimal GetPlayerBalance(ulong steamid, string walletKind);

    void SetPlayerBalance(IPlayer player, string walletKind, decimal amount);
    void SetPlayerBalance(int playerid, string walletKind, decimal amount);
    void SetPlayerBalance(ulong steamid, string walletKind, decimal amount);

    void AddPlayerBalance(IPlayer player, string walletKind, decimal amount);
    void AddPlayerBalance(int playerid, string walletKind, decimal amount);
    void AddPlayerBalance(ulong steamid, string walletKind, decimal amount);

    void SubtractPlayerBalance(IPlayer player, string walletKind, decimal amount);
    void SubtractPlayerBalance(int playerid, string walletKind, decimal amount);
    void SubtractPlayerBalance(ulong steamid, string walletKind, decimal amount);

    bool HasSufficientFunds(IPlayer player, string walletKind, decimal amount);
    bool HasSufficientFunds(int playerid, string walletKind, decimal amount);
    bool HasSufficientFunds(ulong steamid, string walletKind, decimal amount);

    void TransferFunds(IPlayer fromPlayer, IPlayer toPlayer, string walletKind, decimal amount);
    void TransferFunds(int fromPlayerid, int toPlayerid, string walletKind, decimal amount);
    void TransferFunds(ulong fromSteamid, ulong toSteamid, string walletKind, decimal amount);

    void SaveData(IPlayer player);
    void SaveData(int playerid);
    void SaveData(ulong steamid);

    void LoadData(IPlayer player);

    bool WalletKindExists(string kindName);

    event Action<ulong, string, decimal, decimal>? OnPlayerBalanceChanged;
    event Action<ulong, ulong, string, decimal>? OnPlayerFundsTransferred;
    event Action<IPlayer>? OnPlayerLoad;
    event Action<IPlayer>? OnPlayerSave;
}