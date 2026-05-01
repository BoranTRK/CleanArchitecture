using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Persistence.Repositories;

// ENTITY NEDİR VE NEDEN VAR?
// Projedeki tüm veritabanı tablolarına karşılık gelen sınıfların (User, Product, Order...)
// miras alacağı temel (base) sınıftır.
//
// Neden böyle bir base sınıf yapıyoruz?
//   Her entity sınıfında Id, CreatedDate, UpdatedDate, DeletedDate alanlarını
//   tekrar tekrar yazmak zorunda kalmamak için.
//   Entity<TId>'yi miras alan her sınıf bu alanları otomatik olarak kazanır.
//
// <TId> (Generic Id) nedir?
//   Farklı tablolar farklı tipte primary key kullanabilir:
//   - User tablosu int Id kullanıyorsa → Entity<int>
//   - Order tablosu Guid Id kullanıyorsa → Entity<Guid>
//   Bu sayede her entity kendi Id tipini belirleyebilir, ayrı sınıf yazmak gerekmez.
//
// : IEntityTimestamps nedir?
//   Entity sınıfı IEntityTimestamps interface'ini implement eder.
//   Bu sayede EfRepositoryBase, bir nesnenin Entity olduğunu bilirse
//   onun CreatedDate, UpdatedDate, DeletedDate alanlarına güvenle erişebilir.
//
// Örnek kullanım:
//   public class User : Entity<int> { public string Name { get; set; } }
//   → User artık Id(int), CreatedDate, UpdatedDate, DeletedDate alanlarına sahiptir.
public class Entity<TId> : IEntityTimestamps
{
    // Tablodaki primary key (birincil anahtar) alanı.
    // Tipi generic: Entity<int> yapılırsa int, Entity<Guid> yapılırsa Guid olur.
    public TId Id { get; set; }

    // Kaydın oluşturulma tarihi. IEntityTimestamps'ten gelen zorunlu alan.
    // AddAsync() içinde otomatik set edilir, elle doldurulmaz.
    public DateTime CreatedDate { get; set; }

    // Kaydın güncellenme tarihi. Hiç güncellenmemişse null kalır.
    // UpdateAsync() çağrıldığında otomatik set edilir.
    public DateTime? UpdatedDate { get; set; }

    // Kaydın silinme tarihi. Soft Delete için kullanılır.
    // Kayıt aktifse null kalır. DeleteAsync() çağrıldığında otomatik set edilir.
    public DateTime? DeletedDate { get; set; }

    // Parametresiz constructor.
    // Id'yi default değere set eder:
    //   int için default = 0
    //   Guid için default = Guid.Empty (00000000-0000-0000-0000-000000000000)
    //   string için default = null
    // EF Core nesneleri oluştururken bu constructor'ı kullanır.
    public Entity()
    {
        Id = default;
    }

    // Parametreli constructor — Id'yi dışarıdan alarak nesne oluşturur.
    // Örnek: new User(5) → Id=5 olan bir User nesnesi
    // Genellikle var olan bir kaydı temsil eden nesne oluşturulurken kullanılır.
    public Entity(TId id)
    {
        Id = id;
    }
}
