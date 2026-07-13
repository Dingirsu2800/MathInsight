using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MathInsight.Modules.Learning_Lecture.Commands.Materials;
using MathInsight.Modules.Learning_Lecture.Queries.Materials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.Learning_Lecture.Controllers;

[ApiController]
[Route("api/v1/materials")]
[Authorize]
public class MaterialsController : ControllerBase
{
    private readonly IMediator _mediator;

    public MaterialsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    private bool IsStudent => User.FindFirst(ClaimTypes.Role)?.Value == "Student";

    [HttpGet]
    public async Task<IActionResult> GetMaterials([FromQuery] string teacherId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetMaterialListQuery(teacherId, page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> UploadMaterial([FromForm] IFormFile file, [FromForm] string materialName, CancellationToken cancellationToken)
    {
        if (IsStudent) return Forbid();
        try
        {
            using var stream = file.OpenReadStream();
            var result = await _mediator.Send(new UploadMaterialCommand(materialName, stream, file.FileName, CurrentUserId), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMaterial(string id, [FromBody] UpdateMaterialRequest request, CancellationToken cancellationToken)
    {
        if (IsStudent) return Forbid();
        try
        {
            var result = await _mediator.Send(new UpdateMaterialCommand(id, request.MaterialName, CurrentUserId), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/deactivate")]
    public async Task<IActionResult> DeactivateMaterial(string id, CancellationToken cancellationToken)
    {
        if (IsStudent) return Forbid();
        try
        {
            await _mediator.Send(new DeactivateMaterialCommand(id, CurrentUserId), cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/attach")]
    public async Task<IActionResult> AttachToLecture(string id, [FromBody] AttachMaterialRequest request, CancellationToken cancellationToken)
    {
        if (IsStudent) return Forbid();
        try
        {
            await _mediator.Send(new AttachMaterialToLectureCommand(id, request.LectureId, CurrentUserId), cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public record UpdateMaterialRequest(string MaterialName);
public record AttachMaterialRequest(string LectureId);
