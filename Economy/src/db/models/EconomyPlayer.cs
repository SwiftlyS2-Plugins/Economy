using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Dommel;

namespace Economy.Database.Models;

[Table("EconomyPlayers")]
public class EconomyPlayer
{
    [Key]
    public int Id { get; set; }

    [Column("SteamId64")]
    public long SteamId64 { get; set; }

    [Column("Balance")]
    public string BalanceJson
    {
        get => JsonSerializer.Serialize(Balance);
        set => Balance = JsonSerializer.Deserialize<Dictionary<string, long>>(value) ?? new Dictionary<string, long>();
    }

    [Ignore]
    public Dictionary<string, long> Balance { get; set; } = new();
}