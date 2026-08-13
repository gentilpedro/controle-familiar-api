using ControleFamiliarAPI.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ControleFamiliarAPI.Data
{
    public class AppDbContext : IdentityDbContext<Usuario, IdentityRole<int>, int>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Familia> Familias => Set<Familia>();
        public DbSet<Pessoa> Pessoas => Set<Pessoa>();
        public DbSet<Categoria> Categorias => Set<Categoria>();
        public DbSet<FormaPagamento> FormasPagamento => Set<FormaPagamento>();
        public DbSet<Transacao> Transacoes => Set<Transacao>();
        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<TokenRevogado> TokensRevogados => Set<TokenRevogado>();
        public DbSet<RegistroAuditoria> RegistrosAuditoria => Set<RegistroAuditoria>();
        public DbSet<FechamentoMensal> FechamentosMensais => Set<FechamentoMensal>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuração da entidade Familia
            modelBuilder.Entity<Familia>(entity =>
            {
                entity.HasKey(f => f.Id);

                entity.Property(f => f.Nome)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(f => f.CodigoConvite)
                      .IsRequired()
                      .HasMaxLength(12);

                entity.HasIndex(f => f.CodigoConvite)
                      .IsUnique();
            });

            // Configuração da entidade Usuario
            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.Property(u => u.Nome)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.HasOne(u => u.Familia)
                      .WithMany(f => f.Usuarios)
                      .HasForeignKey(u => u.FamiliaId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configuração da entidade Pessoa
            modelBuilder.Entity<Pessoa>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Nome)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(p => p.Idade)
                      .IsRequired();

                entity.HasOne(p => p.Familia)
                      .WithMany()
                      .HasForeignKey(p => p.FamiliaId)
                      .OnDelete(DeleteBehavior.Restrict);

                // SetNull e não Cascade: quando a conta some, as transações da
                // pessoa continuam valendo para a família. Ela só deixa de
                // representar um membro e passa a ser pessoa comum — apagar em
                // cascata levaria junto o histórico dos outros.
                entity.HasOne(p => p.Usuario)
                      .WithOne()
                      .HasForeignKey<Pessoa>(p => p.UsuarioId)
                      .OnDelete(DeleteBehavior.SetNull);

                // Uma conta tem no máximo uma Pessoa. O índice precisa ser
                // filtrado: UsuarioId é nulo em toda pessoa cadastrada à mão, e
                // no SQL Server nulos colidem entre si num índice único —
                // sem o filtro, a família só poderia ter um dependente.
                entity.HasIndex(p => p.UsuarioId)
                      .IsUnique()
                      .HasFilter("[UsuarioId] IS NOT NULL");
            });

            // Configuração da entidade Categoria
            modelBuilder.Entity<Categoria>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.Descricao)
                      .IsRequired()
                      .HasMaxLength(400);

                entity.Property(c => c.Finalidade)
                      .IsRequired();

                entity.Property(c => c.AceitaDivisaoPercentual)
                      .IsRequired()
                      .HasDefaultValue(false);

                // IsRequired(false): categoria do sistema não tem família dona.
                // Sem isso o EF infere obrigatório e a FK volta a ser NOT NULL.
                entity.HasOne(c => c.Familia)
                      .WithMany()
                      .HasForeignKey(c => c.FamiliaId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configuração da entidade FormaPagamento
            modelBuilder.Entity<FormaPagamento>(entity =>
            {
                entity.HasKey(f => f.Id);

                entity.Property(f => f.Descricao)
                      .IsRequired()
                      .HasMaxLength(100);

                // IsRequired(false): forma de pagamento do sistema não tem
                // família dona (mesma razão da Categoria acima).
                entity.HasOne(f => f.Familia)
                      .WithMany()
                      .HasForeignKey(f => f.FamiliaId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configuração da entidade Transacao
            modelBuilder.Entity<Transacao>(entity =>
            {
                entity.HasKey(t => t.Id);

                entity.Property(t => t.Descricao)
                      .IsRequired()
                      .HasMaxLength(400);

                entity.Property(t => t.Valor)
                      .IsRequired()
                      .HasColumnType("decimal(18,2)");

                entity.Property(t => t.Tipo)
                      .IsRequired();

                entity.Property(t => t.Data)
                      .IsRequired();

                entity.Property(t => t.Pago)
                      .IsRequired()
                      .HasDefaultValue(true);

                // Relacionamento: uma Pessoa possui muitas Transacoes
                entity.HasOne(t => t.Pessoa)
                      .WithMany(p => p.Transacoes)
                      .HasForeignKey(t => t.PessoaId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Relacionamento: uma Categoria possui muitas Transacoes
                entity.HasOne(t => t.Categoria)
                      .WithMany(c => c.Transacoes)
                      .HasForeignKey(t => t.CategoriaId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Relacionamento: uma FormaPagamento possui muitas Transacoes.
                // Restrict como na Categoria: apagar a forma não pode levar
                // junto o lançamento que a usou.
                entity.HasOne(t => t.FormaPagamento)
                      .WithMany(f => f.Transacoes)
                      .HasForeignKey(t => t.FormaPagamentoId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.Restrict);

                // Relacionamento: uma Familia possui muitas Transacoes
                entity.HasOne(t => t.Familia)
                      .WithMany()
                      .HasForeignKey(t => t.FamiliaId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Índices compostos usados pelo RelatorioService, que sempre
                // filtra por FamiliaId/PessoaId + Tipo. Sem eles, o SQL Server
                // cai pra Key Lookup linha a linha (ou scan) pra buscar
                // Valor/CategoriaId, já que os índices de FK criados por
                // convenção só cobrem uma coluna.
                entity.HasIndex(t => new { t.FamiliaId, t.Tipo })
                      .IncludeProperties(t => new { t.Valor, t.CategoriaId });

                entity.HasIndex(t => new { t.PessoaId, t.Tipo })
                      .IncludeProperties(t => new { t.Valor });

                // Cobre a Listar paginada, que filtra por família e ordena
                // por Data — sem isso o SQL Server ordenaria pela clustered
                // key (Id) e descartaria a maior parte antes de aplicar
                // Skip/Take. INCLUDE dos campos escalares devolvidos pela
                // listagem evita um Key Lookup extra por linha da página.
                entity.HasIndex(t => new { t.FamiliaId, t.Data })
                      .IncludeProperties(t => new { t.Valor, t.Tipo, t.CategoriaId, t.PessoaId });

                // Suporta a propagação de PATCH/DELETE "aplicar às futuras":
                // busca por SerieId com NumeroParcela >= a de uma ocorrência.
                entity.HasIndex(t => new { t.SerieId, t.NumeroParcela });
            });

            // Configuração da entidade TokenRevogado
            modelBuilder.Entity<TokenRevogado>(entity =>
            {
                entity.HasKey(t => t.Jti);

                entity.Property(t => t.Jti)
                      .HasMaxLength(64);
            });

            // Configuração da entidade RegistroAuditoria — sem HasOne/FK de
            // propósito (ver comentário no model): é um log somente-inserção
            // que precisa sobreviver à exclusão do Usuario/Familia que ele
            // descreve.
            modelBuilder.Entity<RegistroAuditoria>(entity =>
            {
                entity.HasKey(r => r.Id);

                entity.Property(r => r.Acao)
                      .IsRequired()
                      .HasMaxLength(50);

                // Consulta mais comum: auditoria de uma família, mais recente primeiro.
                entity.HasIndex(r => new { r.FamiliaId, r.CriadoEm });
            });

            // Configuração da entidade FechamentoMensal
            modelBuilder.Entity<FechamentoMensal>(entity =>
            {
                entity.HasKey(f => f.Id);

                entity.Property(f => f.SaldoTransportado)
                      .IsRequired()
                      .HasColumnType("decimal(18,2)");

                entity.HasOne(f => f.Familia)
                      .WithMany()
                      .HasForeignKey(f => f.FamiliaId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Restrict, não Cascade: a Transacao de saldo é uma
                // transação normal (pode ser filtrada, listada, etc.) — não
                // faz sentido ela sumir se o FechamentoMensal for removido
                // (o que hoje nem existe como operação, mas o comportamento
                // de FK já fica correto se um dia existir).
                entity.HasOne(f => f.TransacaoGerada)
                      .WithMany()
                      .HasForeignKey(f => f.TransacaoGeradaId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Impede fechar o mesmo mês duas vezes a nível de banco, não
                // só checando na aplicação.
                entity.HasIndex(f => new { f.FamiliaId, f.Mes })
                      .IsUnique();
            });
        }
    }
}