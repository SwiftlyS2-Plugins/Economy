using SwiftlyS2.Shared.Players;

namespace Economy.Extensions;

public static class PlayerExtensions
{
	public static string GetName(this IPlayer player)
	{
		return player.Controller?.PlayerName ?? "Unknown";
	}
}
