using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlexiSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPasswordHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PhoneNumber already exists from InitialCreate; only add PasswordHash.
            // Guard in case a previous partial apply already created the column.
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.Users', 'PasswordHash') IS NULL
                BEGIN
                    ALTER TABLE [Users] ADD [PasswordHash] nvarchar(200) NULL;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.Users', 'PasswordHash') IS NOT NULL
                BEGIN
                    ALTER TABLE [Users] DROP COLUMN [PasswordHash];
                END
                """);
        }
    }
}
