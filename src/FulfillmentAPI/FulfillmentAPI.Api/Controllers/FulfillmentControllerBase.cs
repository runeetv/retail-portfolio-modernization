using FulfillmentAPI.Application;
using Microsoft.AspNetCore.Mvc;

namespace FulfillmentAPI.Api.Controllers;

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
