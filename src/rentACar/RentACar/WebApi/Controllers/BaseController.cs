using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

public class BaseController : ControllerBase
{
    private IMediator? _mediatr;
    protected IMediator? Mediator => _mediatr??= HttpContext.RequestServices.GetService<IMediator>(); 
    //protected yaptık çünkü sadece bu controlleri miras alanlar erişebilsin

    //Daha önce MediatR enjekte edilmişse onu döndür
    //ama edilmemişse IOC ortamına bak ve MediatR karşılığna bak ve onu döndür. Eğer IOC ortamında da yoksa null döndür.
}
