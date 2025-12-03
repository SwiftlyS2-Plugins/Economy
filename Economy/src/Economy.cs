using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using Economy.Database;
using Economy.Contract;
using Economy.API;
using System.Collections.Concurrent;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Economy;

[PluginMetadata(Id = "Economy", Version = "1.0.3", Name = "Economy", Author = "Swiftly Development Team", Description = "The base economy plugin for your server.")]
public partial class Economy : BasePlugin
{
	private ConcurrentDictionary<string, bool> walletKinds = new();
	private ConcurrentDictionary<ulong, ConcurrentDictionary<string, int>> playerBalances = new();
	private ConcurrentQueue<IPlayer> playerSaveQueue = new();
	private ConcurrentDictionary<ulong, IPlayer> playerBySteamId = new();
	private CancellationTokenSource? saveTaskCancellationTokenSource;

	private IEconomyAPIv1? economyAPI;
	private PluginConfig _config = null!;

	public Economy(ISwiftlyCore core) : base(core)
	{
		LoadConfiguration();

		var connection = core.Database.GetConnection(_config.DatabaseConnection);
		var connectionString = core.Database.GetConnectionString(_config.DatabaseConnection);

		MigrationRunner.RunMigrations(connection, connectionString);
	}

	private void LoadConfiguration()
	{
		const string ConfigFileName = "config.json";
		const string ConfigSection = "Economy";

		Core.Configuration
			.InitializeJsonWithModel<PluginConfig>(ConfigFileName, ConfigSection)
			.Configure(cfg => cfg.AddJsonFile(Core.Configuration.GetConfigPath(ConfigFileName), optional: false, reloadOnChange: false));

		ServiceCollection services = new();
		services.AddSwiftly(Core)
			.AddOptionsWithValidateOnStart<PluginConfig>()
			.BindConfiguration(ConfigSection);

		var provider = services.BuildServiceProvider();
		_config = provider.GetRequiredService<IOptions<PluginConfig>>().Value;
	}

	public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
	{
		interfaceManager.AddSharedInterface<IEconomyAPIv1, EconomyAPIv1>(
			"Economy.API.v1", new EconomyAPIv1(Core, ref walletKinds, ref playerBalances, ref playerSaveQueue, ref playerBySteamId)
		);
	}

	public override void UseSharedInterface(IInterfaceManager interfaceManager)
	{
		economyAPI = interfaceManager.GetSharedInterface<IEconomyAPIv1>(
			"Economy.API.v1"
		);
	}

	[EventListener<EventDelegates.OnClientSteamAuthorize>]
	public void OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent @event)
	{
		var playerid = @event.PlayerId;

		Task.Run(() =>
		{
			if (economyAPI == null) return;

			var player = Core.PlayerManager.GetPlayer(playerid);
			if (player == null) return;

			economyAPI.LoadData(player);
		});
	}

	[EventListener<EventDelegates.OnClientDisconnected>]
	public void OnClientDisconnected(IOnClientDisconnectedEvent @event)
	{
		var playerid = @event.PlayerId;

		var player = Core.PlayerManager.GetPlayer(playerid);
		if (player == null) return;

		var steamid = player.SteamID;

		Task.Run(() =>
		{
			if (economyAPI == null) return;

			economyAPI.SaveData(steamid);

			playerBalances.TryRemove(steamid, out _);
			playerBySteamId.TryRemove(steamid, out _);
		});
	}

	public override void Load(bool hotReload)
	{
		if (saveTaskCancellationTokenSource != null) saveTaskCancellationTokenSource.Cancel();

		saveTaskCancellationTokenSource = Core.Scheduler.RepeatBySeconds(10, () =>
		{
			Task.Run(() =>
			{
				while (playerSaveQueue.TryDequeue(out var player))
				{
					if (economyAPI == null) continue;
					if (!player.IsValid) continue;

					economyAPI.SaveData(player);
				}
			});
		});
	}

	public override void Unload()
	{
		saveTaskCancellationTokenSource?.Cancel();
	}
}