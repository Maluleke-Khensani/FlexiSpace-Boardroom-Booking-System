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
            // Superseded by 20261004213134_AddBlockedPeriods, which created the
            // current BlockedPeriods schema. Keep this migration ID so databases
            // that already applied it remain compatible, but do not recreate the
            // table on databases where the newer migration is already recorded.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: this migration no longer owns the table. The later migration
            // is responsible for creating and dropping the current schema.
        }
    }
}
