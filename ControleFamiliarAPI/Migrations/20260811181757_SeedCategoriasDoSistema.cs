using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFamiliarAPI.Migrations
{
    /// <summary>
    /// Transforma as categorias base em catálogo do sistema, sem dono.
    /// </summary>
    // Antes, cada família recebia uma cópia própria das 13 categorias base
    // (migration SeedCategoriasPadrao). Agora elas existem uma única vez, com
    // FamiliaId nulo, e ficam disponíveis para todas as famílias.
    public partial class SeedCategoriasDoSistema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Precisa vir primeiro: sem a coluna anulável, o INSERT abaixo falha.
            migrationBuilder.AlterColumn<int>(
                name: "FamiliaId",
                table: "Categorias",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // Remove as cópias por família que a migration anterior criou —
            // sem isso elas ficariam duplicadas ao lado das do sistema.
            //
            // Conservador de propósito: só apaga a cópia que continua com a
            // descrição original E não é usada por nenhuma transação. Cópia
            // renomeada pela família virou categoria dela; cópia já usada num
            // lançamento é dado do usuário. Nos dois casos ela fica, e no
            // máximo aparece junto com a homônima do sistema.
            migrationBuilder.Sql(@"
                DELETE FROM Categorias
                WHERE FamiliaId IS NOT NULL
                  AND Descricao IN (
                        N'Salário', N'Renda extra', N'Moradia', N'Água', N'Luz',
                        N'Gás', N'Internet e telefone', N'Mercado', N'Transporte',
                        N'Saúde', N'Educação', N'Lazer', N'Outros'
                      )
                  AND NOT EXISTS (
                        SELECT 1 FROM Transacoes t WHERE t.CategoriaId = Categorias.Id
                  );
            ");

            // Insere o catálogo. O NOT EXISTS torna a migration segura de
            // reexecutar e evita duplicar se ela rodar num banco que já tenha
            // as categorias do sistema.
            migrationBuilder.Sql(@"
                INSERT INTO Categorias (Descricao, Finalidade, FamiliaId)
                SELECT catalogo.Descricao, catalogo.Finalidade, NULL
                FROM (VALUES
                    (N'Salário', 1),
                    (N'Renda extra', 1),
                    (N'Moradia', 2),
                    (N'Água', 2),
                    (N'Luz', 2),
                    (N'Gás', 2),
                    (N'Internet e telefone', 2),
                    (N'Mercado', 2),
                    (N'Transporte', 2),
                    (N'Saúde', 2),
                    (N'Educação', 2),
                    (N'Lazer', 2),
                    (N'Outros', 3)
                ) AS catalogo(Descricao, Finalidade)
                WHERE NOT EXISTS (
                    SELECT 1 FROM Categorias existente
                    WHERE existente.FamiliaId IS NULL
                      AND existente.Descricao = catalogo.Descricao
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Obrigatório antes do AlterColumn: voltar a coluna para NOT NULL
            // falha enquanto existir qualquer linha com FamiliaId nulo.
            //
            // Se alguma categoria do sistema já estiver em uso por uma
            // transação, este DELETE não a remove (a FK é Restrict) e o
            // AlterColumn abaixo falha — reverter esta migration depois que os
            // lançamentos começaram exige decidir na mão o que fazer com eles.
            migrationBuilder.Sql(@"
                DELETE FROM Categorias
                WHERE FamiliaId IS NULL
                  AND NOT EXISTS (
                        SELECT 1 FROM Transacoes t WHERE t.CategoriaId = Categorias.Id
                  );
            ");

            migrationBuilder.AlterColumn<int>(
                name: "FamiliaId",
                table: "Categorias",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
