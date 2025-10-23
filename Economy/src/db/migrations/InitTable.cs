using FluentMigrator;

namespace Economy.Database.Migrations;

[Migration(1761193038)]
public class InitTable : Migration
{
    public override void Up()
    {
        Create.Table("EconomyPlayers")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("SteamId64").AsInt64().NotNullable()
            .WithColumn("Balance").AsFixedLengthString(8192).NotNullable().WithDefaultValue("{}");
    }

    public override void Down()
    {
        Delete.Table("EconomyPlayers");
    }
}