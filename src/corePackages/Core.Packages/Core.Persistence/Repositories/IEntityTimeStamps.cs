using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Persistence.Repositories;

// IEntityTimestamps NEDİR VE NEDEN VAR?
// "Bu entity'nin zaman damgası (timestamp) alanları vardır" garantisini veren bir interface'tir.
//
// Interface nedir (kısa hatırlatma)?
//   Interface, bir sınıfın "ne yapabileceğini" tanımlayan sözleşmedir.
//   Bir sınıf bu interface'i implement ederse, içindeki tüm alanları/metodları
//   sağlamak zorunda kalır. Aksi halde derleme hatası alır.
//
// Neden böyle bir interface var?
//   Projedeki her entity (User, Product, Order...) oluşturulma, güncellenme
//   ve silinme tarihlerini takip etmek zorundadır.
//   Bu alanları her entity sınıfına tek tek yazmak yerine, Entity<T> base sınıfı
//   bu interface'i implement eder ve tüm entity'ler bu alanları miras alır.
//
//   Ayrıca EfRepositoryBase içindeki soft delete mekanizması bu interface'e ihtiyaç duyar:
//   Bir nesnenin IEntityTimestamps olduğunu bilirse DeletedDate alanını set edebilir.
//   Bunu bilmeden DeletedDate'e erişemez çünkü her tipin DeletedDate'i olmayabilir.
public interface IEntityTimestamps
{
    // Kaydın veritabanına ilk eklendiği tarih ve saat.
    // Hiç null olamaz çünkü her kaydın mutlaka bir oluşturulma tarihi vardır.
    // AddAsync() içinde otomatik olarak DateTime.UtcNow atanır, elle set edilmez.
    DateTime CreatedDate { get; set; }

    // Kaydın en son güncellendiği tarih ve saat.
    // Neden null olabilir (?)?
    //   Kayıt hiç güncellenmemişse bu alan null kalır.
    //   "null" = "bu kayıt hiç güncellenmedi" anlamına gelir.
    //   UpdateAsync() çağrıldığında DateTime.UtcNow atanır.
    DateTime? UpdatedDate { get; set; }

    // Kaydın silindiği tarih ve saat — Soft Delete (yumuşak silme) için kullanılır.
    // Neden null olabilir (?)?
    //   Kayıt silinmemişse bu alan null kalır.
    //   "null" = "bu kayıt aktif, silinmedi" anlamına gelir.
    //   "null değil" = "bu kayıt silinmiş, artık gösterilmemeli" anlamına gelir.
    //
    // Soft Delete nedir?
    //   Kaydı veritabanından fiziksel olarak silmek yerine, DeletedDate alanını doldurmaktır.
    //   Kayıt veritabanında durmaya devam eder ama sorgularda filtrelenerek gösterilmez.
    //   Avantajı: Yanlışlıkla silinen veriler geri getirilebilir, geçmiş korunur.
    DateTime? DeletedDate { get; set; }
}
