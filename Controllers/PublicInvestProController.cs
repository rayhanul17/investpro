using FlexCms.InvestPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexCms.InvestPro.Controllers;

/// <summary>
/// Public-facing controller — JSON endpoint your frontend, mobile app, or other
/// modules can consume without authentication. Delete or extend as needed.
/// </summary>
[Route("api/investpro")]
public class PublicInvestProController : ControllerBase
{
    private readonly InvestProService _service;
    public PublicInvestProController(InvestProService service) => _service = service;

    [HttpGet("")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _service.GetAllAsync(ct);
        return Ok(items
            .Where(x => x.IsPublished)
            .Select(x => new { x.Id, x.Title, x.Description, x.CreatedAt }));
    }
}
