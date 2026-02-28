using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using Economy.Database;
using Economy.Api;
using Economy.Commands;
using Economy.Contract;
using Economy.Config;
using Economy.Services;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Economy;

[PluginMetadata(Id = "Economy", Version = "2.0.2", Name = "Economy", Author = "Swiftly Development Team", Description = "The base economy plugin for your server.")]
public partial class Economy : BasePlugin
{
	private EconomyService? _economyService;
	private PluginConfig _config = null!;
	private CancellationTokenSource? _saveTaskCancellationTokenSource;

	// Public accessors for commands
	internal new ISwiftlyCore Core => base.Core;
	internal PluginConfig Config => _config;
	internal EconomyService EconomyService => _economyService!;

	public Economy(ISwiftlyCore core) : base(core)
	{
		LoadConfiguration();

		var connection = core.Database.GetConnection(_config.DatabaseConnection);

		MigrationRunner.RunMigrations(connection);
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
		_economyService = new EconomyService(Core, _config);
		var api = new EconomyAPIv1(_economyService);

		interfaceManager.AddSharedInterface<IEconomyAPIv1, EconomyAPIv1>("Economy.API.v1", api);
	}

	public override void UseSharedInterface(IInterfaceManager interfaceManager)
	{
		// Register wallet kinds from config
		if (_economyService != null)
		{
			foreach (var walletKind in _config.WalletKinds)
			{
				_economyService.EnsureWalletKind(walletKind);
			}
		}
	}

	/* ==================== Player Events ==================== */

	[EventListener<EventDelegates.OnClientSteamAuthorize>]
	public void OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent @event)
	{
		var playerId = @event.PlayerId;

		Task.Run(() =>
		{
			if (_economyService == null) return;

			var player = Core.PlayerManager.GetPlayer(playerId);
			if (player == null) return;

			_economyService.LoadData(player);
		});
	}

	[EventListener<EventDelegates.OnClientDisconnected>]
	public void OnClientDisconnected(IOnClientDisconnectedEvent @event)
	{
		var playerId = @event.PlayerId;

		var player = Core.PlayerManager.GetPlayer(playerId);
		if (player == null) return;

		var steamId = player.SteamID;

		Task.Run(() =>
		{
			if (_economyService == null) return;

			_economyService.SaveData(steamId);
			_economyService.RemovePlayer(steamId);
		});
	}

	/* ==================== Round Events ==================== */

	private void RegisterRoundEvents()
	{
		if (_config.SaveOnRoundEnd)
		{
			Core.GameEvent.HookPost<EventRoundEnd>(OnRoundEnd);
		}
	}

	private HookResult OnRoundEnd(EventRoundEnd @event)
	{
		Task.Run(() => _economyService?.SaveAllOnlinePlayers());
		return HookResult.Continue;
	}

	/* ==================== Lifecycle ==================== */

	public override void Load(bool hotReload)
	{
		RegisterRoundEvents();
		RegisterCommands();
		StartSaveQueueProcessor();
	}

	public override void Unload()
	{
		_saveTaskCancellationTokenSource?.Cancel();

		// Save all dirty players before unloading
		if (_economyService != null)
		{
			Task.Run(() => _economyService.SaveAllOnlinePlayers()).Wait(TimeSpan.FromSeconds(5));
		}
	}

	private void StartSaveQueueProcessor()
	{
		_saveTaskCancellationTokenSource?.Cancel();

		var interval = _config.SaveQueueIntervalSeconds;
		if (interval <= 0) return; // Disabled

		_saveTaskCancellationTokenSource = Core.Scheduler.RepeatBySeconds(interval, () =>
		{
			Task.Run(() =>
			{
				if (_economyService == null) return;

				while (_economyService.TryDequeueSave(out var steamId))
				{
					_economyService.SaveData(steamId);
				}
			});
		});
	}

	/* ==================== Commands ==================== */

	private void RegisterCommands()
	{
		var mainCmd = _config.Commands.MainCommand;
		if (string.IsNullOrEmpty(mainCmd))
			return;

		// Register main command
		Core.Command.RegisterCommand(mainCmd, ctx => EcoCommand.OnCommand(this, ctx));

		// Register aliases
		foreach (var alias in _config.Commands.MainCommandAliases)
		{
			Core.Command.RegisterCommandAlias(mainCmd, alias);
		}
	}

	/* ==================== Helpers ==================== */

	internal IEnumerable<IPlayer> FindTargets(IPlayer sender, string target, bool allowMultiple = true, bool requireAlive = false)
	{
		var searchMode = TargetSearchMode.IncludeSelf;

		if (!allowMultiple)
			searchMode |= TargetSearchMode.NoMultipleTargets;

		if (requireAlive)
			searchMode |= TargetSearchMode.Alive;

		return Core.PlayerManager.FindTargettedPlayers(sender, target, searchMode);
	}
}
