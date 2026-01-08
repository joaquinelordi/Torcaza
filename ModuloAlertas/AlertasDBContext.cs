using Microsoft.EntityFrameworkCore;
using Npgsql.PostgresTypes;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuloAlertas
{
    public class AlertasDBContext : DbContext
    {
        public AlertasDBContext(DbContextOptions<AlertasDBContext> options) : base(options) { }

        public DbSet<CercoVirtualDB> CercosVirtuales => Set<CercoVirtualDB>();
        public DbSet<DispositivoDB> DispositivoCercos => Set<DispositivoDB>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configuración de la entidad CercoVirtualDB
            modelBuilder.Entity<CercoVirtualDB>(entity =>
            {
                entity.ToTable("cercos_virtuales");
                entity.HasKey(e => e.CercoId);
                entity.Property(e => e.CercoId).HasColumnName("cerco_id");
                entity.Property(e => e.Nombre).HasColumnName("cerco_nombre");
                entity.Property(e => e.Activo).HasColumnName("cerco_activo");
                entity.Property(e => e.Creado).HasColumnName("cerco_creado");
                entity.Property(e => e.Geom4326).HasColumnName("cerco_geom_4326");
                entity.Property(e => e.Geom22185).HasColumnName("cerco_geom_22185");
            });


            // Configuración de la entidad DispositivoDB
            modelBuilder.Entity<DispositivoDB>(entity =>
            {
                entity.ToTable("dispositivo_cerco");
                entity.HasKey(e => new { e.DispositivoId, e.CercoId });
                entity.Property(e => e.DispositivoId).HasColumnName("dc_disp_id");
                entity.Property(e => e.CercoId).HasColumnName("dc_cerco_id");
                entity.Property(e => e.Activo).HasColumnName("dc_activo");
                entity.Property(e => e.Creado).HasColumnName("dc_creado");
                entity.Property(e => e.TipoAlerta).HasColumnName("dc_tipo_alerta");
            });

            // Configuración de la entidad AlertaGeograficaDB
            modelBuilder.Entity<AlertaGeograficaDB>(entity =>
            {
                entity.ToTable("alertas_geograficas");
                entity.HasKey(e => e.AlertaId);
                entity.Property(e => e.AlertaId).HasColumnName("alerta_id");
                entity.Property(e => e.RegistroId).HasColumnName("ubi_registro_id");
                entity.Property(e => e.DispositivoId).HasColumnName("ubi_dispositivo_id");
                entity.Property(e => e.Timestamp).HasColumnName("ubi_timestamp");
                entity.Property(e => e.CercoId).HasColumnName("cerco_id");
                entity.Property(e => e.TipoAlerta).HasColumnName("tipo_alerta");
                entity.Property(e => e.AlertaGenerada).HasColumnName("alerta_generada");
                entity.Property(e => e.AlertaEstado).HasColumnName("alerta_estado");
            });

        }



    }

    public class CercoVirtualDB
    {
        public int CercoId { get; set; }
        public string? Nombre { get; set; }
        public bool Activo { get; set; }
        public DateTime Creado { get; set; }
        public Geometry? Geom4326 { get; set; }
        public Geometry? Geom22185 { get; set; }
    }

    public class DispositivoDB
    {
        public int DispositivoId { get; set; }
        public int CercoId { get; set; }
        public bool Activo { get; set; }
        public DateTime Creado { get; set; }
        public eTipoAlertaCercoVirtual TipoAlerta { get; set; }
    }

    public class AlertaGeograficaDB
    {
        public int AlertaId { get; set; }
        public Guid RegistroId { get; set; }
        public Guid DispositivoId { get; set; }
        public DateTime Timestamp { get; set; }
        public int CercoId { get; set; }
        public int TipoAlerta { get; set; }
        public DateTime AlertaGenerada { get; set; }
        public int AlertaEstado { get; set; }
    }
}
