using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadarPlural.Api.Migrations
{
    /// <inheritdoc />
    public partial class TriagemHistorias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscardReason",
                table: "FeedItems",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Flags",
                table: "FeedItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "FeedItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "triado");

            migrationBuilder.AddColumn<Guid>(
                name: "StoryId",
                table: "FeedItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Stories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Topic = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "agrupada"),
                    ItemCount = table.Column<int>(type: "integer", nullable: false),
                    SourceCount = table.Column<int>(type: "integer", nullable: false),
                    FirstPublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastPublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedItems_Status_CollectedAt",
                table: "FeedItems",
                columns: new[] { "Status", "CollectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedItems_StoryId",
                table: "FeedItems",
                column: "StoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Stories_LastPublishedAt",
                table: "Stories",
                column: "LastPublishedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_FeedItems_Stories_StoryId",
                table: "FeedItems",
                column: "StoryId",
                principalTable: "Stories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeedItems_Stories_StoryId",
                table: "FeedItems");

            migrationBuilder.DropTable(
                name: "Stories");

            migrationBuilder.DropIndex(
                name: "IX_FeedItems_Status_CollectedAt",
                table: "FeedItems");

            migrationBuilder.DropIndex(
                name: "IX_FeedItems_StoryId",
                table: "FeedItems");

            migrationBuilder.DropColumn(
                name: "DiscardReason",
                table: "FeedItems");

            migrationBuilder.DropColumn(
                name: "Flags",
                table: "FeedItems");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FeedItems");

            migrationBuilder.DropColumn(
                name: "StoryId",
                table: "FeedItems");
        }
    }
}
