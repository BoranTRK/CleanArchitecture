using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core; // NuGet paketi: string ifadelerle dinamik LINQ sorgusu yazmayı sağlar.
                                 // Normalde LINQ'da "OrderBy(x => x.Name)" yazarız (derleme zamanında bilinir).
                                 // Bu paket sayesinde "OrderBy("Name asc")" gibi çalışma zamanında
                                 // string olarak gelen ifadeleri de kullanabiliyoruz.
using System.Text;
using System.Threading.Tasks;

namespace Core.Persistence.Dynamic;

// BU SINIF NE İŞE YARAR?
// IQueryable<T> tipine yeni metodlar ekleyen bir "Extension Method" sınıfıdır.
//
// Extension Method nedir?
//   Var olan bir sınıfı değiştirmeden ona yeni metod eklemek demektir.
//   Bu sınıf sayesinde herhangi bir IQueryable<T> sorgusuna direkt
//   .ToDynamic(dynamicQuery) diyebiliyoruz; sanki IQueryable'ın kendi metoduymuş gibi.
//
// Neden static olmak zorunda?
//   C#'ta Extension Method yazmak için sınıfın static olması zorunludur.
//   Bu bir dil kuralıdır.
//
// Büyük resimde ne yapıyor?
//   Veritabanına gidecek olan ham sorguyu (IQueryable) alıyor,
//   üzerine dinamik WHERE (filtre) ve ORDER BY (sıralama) ekliyor,
//   sonucu geri döndürüyor. Veritabanı sorgusu henüz çalıştırılmamış olur;
//   sadece üzerine koşullar eklendi. Asıl sorgu .ToList() veya .FirstOrDefault()
//   gibi bir metod çağrıldığında veritabanına gider.
public static class IQueryableDynamicFilterExtensions
{
    // Geçerli sıralama yönleri. Bunların dışında bir değer gelirse hata fırlatılır.
    // "asc" = artan (ascending), "desc" = azalan (descending)
    private static readonly string[] _orders = { "asc", "desc" };

    // Geçerli mantıksal bağlaçlar.
    // "and" = her iki koşul da sağlanmalı, "or" = koşullardan biri yeterli
    private static readonly string[] _logics = { "and", "or" };

    // API'dan gelen kısa operatör kodlarını Dynamic LINQ'un anlayacağı ifadelere çeviren sözlük.
    //
    // Neden böyle bir sözlük var?
    //   API'dan "gt", "lte" gibi kısa ve tarafsız kodlar gelir.
    //   Bu kodlar veritabanından bağımsızdır; yarın SQL yerine başka bir veritabanına
    //   geçsek bile API tarafı değişmez, sadece bu sözlük güncellenir.
    //   Ayrıca kullanıcıya "<", ">=" gibi özel karakterler yerine okunabilir kodlar sunulur.
    //
    // Sol taraf: API'dan gelen kod | Sağ taraf: Dynamic LINQ ifadesi
    private static readonly IDictionary<string, string> _operators = new Dictionary<string, string>
    {
        { "eq",             "=" },          // Eşit:                Age == 18
        { "neq",            "!=" },         // Eşit değil:          Age != 18
        { "lt",             "<" },          // Küçük:               Age < 18
        { "lte",            "<=" },         // Küçük veya eşit:     Age <= 18
        { "gt",             ">" },          // Büyük:               Age > 18
        { "gte",            ">=" },         // Büyük veya eşit:     Age >= 18
        { "isnull",         "== null" },    // Null mı:             Email == null
        { "isnotnull",      "!= null" },    // Null değil mi:       Email != null
        { "startswith",     "StartsWith" }, // Şununla başlar mı:   Name.StartsWith("A")
        { "endswith",       "EndsWith" },   // Şununla biter mi:    Name.EndsWith("i")
        { "contains",       "Contains" },   // İçeriyor mu:         Name.Contains("li")
        { "doesnotcontain", "Contains" }    // İçermiyor mu:        !Name.Contains("li")
                                            // (doesnotcontain da "Contains" kullanır ama
                                            //  Transform() metodunda başına ! eklenerek tersine çevrilir)
    };

