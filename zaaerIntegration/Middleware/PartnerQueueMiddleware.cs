using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace zaaerIntegration.Middleware
{
	public sealed class PartnerQueueMiddleware
	{
		private readonly RequestDelegate _next;

		public PartnerQueueMiddleware(RequestDelegate next)
		{
			_next = next;
		}

		public async Task InvokeAsync(HttpContext context, ILogger<PartnerQueueMiddleware> logger)
		{
			logger.LogDebug("PartnerQueue middleware bypassed (Sprint 3B).");
			await _next(context);
		}
	}
}


