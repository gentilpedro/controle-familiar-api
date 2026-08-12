using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFamiliarAPI.Migrations
{
    /// <inheritdoc />
    public partial class BackfillPessoaDosUsuariosExistentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Conta criada antes de VinculaPessoaAoUsuario não ganhou a Pessoa
            // do titular na hora do cadastro — essa migration fecha essa
            // lacuna pra quem já existia.
            //
            // Passo 1: casa pelo nome. Quem já tinha se cadastrado à mão como
            // sua própria Pessoa (prática comum antes dessa mudança) ganha o
            // vínculo em vez de uma pessoa duplicada — só quando o casamento é
            // 1:1 nos dois sentidos: exatamente uma Pessoa sem dono com esse
            // nome, E exatamente um Usuario com esse nome pedindo ela.
            //
            // O segundo lado importa: sem ele, dois Usuario de mesmo nome na
            // mesma família (ex.: duas contas "Ana") cada um veria a mesma
            // Pessoa "Ana" como candidata única, e o UPDATE ligaria os dois ao
            // mesmo registro — SQL Server escolhe um dos dois de forma não
            // determinística quando o FROM casa a mesma linha de destino mais
            // de uma vez.
            migrationBuilder.Sql(@"
                ;WITH Candidatos AS (
                    SELECT
                        u.Id AS UsuarioId,
                        p.Id AS PessoaId,
                        COUNT(*) OVER (PARTITION BY u.Id) AS TotalPorUsuario,
                        COUNT(*) OVER (PARTITION BY p.Id) AS TotalPorPessoa
                    FROM AspNetUsers u
                    JOIN Pessoas p
                        ON p.FamiliaId = u.FamiliaId
                        AND p.UsuarioId IS NULL
                        AND LTRIM(RTRIM(LOWER(p.Nome))) = LTRIM(RTRIM(LOWER(u.Nome)))
                    WHERE NOT EXISTS (SELECT 1 FROM Pessoas px WHERE px.UsuarioId = u.Id)
                )
                UPDATE Pessoas
                SET UsuarioId = c.UsuarioId
                FROM Pessoas p
                JOIN Candidatos c ON c.PessoaId = p.Id
                WHERE c.TotalPorUsuario = 1 AND c.TotalPorPessoa = 1;
            ");

            // Passo 2: quem sobrou (sem match de nome) ganha uma Pessoa nova.
            // Idade 18 é o padrão de FamiliaService.RemoverMembro pro mesmo
            // caso — não temos a idade real de uma conta que nasceu antes do
            // cadastro pedir isso, e assumir adulto evita bloquear receita
            // (TransacaoService) de alguém que provavelmente não é menor.
            migrationBuilder.Sql(@"
                INSERT INTO Pessoas (Nome, Idade, FamiliaId, UsuarioId)
                SELECT u.Nome, 18, u.FamiliaId, u.Id
                FROM AspNetUsers u
                WHERE NOT EXISTS (SELECT 1 FROM Pessoas p WHERE p.UsuarioId = u.Id);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Migration de dados: não há como distinguir depois do fato quais
            // vínculos vieram desta migration e quais já existiam (ex.: contas
            // criadas via Registrar entre esta migration ser escrita e ser
            // aplicada). Reverter arriscaria desvincular Pessoa legítima.
            // Rollback aqui é restaurar backup, não migrations Down().
        }
    }
}
