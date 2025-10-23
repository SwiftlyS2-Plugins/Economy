using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using Economy.Database;
using Economy.Contract;
using Economy.API;
using System.Collections.Concurrent;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Events;
using Dommel;
using Economy.Database.Models;

namespace Economy;

[PluginMetadata(Id = "Economy", Version = "1.0.0", Name = "Economy", Author = "Swiftly Development Team", Description = "The base economy plugin for your server.")]
public partial class Economy : BasePlugin
{
	private ConcurrentDictionary<string, bool> walletKinds = new();
	private ConcurrentDictionary<ulong, ConcurrentDictionary<string, int>> playerBalances = new();
	private ConcurrentQueue<IPlayer> playerSaveQueue = new();
	private ConcurrentDictionary<ulong, IPlayer> playerBySteamId = new();
	private CancellationTokenSource? saveTaskCancellationTokenSource;

	private IEconomyAPIv1? economyAPI;

	public Economy(ISwiftlyCore core) : base(core)
	{
		var connection = core.Database.GetConnection("economyapi");
		var connectionString = core.Database.GetConnectionString("economyapi");

		MigrationRunner.RunMigrations(connection, connectionString);
	}

	public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
	{
		interfaceManager.AddSharedInterface<IEconomyAPIv1, EconomyAPIv1>(
			"Economy.API.v1", new EconomyAPIv1(Core, ref walletKinds, ref playerBalances, ref playerSaveQueue, ref playerBySteamId)
		);
	}

	public override void UseSharedInterface(IInterfaceManager interfaceManager)
	{
		var api = interfaceManager.GetSharedInterface<IEconomyAPIv1>(
			"Economy.API.v1"
		);
		economyAPI = api;
	}

	[EventListener<EventDelegates.OnClientPutInServer>]
	public void OnClientPutInServer(IOnClientPutInServerEvent @event)
	{
		var playerid = @event.PlayerId;
		var kind = @event.Kind;
		if (kind != ClientKind.Player) return;

		Task.Run(() =>
		{
			if (economyAPI == null) return;

			var player = Core.PlayerManager.GetPlayer(playerid);
			if (player == null) return;

			_ = Task.Run(() =>
			{
				economyAPI.LoadData(player);
			});
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

			_ = Task.Run(() =>
			{
				economyAPI.SaveData(steamid);

				playerBalances.TryRemove(steamid, out _);
				playerBySteamId.TryRemove(steamid, out _);
			});
		});
	}

	public override void Load(bool hotReload)
	{
		if (saveTaskCancellationTokenSource != null) saveTaskCancellationTokenSource.Cancel();

		saveTaskCancellationTokenSource = Core.Scheduler.RepeatBySeconds(10, () =>
		{
			Task.Run(() =>
			{
				var tasks = new List<Task>();

				while (playerSaveQueue.TryDequeue(out var player))
				{
					if (economyAPI == null) continue;
					if (!player.IsValid) continue;

					_ = Task.Run(() => economyAPI.SaveData(player));
				}
			});
		});
	}

	public override void Unload()
	{
		saveTaskCancellationTokenSource?.Cancel();
	}
}