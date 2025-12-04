using Economy.Contract;
using Economy.Services;
using SwiftlyS2.Shared.Players;

namespace Economy.Api;

/// <summary>
/// API wrapper that implements IEconomyAPIv1 interface using EconomyService
/// </summary>
public class EconomyAPIv1(EconomyService service) : IEconomyAPIv1
{
	public event Action<ulong, string, long, long>? OnPlayerBalanceChanged
	{
		add => service.OnPlayerBalanceChanged += value;
		remove => service.OnPlayerBalanceChanged -= value;
	}

	public event Action<ulong, ulong, string, long>? OnPlayerFundsTransferred
	{
		add => service.OnPlayerFundsTransferred += value;
		remove => service.OnPlayerFundsTransferred -= value;
	}

	public event Action<IPlayer>? OnPlayerLoad
	{
		add => service.OnPlayerLoad += value;
		remove => service.OnPlayerLoad -= value;
	}

	public event Action<IPlayer>? OnPlayerSave
	{
		add => service.OnPlayerSave += value;
		remove => service.OnPlayerSave -= value;
	}

	/* ==================== Wallet Kind ==================== */

	public void EnsureWalletKind(string kindName) => service.EnsureWalletKind(kindName);
	public bool WalletKindExists(string kindName) => service.WalletKindExists(kindName);

	/* ==================== Get Balance ==================== */

	public int GetPlayerBalance(IPlayer player, string walletKind) => service.GetPlayerBalance(player, walletKind);
	public int GetPlayerBalance(int playerid, string walletKind) => service.GetPlayerBalance(playerid, walletKind);
	public int GetPlayerBalance(ulong steamid, string walletKind) => service.GetPlayerBalance(steamid, walletKind);

	/* ==================== Has Sufficient Funds ==================== */

	public bool HasSufficientFunds(IPlayer player, string walletKind, int amount) => service.HasSufficientFunds(player, walletKind, amount);
	public bool HasSufficientFunds(int playerid, string walletKind, int amount) => service.HasSufficientFunds(playerid, walletKind, amount);
	public bool HasSufficientFunds(ulong steamid, string walletKind, int amount) => service.HasSufficientFunds(steamid, walletKind, amount);

	/* ==================== Set Balance ==================== */

	public void SetPlayerBalance(IPlayer player, string walletKind, int amount) => service.SetPlayerBalance(player, walletKind, amount);
	public void SetPlayerBalance(int playerid, string walletKind, int amount) => service.SetPlayerBalance(playerid, walletKind, amount);
	public void SetPlayerBalance(ulong steamid, string walletKind, int amount) => service.SetPlayerBalance(steamid, walletKind, amount);

	/* ==================== Add Balance ==================== */

	public void AddPlayerBalance(IPlayer player, string walletKind, int amount) => service.AddPlayerBalance(player, walletKind, amount);
	public void AddPlayerBalance(int playerid, string walletKind, int amount) => service.AddPlayerBalance(playerid, walletKind, amount);
	public void AddPlayerBalance(ulong steamid, string walletKind, int amount) => service.AddPlayerBalance(steamid, walletKind, amount);

	/* ==================== Subtract Balance ==================== */

	public void SubtractPlayerBalance(IPlayer player, string walletKind, int amount) => service.SubtractPlayerBalance(player, walletKind, amount);
	public void SubtractPlayerBalance(int playerid, string walletKind, int amount) => service.SubtractPlayerBalance(playerid, walletKind, amount);
	public void SubtractPlayerBalance(ulong steamid, string walletKind, int amount) => service.SubtractPlayerBalance(steamid, walletKind, amount);

	/* ==================== Transfer ==================== */

	public void TransferFunds(IPlayer fromPlayer, IPlayer toPlayer, string walletKind, int amount) => service.TransferFunds(fromPlayer, toPlayer, walletKind, amount);
	public void TransferFunds(int fromPlayerid, int toPlayerid, string walletKind, int amount) => service.TransferFunds(fromPlayerid, toPlayerid, walletKind, amount);
	public void TransferFunds(ulong fromSteamid, ulong toSteamid, string walletKind, int amount) => service.TransferFunds(fromSteamid, toSteamid, walletKind, amount);

	/* ==================== Data ==================== */

	public void LoadData(IPlayer player) => service.LoadData(player);
	public void SaveData(IPlayer player) => service.SaveData(player);
	public void SaveData(int playerid) => service.SaveData(playerid);
	public void SaveData(ulong steamid) => service.SaveData(steamid);
}
