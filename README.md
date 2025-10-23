<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>Economy</strong></h2>
  <h3>The base economy plugin for your server.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/SwiftlyS2-Plugins/Economy/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/SwiftlyS2-Plugins/Economy?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/SwiftlyS2-Plugins/Economy" alt="License">
</p>

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.

## API Interface

The Economy plugin provides a comprehensive API through the `IEconomyAPIv1` interface in the [Economy.Contract](Economy.Contract/src/IEconomyAPIv1.cs) namespace. This interface allows other plugins to interact with the economy system.

### Accessing the API

```csharp
using Economy.Contract;

// ...

public override void UseSharedInterface(IInterfaceManager interfaceManager)
	{
		var api = interfaceManager.GetSharedInterface<IEconomyAPIv1>(
			"Economy.API.v1"
		);
		economyAPI = api;
	}
```

### Wallet Management

#### EnsureWalletKind
```csharp
void EnsureWalletKind(string kindName);
```
Creates a new wallet type if it doesn't already exist. Use this to initialize custom currency types (e.g., "coins", "gems", "credits").

#### WalletKindExists
```csharp
bool WalletKindExists(string kindName);
```
Checks if a wallet type has been registered.

### Balance Operations

All balance operations support three ways to identify players:
- `IPlayer player` - Using the player object
- `int playerid` - Using the player ID
- `ulong steamid` - Using the Steam ID

#### GetPlayerBalance
```csharp
int GetPlayerBalance(IPlayer player, string walletKind);
int GetPlayerBalance(int playerid, string walletKind);
int GetPlayerBalance(ulong steamid, string walletKind);
```
Returns the current balance for the specified player and wallet type.

#### SetPlayerBalance
```csharp
void SetPlayerBalance(IPlayer player, string walletKind, int amount);
void SetPlayerBalance(int playerid, string walletKind, int amount);
void SetPlayerBalance(ulong steamid, string walletKind, int amount);
```
Sets the player's balance to a specific amount.

#### AddPlayerBalance
```csharp
void AddPlayerBalance(IPlayer player, string walletKind, int amount);
void AddPlayerBalance(int playerid, string walletKind, int amount);
void AddPlayerBalance(ulong steamid, string walletKind, int amount);
```
Adds the specified amount to the player's current balance.

#### SubtractPlayerBalance
```csharp
void SubtractPlayerBalance(IPlayer player, string walletKind, int amount);
void SubtractPlayerBalance(int playerid, string walletKind, int amount);
void SubtractPlayerBalance(ulong steamid, string walletKind, int amount);
```
Subtracts the specified amount from the player's current balance.

#### HasSufficientFunds
```csharp
bool HasSufficientFunds(IPlayer player, string walletKind, int amount);
bool HasSufficientFunds(int playerid, string walletKind, int amount);
bool HasSufficientFunds(ulong steamid, string walletKind, int amount);
```
Checks if the player has at least the specified amount in their wallet.

### Transfer Operations

```csharp
void TransferFunds(IPlayer fromPlayer, IPlayer toPlayer, string walletKind, int amount);
void TransferFunds(int fromPlayerid, int toPlayerid, string walletKind, int amount);
void TransferFunds(ulong fromSteamid, ulong toSteamid, string walletKind, int amount);
```
Transfers funds from one player to another within the same wallet type.

### Data Persistence

#### SaveData
```csharp
void SaveData(IPlayer player);
void SaveData(int playerid);
void SaveData(ulong steamid);
```
Manually saves player economy data to the database.

#### LoadData
```csharp
void LoadData(IPlayer player);
```
Loads player economy data from the database.

### Events

The API provides several events to monitor economy activities:

#### OnPlayerBalanceChanged
```csharp
event Action<ulong, string, long, long>? OnPlayerBalanceChanged;
```
Triggered when a player's balance changes. Parameters: `(steamid, walletKind, newBalance, oldBalance)`

#### OnPlayerFundsTransferred
```csharp
event Action<ulong, ulong, string, long>? OnPlayerFundsTransferred;
```
Triggered when funds are transferred between players. Parameters: `(fromSteamid, toSteamid, walletKind, amount)`

#### OnPlayerLoad
```csharp
event Action<IPlayer>? OnPlayerLoad;
```
Triggered when a player's economy data is loaded.

#### OnPlayerSave
```csharp
event Action<IPlayer>? OnPlayerSave;
```
Triggered when a player's economy data is saved.