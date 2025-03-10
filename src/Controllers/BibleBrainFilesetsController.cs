﻿using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers;

[Route("api/[controller]")]
public class BiblebrainfilesetsController(
ILoggerFactory loggerFactory,
IJsonApiOptions options,
IResourceGraph resourceGraph,
IResourceService<Biblebrainfileset, int> resourceService,
ICurrentUserContext currentUserContext,
UserService userService
) : BaseController<Biblebrainfileset>(
    loggerFactory,
    options,
    resourceGraph,
    resourceService,
    currentUserContext,
    userService
    )
{
    private readonly BibleBrainFilesetService _service = (BibleBrainFilesetService)resourceService;

    [AllowAnonymous]
    [HttpPost("allowed")]
    public IActionResult PostAllowed([FromBody] Biblebrainfileset entity)
    {
        AllowedFileset? afs = JsonConvert.DeserializeObject<AllowedFileset>(entity.Allowed);
        if (afs is null)
            return BadRequest("Invalid Allowed Fileset");
        Biblebrainfileset? fs = _service.PostAllowed(afs);
        return Ok(fs);
    }
    [AllowAnonymous]
    [HttpGet("fs/{filesetid}")]
    public IActionResult GetFileset([FromRoute] string filesetid)
    {
        Biblebrainfileset? fs = _service.GetFileset(filesetid);
        return Ok(fs);
    }
    [AllowAnonymous]
    [HttpPost("timing/{filesetid}")]
    public async Task<IActionResult> SetTiming([FromRoute] string filesetid)
    {
        await _service.UpdateTiming(filesetid);
        return Ok();
    }
    [AllowAnonymous]
    [HttpPatch("{id}")]
    public override Task<IActionResult> PatchAsync([FromRoute] int id, [FromBody] Biblebrainfileset entity, CancellationToken ct) => base.PatchAsync(id, entity, ct);
}
