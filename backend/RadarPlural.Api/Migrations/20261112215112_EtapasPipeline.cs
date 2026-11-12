using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadarPlural.Api.Migrations
{
    /// <inheritdoc />
    public partial class EtapasPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "Stories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "Stories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseOwner",
                table: "Stories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseUntil",
                table: "Stories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Stories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Stories_Status_LeaseUntil",
                table: "Stories",
                columns: new[] { "Status", "LeaseUntil" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Stories_Status_LeaseUntil",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "LeaseOwner",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "LeaseUntil",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Stories");
        }
    }
}
