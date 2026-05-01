using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Persistence.Repositories;

// IQUERY NEDİR VE NEDEN VAR?
// "Bu repository ham sorgu (IQueryable) döndürebilir" garantisini veren küçük bir interface'tir.
//
// Neden ayrı bir interface olarak tanımlandı?
//   IAsyncRepository ve IRepository'nin ikisi de Query() metoduna ihtiyaç duyar.
//   Bu metodu her ikisine ayrı ayrı yazmak yerine, IQuery interface'ine koyup
//   her ikisine de ": IQuery<T>" diyerek dahil ediyoruz. Tekrar önlenir.
//
// IQueryable<T> nedir (kısa hatırlatma)?
//   Henüz veritabanına gönderilmemiş, sadece "tarif edilmiş" bir sorgudur.
//   Üzerine .Where(), .OrderBy(), .Skip(), .Take() ekleyebilirsin.
//   .ToList() veya .FirstOrDefault() diyene kadar veritabanına gitmez.
//   Bu sayede sorguyu adım adım inşa edebiliriz.
//
// Query() metodu ne işe yarar?
//   EfRepositoryBase içinde Context.Set<TEntity>() döndürür.
//   Bu, o entity'nin tüm kayıtlarını temsil eden ham bir sorgu başlangıcıdır.
//   Tüm GetAsync, GetListAsync gibi metodlar Query() ile başlayıp
//   üzerine filtre/sıralama ekleyerek sorguyu şekillendirir.
public interface IQuery<T>
{
    // Ham veritabanı sorgusunu döndürür. Henüz veritabanına gitmez.
    // Bunu alan kod üzerine istediği kadar .Where(), .OrderBy() vb. ekleyebilir.
    IQueryable<T> Query();
}
