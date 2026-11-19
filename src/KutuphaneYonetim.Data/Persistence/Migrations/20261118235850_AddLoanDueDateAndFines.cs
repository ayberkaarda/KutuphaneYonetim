using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KutuphaneYonetim.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanDueDateAndFines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ceza_odeme_tarihi",
                table: "OduncIslemleri",
                type: "datetime",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ceza_tutari",
                table: "OduncIslemleri",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "teslim_tarihi",
                table: "OduncIslemleri",
                type: "date",
                nullable: true);

            // The scaffolded UpdateData calls for the sample loans were removed: they only set the new
            // columns to NULL, which they already are, and a legacy database may hold real rows with those ids.

            migrationBuilder.CreateIndex(
                name: "IX_OduncIslemleri_teslim_tarihi",
                table: "OduncIslemleri",
                column: "teslim_tarihi");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OduncIslemleri_ceza_tutari",
                table: "OduncIslemleri",
                sql: "[ceza_tutari] IS NULL OR [ceza_tutari] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OduncIslemleri_teslim_tarihi",
                table: "OduncIslemleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OduncIslemleri_ceza_tutari",
                table: "OduncIslemleri");

            migrationBuilder.DropColumn(
                name: "ceza_odeme_tarihi",
                table: "OduncIslemleri");

            migrationBuilder.DropColumn(
                name: "ceza_tutari",
                table: "OduncIslemleri");

            migrationBuilder.DropColumn(
                name: "teslim_tarihi",
                table: "OduncIslemleri");
        }
    }
}
