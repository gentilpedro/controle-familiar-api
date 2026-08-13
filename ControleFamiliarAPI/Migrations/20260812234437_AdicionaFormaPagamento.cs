using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFamiliarAPI.Migrations
{
    /// <summary>
    /// Cria o catálogo de formas de pagamento (Pix, Dinheiro, Saque como
    /// sistema, sem dono) e liga a transação a ele.
    /// </summary>
    // Transacoes.FormaPagamentoId nasce anulável e fica assim: as transações
    // que já existem não têm forma de pagamento nenhuma, e não há dado de
    // origem para preencher — inventar um valor aqui seria o mesmo erro que
    // AdicionaDataNaTransacao teve que documentar. Lançamento sem forma de
    // pagamento continua válido depois desta migration.
    public partial class AdicionaFormaPagamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FormaPagamentoId",
                table: "Transacoes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FormasPagamento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Descricao = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FamiliaId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormasPagamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormasPagamento_Familias_FamiliaId",
                        column: x => x.FamiliaId,
                        principalTable: "Familias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transacoes_FormaPagamentoId",
                table: "Transacoes",
                column: "FormaPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_FormasPagamento_FamiliaId",
                table: "FormasPagamento",
                column: "FamiliaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transacoes_FormasPagamento_FormaPagamentoId",
                table: "Transacoes",
                column: "FormaPagamentoId",
                principalTable: "FormasPagamento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Catálogo do sistema (FamiliaId nulo), mesmo padrão de
            // SeedCategoriasDoSistema — a lista espelha Data/FormasPagamentoPadrao.cs,
            // que é a fonte usada pelo EnsureCreated dos testes. Os dois lugares
            // precisam ficar sincronizados na mão, não há automação entre eles.
            //
            // O NOT EXISTS torna a migration segura de reexecutar e evita
            // duplicar num banco que já tenha o catálogo.
            migrationBuilder.Sql(@"
                INSERT INTO FormasPagamento (Descricao, FamiliaId)
                SELECT catalogo.Descricao, NULL
                FROM (VALUES
                    (N'Pix'),
                    (N'Dinheiro'),
                    (N'Saque')
                ) AS catalogo(Descricao)
                WHERE NOT EXISTS (
                    SELECT 1 FROM FormasPagamento existente
                    WHERE existente.FamiliaId IS NULL
                      AND existente.Descricao = catalogo.Descricao
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transacoes_FormasPagamento_FormaPagamentoId",
                table: "Transacoes");

            migrationBuilder.DropTable(
                name: "FormasPagamento");

            migrationBuilder.DropIndex(
                name: "IX_Transacoes_FormaPagamentoId",
                table: "Transacoes");

            migrationBuilder.DropColumn(
                name: "FormaPagamentoId",
                table: "Transacoes");
        }
    }
}
