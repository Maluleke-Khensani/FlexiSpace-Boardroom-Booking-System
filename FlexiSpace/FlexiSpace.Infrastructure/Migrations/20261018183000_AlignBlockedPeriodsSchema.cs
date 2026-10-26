using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlexiSpace.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261018183000_AlignBlockedPeriodsSchema")]
public sealed class AlignBlockedPeriodsSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add the new columns in their own command. SQL Server compiles each
        // command before executing it, so later commands must not reference
        // these columns in the same SQL batch that creates them.
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.BlockedPeriods', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'Start') IS NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'StartDate') IS NOT NULL
            BEGIN
                ALTER TABLE dbo.BlockedPeriods ADD [Start] datetime2 NULL, [End] datetime2 NULL;
            END;
            """);


        // Copy existing date/time values without discarding fractional seconds.
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.BlockedPeriods', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'Start') IS NOT NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'StartDate') IS NOT NULL
            BEGIN
                UPDATE dbo.BlockedPeriods
                SET [Start] = DATEADD(NANOSECOND, DATEPART(NANOSECOND, [StartTime]),
                                      DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00:00' AS time), [StartTime]), CAST([StartDate] AS datetime2))),
                    [End] = DATEADD(NANOSECOND, DATEPART(NANOSECOND, [EndTime]),
                                    DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00:00' AS time), [EndTime]), CAST([EndDate] AS datetime2)));

                ALTER TABLE dbo.BlockedPeriods ALTER COLUMN [Start] datetime2 NOT NULL;
                ALTER TABLE dbo.BlockedPeriods ALTER COLUMN [End] datetime2 NOT NULL;
            END;
            """);

        // Remove the old index before dropping the columns it uses.
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.BlockedPeriods', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'StartDate') IS NOT NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'Start') IS NOT NULL
            BEGIN
                DROP INDEX IF EXISTS [IX_BlockedPeriods_BoardroomId_StartDate_EndDate]
                    ON dbo.BlockedPeriods;
                ALTER TABLE dbo.BlockedPeriods DROP COLUMN [StartDate], [StartTime], [EndDate], [EndTime];
            END;
            """);

        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.BlockedPeriods', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'Start') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1 FROM sys.indexes
                   WHERE object_id = OBJECT_ID(N'dbo.BlockedPeriods')
                     AND name = N'IX_BlockedPeriods_BoardroomId_Start_End'
               )
            BEGIN
                CREATE INDEX [IX_BlockedPeriods_BoardroomId_Start_End]
                    ON dbo.BlockedPeriods ([BoardroomId], [Start], [End]);
            END;
            """);

        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.BlockedPeriods', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'CreatedById') IS NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'CreatedByUserId') IS NOT NULL
            BEGIN
                EXEC sp_rename N'dbo.BlockedPeriods.CreatedByUserId', N'CreatedById', N'COLUMN';
            END;
            """);

        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.BlockedPeriods', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'Reason') IS NOT NULL
            BEGIN
                ALTER TABLE dbo.BlockedPeriods ALTER COLUMN [Reason] nvarchar(1000) NOT NULL;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.BlockedPeriods', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'StartDate') IS NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'Start') IS NOT NULL
            BEGIN
                ALTER TABLE dbo.BlockedPeriods ADD [StartDate] date NULL, [StartTime] time NULL,
                    [EndDate] date NULL, [EndTime] time NULL;
            END;
            """);

        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.BlockedPeriods', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'StartDate') IS NOT NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'Start') IS NOT NULL
            BEGIN
                UPDATE dbo.BlockedPeriods
                SET [StartDate] = CONVERT(date, [Start]), [StartTime] = CONVERT(time, [Start]),
                    [EndDate] = CONVERT(date, [End]), [EndTime] = CONVERT(time, [End]);

                ALTER TABLE dbo.BlockedPeriods ALTER COLUMN [StartDate] date NOT NULL;
                ALTER TABLE dbo.BlockedPeriods ALTER COLUMN [StartTime] time NOT NULL;
                ALTER TABLE dbo.BlockedPeriods ALTER COLUMN [EndDate] date NOT NULL;
                ALTER TABLE dbo.BlockedPeriods ALTER COLUMN [EndTime] time NOT NULL;
            END;
            """);

        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.BlockedPeriods', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'StartDate') IS NOT NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'Start') IS NOT NULL
            BEGIN
                DROP INDEX IF EXISTS [IX_BlockedPeriods_BoardroomId_Start_End]
                    ON dbo.BlockedPeriods;
                ALTER TABLE dbo.BlockedPeriods DROP COLUMN [Start], [End];
            END;
            """);

        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.BlockedPeriods', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'StartDate') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1 FROM sys.indexes
                   WHERE object_id = OBJECT_ID(N'dbo.BlockedPeriods')
                     AND name = N'IX_BlockedPeriods_BoardroomId_StartDate_EndDate'
               )
            BEGIN
                CREATE INDEX [IX_BlockedPeriods_BoardroomId_StartDate_EndDate]
                    ON dbo.BlockedPeriods ([BoardroomId], [StartDate], [EndDate]);
            END;
            """);

        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.BlockedPeriods', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'CreatedByUserId') IS NULL
               AND COL_LENGTH(N'dbo.BlockedPeriods', N'CreatedById') IS NOT NULL
            BEGIN
                EXEC sp_rename N'dbo.BlockedPeriods.CreatedById', N'CreatedByUserId', N'COLUMN';
            END;
            """);
    }
}
