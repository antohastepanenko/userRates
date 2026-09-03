using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rates.FinanceService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

            migrationBuilder.CreateTable(
                name: "currency",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", maxLength: 3, nullable: false),
                    name = table.Column<string>(type: "text", maxLength: 200, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    nominal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    rate_date = table.Column<DateOnly>(type: "date", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currency", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_currency_code",
                schema: "finance",
                table: "currency",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_currency_rate_date",
                schema: "finance",
                table: "currency",
                column: "rate_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "currency",
                schema: "finance");
        }
    }
}
