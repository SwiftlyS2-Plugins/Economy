namespace Economy.Config;

/// <summary>
/// Main configuration for Economy plugin
/// </summary>
public sealed class PluginConfig
{
	/// <summary>DB connection name (from SwiftlyS2's database.jsonc)</summary>
	public string DatabaseConnection { get; set; } = "default";

	/// <summary>Allow player balance to go negative (default: false)</summary>
	public bool AllowNegativeBalance { get; set; } = false;

	/// <summary>Save all online players at round end (default: false)</summary>
	public bool SaveOnRoundEnd { get; set; } = false;

	/// <summary>
	/// How often to process the save queue in seconds. It is useful for servers without rounds.
	/// Set to 0 to disable periodic saving (only round end saves).
	/// Default: 0 (disabled)
	/// </summary>
	public int SaveQueueIntervalSeconds { get; set; } = 0;

	/// <summary>
	/// Wallet kinds to register on plugin load.
	/// Other plugins can also register their own wallet kinds via the API.
	/// Example: ["credits", "coins", "tokens"]
	/// </summary>
	public List<string> WalletKinds { get; set; } = ["credits"];

	/// <summary>Command settings</summary>
	public CommandSettings Commands { get; set; } = new();
}

/// <summary>
/// Command configuration settings
/// </summary>
public sealed class CommandSettings
{
	/// <summary>Main economy command (shows balance + help)</summary>
	public string MainCommand { get; set; } = "eco";

	/// <summary>Aliases for main command</summary>
	public List<string> MainCommandAliases { get; set; } = ["economy"];

	/// <summary>Give subcommand name</summary>
	public SubCommandConfig Give { get; set; } = new()
	{
		Name = "give",
		Permission = "economy.admin"
	};

	/// <summary>Take subcommand name</summary>
	public SubCommandConfig Take { get; set; } = new()
	{
		Name = "take",
		Permission = "economy.admin"
	};

	/// <summary>Set subcommand name</summary>
	public SubCommandConfig Set { get; set; } = new()
	{
		Name = "set",
		Permission = "economy.admin"
	};

	/// <summary>Pay subcommand for player-to-player transfers</summary>
	public SubCommandConfig Pay { get; set; } = new()
	{
		Name = "pay",
		Permission = ""
	};
}

/// <summary>
/// Subcommand configuration
/// </summary>
public sealed class SubCommandConfig
{
	/// <summary>Subcommand name (e.g., "give" for !eco give)</summary>
	public string Name { get; set; } = "";

	/// <summary>Required permission (empty for no permission required)</summary>
	public string Permission { get; set; } = "";
}
