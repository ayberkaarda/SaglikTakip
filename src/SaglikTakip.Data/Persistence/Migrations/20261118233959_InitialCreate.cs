using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SaglikTakip.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Kullanicilar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Yas = table.Column<int>(type: "int", nullable: false),
                    Cinsiyet = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kullanicilar", x => x.Id);
                    table.CheckConstraint("CK_Kullanicilar_Cinsiyet", "[Cinsiyet] IN (N'Erkek', N'Kadın', N'Diğer')");
                    table.CheckConstraint("CK_Kullanicilar_Yas", "[Yas] >= 0 AND [Yas] <= 150");
                });

            migrationBuilder.CreateTable(
                name: "Egzersizler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KullaniciId = table.Column<int>(type: "int", nullable: false),
                    EgzersizAdi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SureDakika = table.Column<int>(type: "int", nullable: false),
                    TekrarSayisi = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Egzersizler", x => x.Id);
                    table.CheckConstraint("CK_Egzersizler_SureDakika", "[SureDakika] >= 0 AND [SureDakika] <= 1440");
                    table.CheckConstraint("CK_Egzersizler_TekrarSayisi", "[TekrarSayisi] >= 0 AND [TekrarSayisi] <= 1000");
                    table.ForeignKey(
                        name: "FK_Egzersizler_Kullanicilar",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaglikKayitlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KullaniciId = table.Column<int>(type: "int", nullable: false),
                    Tarih = table.Column<DateTime>(type: "date", nullable: false),
                    Kilo = table.Column<double>(type: "float", nullable: false),
                    Nabiz = table.Column<int>(type: "int", nullable: false),
                    Notlar = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaglikKayitlari", x => x.Id);
                    table.CheckConstraint("CK_SaglikKayitlari_Kilo", "[Kilo] >= 0 AND [Kilo] <= 200");
                    table.CheckConstraint("CK_SaglikKayitlari_Nabiz", "[Nabiz] >= 0 AND [Nabiz] <= 250");
                    table.ForeignKey(
                        name: "FK_SaglikKayitlari_Kullanicilar",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Kullanicilar",
                columns: new[] { "Id", "Yas", "Cinsiyet", "Ad" },
                values: new object[,]
                {
                    { 1, 34, "Kadın", "Örnek Kullanıcı" },
                    { 2, 41, "Erkek", "Deneme Kişi" },
                    { 3, 26, "Diğer", "Test Sporcu" }
                });

            migrationBuilder.InsertData(
                table: "Egzersizler",
                columns: new[] { "Id", "SureDakika", "EgzersizAdi", "TekrarSayisi", "KullaniciId" },
                values: new object[,]
                {
                    { 1, 45, "Yürüyüş", 1, 1 },
                    { 2, 30, "Bisiklet", 1, 2 },
                    { 3, 10, "Şınav", 20, 3 }
                });

            migrationBuilder.InsertData(
                table: "SaglikKayitlari",
                columns: new[] { "Id", "Tarih", "Notlar", "Nabiz", "KullaniciId", "Kilo" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 3, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), "Rutin kontrol", 72, 1, 64.0 },
                    { 2, new DateTime(2025, 4, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 70, 1, 63.0 },
                    { 3, new DateTime(2025, 3, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), "Diyete başlandı", 78, 2, 88.0 },
                    { 4, new DateTime(2025, 5, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 58, 3, 71.0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Egzersizler_KullaniciId",
                table: "Egzersizler",
                column: "KullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_Kullanicilar_Ad",
                table: "Kullanicilar",
                column: "Ad");

            migrationBuilder.CreateIndex(
                name: "IX_SaglikKayitlari_KullaniciId_Tarih",
                table: "SaglikKayitlari",
                columns: new[] { "KullaniciId", "Tarih" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Egzersizler");

            migrationBuilder.DropTable(
                name: "SaglikKayitlari");

            migrationBuilder.DropTable(
                name: "Kullanicilar");
        }
    }
}
