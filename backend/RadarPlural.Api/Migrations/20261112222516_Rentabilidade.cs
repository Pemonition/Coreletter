using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadarPlural.Api.Migrations
{
    /// <inheritdoc />
    public partial class Rentabilidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlanId",
                table: "Members",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "free");

            migrationBuilder.CreateTable(
                name: "UsageMonths",
                columns: table => new
                {
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Used = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageMonths", x => new { x.MemberId, x.Month });
                    table.CheckConstraint("CK_UsageMonths_Used", "\"Used\" >= 0");
                    table.ForeignKey(
                        name: "FK_UsageMonths_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsageReservations",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageReservations", x => x.Key);
                    table.ForeignKey(
                        name: "FK_UsageReservations_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsageReservations_MemberId_Month",
                table: "UsageReservations",
                columns: new[] { "MemberId", "Month" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsageMonths");

            migrationBuilder.DropTable(
                name: "UsageReservations");

            migrationBuilder.DropColumn(
                name: "PlanId",
                table: "Members");
        }
    }
}
