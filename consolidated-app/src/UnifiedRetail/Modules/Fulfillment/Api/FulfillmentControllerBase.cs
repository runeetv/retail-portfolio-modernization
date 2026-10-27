using FulfillmentAPI.Application;
using Microsoft.AspNetCore.Mvc;

namespace UnifiedRetail.Modules.Fulfillment.Api;

/// <summary>
/// Preserves the exact error-to-status mapping of the original FulfillmentControllerBase
/// (FA-RULE-024): success -> 200 Ok(value); NotFound -> 404 {error}; Conflict -> 409 {error};
/// Validation/other -> 400 {error}.
/// </summary>
public abstract class FulfillmentControllerBase : ControllerBase
{
    protected IActionResult FromResult<T>(ApiResult<T> result) =>
        result.Success
            ? Ok(result.Value)
            : result.ErrorKind switch
            {
                ApiErrorKind.NotFound => NotFound(new { error = result.Error }),
                ApiErrorKind.Conflict => Conflict(new { error = result.Error }),
                _ => BadRequest(new { error = result.Error })
            };
}
