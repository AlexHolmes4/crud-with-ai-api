using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CrudApi.API.ExceptionHandler;

/// <summary>
/// A global exception handler.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
	/// <inheritdoc/>
	public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
	{
		var problemDetails = new ProblemDetails
		{
			Status = StatusCodes.Status500InternalServerError,
			Title = "An error occurred",
			Type = "https://httpstatuses.com/500"
		};

		httpContext.Response.StatusCode = problemDetails.Status.Value;

		await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

		return true;
	}
}