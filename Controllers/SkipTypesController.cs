using Microsoft.AspNetCore.Mvc;

namespace SkipHire.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SkipTypesController : ControllerBase
{
    // For now: hardcoded mock data (later this comes from DB)
    private static readonly object[] SkipTypes =
    [
        new { id = 1, sizeYards = 2, name = "3 Yards - 35+ Bags", description = "A 3 Yard Skip with a Capacity of 35 Bags" },
        new { id = 2, sizeYards = 4, name = "6 Yards - 65+ Bags", description = "A 6 Yard Skip with a Capacity of 65 Bags" },
        new { id = 3, sizeYards = 6, name = "25 Yards - 275+ Bags", description = "A 25 Yard Skip with a Capacity of 275 Bags" },
    ];

    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(SkipTypes);
    }
}