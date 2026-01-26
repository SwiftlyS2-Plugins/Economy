using FluentMigrator;

namespace Economy.Database.Migrations;

[Migration(1761193038)]
public class InitTable : Migration
{
    public override void Up()
    {
        if (!Schema.Table("EconomyPlayers").Exists())
        {
            Create.Table("EconomyPlayers")
                .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                .WithColumn("SteamId64").AsInt64().NotNullable()
                .WithColumn("Balance").AsFixedLengthString(8192).NotNullable();
        }
    }

    public override void Down()
    {
        if (Schema.Table("EconomyPlayers").Exists())
            Delete.Table("EconomyPlayers");
    }
}