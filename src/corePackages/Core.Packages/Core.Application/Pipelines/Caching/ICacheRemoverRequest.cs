using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Application.Pipelines.Caching;

public interface ICacheRemoverRequest
{
    string? CacheKey { get; } // Cache'nin anahtarı, her request bir cacheKey'e bağlanır, isim gibi düşünülebilir
    bool BypassCache { get; } // İstenildiği zaman bypass edilebilmesine yarıyor
    string? CacheGroupKey { get; } // Cache'in hangi gruba ait olduğunu belirler, null ise grup yoktur, grup bazında cache temizleme işlemi için kullanılır
}
