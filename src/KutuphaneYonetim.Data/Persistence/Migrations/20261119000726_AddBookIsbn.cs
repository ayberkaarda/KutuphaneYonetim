using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KutuphaneYonetim.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookIsbn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "isbn",
                table: "Kitaplar",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: true);

            // The scaffolded UpdateData calls for the sample books were removed: they only set the new
            // column to NULL, which it already is, and a legacy database may hold real rows with those ids.

            migrationBuilder.CreateIndex(
                name: "IX_Kitaplar_isbn",
                table: "Kitaplar",
                column: "isbn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Kitaplar_isbn",
                table: "Kitaplar");

            migrationBuilder.DropColumn(
                name: "isbn",
                table: "Kitaplar");
        }
    }
}
