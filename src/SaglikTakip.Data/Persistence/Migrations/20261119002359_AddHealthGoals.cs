using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaglikTakip.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hedefler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KullaniciId = table.Column<int>(type: "int", nullable: false),
                    HedefKilo = table.Column<double>(type: "float", nullable: true),
                    BaslangicKilo = table.Column<double>(type: "float", nullable: true),
                    MinKilo = table.Column<double>(type: "float", nullable: true),
                    MaxKilo = table.Column<double>(type: "float", nullable: true),
                    MinNabiz = table.Column<int>(type: "int", nullable: true),
                    MaxNabiz = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hedefler", x => x.Id);
                    table.CheckConstraint("CK_Hedefler_Kilo", "([HedefKilo] IS NULL OR ([HedefKilo] >= 1 AND [HedefKilo] <= 200)) AND ([BaslangicKilo] IS NULL OR ([BaslangicKilo] >= 1 AND [BaslangicKilo] <= 200)) AND ([MinKilo] IS NULL OR ([MinKilo] >= 1 AND [MinKilo] <= 200)) AND ([MaxKilo] IS NULL OR ([MaxKilo] >= 1 AND [MaxKilo] <= 200))");
                    table.CheckConstraint("CK_Hedefler_KiloAraligi", "[MinKilo] IS NULL OR [MaxKilo] IS NULL OR [MinKilo] <= [MaxKilo]");
                    table.CheckConstraint("CK_Hedefler_Nabiz", "([MinNabiz] IS NULL OR ([MinNabiz] >= 1 AND [MinNabiz] <= 250)) AND ([MaxNabiz] IS NULL OR ([MaxNabiz] >= 1 AND [MaxNabiz] <= 250))");
                    table.CheckConstraint("CK_Hedefler_NabizAraligi", "[MinNabiz] IS NULL OR [MaxNabiz] IS NULL OR [MinNabiz] <= [MaxNabiz]");
                    table.ForeignKey(
                        name: "FK_Hedefler_Kullanicilar",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hedefler_KullaniciId",
                table: "Hedefler",
                column: "KullaniciId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Hedefler");
        }
    }
}
