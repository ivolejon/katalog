using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalog.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "artists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    spotify_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    external_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    genres = table.Column<string[]>(type: "text[]", nullable: true),
                    popularity = table.Column<int>(type: "integer", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "labels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_labels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "poll_cursors",
                columns: table => new
                {
                    job_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cursor_value = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    last_run_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_poll_cursors", x => x.job_name);
                });

            migrationBuilder.CreateTable(
                name: "albums",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    spotify_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    album_type = table.Column<int>(type: "integer", nullable: false),
                    release_date = table.Column<DateOnly>(type: "date", nullable: true),
                    release_date_precision = table.Column<int>(type: "integer", nullable: false),
                    label_spotify = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    label_id = table.Column<Guid>(type: "uuid", nullable: true),
                    image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    external_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    total_tracks = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_albums", x => x.id);
                    table.ForeignKey(
                        name: "fk_albums_labels_label_id",
                        column: x => x.label_id,
                        principalTable: "labels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "artist_label",
                columns: table => new
                {
                    label_id = table.Column<Guid>(type: "uuid", nullable: false),
                    artist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provenance = table.Column<int>(type: "integer", nullable: false),
                    first_seen_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    last_confirmed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artist_label", x => new { x.label_id, x.artist_id });
                    table.ForeignKey(
                        name: "fk_artist_label_artists_artist_id",
                        column: x => x.artist_id,
                        principalTable: "artists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_artist_label_labels_label_id",
                        column: x => x.label_id,
                        principalTable: "labels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "album_artists",
                columns: table => new
                {
                    album_id = table.Column<Guid>(type: "uuid", nullable: false),
                    artist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_album_artists", x => new { x.album_id, x.artist_id });
                    table.ForeignKey(
                        name: "fk_album_artists_albums_album_id",
                        column: x => x.album_id,
                        principalTable: "albums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_album_artists_artists_artist_id",
                        column: x => x.artist_id,
                        principalTable: "artists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_album_artists_artist_id",
                table: "album_artists",
                column: "artist_id");

            migrationBuilder.CreateIndex(
                name: "ix_albums_label_id",
                table: "albums",
                column: "label_id");

            migrationBuilder.CreateIndex(
                name: "ix_albums_spotify_id",
                table: "albums",
                column: "spotify_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_artist_label_artist_id",
                table: "artist_label",
                column: "artist_id");

            migrationBuilder.CreateIndex(
                name: "ix_artists_spotify_id",
                table: "artists",
                column: "spotify_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_labels_slug",
                table: "labels",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "album_artists");

            migrationBuilder.DropTable(
                name: "artist_label");

            migrationBuilder.DropTable(
                name: "poll_cursors");

            migrationBuilder.DropTable(
                name: "albums");

            migrationBuilder.DropTable(
                name: "artists");

            migrationBuilder.DropTable(
                name: "labels");
        }
    }
}
