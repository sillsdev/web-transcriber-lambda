﻿using System;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class VwPassageStateHistoryEmailService : BaseService<VwPassageStateHistoryEmail>
    {
        public VwPassageStateHistoryEmailService(
            IJsonApiContext jsonApiContext,
           VwPassageStateHistoryEmailRepository Repository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, Repository, loggerFactory)
        {
        }
        public IEnumerable<VwPassageStateHistoryEmail> GetHistorySince(DateTime since)
        {
            return GetAsync().Result.Where(h => h.StateUpdated > since).OrderBy(o => o.Id);   //view has an orderby now 
        }
    }
}