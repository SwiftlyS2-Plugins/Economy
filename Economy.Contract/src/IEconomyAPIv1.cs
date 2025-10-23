using SwiftlyS2.Shared.Players;

namespace Economy.Contract;

public interface IEconomyAPIv1
{
    void EnsureWalletKind(string kindName);

    int GetPlayerBalance(IPlayer player, string walletKind);
    int GetPlayerBalance(int playerid, string walletKind);
    int GetPlayerBalance(ulong steamid, string walletKind);

    void SetPlayerBalance(IPlayer player, string walletKind, int amount);
    void SetPlayerBalance(int playerid, string walletKind, int amount);
    void SetPlayerBalance(ulong steamid, string walletKind, int amount);

    void AddPlayerBalance(IPlayer player, string walletKind, int amount);
    void AddPlayerBalance(int playerid, string walletKind, int amount);
    void AddPlayerBalance(ulong steamid, string walletKind, int amount);

    void SubtractPlayerBalance(IPlayer player, string walletKind, int amount);
    void SubtractPlayerBalance(int playerid, string walletKind, int amount);
    void SubtractPlayerBalance(ulong steamid, string walletKind, int amount);

    bool HasSufficientFunds(IPlayer player, string walletKind, int amount);
    bool HasSufficientFunds(int playerid, string walletKind, int amount);
    bool HasSufficientFunds(ulong steamid, string walletKind, int amount);

    void TransferFunds(IPlayer fromPlayer, IPlayer toPlayer, string walletKind, int amount);
    void TransferFunds(int fromPlayerid, int toPlayerid, string walletKind, int amount);
    void TransferFunds(ulong fromSteamid, ulong toSteamid, string walletKind, int amount);

    void SaveData(IPlayer player);
    void SaveData(int playerid);
    void SaveData(ulong steamid);

    void LoadData(IPlayer player);

    bool WalletKindExists(string kindName);

    event Action<ulong, string, long, long>? OnPlayerBalanceChanged;
    event Action<ulong, ulong, string, long>? OnPlayerFundsTransferred;
    event Action<IPlayer>? OnPlayerLoad;
    event Action<IPlayer>? OnPlayerSave;
}