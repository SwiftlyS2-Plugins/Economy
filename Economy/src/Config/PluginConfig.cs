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
}
