namespace Core.Application.Pipelines.Caching;

// AppSettings içerisinde Cache ile ilgili ayarlar tutuluyor,
// bunun için bu sınıfı oluşturduk. AppSetting dosyasında set edilecek
public class CacheSettings
{
    public int SlidingExpiration { get; set; }

}
