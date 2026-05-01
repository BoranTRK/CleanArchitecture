using Microsoft.EntityFrameworkCore; // CountAsync() ve ToListAsync() bu paketten gelir (EF Core)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Persistence.Paging;

// BU SINIF NE İŞE YARAR?
// IQueryable<T>'ye sayfalama (pagination) metodları ekleyen Extension Method sınıfıdır.
//
// Dynamic klasöründeki IQueryableDynamicFilterExtensions ile aynı mantıkta çalışır:
// var olan bir tipe dokunmadan ona yeni metodlar ekliyoruz.
// Bu sayede herhangi bir sorguya direkt .ToPaginateAsync() veya .ToPaginate() diyebiliriz.
//
// İki metod var, farkları ne?
//   ToPaginateAsync → Asenkron çalışır. Veritabanı işlemi biterken thread beklemez,
//                     başka işler yapabilir. Web uygulamalarında tercih edilen yöntemdir.
//   ToPaginate      → Senkron çalışır. Veritabanı işlemi bitene kadar thread bekler (bloklar).
//                     Basit veya test senaryolarında kullanılır.
public static class IQueryablePaginateExtensions
{
    // ASENKRONsayfalama metodu — web uygulamalarında kullanılan asıl metod budur.
    //
    // "this IQueryable<T> source" → Extension Method işareti.
    //   source: veritabanı sorgusu (henüz çalıştırılmamış, sadece tarif edilmiş hali)
    //   Örnek: dbContext.Users.Where(u => u.IsActive) gibi bir sorgu
    //
    // index → Hangi sayfa isteniyor? (0'dan başlar: 0=1.sayfa, 1=2.sayfa...)
    // size  → Sayfada kaç kayıt olacak? (Örnek: 10, 20, 50)
    //
    // CancellationToken nedir?
    //   İstek iptal edildiğinde (kullanıcı sayfayı kapattı, timeout oldu vb.)
    //   devam eden veritabanı işlemini durdurmamızı sağlar.
    //   default yazılması: çağıran kod token vermezse işlem normal devam eder, iptal edilmez.
    //
    // Task<Paginate<T>> dönüş tipi:
    //   Task → bu metodun asenkron (await ile beklenebilir) olduğunu gösterir
    //   Paginate<T> → metodun sonunda döneceği asıl nesne
    public static async Task<Paginate<T>> ToPaginateAsync<T>(
        this IQueryable<T> source,
        int index,
        int size,
        CancellationToken cancellationToken = default
        )
    {
        // Veritabanındaki TOPLAM kayıt sayısını asenkron olarak al.
        // Bu, kullanıcıya "Toplam X sonuç bulundu" bilgisi ve toplam sayfa hesabı için gerekli.
        //
        // .ConfigureAwait(false) nedir?
        //   Asenkron işlem bitince kodu başlatan thread'e (UI thread gibi) dönmeye gerek olmadığını söyler.
        //   Bu sayede gereksiz thread geçişi olmaz, performans artar.
        //   Web API'larda genellikle hangi thread'de devam ettiğimiz önemli olmadığından hep false kullanılır.
        int count = await source.CountAsync(cancellationToken).ConfigureAwait(false);

        // O sayfanın kayıtlarını asenkron olarak al.
        //
        // Skip(index * size) → Kaç kayıt atlanacak?
        //   Index=0, Size=10 → Skip(0)  → Hiç atlama, 1. sayfanın kayıtları
        //   Index=1, Size=10 → Skip(10) → İlk 10'u atla, 2. sayfanın kayıtları
        //   Index=2, Size=10 → Skip(20) → İlk 20'yi atla, 3. sayfanın kayıtları
        //
        // Take(size) → Kaç kayıt alınacak?
        //   Size=10 → Sıradaki 10 kaydı al
        //
        // Skip ve Take veritabanında çalışır (SQL'e OFFSET ve FETCH/LIMIT olarak çevrilir).
        // Yani 100.000 kayıt C#'a çekilmez; sadece istenen 10 kayıt gelir.
        List<T> items = await source.Skip(index * size).Take(size).ToListAsync(cancellationToken).ConfigureAwait(false);

        // Sonuçları Paginate<T> nesnesine doldur ve döndür.
        Paginate<T> list = new()
        {
            Index = index,                                             // Hangi sayfadasın
            Count = count,                                             // Toplam kayıt sayısı
            Items = items,                                             // Bu sayfanın kayıtları
            Size = size,                                               // Sayfada kaç kayıt var

            // Toplam sayfa sayısını hesapla.
            // Neden (double)size? Neden Math.Ceiling?
            //   count=101, size=10 durumunu düşün:
            //   101 / 10 = 10  (int bölme, küsurat atılır) → YanlIŞ! 11 sayfa olmalı.
            //   101 / (double)10 = 10.1 (double bölme, küsurat korunur)
            //   Math.Ceiling(10.1) = 11  → DOĞRU! Yarım sayfa bile tam sayfa sayılır.
            Pages = (int)Math.Ceiling(count / (double)size)
        };

        return list;
    }

    // SENKRON sayfalama metodu — ToPaginateAsync'in beklemesiz (blocking) versiyonu.
    //
    // ToPaginateAsync'ten farkları:
    //   - async/await yok: veritabanı sorgusu bitene kadar thread bloklanır (bekler)
    //   - ConfigureAwait yok: zaten asenkron değil
    //   - CancellationToken yok: iptal desteği yok
    //   - CountAsync/ToListAsync yerine Count/ToList kullanılır (senkron versiyonları)
    //
    // Ne zaman kullanılır?
    //   - Unit test yazarken (asenkron test kurulumu gerekmediğinde)
    //   - Konsol uygulamaları gibi asenkron altyapının olmadığı yerlerde
    //   - Küçük veri setlerinde hızlı prototip yaparken
    //
    // Web API'da ToPaginateAsync yerine bu tercih edilmez çünkü:
    //   Thread bloklandığında o thread başka isteğe cevap veremez.
    //   Çok kullanıcılı sistemlerde bu performans sorununa yol açar.
    public static Paginate<T> ToPaginate<T>(this IQueryable<T> source, int index, int size)
    {
        // Toplam kayıt sayısını SENKRON olarak al (thread burada bekler)
        int count = source.Count();

        // O sayfanın kayıtlarını SENKRON olarak al (thread burada da bekler)
        // Skip ve Take mantığı ToPaginateAsync ile aynıdır.
        var items = source.Skip(index * size).Take(size).ToList();

        // Sonuçları Paginate<T> nesnesine doldur ve döndür.
        // Pages hesabı ToPaginateAsync ile aynı mantıkta (Math.Ceiling ile yukarı yuvarla).
        Paginate<T> list = new()
        {
            Index = index,
            Size = size,
            Count = count,
            Items = items,
            Pages = (int)Math.Ceiling(count / (double)size)
        };

        return list;
    }
}
