using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class SimpleResponseController(MediafileRepository mediafileRepository) : ControllerBase
{
    [HttpGet("baddurations")]
    public IActionResult GetBadDurations() => Ok(mediafileRepository.GetFixDuration());

    [HttpGet]
    public IActionResult GetAll() => Forbid();

    [HttpGet("{id:int}")]
#pragma warning disable IDE0060 // Remove unused parameter
    public IActionResult GetById([FromRoute] int id) => Forbid();


    [HttpPost]
    public IActionResult Post() => Forbid();

    [HttpPatch("{id:int}")]
    public IActionResult Patch([FromRoute] int id) => Forbid();

    [HttpDelete("{id:int}")]
    public IActionResult Delete([FromRoute] int id) => Forbid();
#pragma warning restore IDE0060 // Remove unused parameter
}