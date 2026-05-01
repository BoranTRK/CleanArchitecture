using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Persistence.Paging;

// PAGINATE NEDİR VE NEDEN VAR?
// Sayfalama (pagination) sonucunu taşıyan generic bir sınıftır.
//
// Neden sayfalamaya ihtiyacımız var?
//   Veritabanında 100.000 kullanıcı olduğunu düşün.
//   Hepsini tek seferde çekip kullanıcıya göndermek hem çok yavaş olur
//   hem de gereksiz yere sunucu belleğini doldurur.
//   Bunun yerine "sayfa 1'deki 20 kullanıcıyı getir", "sayfa 2'dekini getir"
//   şeklinde parça parça getiririz. Paginate<T> bu parçanın sonucunu taşır.
//
// <T> (Generic) nedir?
//   Bu sınıfın herhangi bir veri tipiyle çalışabilmesini sağlar.
//   Paginate<User>    → kullanıcıları sayfalı getir
//   Paginate<Product> → ürünleri sayfalı getir
//   <T> yerine her tip için ayrı sınıf yazmak zorunda kalmayız.
//
// Örnek senaryo:
//   100 kullanıcı var, sayfada 10 gösterilecek, 3. sayfadasın:
//   → Size  = 10   (sayfada kaç kayıt var)
//   → Index = 2    (3. sayfa, ama 0'dan sayılıyor: 0=1.sayfa, 1=2.sayfa, 2=3.sayfa)
//   → Count = 100  (toplam kayıt)
//   → Pages = 10   (toplam sayfa: 100/10 = 10)
//   → Items = [21. kullanıcıdan 30. kullanıcıya kadar olan liste]
public class Paginate<T>
{
    // Constructor — Items alanını boş bir dizi ile başlatır.
    //
    // Neden Array.Empty<T>() kullanıyoruz?
    //   Items bir IList<T> olduğu için başlangıçta null olur.
    //   Kod ileride Items.Count veya Items.Any() derse NullReferenceException alırız.
    //   Array.Empty<T>() ile boş ama null olmayan bir liste atıyoruz.
    //   Bu sayede hiç kayıt gelmese bile Items üzerinde güvenle işlem yapabiliriz.
    //
    //   Not: Array.Empty<T>(), her seferinde yeni bir dizi oluşturmaz;
    //   bellekte tek bir boş dizi tutar ve hep onu döndürür (performans optimizasyonu).
    public Paginate()
    {
        Items = Array.Empty<T>();
    }

    // Sayfada kaç kayıt gösterileceği.
    // Örnek: Size = 10 → her sayfada 10 kayıt göster
    public int Size { get; set; }

    // Hangi sayfada olunduğu — 0'dan başlar!
    // Örnek: Index = 0 → 1. sayfa, Index = 1 → 2. sayfa, Index = 2 → 3. sayfa
    // 0'dan başlamasının sebebi: Skip(index * size) hesabında kolaylık sağlar.
    //   Index=0 → Skip(0*10)=Skip(0)  → 1. sayfanın kayıtları
    //   Index=1 → Skip(1*10)=Skip(10) → 2. sayfanın kayıtları
    //   Index=2 → Skip(2*10)=Skip(20) → 3. sayfanın kayıtları
    public int Index { get; set; }

    // Veritabanındaki toplam kayıt sayısı (tüm sayfalar dahil).
    // Örnek: Count = 100 → veritabanında toplamda 100 kullanıcı var
    // UI'da "Toplam 100 sonuç" gibi bir bilgi göstermek için kullanılır.
    public int Count { get; set; }

    // Toplam sayfa sayısı.
    // Örnek: Count=100, Size=10 → Pages=10 (10 sayfa var)
    // ToPaginate metodu içinde Math.Ceiling ile hesaplanır.
    // Örnek: Count=101, Size=10 → 101/10 = 10.1 → Math.Ceiling(10.1) = 11 sayfa
    //   (Son sayfada sadece 1 kayıt olsa bile tam bir sayfa sayılır)
    public int Pages { get; set; }

    // O sayfanın kayıtları — asıl veri burada.
    // Örnek: 3. sayfadaki 10 kullanıcının listesi
    public IList<T> Items { get; set; }

    // Önceki sayfa var mı? — Hesaplanan (computed) bir property, veri taşımaz.
    // Index > 0 ise önceki sayfa vardır.
    // Örnek: Index=0 (1. sayfa) → HasPrevious=false (öncesi yok)
    //        Index=1 (2. sayfa) → HasPrevious=true  (öncesi var)
    // UI'da "Önceki" butonunu göster/gizle kararı için kullanılır.
    public bool HasPrevious => Index > 0;

    // Sonraki sayfa var mı? — Hesaplanan (computed) bir property, veri taşımaz.
    // "Şu anki sayfa numarası (Index+1), toplam sayfa sayısından küçükse sonraki sayfa vardır."
    //
    // Neden Index + 1 < Pages?
    //   Index 0'dan başlıyor ama Pages gerçek sayfa sayısı.
    //   Örnek: 10 sayfa var (Pages=10), şu an 10. sayfadasın (Index=9)
    //   → Index+1 = 10, Pages = 10 → 10 < 10 = false → Sonraki sayfa yok ✓
    //
    //   Örnek: 10 sayfa var (Pages=10), şu an 9. sayfadasın (Index=8)
    //   → Index+1 = 9, Pages = 10 → 9 < 10 = true → Sonraki sayfa var ✓
    //
    // UI'da "Sonraki" butonunu göster/gizle kararı için kullanılır.
    public bool HasIndex => Index + 1 < Pages;
}
