using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace KutuphaneYonetim.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Kitaplar",
                columns: table => new
                {
                    kitap_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    yazar = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    durum = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kitaplar", x => x.kitap_id);
                });

            migrationBuilder.CreateTable(
                name: "Uyeler",
                columns: table => new
                {
                    uye_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ad = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    soyad = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Uyeler", x => x.uye_id);
                });

            migrationBuilder.CreateTable(
                name: "OduncIslemleri",
                columns: table => new
                {
                    odunc_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    kitap_id = table.Column<int>(type: "int", nullable: false),
                    uye_id = table.Column<int>(type: "int", nullable: false),
                    odunc_tarihi = table.Column<DateTime>(type: "datetime", nullable: false),
                    iade_tarihi = table.Column<DateTime>(type: "datetime", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OduncIslemleri", x => x.odunc_id);
                    table.CheckConstraint("CK_OduncIslemleri_iade_tarihi", "[iade_tarihi] IS NULL OR [iade_tarihi] >= [odunc_tarihi]");
                    table.ForeignKey(
                        name: "FK_OduncIslemleri_Kitaplar",
                        column: x => x.kitap_id,
                        principalTable: "Kitaplar",
                        principalColumn: "kitap_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OduncIslemleri_Uyeler",
                        column: x => x.uye_id,
                        principalTable: "Uyeler",
                        principalColumn: "uye_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Kitaplar",
                columns: new[] { "kitap_id", "yazar", "durum", "ad" },
                values: new object[,]
                {
                    { 1, "George Orwell", true, "1984" },
                    { 2, "George Orwell", true, "Hayvan Çiftliği" },
                    { 3, "Paulo Coelho", false, "Simyacı" },
                    { 4, "Sabahattin Ali", true, "Kürk Mantolu Madonna" },
                    { 5, "Fyodor Dostoyevski", true, "Suç ve Ceza" },
                    { 6, "Victor Hugo", true, "Sefiller" },
                    { 7, "Oğuz Atay", true, "Tutunamayanlar" },
                    { 8, "Yaşar Kemal", true, "İnce Memed" }
                });

            migrationBuilder.InsertData(
                table: "Uyeler",
                columns: new[] { "uye_id", "ad", "soyad" },
                values: new object[,]
                {
                    { 1, "Örnek", "Üye" },
                    { 2, "Deneme", "Kullanıcı" },
                    { 3, "Test", "Okur" }
                });

            migrationBuilder.InsertData(
                table: "OduncIslemleri",
                columns: new[] { "odunc_id", "kitap_id", "odunc_tarihi", "uye_id", "iade_tarihi" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2025, 1, 6, 10, 0, 0, 0, DateTimeKind.Unspecified), 1, new DateTime(2025, 1, 20, 16, 30, 0, 0, DateTimeKind.Unspecified) },
                    { 2, 3, new DateTime(2025, 2, 3, 11, 15, 0, 0, DateTimeKind.Unspecified), 2, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Kitaplar_ad",
                table: "Kitaplar",
                column: "ad");

            migrationBuilder.CreateIndex(
                name: "IX_OduncIslemleri_kitap_id",
                table: "OduncIslemleri",
                column: "kitap_id");

            migrationBuilder.CreateIndex(
                name: "IX_OduncIslemleri_uye_id",
                table: "OduncIslemleri",
                column: "uye_id");

            migrationBuilder.CreateIndex(
                name: "UX_OduncIslemleri_active_kitap",
                table: "OduncIslemleri",
                column: "kitap_id",
                unique: true,
                filter: "[iade_tarihi] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Uyeler_soyad_ad",
                table: "Uyeler",
                columns: new[] { "soyad", "ad" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OduncIslemleri");

            migrationBuilder.DropTable(
                name: "Kitaplar");

            migrationBuilder.DropTable(
                name: "Uyeler");
        }
    }
}
