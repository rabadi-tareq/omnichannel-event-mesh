using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DsgOmnichannel.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    [HttpGet("public")]
    [AllowAnonymous]
    public IActionResult Public()
    {
        return Ok("Public endpoint accessible");
    }

    [HttpGet("secured")]
    [Authorize(Policy = "RequireCustomerRole")]
    public IActionResult Secured()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
        return Ok(new { Message = "Secured endpoint accessed", Claims = claims });
    }
}
