using FluentMigrator;

namespace Economy.Database.Migrations;

/// <summary>
/// Creates the normalized balance table following SQL best practices:
/// - Lowercase table and column names
/// - DECIMAL type for balance
/// - Proper timestamps for audit trails
/// - Normalized structure for multi-currency support
/// </summary>
[Migration(1761193038)]
public class InitTable : Migration
{
    public override void Up()
    {
        if (!Schema.Table("balance").Exists())
        {
            Create.Table("balance")
                .WithColumn("id").AsInt32().PrimaryKey().Identity()
                .WithColumn("steam_id").AsInt64().NotNullable()
                .WithColumn("wallet_kind").AsString(255).NotNullable()
                .WithColumn("balance").AsDecimal(18, 2).NotNullable().WithDefaultValue(0)
                .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime)
                .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime);

            Create.Index("idx_balance_steam_wallet")
                .OnTable("balance")
                .OnColumn("steam_id").Ascending()
                .OnColumn("wallet_kind").Ascending()
                .WithOptions().Unique();

            Create.Index("idx_balance_steam_id")
                .OnTable("balance")
                .OnColumn("steam_id").Ascending();
        }
    }

    public override void Down()
    {
        if (Schema.Table("balance").Exists())
        {
            Delete.Table("balance");
        }
    }
}

