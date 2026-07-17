using ControleGastos.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ControleGastos.Api.Data
{
    public class AppDbContext : IdentityDbContext<Usuario, IdentityRole<int>, int>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Familia> Familias => Set<Familia>();
        public DbSet<Pessoa> Pessoas => Set<Pessoa>();
        public DbSet<Categoria> Categorias => Set<Categoria>();
        public DbSet<Transacao> Transacoes => Set<Transacao>();
        public DbSet<Usuario> Usuarios => Set<Usuario>();

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

                entity.HasOne(c => c.Familia)
                      .WithMany()
                      .HasForeignKey(c => c.FamiliaId)
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
            });
        }
    }
}