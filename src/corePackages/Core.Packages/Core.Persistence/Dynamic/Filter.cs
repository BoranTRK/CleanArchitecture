using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Persistence.Dynamic;

// FILTER NEDİR VE NEDEN VAR?
// Kullanıcı "sadece yaşı 18'den büyük olanları getir" veya "adı 'Ali' ile başlayanları getir"
// gibi bir filtre uygulamak istediğinde, bu isteği taşıyacak bir nesneye ihtiyacımız var.
// Filter sınıfı tam olarak bunu yapar: bir filtreleme koşulunu temsil eder.
//
// Basit senaryo — tek filtre:
//   "Yaşı 18'den büyük kullanıcıları getir"
//   → Field = "Age", Operator = "gt", Value = "18"
//
// Gelişmiş senaryo — iç içe filtreler:
//   "Yaşı 18'den büyük VE şehri Ankara VEYA İstanbul olan kullanıcıları getir"
//   → Ana filtre: Field="Age", Operator="gt", Value="18", Logic="and"
//   → Alt filtreler:
//       Filter 1: Field="City", Operator="eq", Value="Ankara", Logic="or"
//       Filter 2: Field="City", Operator="eq", Value="İstanbul"
//   Bu şekilde filtreler birbirine zincirlenerek karmaşık sorgular oluşturulabilir.
public class Filter
{
    // Filtrenin uygulanacağı kolon adı.
    // Örnek: "Age", "City", "FirstName", "CreatedDate"
    // Veritabanındaki kolon adıyla eşleşmelidir.
    public string Field { get; set; }

    // Filtrede karşılaştırılacak değer.
    // Örnek: "18", "Ankara", "Ali"
    //
    // Neden null olabilir (?)?
    //   "isnull" ve "isnotnull" gibi operatörlerde karşılaştırılacak bir değer yoktur.
    //   "Null olan kayıtları getir" derken herhangi bir değere ihtiyaç duymayız.
    //   Bu yüzden Value null olabilir şeklinde işaretlendi.
    public string? Value { get; set; }

    // Karşılaştırma operatörü. API'dan kısa kodlar olarak gelir.
    // Desteklenen operatörler (IQueryableDynamicFilterExtensions._operators sözlüğünde tanımlı):
    //   "eq"            → Eşit               (Age == 18)
    //   "neq"           → Eşit değil         (Age != 18)
    //   "lt"            → Küçük              (Age < 18)
    //   "lte"           → Küçük veya eşit    (Age <= 18)
    //   "gt"            → Büyük              (Age > 18)
    //   "gte"           → Büyük veya eşit    (Age >= 18)
    //   "isnull"        → Null mı?           (Email == null)
    //   "isnotnull"     → Null değil mi?     (Email != null)
    //   "startswith"    → Şununla başlar mı? (Name.StartsWith("A"))
    //   "endswith"      → Şununla biter mi?  (Name.EndsWith("i"))
    //   "contains"      → İçeriyor mu?       (Name.Contains("li"))
    //   "doesnotcontain"→ İçermiyor mu?      (!Name.Contains("li"))
    public string Operator { get; set; }

    // Birden fazla filtre varsa aralarındaki mantıksal bağlaç.
    //   "and" → İki koşul da sağlanmalı  (Yaş > 18 AND Şehir = "Ankara")
    //   "or"  → Koşullardan biri sağlanmalı (Şehir = "Ankara" OR Şehir = "İstanbul")
    //
    // Neden null olabilir (?)?
    //   Sadece tek bir filtre kullanıldığında Logic'e gerek yoktur.
    //   Alt filtre yoksa bu alan null bırakılır.
    public string? Logic { get; set; }

    // Alt filtreler — filtreleri iç içe zincirlemeyi sağlar.
    // Bu sayede "(Yaş > 18) AND (Şehir = 'Ankara' OR Şehir = 'İstanbul')"
    // gibi karmaşık koşullar tek bir Filter nesnesiyle ifade edilebilir.
    //
    // Yani Filter sınıfı kendi içinde yine Filter listesi taşıyor.
    // Buna yazılımda "recursive (özyinelemeli) yapı" denir.
    public IEnumerable<Filter>? Filters { get; set; }

    // Parametresiz constructor — boş bir Filter nesnesi oluşturur.
    // Field ve Operator string.Empty ile başlatılır.
    //
    // Neden sadece bu ikisi başlatılıyor, Value ve Logic değil?
    //   Field ve Operator her zaman zorunludur, bunlar olmadan filtre anlamsızdır.
    //   Value ve Logic ise duruma göre null olabilir (nullable olarak işaretlendi zaten).
    //   Bu yüzden onları başlatmaya gerek yok, null kalabilirler.
    public Filter()
    {
        Field = string.Empty;
        Operator = string.Empty;
    }

    // Parametreli constructor — temel bir filtre oluşturmak için Field ve Operator alır.
    //
    // Neden Value burada yok?
    //   Value her zaman gerekli değildir ("isnull", "isnotnull" operatörlerinde kullanılmaz).
    //   İsteğe bağlı olduğu için constructor'a eklenmedi; sonradan atanabilir.
    //
    // @ işareti neden var? (@operator)
    //   "operator" kelimesi C#'ta özel (rezerve) bir kelimedir.
    //   Değişken adı olarak kullanmak istiyorsak başına @ koymamız gerekiyor.
    //   Sadece bu dosyadaki parametre adı için geçerli, dışarıdan çağırırken fark etmez.
    //
    // Kullanım örneği:
    //   var f = new Filter("Age", "gt");
    //   f.Value = "18";
    public Filter(string field, string @operator)
    {
        Field = field;
        Operator = @operator;
    }
}
