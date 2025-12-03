namespace Economy;

public partial class Economy
{
	/// <summary>
	/// Main config for Economy
	/// </summary>
	public sealed class PluginConfig
	{
		/// <summary>DB connection name (from SwiftlyS2's database.jsonc)</summary>
		public string DatabaseConnection { get; set; } = "default";

		/// <summary>Allow player balance to go negative (default: false)</summary>
		public bool AllowNegativeBalance { get; set; } = false;
	}
}
