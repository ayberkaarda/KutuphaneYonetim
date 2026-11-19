using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KutuphaneYonetim.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rezervasyonlar",
                columns: table => new
                {
                    rezervasyon_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    kitap_id = table.Column<int>(type: "int", nullable: false),
                    uye_id = table.Column<int>(type: "int", nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "datetime", nullable: false),
                    durum = table.Column<int>(type: "int", nullable: false),
                    hazir_tarihi = table.Column<DateTime>(type: "datetime", nullable: true),
                    son_tarih = table.Column<DateTime>(type: "datetime", nullable: true),
                    kapanis_tarihi = table.Column<DateTime>(type: "datetime", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rezervasyonlar", x => x.rezervasyon_id);
                    table.CheckConstraint("CK_Rezervasyonlar_durum", "[durum] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_Rezervasyonlar_hazir", "[durum] <> 1 OR ([hazir_tarihi] IS NOT NULL AND [son_tarih] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Rezervasyonlar_Kitaplar",
                        column: x => x.kitap_id,
                        principalTable: "Kitaplar",
                        principalColumn: "kitap_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rezervasyonlar_Uyeler",
                        column: x => x.uye_id,
                        principalTable: "Uyeler",
                        principalColumn: "uye_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_kitap_durum",
                table: "Rezervasyonlar",
                columns: new[] { "kitap_id", "durum", "olusturma_tarihi" });

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_uye_id",
                table: "Rezervasyonlar",
                column: "uye_id");

            migrationBuilder.CreateIndex(
                name: "UX_Rezervasyonlar_active_kitap_uye",
                table: "Rezervasyonlar",
                columns: new[] { "kitap_id", "uye_id" },
                unique: true,
                filter: "[durum] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "UX_Rezervasyonlar_hold_kitap",
                table: "Rezervasyonlar",
                column: "kitap_id",
                unique: true,
                filter: "[durum] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Rezervasyonlar");
        }
    }
}
