using Core.Persistence.Dynamic;
using Core.Persistence.Paging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Core.Persistence.Repositories;

// EFREPOSITORYBASE NEDİR VE NEDEN VAR?
// IAsyncRepository ve IRepository interface'lerinin Entity Framework Core ile
// gerçek veritabanı işlemlerini yapan implementasyonudur.
//
// "Base" (temel) neden var?
//   Bu sınıf direkt kullanılmaz. Her entity için ayrı bir repository sınıfı yazılır
//   ve bu sınıftan miras alınır:
//   Örnek: public class UserRepository : EfRepositoryBase<User, int, AppDbContext> { }
//   Bu sayede UserRepository, tüm CRUD metodlarını sıfırdan yazmadan kazanır.
//   Sadece User'a özel ihtiyaçlar varsa UserRepository içine eklenir.
//
// Üç generic parametre ne anlama gelir?
//   TEntity    → Hangi entity? Örnek: User, Product, Order
//   TEntityId  → Entity'nin Id tipi nedir? Örnek: int, Guid
//   TContext   → Hangi DbContext kullanılıyor? Örnek: AppDbContext
//
// Kısıtlamalar (constraints) ne anlama gelir?
//   where TEntity : Entity<TEntityId>
//     → TEntity olarak sadece Entity<TEntityId>'yi miras alan sınıflar kullanılabilir.
//       Bu sayede TEntity'nin Id, CreatedDate, DeletedDate gibi alanlara sahip olduğu garantilenir.
//   where TContext : DbContext
//     → TContext olarak sadece DbContext'i miras alan sınıflar kullanılabilir.
//       Bu sayede Context üzerinde EF Core işlemleri güvenle yapılabilir.
public class EfRepositoryBase<TEntity, TEntityId, TContext>
    : IAsyncRepository<TEntity, TEntityId>, IRepository<TEntity, TEntityId>
    where TEntity : Entity<TEntityId>
    where TContext : DbContext
{
    // Veritabanı bağlantısını ve entity takibini yöneten EF Core nesnesi.
    // protected: sadece bu sınıf ve miras alanlar erişebilir (dışarıya kapalı).
    // readonly: constructor'da bir kez set edilir, sonradan değiştirilemez.
    protected readonly TContext Context;

    // Constructor — DbContext dışarıdan (Dependency Injection ile) enjekte edilir.
    // Neden DI kullanıyoruz?
    //   Repository sınıfı kendi DbContext'ini "new" ile oluştursa bağımlılık yaratır.
    //   DI ile dışarıdan verilirse test sırasında sahte (mock) bir context verilebilir,
    //   ve context'in yaşam döngüsü (scoped/singleton) framework tarafından yönetilir.
    public EfRepositoryBase(TContext context)
    {
        Context = context;
    }

    // ==================== EKLEME İŞLEMLERİ ====================

    // Tek bir entity'yi veritabanına ekler ve eklenen entity'yi döndürür.
    // CreatedDate otomatik set edilir; servis katmanı bunu elle yazmak zorunda değil.
    // DateTime.UtcNow: Sunucu saat diliminden bağımsız, evrensel UTC zamanı kullanılır.
    // Farklı ülkelerdeki sunucularda tutarsızlık olmaması için UTC tercih edilir.
    public async Task<TEntity> AddAsync(TEntity entity)
    {
        entity.CreatedDate = DateTime.UtcNow;   // Oluşturma tarihini şu anki UTC zamanı yap
        await Context.AddAsync(entity);          // Entity'yi EF Core takibine ekle (henüz veritabanına gitmiyor)
        await Context.SaveChangesAsync();         // Şimdi veritabanına yaz (INSERT sorgusu burada çalışır)
        return entity;                            // Id artık veritabanı tarafından doldurulmuş halde döner
    }

    // Birden fazla entity'yi tek seferde veritabanına ekler.
    // Her biri için CreatedDate ayrı ayrı set edilir, sonra hepsi tek SaveChanges ile kaydedilir.
    // Tek tek AddAsync çağırmak yerine bunu kullanmak çok daha performanslıdır:
    // tek bir veritabanı round-trip'i (gidiş-dönüş) yapılır.
    public async Task<ICollection<TEntity>> AddRangeAsync(ICollection<TEntity> entities)
    {
        foreach (TEntity entity in entities)
            entity.CreatedDate = DateTime.UtcNow;
        await Context.AddRangeAsync(entities);
        await Context.SaveChangesAsync();
        return entities;
    }

    // ==================== OKUMA İŞLEMLERİ ====================

    // Koşula uyan herhangi bir kayıt var mı?
    // COUNT(*) yerine EXISTS kullanır, bu yüzden çok daha hızlıdır.
    // (COUNT tüm kayıtları sayar, EXISTS ilk eşleşmede durur)
    public async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        bool withDeleted = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> queryable = Query();                         // Ham sorguyu başlat

        if (!enableTracking)
            queryable = queryable.AsNoTracking();                        // Sadece okuma: EF Core takibini kapat, performans artar

        if (withDeleted)
            queryable = queryable.IgnoreQueryFilters();                  // Soft delete filtresi varsa onu görmezden gel, silinmişleri de dahil et

        if (predicate != null)
            queryable = queryable.Where(predicate);                      // Koşul varsa uygula

        return await queryable.AnyAsync(cancellationToken);              // Veritabanında bu sorguya uyan kayıt var mı?
    }

    // Koşula uyan tek bir kayıt getirir. Yoksa null döner (TEntity? dönüş tipi).
    //
    // Expression<Func<TEntity, bool>> predicate nedir?
    //   Lambda ifadesinin "ağaç" halidir. Normal lambda gibi yazılır:
    //   u => u.Email == "ali@mail.com"
    //   Ama Expression olduğu için EF Core bunu SQL'e çevirebilir:
    //   WHERE Email = 'ali@mail.com'
    //   Eğer Func<TEntity, bool> olsaydı, bu dönüşüm yapılamazdı.
    //
    // Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include nedir?
    //   "Sorguyu al, Include ekleyip geri ver" fonksiyonudur.
    //   Örnek kullanım: q => q.Include(u => u.Orders).ThenInclude(o => o.Items)
    //   null bırakılırsa ilişkili tablolar yüklenmez (lazy loading değil, hiç yüklenmez).
    public async Task<TEntity?> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool withDeleted = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> queryable = Query();

        if (!enableTracking)
            queryable = queryable.AsNoTracking();

        if (include != null)
            queryable = include(queryable);                              // Include fonksiyonunu uygula: ilişkili tablolar yüklensin

        if (withDeleted)
            queryable = queryable.IgnoreQueryFilters();

        return await queryable.FirstOrDefaultAsync(predicate, cancellationToken); // Koşula uyan ilk kaydı getir, yoksa null
    }

    // Koşula uyan kayıtları sayfalanmış olarak getirir.
    // Sıralama varsa önce sıralar sonra sayfalama yapar. Bu önemlidir:
    // sıralamadan önce sayfalama yapılırsa her sorguda farklı sonuçlar gelebilir.
    public async Task<Paginate<TEntity>> GetListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withDeleted = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> queryable = Query();

        if (!enableTracking)
            queryable = queryable.AsNoTracking();

        if (include != null)
            queryable = include(queryable);

        if (withDeleted)
            queryable = queryable.IgnoreQueryFilters();

        if (predicate != null)
            queryable = queryable.Where(predicate);

        // Sıralama varsa önce sırala, sonra sayfalama uygula.
        // Sıralama yoksa direkt sayfalama uygula.
        if (orderBy != null)
            return await orderBy(queryable).ToPaginateAsync(index, size, cancellationToken);
        return await queryable.ToPaginateAsync(index, size, cancellationToken);
    }

    // UI'dan gelen dinamik filtre+sıralama (DynamicQuery) ile kayıtları sayfalanmış getirir.
    // GetListAsync'ten farkı: filtre/sıralama sabit değil, kullanıcının girdiğine göre değişir.
    // Önce ToDynamic() ile filtre+sıralama uygulanır, sonra diğer koşullar eklenir.
    public async Task<Paginate<TEntity>> GetListByDynamicAsync(
        DynamicQuery dynamic,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withDeleted = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default)
    {
        // Query() ile başla, hemen ToDynamic() ile dinamik filtre+sıralamayı uygula
        IQueryable<TEntity> queryable = Query().ToDynamic(dynamic);

        if (!enableTracking)
            queryable = queryable.AsNoTracking();

        if (include != null)
            queryable = include(queryable);

        if (withDeleted)
            queryable = queryable.IgnoreQueryFilters();

        if (predicate != null)
            queryable = queryable.Where(predicate);

        return await queryable.ToPaginateAsync(index, size, cancellationToken);
    }

    // Ham sorguyu döndürür. Context.Set<TEntity>() o entity'nin tüm kayıtlarını temsil eder.
    // Henüz veritabanına gitmez. Üzerine Where, OrderBy, Include eklenebilir.
    // IQuery<TEntity> interface'inden gelen zorunlu metodun implementasyonu.
    public IQueryable<TEntity> Query() => Context.Set<TEntity>();

    // ==================== GÜNCELLEME İŞLEMLERİ ====================

    // Tek bir entity'yi günceller.
    // UpdatedDate otomatik set edilir.
    // Context.Update(): EF Core'a "bu nesne değişti, SaveChanges'te UPDATE yaz" der.
    public async Task<TEntity> UpdateAsync(TEntity entity)
    {
        entity.UpdatedDate = DateTime.UtcNow;
        Context.Update(entity);
        await Context.SaveChangesAsync();
        return entity;
    }

    // Birden fazla entity'yi tek SaveChanges ile günceller.
    public async Task<ICollection<TEntity>> UpdateRangeAsync(ICollection<TEntity> entities)
    {
        foreach (TEntity entity in entities)
            entity.UpdatedDate = DateTime.UtcNow;
        Context.UpdateRange(entities);
        await Context.SaveChangesAsync();
        return entities;
    }

    // ==================== SİLME İŞLEMLERİ ====================

    // Tek bir entity'yi siler.
    // permanent=false → Soft delete (DeletedDate doldurulur, kayıt veritabanında kalır)
    // permanent=true  → Hard delete (kayıt fiziksel olarak silinir, geri alınamaz)
    // Asıl iş SetEntityAsDeletedAsync'e devredilir.
    public async Task<TEntity> DeleteAsync(TEntity entity, bool permanent = false)
    {
        await SetEntityAsDeletedAsync(entity, permanent);
        await Context.SaveChangesAsync();
        return entity;
    }

    // Birden fazla entity'yi siler.
    public async Task<ICollection<TEntity>> DeleteRangeAsync(ICollection<TEntity> entities, bool permanent = false)
    {
        await SetEntityAsDeletedAsync(entities, permanent);
        await Context.SaveChangesAsync();
        return entities;
    }

    // ==================== SOFT DELETE YARDIMCI METODLARI ====================

    // Silme işleminin türüne karar verir.
    // permanent=true  → EF Core'un Remove() ile fiziksel sil
    // permanent=false → Soft delete: önce bire-bir ilişki kontrolü yap, sonra DeletedDate'i doldur
    protected async Task SetEntityAsDeletedAsync(TEntity entity, bool permanent)
    {
        if (!permanent)
        {
            CheckHasEntityHaveOneToOneRelation(entity);              // Bire-bir ilişki var mı kontrol et
            await setEntityAsSoftDeletedAsync((IEntityTimestamps)entity); // Soft delete uygula
        }
        else
        {
            Context.Remove(entity);                                  // Fiziksel sil (veritabanından tamamen kaldır)
        }
    }

    // Soft delete'in bire-bir (one-to-one) ilişkili entity'lerde sorun çıkarabileceğini kontrol eder.
    //
    // Neden bu kontrol gerekli?
    //   Örnek: User ve UserProfile bire-bir ilişkili olsun.
    //   User'ı soft delete ile silersen UserProfile'ın UserId'si hala dolu.
    //   Aynı UserId ile yeni bir User oluşturmak istersen veritabanı hata verir
    //   çünkü o UserId için zaten bir UserProfile kaydı var.
    //   Bu yüzden bire-bir ilişki varsa soft delete yerine hard delete önerilir.
    //
    // Kod ne yapıyor?
    //   Entity'nin tüm foreign key ilişkilerini kontrol eder.
    //   Eğer bir ilişki bire-bir ise (her iki taraf da koleksiyon değilse ve
    //   bağımlı entity bu entity'nin kendisiyse) hata fırlatır.
    protected void CheckHasEntityHaveOneToOneRelation(TEntity entity)
    {
        bool hasEntityHaveOneToOneRelation =
            Context
                .Entry(entity)
                .Metadata.GetForeignKeys()           // Bu entity'nin tüm foreign key ilişkilerini al
                .All(
                    x =>
                        x.DependentToPrincipal?.IsCollection == true       // Bağımlı taraf koleksiyon mu? (bire-çok)
                        || x.PrincipalToDependent?.IsCollection == true    // Ana taraf koleksiyon mu? (bire-çok)
                        || x.DependentToPrincipal?.ForeignKey.DeclaringEntityType.ClrType == entity.GetType() // Bağımlı bu entity'nin kendisi mi?
                ) == false; // Hiçbiri bire-çok değilse → bire-bir ilişki var demek

        if (hasEntityHaveOneToOneRelation)
            throw new InvalidOperationException(
                "Entity has one-to-one relationship. Soft Delete causes problems if you try to create entry again by same foreign key."
            );
    }

    // Soft delete işleminin kendisi — DeletedDate'i doldurur ve ilişkili entity'leri de siler.
    //
    // Neden private ve küçük harf ile başlıyor?
    //   C# convention olarak private metodlar küçük harfle başlayabilir.
    //   Dışarıdan çağrılamaz; yalnızca SetEntityAsDeletedAsync tarafından kullanılır.
    //
    // Cascade soft delete nedir?
    //   Bir entity silindiğinde ona bağlı entity'ler de otomatik silinmeli mi?
    //   Örnek: Order silinirse OrderItems da silinmeli.
    //   Bu metod, Cascade veya ClientCascade davranışı olan ilişkileri bulur
    //   ve onlar için de recursively (özyinelemeli) soft delete uygular.
    private async Task setEntityAsSoftDeletedAsync(IEntityTimestamps entity)
    {
        // Zaten silinmişse tekrar silme (DeletedDate doluysa dur)
        if (entity.DeletedDate.HasValue)
            return;

        entity.DeletedDate = DateTime.UtcNow; // Silinme tarihini şu an olarak işaretle

        // Bu entity'nin cascade silme davranışı olan navigation property'lerini bul.
        // Navigation property: ilişkili tabloyu temsil eden alan. Örnek: User.Orders
        //
        // Filtre: IsOnDependent=false (bu entity ana taraf, bağımlı değil)
        //         DeleteBehavior: Cascade veya ClientCascade (otomatik sil)
        var navigations = Context
            .Entry(entity)
            .Metadata.GetNavigations()
            .Where(x => x is { IsOnDependent: false, ForeignKey.DeleteBehavior: DeleteBehavior.ClientCascade or DeleteBehavior.Cascade })
            .ToList();

        foreach (INavigation? navigation in navigations)
        {
            // Owned entity'leri atla (bunlar bağımsız yaşayamaz, ayrı silinmez)
            if (navigation.TargetEntityType.IsOwned())
                continue;

            // PropertyInfo yoksa bu navigation'a erişemeyiz, atla
            if (navigation.PropertyInfo == null)
                continue;

            // Navigation property'nin değerini al (bellekte yüklüyse direkt gelir)
            object? navValue = navigation.PropertyInfo.GetValue(entity);

            if (navigation.IsCollection)
            {
                // Bire-çok ilişki: Örnek Order.Items — bir siparişin birden fazla ürünü var

                if (navValue == null)
                {
                    // Bellekte yüklenmemişse veritabanından yükle
                    IQueryable query = Context.Entry(entity).Collection(navigation.PropertyInfo.Name).Query();
                    navValue = await GetRelationLoaderQuery(query, navigationPropertyType: navigation.PropertyInfo.GetType()).ToListAsync();
                    if (navValue == null)
                        continue;
                }

                // Koleksiyondaki her öğe için recursive soft delete uygula
                foreach (IEntityTimestamps navValueItem in (IEnumerable)navValue)
                    await setEntityAsSoftDeletedAsync(navValueItem);
            }
            else
            {
                // Bire-bir ilişki: Örnek User.Address — bir kullanıcının tek adresi var

                if (navValue == null)
                {
                    // Bellekte yüklenmemişse veritabanından yükle
                    IQueryable query = Context.Entry(entity).Reference(navigation.PropertyInfo.Name).Query();
                    navValue = await GetRelationLoaderQuery(query, navigationPropertyType: navigation.PropertyInfo.GetType())
                        .FirstOrDefaultAsync();
                    if (navValue == null)
                        continue;
                }

                // İlişkili tek nesne için recursive soft delete uygula
                await setEntityAsSoftDeletedAsync((IEntityTimestamps)navValue);
            }
        }

        Context.Update(entity); // DeletedDate değiştiği için EF Core'a "bu nesneyi güncelle" de
    }

    // İlişkili entity'leri veritabanından yüklemek için sorgu oluşturur.
    // Aynı zamanda zaten silinmiş olanları otomatik filtreler (DeletedDate doluysa alma).
    //
    // Neden Reflection (MethodInfo, MakeGenericMethod) kullanılıyor?
    //   Navigation property'nin tipi çalışma zamanında belli oluyor.
    //   Generic metod olan CreateQuery<T> çalışma zamanında tipiyle çağrılamaz.
    //   Reflection ile metodun generic versiyonunu dinamik olarak oluşturup çağırıyoruz.
    //   Bu, çok az rastlanan ama ilişki tipini bilmeden sorgu oluşturmak için gereken bir tekniktir.
    protected IQueryable<object> GetRelationLoaderQuery(IQueryable query, Type navigationPropertyType)
    {
        Type queryProviderType = query.Provider.GetType();

        // CreateQuery<T> metodunu bul ve navigationPropertyType ile generic hale getir
        MethodInfo createQueryMethod =
            queryProviderType
                .GetMethods()
                .First(m => m is { Name: nameof(query.Provider.CreateQuery), IsGenericMethod: true })
                ?.MakeGenericMethod(navigationPropertyType)
            ?? throw new InvalidOperationException("CreateQuery<TElement> method is not found in IQueryProvider.");

        // Metodun yaptığı sorguyu çalıştır
        var queryProviderQuery =
            (IQueryable<object>)createQueryMethod.Invoke(query.Provider, parameters: new object[] { query.Expression })!;

        // Zaten soft delete ile silinmiş olanları filtrele (DeletedDate doluysa alma)
        return queryProviderQuery.Where(x => !((IEntityTimestamps)x).DeletedDate.HasValue);
    }

    // Birden fazla entity için SetEntityAsDeletedAsync'i sırayla çağırır.
    protected async Task SetEntityAsDeletedAsync(IEnumerable<TEntity> entities, bool permanent)
    {
        foreach (TEntity entity in entities)
            await SetEntityAsDeletedAsync(entity, permanent);
    }

    // ==================== SENKRON METODLAR (HENÜz IMPLEMENT EDİLMEDİ) ====================
    // Aşağıdaki metodlar IRepository interface'inden gelen senkron versiyonlardır.
    // Şu an hepsi NotImplementedException fırlatıyor; yani kasıtlı olarak boş bırakıldı.
    //
    // Neden implement edilmedi?
    //   Bu proje web API odaklı ve asenkron metodlar yeterli.
    //   İhtiyaç duyulursa ileride doldurulacak şekilde iskelet olarak bırakıldı.
    //   Senkron metodlara ihtiyaç duyulmayacağı öngörülüyorsa bu yaygın bir yaklaşımdır.

    public TEntity? Get(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null, bool withDeleted = false, bool enableTracking = true)
    {
        throw new NotImplementedException();
    }

    public Paginate<TEntity> GetList(Expression<Func<TEntity, bool>>? predicate = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null, int index = 0, int size = 10, bool withDeleted = false, bool enableTracking = true)
    {
        throw new NotImplementedException();
    }

    public Paginate<TEntity> GetListByDynamic(DynamicQuery dynamic, Expression<Func<TEntity, bool>>? predicate = null, Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null, int index = 0, int size = 10, bool withDeleted = false, bool enableTracking = true)
    {
        throw new NotImplementedException();
    }

    public bool Any(Expression<Func<TEntity, bool>>? predicate = null, bool withDeleted = false, bool enableTracking = true)
    {
        throw new NotImplementedException();
    }

    public TEntity Add(TEntity entity)
    {
        throw new NotImplementedException();
    }

    public ICollection<TEntity> AddRange(ICollection<TEntity> entities)
    {
        throw new NotImplementedException();
    }

    public TEntity Update(TEntity entity)
    {
        throw new NotImplementedException();
    }

    public ICollection<TEntity> UpdateRange(ICollection<TEntity> entities)
    {
        throw new NotImplementedException();
    }

    public TEntity Delete(TEntity entity, bool permanent = false)
    {
        throw new NotImplementedException();
    }

    public ICollection<TEntity> DeleteRange(ICollection<TEntity> entity, bool permanent = false)
    {
        throw new NotImplementedException();
    }
}