    // ANA GİRİŞ NOKTASI — Bu sınıftaki tek public metod budur (diğerleri private yardımcılar).
    //
    // "this IQueryable<T> query" ne anlama geliyor?
    //   Extension Method'un işareti budur. "this" anahtar kelimesi sayesinde bu metod,
    //   IQueryable<T> tipinin kendi metoduymuş gibi çağrılabilir:
    //   Örnek: dbContext.Users.AsQueryable().ToDynamic(dynamicQuery)
    //
    // Ne yapar?
    //   Gelen sorguya, DynamicQuery içindeki bilgilere göre filtre ve sıralama ekler.
    //   Filtre yoksa filtreleme adımını atlar, sıralama yoksa sıralama adımını atlar.
    //
    // Örnek:
    //   var query = dbContext.Users.AsQueryable();
    //   var result = query.ToDynamic(dynamicQuery);
    //   // result artık WHERE ve ORDER BY içeren ama henüz çalıştırılmamış bir sorgudur.
    //   var users = result.ToList(); // Ancak burada veritabanına gider.
    public static IQueryable<T> ToDynamic<T>(this IQueryable<T> query, DynamicQuery dynamicQuery)
    {
        // Filter nesnesi geldiyse sorguya WHERE koşulu ekle
        if (dynamicQuery.Filter is not null)
            query = Filter(query, dynamicQuery.Filter);

        // Sort listesi geldiyse ve en az bir eleman varsa sorguya ORDER BY ekle
        if (dynamicQuery.Sort is not null && dynamicQuery.Sort.Any())
            query = Sort(query, dynamicQuery.Sort);

        // Filtresi ve/veya sıralaması eklenmiş sorguyu geri döndür
        return query;
    }

    // WHERE koşulunu sorguya ekleyen özel (private) yardımcı metod.
    // Dışarıdan direkt çağrılamaz; sadece ToDynamic() tarafından kullanılır.
    //
    // Ne yapar?
    //   1. İç içe geçmiş tüm filtreleri düz bir listeye çıkarır
    //   2. Bu filtrelerin değerlerini bir diziye alır (parametre olarak kullanmak için)
    //   3. Filtreyi "np(Age) > @0 and np(City).Contains(@1)" gibi bir stringe çevirir
    //   4. Bu stringi Dynamic LINQ ile sorguya WHERE olarak ekler
    private static IQueryable<T> Filter<T>(IQueryable<T> queryable, Filter filter)
    {
        // İç içe geçmiş tüm filtreleri düz bir listeye çıkar.
        // Örneğin ana filtre + 2 alt filtre varsa, sonuç 3 elemanlı bir liste olur.
        // Bu listeye indeks numarasıyla erişeceğiz: filters[0], filters[1]...
        IList<Filter> filters = GetAllFilters(filter);

        // Her filtrenin Value'sunu bir diziye al.
        // Dynamic LINQ'da @0, @1, @2... şeklinde parametre olarak kullanılacak.
        // Örnek: values = ["18", "Ankara"] → @0="18", @1="Ankara"
        //
        // Neden direkt string yazmıyoruz da parametre kullanıyoruz?
        //   SQL Injection saldırılarına karşı koruma sağlar.
        //   "np(Age) > 18" yazmak yerine "np(Age) > @0" yazıp değeri ayrı geçiriyoruz.
        string?[] values = filters.Select(f => f.Value).ToArray();

        // Filter nesnesini okunabilir bir WHERE string ifadesine çevir.
        // Örnek çıktı: "np(Age) > @0 and (np(City).Contains(@1))"
        string where = Transform(filter, filters);

        // WHERE ifadesi doluysa sorguya ekle.
        // Dynamic LINQ'un .Where() metodu string ifade + parametre dizisi alır.
        if (!string.IsNullOrEmpty(where) && values != null)
            queryable = queryable.Where(where, values);

        return queryable;
    }

