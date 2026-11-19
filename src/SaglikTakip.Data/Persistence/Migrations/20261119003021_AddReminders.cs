using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaglikTakip.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hatirlaticilar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KullaniciId = table.Column<int>(type: "int", nullable: false),
                    Tur = table.Column<int>(type: "int", nullable: false),
                    Baslik = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AralikSaat = table.Column<int>(type: "int", nullable: false),
                    SonrakiZaman = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hatirlaticilar", x => x.Id);
                    table.CheckConstraint("CK_Hatirlaticilar_AralikSaat", "[AralikSaat] >= 1 AND [AralikSaat] <= 8760");
                    table.CheckConstraint("CK_Hatirlaticilar_Tur", "[Tur] IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_Hatirlaticilar_Kullanicilar",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hatirlaticilar_KullaniciId",
                table: "Hatirlaticilar",
                column: "KullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_Hatirlaticilar_SonrakiZaman",
                table: "Hatirlaticilar",
                column: "SonrakiZaman");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Hatirlaticilar");
        }
    }
}
