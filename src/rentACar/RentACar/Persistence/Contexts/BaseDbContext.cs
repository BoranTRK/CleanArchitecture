using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Persistence.Contexts;

public class BaseDbContext : DbContext
{
    protected IConfiguration Configuration { get; set; }
    public DbSet<Brand> Brands { get; set; }
    public DbSet<Car> Cars { get; set; }
    public DbSet<Fuel> Fuels { get; set; }
    public DbSet<Transmission> Transmissions { get; set; }
    public DbSet<Model> Models { get; set; }

    // Veri tabanı ile ilgili konfigürasyonlar burada yapılıyor
    public BaseDbContext(DbContextOptions dbContextOptions, IConfiguration configuration) : base(dbContextOptions)
    {
        Configuration = configuration;
        Database.EnsureCreated(); // Veritabanının oluşmasından emin olmamızı sağlar
    }

    // Veri tabanı modeli oluşturulurken yapılacak konfigürasyonlar burada yapılır
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Mevcut Assembly içerisindeki konfigürasyonları bulacak ve getirerek uygulayacak
        // Bakacağı dosya da Persistence/EntityConfigurations klasöründe bulunan konfigürasyon dosyalarıdır
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
