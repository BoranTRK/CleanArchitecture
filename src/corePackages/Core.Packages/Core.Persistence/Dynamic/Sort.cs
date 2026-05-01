using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Persistence.Dynamic;

// SORT NEDİR VE NEDEN VAR?
// Kullanıcı bir tabloda verileri sıralamak istediğinde (örneğin "ada göre A-Z sırala"),
// bu isteği taşıyacak bir nesneye ihtiyacımız var. İşte Sort sınıfı bu isteği taşır.
//
// Örnek senaryo: Kullanıcı "kullanıcıları ada göre artan sırala" dedi
//   → Field = "FirstName", Dir = "asc" olan bir Sort nesnesi oluşturulur
//   → Bu nesne DynamicQuery'ye verilir
//   → DynamicQuery veritabanı sorgusuna ORDER BY FirstName ASC ekler
public class Sort
{
    // Hangi kolona göre sıralama yapılacak?
    // Örnek değerler: "FirstName", "Age", "CreatedDate"
    // Veritabanındaki kolon adıyla birebir eşleşmesi gerekiyor.
    public string Field { get; set; }

    // Sıralama yönü. Sadece iki değer kabul edilir:
    //   "asc"  → Küçükten büyüğe (A→Z veya 1→100)
    //   "desc" → Büyükten küçüğe (Z→A veya 100→1)
    // Başka bir değer gelirse IQueryableDynamicFilterExtensions içinde hata fırlatılır,
    // yani geçersiz bir yön veritabanına kadar ulaşamaz.
    public string Dir { get; set; }

    // Parametresiz constructor — boş bir Sort nesnesi oluşturur.
    //
    // Neden string.Empty kullanıyoruz, null bıraksak olmaz mıydı?
    //   C#'ta string tipi varsayılan olarak null olur.
    //   Eğer null bırakırsak, kod ileride Field.Length veya Field.ToUpper() gibi
    //   bir işlem yapmaya çalıştığında NullReferenceException hatası alırız ve uygulama çöker.
    //   string.Empty atayarak alanın en azından boş bir string olduğunu garanti ediyoruz.
    //
    // Bu constructor genellikle şu şekilde kullanılır:
    //   var s = new Sort();
    //   s.Field = "Age";
    //   s.Dir = "desc";
    public Sort()
    {
        Field = string.Empty;
        Dir = string.Empty;
    }

    // Parametreli constructor — tek satırda hazır bir Sort nesnesi oluşturmak için kullanılır.
    // Parametresiz constructor'a göre çok daha pratik ve okunaklıdır.
    //
    // Kullanım örneği:
    //   var s = new Sort("FirstName", "asc");
    public Sort(string field, string dir)
    {
        Field = field;
        Dir = dir;
    }
}
