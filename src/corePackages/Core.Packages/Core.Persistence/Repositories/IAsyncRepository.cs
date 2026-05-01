using Core.Persistence.Dynamic;
using Core.Persistence.Paging;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Core.Persistence.Repositories;

// IASYNCREPOSITORY NEDİR VE NEDEN VAR?
// IRepository'nin asenkron (async/await) versiyonudur.
// Tüm metodlar Task döndürür, yani await ile beklenebilir.
//
// IRepository'den farkı nedir?
//   IRepository → Senkron: veritabanı cevap verene kadar thread bekler (bloklanır).
//   IAsyncRepository → Asenkron: veritabanı cevap verene kadar thread serbest kalır,
//                      başka isteklere hizmet verebilir. Web API'larda bu şekilde
//                      çok daha fazla eş zamanlı kullanıcıya hizmet verilebilir.
//
// : IQuery<TEntity> nedir?
//   IQuery'yi miras alarak Query() metodunu da bu interface'e dahil eder.
//   IRepository da IQuery'yi miras alıyordu; bu iki interface birbirinden bağımsız
//   ama ikisi de Query() metoduna ihtiyaç duyduğu için IQuery ayrı tutuldu.
//
// EfRepositoryBase bu interface'i implement eder.
// UserRepository gibi sınıflar EfRepositoryBase'i miras alır
// ve dolayısıyla bu interface'in tüm metodlarını miras yoluyla kazanır.
public interface IAsyncRepository<TEntity, TEntityId> : IQuery<TEntity>
    where TEntity : Entity<TEntityId>
{
    // Tek bir kayıt asenkron olarak getirir. Koşula uyan ilk kaydı döndürür, yoksa null.
    //
    // Task<TEntity?> → asenkron, null dönebilir
    // predicate     → hangi kayıt? Örnek: u => u.Id == 5
    // include       → ilişkili tablolar da gelsin mi? Örnek: q => q.Include(u => u.Orders)
    // withDeleted   → soft delete ile silinmişler de dahil edilsin mi?
    // enableTracking→ EF Core bu nesneyi değişiklik takibi için izlesin mi?
    //                 Sadece okuma yapılacaksa false ver, performans artar.
    // cancellationToken → İstek iptal edilirse veritabanı sorgusunu durdurur.
    Task<TEntity?> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool withDeleted = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default);

    // Birden fazla kayıt asenkron olarak getirir, sayfalanmış döner.
    // predicate null → filtre yok, tüm kayıtlar gelir (sayfa boyutuna göre)
    // orderBy   → sıralama fonksiyonu, null bırakılırsa sıralama yok
    // index=0, size=10 → varsayılan: 1. sayfa, 10 kayıt
    Task<Paginate<TEntity>> GetListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withDeleted = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default
    );

    // DynamicQuery ile filtreleme+sıralama yaparak sayfalanmış liste asenkron döner.
    // Kullanıcının UI'da seçtiği dinamik filtreler bu metoda aktarılır.
    // GetListAsync'ten farkı: filtre ve sıralama sabit kod değil, çalışma zamanında belirlenir.
    Task<Paginate<TEntity>> GetListByDynamicAsync(
        DynamicQuery dynamic,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withDeleted = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default
    );

    // Koşula uyan herhangi bir kayıt var mı? Asenkron olarak true/false döner.
    // Örnek: await AnyAsync(u => u.Email == "ali@mail.com") → Email kayıtlı mı?
    // predicate null → tabloda hiç kayıt var mı?
    Task<bool> AnyAsync(
       Expression<Func<TEntity, bool>>? predicate = null,
       bool withDeleted = false,
       bool enableTracking = true,
       CancellationToken cancellationToken = default
   );

    // Tek kayıt ekler ve eklenen entity'yi asenkron döndürür.
    // EfRepositoryBase içinde CreatedDate otomatik set edilir.
    Task<TEntity> AddAsync(TEntity entity);

    // Birden fazla kaydı aynı anda ekler.
    // Her birinin CreatedDate'i otomatik set edilir.
    Task<ICollection<TEntity>> AddRangeAsync(ICollection<TEntity> entities);

    // Var olan kaydı günceller.
    // EfRepositoryBase içinde UpdatedDate otomatik set edilir.
    Task<TEntity> UpdateAsync(TEntity entity);

    // Birden fazla kaydı aynı anda günceller.
    Task<ICollection<TEntity>> UpdateRangeAsync(ICollection<TEntity> entities);

    // Kaydı siler.
    // permanent=false (varsayılan) → Soft delete: DeletedDate doldurulur, kayıt veritabanında kalır.
    // permanent=true              → Hard delete: kayıt fiziksel olarak silinir, geri alınamaz.
    Task<TEntity> DeleteAsync(TEntity entity, bool permanent = false);

    // Birden fazla kaydı aynı anda siler.
    Task<ICollection<TEntity>> DeleteRangeAsync(ICollection<TEntity> entities, bool permanent = false);
}
