using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Persistence.Dynamic;

// DYNAMICQUERY NEDİR VE NEDEN VAR?
// Sort ve Filter sınıflarını bir arada tutan "taşıyıcı" sınıftır.
//
// Neden ayrı bir sınıfa ihtiyaç var, Sort ve Filter'ı direkt kullansak olmaz mıydı?
//   Çünkü bir sorgu hem filtreleme hem de sıralama içerebilir.
//   Bunları tek bir nesnede toplamak, API'dan gelen veriyi tek seferde almayı
//   ve ToDynamic() metoduna tek parametre olarak geçirmeyi kolaylaştırır.
//
// Büyük resim — bu sınıf nasıl kullanılır?
//   1. Kullanıcı UI'da filtre ve sıralama seçer
//   2. Frontend bu bilgileri DynamicQuery nesnesi olarak API'a gönderir
//   3. API bu nesneyi alır ve ToDynamic() metoduna geçirir
//   4. ToDynamic() veritabanı sorgusuna WHERE ve ORDER BY ekler
//   5. Filtrelenmiş ve sıralanmış sonuçlar kullanıcıya döner
//
// Örnek senaryo:
//   "18 yaşından büyük kullanıcıları ada göre A-Z sırala"
//   → Sort: [ new Sort("FirstName", "asc") ]
//   → Filter: new Filter { Field="Age", Operator="gt", Value="18" }
//   → new DynamicQuery(sort, filter) ile bu ikisi birleştirilir
public class DynamicQuery
{
    // Sıralama bilgileri.
    // IEnumerable kullanılmasının sebebi: birden fazla alana göre aynı anda sıralanabilir.
    // Örnek: Önce şehre göre A-Z, aynı şehirdekiler arasında ada göre A-Z
    //   → [ new Sort("City", "asc"), new Sort("FirstName", "asc") ]
    //
    // Neden null olabilir (?)?
    //   Kullanıcı sıralama istemeyebilir. Sadece filtreleme yapıp sıralamayı
    //   veritabanının varsayılan davranışına bırakabilir. O durumda Sort null gelir
    //   ve ToDynamic() içinde null kontrolü yapılarak atlanır.
    public IEnumerable<Sort>? Sort { get; set; }

    // Filtreleme bilgisi.
    // Tek bir Filter nesnesi taşır; ama Filter kendi içinde alt filtreler (Filters) barındırabilir.
    // Yani tek bir Filter nesnesi aslında çok katmanlı karmaşık bir koşul ifade edebilir.
    //
    // Neden null olabilir (?)?
    //   Kullanıcı filtre uygulamak istemeyebilir. Sadece sıralama isteyip
    //   tüm kayıtları görmek isteyebilir. O durumda Filter null gelir ve atlanır.
    public Filter? Filter { get; set; }

    // Parametresiz constructor — boş bir DynamicQuery nesnesi oluşturur.
    // Sort ve Filter daha sonra ayrı ayrı atanacaksa bu kullanılır.
    // Örnek:
    //   var dq = new DynamicQuery();
    //   dq.Sort = sortList;
    //   dq.Filter = myFilter;
    public DynamicQuery()
    {
        
    }

    // Parametreli constructor — Sort ve Filter'ı direkt alarak nesneyi tek satırda oluşturur.
    // Örnek:
    //   var dq = new DynamicQuery(sortList, myFilter);
    //
    // Dikkat: İkisi de null kabul eder. Sadece sıralama istiyorsan:
    //   new DynamicQuery(sortList, null)
    // Sadece filtreleme istiyorsan:
    //   new DynamicQuery(null, myFilter)
    public DynamicQuery(IEnumerable<Sort>? sort, Filter? filter)
    {
        Filter = filter;
        Sort = sort;
    }
}
