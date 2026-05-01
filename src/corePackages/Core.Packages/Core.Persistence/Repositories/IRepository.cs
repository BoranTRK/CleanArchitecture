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

// IREPOSITORY NEDİR VE NEDEN VAR?
// Senkron (beklemeli, async olmayan) veritabanı işlemlerinin sözleşmesini tanımlar.
// IAsyncRepository'nin senkron kardeşidir.
//
// Repository Pattern nedir?
//   Veritabanı işlemlerini (ekle, sil, güncelle, listele) uygulama kodundan soyutlar.
//   Servis katmanı "nasıl" çekileceğini bilmez, sadece "ne" istediğini söyler.
//   Örnek: UserService, "kullanıcıyı getir" der. Nasıl getirileceği (EF Core, Dapper...)
//   UserRepository içinde gizlidir. Yarın veritabanı değişse servis kodu değişmez.
//
// : IQuery<TEntity> nedir?
//   IRepository, IQuery'yi de miras alır. Bu sayede Query() metodunu da içerir.
//   IQuery ayrı tutulmuştu çünkü hem IRepository hem IAsyncRepository'de gerekiyor.
//
// where TEntity : Entity<TEntityId> nedir?
//   Generic kısıtlama (constraint). TEntity olarak sadece Entity<TEntityId>'yi
//   miras alan sınıflar kullanılabilir demektir.
//   Yani IRepository<string, int> yapamazsın; TEntity bir Entity olmak zorunda.
//   Bu sayede repository içinde Id, CreatedDate gibi alanlara güvenle erişilebilir.
//
// Neden bu interface var, direkt EfRepositoryBase kullansak olmaz mıydı?
//   Clean Architecture'ın temel kuralı: üst katmanlar (servisler) alt katmanlara
//   (EF Core, veritabanı) direkt bağımlı olmamalı.
//   Servis katmanı IRepository'ye bağımlıdır, EfRepositoryBase'e değil.
//   Yarın EF Core yerine Dapper kullansak, servislere dokunmadan sadece
//   yeni bir repository sınıfı yazıp interface'i implement ederiz.
public interface IRepository<TEntity, TEntityId> : IQuery<TEntity>
    where TEntity : Entity<TEntityId>
{
    // Tek bir kayıt getirir. Koşula uyan ilk kaydı döndürür, yoksa null döner.
    //
    // predicate → Hangi kaydı istiyorsun? Lambda ile filtre.
    //   Örnek: u => u.Email == "ali@mail.com"
    //
    // include → İlişkili tablolar da yüklensin mi? (Eager Loading)
    //   Örnek: q => q.Include(u => u.Orders) → Kullanıcıyla birlikte siparişlerini de getir
    //   null bırakılırsa sadece o entity gelir, ilişkiler yüklenmez.
    //
    // withDeleted → Soft delete ile silinmiş kayıtlar da gelsin mi?
    //   false (varsayılan): silinmişler gösterilmez
    //   true: silinmişler de dahil edilir (örn. admin silinen kullanıcıyı görmek istedi)
    //
    // enableTracking → EF Core bu nesneyi takip etsin mi (Change Tracking)?
    //   true (varsayılan): EF Core nesneyi takip eder, değişiklikler SaveChanges'te kaydedilir.
    //   false (AsNoTracking): EF Core takip etmez, sadece okuma yapılacaksa çok daha hızlıdır.
    TEntity? Get(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool withDeleted = false,
        bool enableTracking = true
    );

    // Birden fazla kayıt getirir, sayfalanmış (Paginate) olarak döner.
    // Tüm parametreler Get() ile aynı mantıkta çalışır, ekstralar:
    //
    // orderBy → Sıralama fonksiyonu.
    //   Örnek: q => q.OrderBy(u => u.Name) → Ada göre sırala
    //   null bırakılırsa sıralama yapılmaz.
    //
    // index, size → Sayfalama parametreleri (Paginate sınıfından biliyoruz):
    //   index=0, size=10 → 1. sayfadaki 10 kayıt
    Paginate<TEntity> GetList(
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withDeleted = false,
        bool enableTracking = true
    );

    // Dynamic klasöründeki DynamicQuery ile filtreleme+sıralama yaparak sayfalanmış liste döner.
    // GetList'ten farkı: filtre ve sıralama bilgisi DynamicQuery olarak dışarıdan gelir.
    // Kullanıcının UI'da seçtiği filtreler doğrudan bu metoda aktarılır.
    Paginate<TEntity> GetListByDynamic(
        DynamicQuery dynamic,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withDeleted = false,
        bool enableTracking = true
    );

    // Koşula uyan herhangi bir kayıt var mı? true/false döner.
    // predicate null bırakılırsa "tabloda hiç kayıt var mı?" sorusuna cevap verir.
    // Örnek: Any(u => u.Email == "ali@mail.com") → Bu email kayıtlı mı?
    bool Any(Expression<Func<TEntity, bool>>? predicate = null, bool withDeleted = false, bool enableTracking = true);

    // Tek kayıt ekler, eklenen entity'yi döndürür.
    TEntity Add(TEntity entity);

    // Birden fazla kaydı aynı anda ekler, eklenen entity koleksiyonunu döndürür.
    ICollection<TEntity> AddRange(ICollection<TEntity> entities);

    // Var olan kaydı günceller, güncellenmiş entity'yi döndürür.
    TEntity Update(TEntity entity);

    // Birden fazla kaydı aynı anda günceller.
    ICollection<TEntity> UpdateRange(ICollection<TEntity> entities);

    // Kaydı siler.
    // permanent=false (varsayılan) → Soft delete: DeletedDate doldurulur, kayıt veritabanında kalır.
    // permanent=true              → Hard delete: kayıt veritabanından tamamen silinir.
    TEntity Delete(TEntity entity, bool permanent = false);

    // Birden fazla kaydı aynı anda siler.
    ICollection<TEntity> DeleteRange(ICollection<TEntity> entity, bool permanent = false);
}
