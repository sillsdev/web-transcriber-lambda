﻿using System;
using System.Collections.Generic;
using System.Security;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SIL.Paratext.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Route("api/[controller]")]
    [ApiController]
    public class ParatextController : ControllerBase
    {
        private readonly IParatextService _paratextService;
        protected ILogger<ParatextController> Logger { get; set; }

        public ParatextController(IParatextService paratextService, ILoggerFactory loggerFactory)
        {
            _paratextService = paratextService;
            this.Logger = loggerFactory.CreateLogger<ParatextController>();
        }

        [HttpGet("projects")]
        public async Task<ActionResult<IEnumerable<ParatextProject>>> GetAsync()
        {
            UserSecret userSecret = _paratextService.ParatextLogin();
            try
            {
                IReadOnlyList<ParatextProject> projects = await _paratextService.GetProjectsAsync(userSecret);
                return Ok(projects);
            }
            catch (SecurityException)
            {
                return NoContent();
            }
        }
        [HttpGet("projects/{languagetag}")]
        public async Task<ActionResult<IEnumerable<ParatextProject>>> GetAsync([FromRoute] string languageTag)
        {
            UserSecret userSecret = _paratextService.ParatextLogin();
            try
            {
                IReadOnlyList<ParatextProject> projects = await _paratextService.GetProjectsAsync(userSecret, languageTag);
                return Ok(projects);
            }
            catch (Exception ex)
            {
                Logger.LogError("Paratext Error projects get {0} {1} {2}", ex.Message, languageTag, userSecret.ParatextTokens.IssuedAt.ToString());
                throw ex;
                //return NoContent();
            }
        }

        [HttpGet("username")]
        public ActionResult<string> Username()
        {
            UserSecret userSecret = _paratextService.ParatextLogin();

            string username = _paratextService.GetParatextUsername(userSecret);
            return Ok(username);
        }

        [HttpGet("section/{sectionid}")]
        public async Task<ActionResult<List<ParatextChapter>>> GetSectionBookAsync([FromRoute] int sectionId)
        {
            UserSecret userSecret = _paratextService.ParatextLogin();
            List<ParatextChapter> chapters = await _paratextService.GetSectionChaptersAsync(userSecret, sectionId);
            return Ok(chapters);
        }
        [HttpGet("project/{projectId}/count")]
        public async Task<ActionResult<int>> ProjectPassagesToSyncCount([FromRoute] int projectId)
        {

            int passages = await _paratextService.ProjectPassagesToSyncCountAsync(projectId);
            return Ok(passages);
        }

        [HttpGet("plan/{planid}/count")]
        public ActionResult<int> PassageReadyToSyncCount([FromRoute] int planId)
        {
            int passages = _paratextService.PlanPassagesToSyncCount(planId);
            return Ok(passages);
        }
        [HttpGet("passage/{passageid}")]
        public async Task<ActionResult<string>> PassageTextAsync([FromRoute] int passageid)
        {
            string text = await _paratextService.PassageTextAsync(passageid);
            return Ok(text);
        }

        [HttpPost("plan/{planid}")]
        public async Task<ActionResult<List<ParatextChapter>>> PostPlanAsync([FromRoute] int planId)
        {
            /* get all the sections that are ready to sync */
            List<ParatextChapter> chapters = await _paratextService.SyncPlanAsync(_paratextService.ParatextLogin(), planId);
            return Ok(chapters);
        }
        [HttpPost("project/{projectid}")]
        public async Task<ActionResult<List<ParatextChapter>>> PostProjectAsync([FromRoute] int projectId)
        {
            List<ParatextChapter> chapters = await _paratextService.SyncProjectAsync(_paratextService.ParatextLogin(), projectId);
            return Ok();
        }

    }
}