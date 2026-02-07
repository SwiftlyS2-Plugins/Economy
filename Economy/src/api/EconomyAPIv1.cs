using Economy.Contract;
using Economy.Services;
using SwiftlyS2.Shared.Players;

namespace Economy.Api;

public class EconomyAPIv1(EconomyService service) : IEconomyAPIv1
{
	public event Action<ulong, string, decimal, decimal>? OnPlayerBalanceChanged
	{
		add => service.OnPlayerBalanceChanged += value;
		remove => service.OnPlayerBalanceChanged -= value;
	}

	public event Action<ulong, ulong, string, decimal>? OnPlayerFundsTransferred
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

	public void EnsureWalletKind(string kindName) => service.EnsureWalletKind(kindName);
	public bool WalletKindExists(string kindName) => service.WalletKindExists(kindName);

	public decimal GetPlayerBalance(IPlayer player, string walletKind) => service.GetPlayerBalance(player, walletKind);
	public decimal GetPlayerBalance(int playerid, string walletKind) => service.GetPlayerBalance(playerid, walletKind);
	public decimal GetPlayerBalance(ulong steamid, string walletKind) => service.GetPlayerBalance(steamid, walletKind);

	public bool HasSufficientFunds(IPlayer player, string walletKind, decimal amount) => service.HasSufficientFunds(player, walletKind, amount);
	public bool HasSufficientFunds(int playerid, string walletKind, decimal amount) => service.HasSufficientFunds(playerid, walletKind, amount);
	public bool HasSufficientFunds(ulong steamid, string walletKind, decimal amount) => service.HasSufficientFunds(steamid, walletKind, amount);

	public void SetPlayerBalance(IPlayer player, string walletKind, decimal amount) => service.SetPlayerBalance(player, walletKind, amount);
	public void SetPlayerBalance(int playerid, string walletKind, decimal amount) => service.SetPlayerBalance(playerid, walletKind, amount);
	public void SetPlayerBalance(ulong steamid, string walletKind, decimal amount) => service.SetPlayerBalance(steamid, walletKind, amount);

	public void AddPlayerBalance(IPlayer player, string walletKind, decimal amount) => service.AddPlayerBalance(player, walletKind, amount);
	public void AddPlayerBalance(int playerid, string walletKind, decimal amount) => service.AddPlayerBalance(playerid, walletKind, amount);
	public void AddPlayerBalance(ulong steamid, string walletKind, decimal amount) => service.AddPlayerBalance(steamid, walletKind, amount);

	public void SubtractPlayerBalance(IPlayer player, string walletKind, decimal amount) => service.SubtractPlayerBalance(player, walletKind, amount);
	public void SubtractPlayerBalance(int playerid, string walletKind, decimal amount) => service.SubtractPlayerBalance(playerid, walletKind, amount);
	public void SubtractPlayerBalance(ulong steamid, string walletKind, decimal amount) => service.SubtractPlayerBalance(steamid, walletKind, amount);

	public void TransferFunds(IPlayer fromPlayer, IPlayer toPlayer, string walletKind, decimal amount) => service.TransferFunds(fromPlayer, toPlayer, walletKind, amount);
	public void TransferFunds(int fromPlayerid, int toPlayerid, string walletKind, decimal amount) => service.TransferFunds(fromPlayerid, toPlayerid, walletKind, amount);
	public void TransferFunds(ulong fromSteamid, ulong toSteamid, string walletKind, decimal amount) => service.TransferFunds(fromSteamid, toSteamid, walletKind, amount);

	public void LoadData(IPlayer player) => service.LoadData(player);
	public void SaveData(IPlayer player) => service.SaveData(player);
	public void SaveData(int playerid) => service.SaveData(playerid);
	public void SaveData(ulong steamid) => service.SaveData(steamid);
}

