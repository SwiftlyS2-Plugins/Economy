using FluentMigrator;

namespace Economy.Database.Migrations;

/// <summary>
/// Migrates data from the legacy EconomyPlayers table to the new normalized balance table.
/// This migration handles JSON balance data where each player has multiple wallet types.
/// 
/// Example old data: {"jewels":100,"credits":66}
/// Results in two rows: (steam_id, "jewels", 100) and (steam_id, "credits", 66)
/// 
/// This migration:
/// - Parses JSON balance data from the Balance column
/// - Creates separate rows for each wallet type (key-value pair in JSON)
/// - Converts balance values to decimal(18,2)
/// - Sets appropriate timestamps
/// - Renames the old EconomyPlayers table to EconomyPlayers_backup (preserves original data)
/// - Supports MySQL, PostgreSQL, and SQLite with database-specific JSON parsing
/// </summary>
[Migration(1761193042)]
public class MigrateEconomyPlayersToBalance : Migration
{
    public override void Up()
    {
        // Only migrate if the old table exists and new table exists
        if (Schema.Table("EconomyPlayers").Exists() && Schema.Table("balance").Exists())
        {
            // MySQL: Parse JSON balance field using JSON_KEYS and JSON_EXTRACT
            IfDatabase("MySQL").Execute.Sql(@"
                INSERT INTO balance (steam_id, wallet_kind, balance, created_at, updated_at)
                SELECT 
                    ep.SteamId64 as steam_id,
                    JSON_UNQUOTE(JSON_EXTRACT(k.wallet_key, '$')) as wallet_kind,
                    CAST(JSON_UNQUOTE(JSON_EXTRACT(ep.Balance, CONCAT('$.', JSON_UNQUOTE(JSON_EXTRACT(k.wallet_key, '$'))))) AS DECIMAL(18,2)) as balance,
                    CURRENT_TIMESTAMP as created_at,
                    CURRENT_TIMESTAMP as updated_at
                FROM EconomyPlayers ep
                CROSS JOIN JSON_TABLE(
                    JSON_KEYS(ep.Balance),
                    '$[*]' COLUMNS(wallet_key JSON PATH '$')
                ) AS k
                WHERE NOT EXISTS (
                    SELECT 1 FROM balance b 
                    WHERE b.steam_id = ep.SteamId64 
                    AND b.wallet_kind = JSON_UNQUOTE(JSON_EXTRACT(k.wallet_key, '$'))
                )
            ");

            // PostgreSQL: Parse JSON balance field
            IfDatabase("Postgres").Execute.Sql(@"
                INSERT INTO balance (steam_id, wallet_kind, balance, created_at, updated_at)
                SELECT 
                    ep.""SteamId64"" as steam_id,
                    je.key as wallet_kind,
                    CAST(je.value AS DECIMAL(18,2)) as balance,
                    CURRENT_TIMESTAMP as created_at,
                    CURRENT_TIMESTAMP as updated_at
                FROM ""EconomyPlayers"" ep
                CROSS JOIN LATERAL json_each_text(ep.""Balance""::json) je
                WHERE NOT EXISTS (
                    SELECT 1 FROM balance b 
                    WHERE b.steam_id = ep.""SteamId64"" AND b.wallet_kind = je.key
                )
            ");

            // SQLite: Parse JSON balance field
            IfDatabase("SQLite").Execute.Sql(@"
                INSERT INTO balance (steam_id, wallet_kind, balance, created_at, updated_at)
                SELECT 
                    ep.SteamId64 as steam_id,
                    je.key as wallet_kind,
                    CAST(je.value AS DECIMAL(18,2)) as balance,
                    datetime('now') as created_at,
                    datetime('now') as updated_at
                FROM EconomyPlayers ep
                CROSS JOIN json_each(ep.Balance) je
                WHERE NOT EXISTS (
                    SELECT 1 FROM balance b 
                    WHERE b.steam_id = ep.SteamId64 AND b.wallet_kind = je.key
                )
            ");

            // Rename the old table to backup instead of deleting it
            Rename.Table("EconomyPlayers").To("EconomyPlayers_backup");
        }
    }

    public override void Down()
    {
        // Restore the old table from backup
        if (Schema.Table("EconomyPlayers_backup").Exists())
        {
            // If EconomyPlayers exists, delete it first
            if (Schema.Table("EconomyPlayers").Exists())
            {
                Delete.Table("EconomyPlayers");
            }

            // Rename backup back to original name
            Rename.Table("EconomyPlayers_backup").To("EconomyPlayers");
        }
        else if (!Schema.Table("EconomyPlayers").Exists())
        {
            // If no backup exists, recreate the table structure and migrate data back
            Create.Table("EconomyPlayers")
                .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                .WithColumn("SteamId64").AsInt64().NotNullable()
                .WithColumn("Balance").AsCustom("TEXT").NotNullable();

            // Migrate data back from balance to EconomyPlayers - reconstruct JSON
            if (Schema.Table("balance").Exists())
            {
                // MySQL: Reconstruct JSON from balance rows
                IfDatabase("MySQL").Execute.Sql(@"
                    INSERT INTO EconomyPlayers (SteamId64, Balance)
                    SELECT 
                        steam_id,
                        CONCAT('{',
                            GROUP_CONCAT(
                                CONCAT('""', wallet_kind, '"":""', balance, '""')
                                ORDER BY wallet_kind
                                SEPARATOR ','
                            ),
                        '}') as Balance
                    FROM balance
                    GROUP BY steam_id
                ");

                // PostgreSQL: Reconstruct JSON from balance rows
                IfDatabase("Postgres").Execute.Sql(@"
                    INSERT INTO ""EconomyPlayers"" (""SteamId64"", ""Balance"")
                    SELECT 
                        steam_id,
                        json_object_agg(wallet_kind, balance)::text as ""Balance""
                    FROM balance
                    GROUP BY steam_id
                ");

                // SQLite: Reconstruct JSON from balance rows
                IfDatabase("SQLite").Execute.Sql(@"
                    INSERT INTO EconomyPlayers (SteamId64, Balance)
                    SELECT 
                        steam_id,
                        '{' || group_concat('""' || wallet_kind || '"":""' || balance || '""') || '}' as Balance
                    FROM balance
                    GROUP BY steam_id
                ");
            }
        }
    }
}
