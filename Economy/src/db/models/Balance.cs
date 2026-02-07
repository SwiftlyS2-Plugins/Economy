using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Dommel;

namespace Economy.Database.Models;

/// <summary>
/// Represents a player's balance for a specific wallet type.
/// Follows SQL best practices with lowercase naming and decimal balance type.
/// </summary>
[Table("balance")]
public class Balance
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("steam_id")]
    public long SteamId { get; set; }

    [Column("wallet_kind")]
    [MaxLength(255)]
    public required string WalletKind { get; set; }

    [Column("balance")]
    public decimal BalanceAmount { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