    // ORDER BY ekleyen özel (private) yardımcı metod.
    // Dışarıdan direkt çağrılamaz; sadece ToDynamic() tarafından kullanılır.
    //
    // Ne yapar?
    //   1. Her Sort nesnesinin geçerli olup olmadığını kontrol eder
    //   2. Geçerliyse "FirstName asc, CreatedDate desc" gibi bir string oluşturur
    //   3. Bu stringi Dynamic LINQ ile sorguya ORDER BY olarak ekler
    private static IQueryable<T> Sort<T>(IQueryable<T> queryable, IEnumerable<Sort> sort)
    {
        foreach (Sort item in sort)
        {
            // Field boşsa sıralama yapılamaz, hata fırlat.
            if (string.IsNullOrEmpty(item.Field))
                throw new ArgumentException("Invalid Field");

            // Dir "asc" veya "desc" değilse geçersiz, hata fırlat.
            // _orders dizisinde olmayan bir değer gelirse burası yakalar.
            if (string.IsNullOrEmpty(item.Dir) || !_orders.Contains(item.Dir))
                throw new ArgumentException("Invalid Order Type");
        }

        if (sort.Any())
        {
            // Tüm Sort nesnelerini "Field Dir" formatında birleştir.
            // Örnek: [ Sort("FirstName","asc"), Sort("Age","desc") ]
            //   → "FirstName asc,Age desc"
            string ordering = string.Join(separator: ",", values: sort.Select(s => $"{s.Field} {s.Dir}"));

            // Dynamic LINQ ile ORDER BY uygula.
            // Normalde .OrderBy(x => x.FirstName) yazarız ama burada alan adı
            // çalışma zamanında string olarak geldiği için Dynamic LINQ kullanıyoruz.
            return queryable.OrderBy(ordering);
        }

        return queryable;
    }

    // İç içe geçmiş tüm filtreleri düz bir listeye çıkaran public yardımcı metod.
    //
    // Neden buna ihtiyaç var?
    //   Filter sınıfı kendi içinde alt Filter'lar barındırabilir (recursive yapı).
    //   Dynamic LINQ'da @0, @1, @2 şeklinde sıralı parametre gerekiyor.
    //   Bu parametreleri doğru atayabilmek için tüm filtrelerin düz bir listede
    //   sıralı şekilde durması gerekiyor.
    //
    // Örnek:
    //   Ana filtre (Age > 18)
    //     └─ Alt filtre 1 (City = "Ankara")
    //          └─ Alt filtre 1.1 (District = "Çankaya")
    //   → Sonuç: [AnaFiltre, AltFiltre1, AltFiltre1.1]
    //   → @0=Ana, @1=AltFiltre1, @2=AltFiltre1.1
    public static IList<Filter> GetAllFilters(Filter filter)
    {
        List<Filter> filters = new();
        GetFilters(filter, filters); // Özyinelemeli metodu başlat
        return filters;
    }

    // Filtreleri düz listeye ekleyen özel recursive (özyinelemeli) metod.
    //
    // Recursive nedir?
    //   Bir metodun kendisini çağırmasıdır. Burada her filtre için:
    //     1. Kendini listeye ekle
    //     2. Alt filtreleri varsa her biri için aynı metodu tekrar çağır
    //   Bu sayede ne kadar derin iç içe filtre olursa olsun hepsi bulunur.
    private static void GetFilters(Filter filter, IList<Filter> filters)
    {
        filters.Add(filter); // Önce bu filtreyi listeye ekle

        // Alt filtreleri varsa her biri için bu metodu tekrar çağır
        if (filter.Filters is not null && filter.Filters.Any())
            foreach (Filter item in filter.Filters)
                GetFilters(item, filters); // Recursive çağrı: metodun kendini çağırması
    }

