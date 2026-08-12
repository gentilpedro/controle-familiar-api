using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFamiliarAPI.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaFechamentoMensalECategoriaSaldoAnterior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FechamentosMensais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FamiliaId = table.Column<int>(type: "int", nullable: false),
                    Mes = table.Column<DateOnly>(type: "date", nullable: false),
                    SaldoTransportado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransacaoGeradaId = table.Column<int>(type: "int", nullable: true),
                    FechadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FechamentosMensais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FechamentosMensais_Familias_FamiliaId",
                        column: x => x.FamiliaId,
                        principalTable: "Familias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FechamentosMensais_Transacoes_TransacaoGeradaId",
                        column: x => x.TransacaoGeradaId,
                        principalTable: "Transacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FechamentosMensais_FamiliaId_Mes",
                table: "FechamentosMensais",
                columns: new[] { "FamiliaId", "Mes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FechamentosMensais_TransacaoGeradaId",
                table: "FechamentosMensais",
                column: "TransacaoGeradaId");

            // Mesmo padrão de SeedCategoriasDoSistema/AdicionaDivisaoPercentualNaCategoria:
            // categoria de sistema (FamiliaId nulo), Finalidade.Ambas = 3.
            // PainelMensalService acha esta categoria por nome — é seguro
            // porque é infraestrutura interna do próprio seed, e o filtro
            // FamiliaId nulo já isola do catálogo de qualquer família.
            migrationBuilder.Sql(
                "INSERT INTO Categorias (Descricao, Finalidade, FamiliaId, AceitaDivisaoPercentual) " +
                "VALUES ('Saldo Anterior', 3, NULL, 0);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM Categorias WHERE Descricao = 'Saldo Anterior' AND FamiliaId IS NULL;");

            migrationBuilder.DropTable(
                name: "FechamentosMensais");
        }
    }
}
