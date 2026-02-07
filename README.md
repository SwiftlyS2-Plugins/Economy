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

## Features

- Multiple wallet types (credits, coins, gems, etc.)
- Decimal-based balances for precise currency tracking
- Player-to-player transfers
- Comprehensive API for plugin integration
- Event system for balance changes
- MySQL, MariaDB, and PostgreSQL support

## Building & Publishing

```bash
# Build the project
dotnet build

# Publish for distribution
dotnet publish -c Release
```

Output files will be placed in the `build/` directory with an automatically generated zip file for distribution.

## Configuration

Edit `configs/Economy/config.jsonc`:

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `DatabaseConnection` | string | `"default"` | Database connection name from SwiftlyS2's `database.jsonc` |
| `AllowNegativeBalance` | bool | `false` | Allow player balances to go negative |
| `SaveOnRoundEnd` | bool | `false` | Save all online players when round ends |
| `SaveQueueIntervalSeconds` | int | `0` | Auto-save interval in seconds (0 = disabled) |
| `WalletKinds` | string[] | `["credits"]` | Default wallet types to register on plugin load |
| `Commands.MainCommand` | string | `"eco"` | Main economy command |
| `Commands.MainCommandAliases` | string[] | `["economy"]` | Aliases for main command |
| `Commands.Give.Name` | string | `"give"` | Give subcommand name |
| `Commands.Give.Permission` | string | `"economy.admin"` | Give command permission |
| `Commands.Take.Name` | string | `"take"` | Take subcommand name |
| `Commands.Take.Permission` | string | `"economy.admin"` | Take command permission |
| `Commands.Set.Name` | string | `"set"` | Set subcommand name |
| `Commands.Set.Permission` | string | `"economy.admin"` | Set command permission |
| `Commands.Pay.Name` | string | `"pay"` | Pay subcommand name |
| `Commands.Pay.Permission` | string | `""` | Pay command permission (empty = no permission required) |

## Database Structure

The plugin uses a single `balance` table:

| Column | Type | Description |
|--------|------|-------------|
| `id` | int | Primary key (auto-increment) |
| `steam_id` | bigint | Player's Steam ID |
| `wallet_kind` | varchar(255) | Wallet type identifier |
| `balance` | decimal(19,4) | Current balance amount |
| `created_at` | datetime | Record creation timestamp |
| `updated_at` | datetime | Last update timestamp |

**Unique Constraint:** (`steam_id`, `wallet_kind`)

## API Usage

### Getting the API

```csharp
using Economy.Contract;

public class YourPlugin : SwiftlyS2Plugin
{
    private IEconomyAPIv1? economyAPI;

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        economyAPI = interfaceManager.GetSharedInterface<IEconomyAPIv1>("Economy.API.v1");
    }
}
```

### Wallet Management

```csharp
// Register a new wallet type
economyAPI.EnsureWalletKind("gems");

// Check if wallet type exists
bool exists = economyAPI.WalletKindExists("gems");
```

### Balance Operations

All operations support three player identification methods: `IPlayer`, `int playerid`, or `ulong steamid`.

```csharp
// Get balance
decimal balance = economyAPI.GetPlayerBalance(player, "credits");
decimal balance = economyAPI.GetPlayerBalance(playerid, "credits");
decimal balance = economyAPI.GetPlayerBalance(steamid, "credits");

// Set balance
economyAPI.SetPlayerBalance(player, "credits", 1000.50m);

// Add to balance
economyAPI.AddPlayerBalance(player, "credits", 100.25m);

// Subtract from balance
economyAPI.SubtractPlayerBalance(player, "credits", 50.75m);

// Check if player has sufficient funds
bool canAfford = economyAPI.HasSufficientFunds(player, "credits", 250.00m);
```

### Transfer Operations

```csharp
// Transfer funds between players
economyAPI.TransferFunds(fromPlayer, toPlayer, "credits", 100.00m);
economyAPI.TransferFunds(fromPlayerid, toPlayerid, "credits", 100.00m);
economyAPI.TransferFunds(fromSteamid, toSteamid, "credits", 100.00m);
```

### Data Persistence

```csharp
// Load player data (automatically done on player connect)
economyAPI.LoadData(player);

// Save player data manually
economyAPI.SaveData(player);
economyAPI.SaveData(playerid);
economyAPI.SaveData(steamid);
```

### Events

```csharp
// Balance changed: (steamid, walletKind, newBalance, oldBalance)
economyAPI.OnPlayerBalanceChanged += (steamid, walletKind, newBalance, oldBalance) =>
{
    Server.PrintToConsole($"Balance changed: {walletKind} {oldBalance} -> {newBalance}");
};

// Funds transferred: (fromSteamid, toSteamid, walletKind, amount)
economyAPI.OnPlayerFundsTransferred += (from, to, walletKind, amount) =>
{
    Server.PrintToConsole($"Transfer: {amount} {walletKind} from {from} to {to}");
};

// Player data loaded
economyAPI.OnPlayerLoad += (player) =>
{
    Server.PrintToConsole($"Loaded economy data for {player.PlayerName}");
};

// Player data saved
economyAPI.OnPlayerSave += (player) =>
{
    Server.PrintToConsole($"Saved economy data for {player.PlayerName}");
};
```

## Commands

| Command | Permission | Description |
|---------|------------|-------------|
| `!eco` | - | Show your balance and available commands |
| `!eco give <player> <amount> <wallet>` | `economy.admin` | Give currency to a player |
| `!eco take <player> <amount> <wallet>` | `economy.admin` | Take currency from a player |
| `!eco set <player> <amount> <wallet>` | `economy.admin` | Set player's balance to specific amount |
| `!eco pay <player> <amount> <wallet>` | - | Transfer your currency to another player |

## License

This project is licensed under the MIT License.