    // Bir Filter nesnesini Dynamic LINQ'un anlayacağı WHERE string ifadesine çeviren metod.
    //
    // np() nedir?
    //   "Null Propagation" — bir alana erişirken o alanın null olması durumunda
    //   NullReferenceException hatası vermek yerine null döndürür.
    //   np(Age) → Age null ise null döner, değilse Age'in değerini döner.
    //   Bu sayede null olan kayıtlar için uygulama çökmez.
    //
    // @index nedir?
    //   Dynamic LINQ'daki parametre sistemi. SQL'deki "?" gibi.
    //   @0 → values[0], @1 → values[1] ...
    //   Değerler doğrudan string'e gömülmeyip parametre olarak geçirilir (SQL Injection önlemi).
    //
    // Örnek çıktılar:
    //   Field="Age",  Operator="gt",       Value="18"      → "np(Age) > @0"
    //   Field="Name", Operator="contains", Value="Ali"     → "(np(Name).Contains(@0))"
    //   Field="Name", Operator="startswith",Value="A"      → "(np(Name).StartsWith(@0))"
    //   Field="Email",Operator="isnull",   Value=null      → "np(Email) == null"
    public static string Transform(Filter filter, IList<Filter> filters)
    {
        // Alan adı boşsa filtre oluşturulamaz
        if (string.IsNullOrEmpty(filter.Field))
            throw new ArgumentException("Invalid Field");

        // Operatör sözlükte yoksa geçersizdir
        if (string.IsNullOrEmpty(filter.Operator) || !_operators.ContainsKey(filter.Operator))
            throw new ArgumentException("Invalid Operator");

        // Bu filtrenin düz listedeki konumu → parametre numarası olarak kullanılacak (@0, @1...)
        int index = filters.IndexOf(filter);

        // Operatörün LINQ karşılığını al. Örnek: "gt" → ">"
        string comparison = _operators[filter.Operator];

        StringBuilder where = new();

        if (!string.IsNullOrEmpty(filter.Value))
        {
            if (filter.Operator == "doesnotcontain")
                // "İçermiyor mu?" → Contains kullan ama başına ! koy (tersine çevir)
                // Çıktı: (!np(Name).Contains(@0))
                where.Append($"(!np({filter.Field}).{comparison}(@{index.ToString()}))");

            else if (comparison is "StartsWith" or "EndsWith" or "Contains")
                // String metodları metod çağrısı formatında yazılır
                // Çıktı: (np(Name).Contains(@0))
                where.Append($"(np({filter.Field}).{comparison}(@{index.ToString()}))");

            else
                // Sayısal ve eşitlik operatörleri standart format
                // Çıktı: np(Age) > @0
                where.Append($"np({filter.Field}) {comparison} @{index.ToString()}");
        }
        else if (filter.Operator is "isnull" or "isnotnull")
        {
            // Değer gerektirmeyen operatörler: sadece alan adı ve null kontrolü
            // Çıktı: np(Email) == null   veya   np(Email) != null
            where.Append($"np({filter.Field}) {comparison}");
        }

        // Alt filtreler varsa hepsini birleştir.
        //
        // Örnek senaryo:
        //   Ana filtre: Age > 18, Logic = "and"
        //   Alt filtre 1: City = "Ankara"
        //   Alt filtre 2: City = "İstanbul", Logic = "or"
        //
        //   Transform sonucu: "np(Age) > @0 and (np(City) = @1 or np(City) = @2)"
        //
        // Alt filtreler için Transform metodu recursive olarak tekrar çağrılır.
        if (filter.Logic is not null && filter.Filters is not null && filter.Filters.Any())
        {
            if (!_logics.Contains(filter.Logic))
                throw new ArgumentException("Invalid Logic");

            // Her alt filtreyi de Transform'dan geçir, aralarına logic koy, paranteze al
            return $"{where} {filter.Logic} ({string.Join(separator: $" {filter.Logic} ", value: filter.Filters.Select(f => Transform(f, filters)).ToArray())})";
        }

        // Alt filtre yoksa sadece bu filtrenin WHERE ifadesini döndür
        return where.ToString();
    }
}
