using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlexiSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockedPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FlexiSpaceDB already has BlockedPeriods from 20260929091423_AddBlockedPeriods
            // (an earlier teammate migration). This file is a second AddBlockedPeriods with a
            // new id, so CREATE TABLE would throw. Only create if the table is missing.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.BlockedPeriods', N'U') IS NULL
                BEGIN
                    CREATE TABLE [BlockedPeriods] (
                        [Id] int NOT NULL IDENTITY,
                        [BoardroomId] int NOT NULL,
                        [StartDate] date NOT NULL,
                        [StartTime] time NOT NULL,
                        [EndDate] date NOT NULL,
                        [EndTime] time NOT NULL,
                        [Reason] nvarchar(500) NOT NULL,
                        [CreatedByUserId] int NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_BlockedPeriods] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_BlockedPeriods_Boardrooms_BoardroomId] FOREIGN KEY ([BoardroomId]) REFERENCES [Boardrooms] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_BlockedPeriods_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id])
                    );

                    CREATE INDEX [IX_BlockedPeriods_BoardroomId_StartDate_EndDate]
                        ON [BlockedPeriods] ([BoardroomId], [StartDate], [EndDate]);
                    CREATE INDEX [IX_BlockedPeriods_CreatedByUserId]
                        ON [BlockedPeriods] ([CreatedByUserId]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockedPeriods");
        }
    }
}
