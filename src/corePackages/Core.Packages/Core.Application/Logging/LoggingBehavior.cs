using Core.CrossCuttingConcerns.Logging;
using Core.CrossCuttingConcerns.Serilog;
using MediatR;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Core.Application.Logging;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ILoggableRequest
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LoggerServiceBase _loggerService;

    public LoggingBehavior(IHttpContextAccessor httpContextAccessor, LoggerServiceBase loggerService)
    {
        _httpContextAccessor = httpContextAccessor;
        _loggerService = loggerService;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        List<LogParameter> logParamters = new()
        {
            new LogParameter{ Type = request.GetType().Name, Value = request }
        };

        LogDetail logDetail =
            new()
            {
                MethodName = next.Method.Name,
                Parameters = logParamters,
                User = _httpContextAccessor.HttpContext.User.Identity?.Name ?? "?"
            };

        _loggerService.Info(JsonSerializer.Serialize(logDetail));

        return await next();
    }
}
