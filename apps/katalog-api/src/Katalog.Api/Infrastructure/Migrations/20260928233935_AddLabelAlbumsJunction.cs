using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalog.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLabelAlbumsJunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "label_albums",
                columns: table => new
                {
                    label_id = table.Column<Guid>(type: "uuid", nullable: false),
                    album_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_seen_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    last_confirmed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_label_albums", x => new { x.label_id, x.album_id });
                    table.ForeignKey(
                        name: "fk_label_albums_albums_album_id",
                        column: x => x.album_id,
                        principalTable: "albums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_label_albums_labels_label_id",
                        column: x => x.label_id,
                        principalTable: "labels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_label_albums_album_id",
                table: "label_albums",
                column: "album_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "label_albums");
        }
    }
}
