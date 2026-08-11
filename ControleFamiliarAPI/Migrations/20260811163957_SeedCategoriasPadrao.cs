using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFamiliarAPI.Migrations
{
    /// <summary>
    /// Cria as categorias padrão para as famílias que já existiam.
    /// </summary>
    // Migration só de dados — não altera schema. A partir daqui, família nova
    // recebe as categorias pelo código (CategoriasPadrao, chamado em
    // AuthService.CriarFamilia e FamiliaService); esta migration existe para as
    // que foram criadas antes disso não ficarem de fora.
    //
    // Mesmo padrão de migration de dados já usado no projeto (o grandfathering
    // das contas na AddAssinaturas, antes do revert do Stripe).
    //
    // Não roda nos testes: lá o schema vem de EnsureCreated a partir do modelo,
    // não das migrations — por isso o SQL pode ser específico de SQL Server.
    public partial class SeedCategoriasPadrao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O NOT EXISTS por descrição torna a migration segura de reexecutar
            // e evita duplicar uma categoria que a família já tenha criado na
            // mão com o mesmo nome (ex.: alguém que já cadastrou "Luz").
            migrationBuilder.Sql(@"
                INSERT INTO Categorias (Descricao, Finalidade, FamiliaId)
                SELECT padrao.Descricao, padrao.Finalidade, f.Id
                FROM Familias f
                CROSS JOIN (VALUES
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
                ) AS padrao(Descricao, Finalidade)
                WHERE NOT EXISTS (
                    SELECT 1 FROM Categorias existente
                    WHERE existente.FamiliaId = f.Id
                      AND existente.Descricao = padrao.Descricao
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove apenas as que continuam sem nenhuma transação vinculada.
            // Uma categoria padrão que já foi usada virou dado do usuário: apagá-la
            // arrastaria as transações junto (ou quebraria a FK), e reverter uma
            // migration não é motivo para destruir lançamento de ninguém.
            migrationBuilder.Sql(@"
                DELETE FROM Categorias
                WHERE Descricao IN (
                        N'Salário', N'Renda extra', N'Moradia', N'Água', N'Luz',
                        N'Gás', N'Internet e telefone', N'Mercado', N'Transporte',
                        N'Saúde', N'Educação', N'Lazer', N'Outros'
                      )
                  AND NOT EXISTS (
                        SELECT 1 FROM Transacoes t WHERE t.CategoriaId = Categorias.Id
                  );
            ");
        }
    }
}
