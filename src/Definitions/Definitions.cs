using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Definitions
{
    public class ArtifactTypeDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Artifacttype>(resourceGraph, loggerFactory, Request)
    {
    }
    public class BibleDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request,
        MediafileService mediafileService
        ) : BaseDefinition<Bible>(resourceGraph, loggerFactory, Request)
    {
        private readonly MediafileService _mediafileService = mediafileService;

        public override async Task OnWritingAsync(
                        Bible resource,
                        WriteOperationKind writeOperation,
                        CancellationToken cancellationToken
                    )
        {
            _ = PublishMediafile(writeOperation, _mediafileService, PublishTitle, resource.IsoMediafileId);
            _ = PublishMediafile(writeOperation, _mediafileService, PublishTitle, resource.BibleMediafileId);
            await base.OnWritingAsync(resource, writeOperation, cancellationToken);
        }
    }

    public class CommentDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Comment>(resourceGraph, loggerFactory, Request)
    {
    }
    public class CopyProjectDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<CopyProject>(resourceGraph, loggerFactory, Request)
    {
    }

    public class DiscussionDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Discussion>(resourceGraph, loggerFactory, Request)
    {
    }

    public class GraphicDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request,
        MediafileService mediafileService

        ) : BaseDefinition<Graphic>(resourceGraph, loggerFactory, Request)
    {
        private readonly MediafileService _mediafileService = mediafileService;

        public override async Task OnWritingAsync(
                        Graphic resource,
                        WriteOperationKind writeOperation,
                        CancellationToken cancellationToken
                    )
        {
            _ = PublishMediafile(writeOperation, _mediafileService, PublishTitle, resource.MediafileId);
            await base.OnWritingAsync(resource, writeOperation, cancellationToken);
        }

    }
    public class GroupDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Group>(resourceGraph, loggerFactory, Request)
    {
    }

    public class GroupMembershipDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Groupmembership>(resourceGraph, loggerFactory, Request)
    {
    }
    public class IntegrationDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Integration>(resourceGraph, loggerFactory, Request)
    {
    }
    public class IntellectualPropertyDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Intellectualproperty>(resourceGraph, loggerFactory, Request)
    {
    }
    public class InvitationDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Invitation>(resourceGraph, loggerFactory, Request)
    {
    }

    public class OrganizationDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Organization>(resourceGraph, loggerFactory, Request)
    {
    }
    public class OrganizationBibleDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Organizationbible>(resourceGraph, loggerFactory, Request)
    {
    }
    public class OrganizationMembershipDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Organizationmembership>(resourceGraph, loggerFactory, Request)
    {
    }
    public class OrgKeytermDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Orgkeyterm>(resourceGraph, loggerFactory, Request)
    {
    }
    public class OrgKeytermreferenceDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Orgkeytermreference>(resourceGraph, loggerFactory, Request)
    {
    }
    public class OrgKeytermTargetDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Orgkeytermtarget>(resourceGraph, loggerFactory, Request)
    {
    }
    public class OrgworkflowstepDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Orgworkflowstep>(resourceGraph, loggerFactory, Request)
    {
    }

    public class PassageDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Passage>(resourceGraph, loggerFactory, Request)
    {
        /*  I couldn't get this to be called...so I just added plan-id to the passage model
         *
        public override QueryStringParameterHandlers<Passage> OnRegisterQueryableHandlersForQueryStringParameters()
        {
            return new QueryStringParameterHandlers<Passage>
            {
                ["plan-id"] = (source, parameterValue) =>
                    source
                        .Include(item => item.Section)
                        .ThenInclude(s => s.Plan)
                        .Where(item => item.Section.Plan.Id == parameterValue)
            };
        }
        */
    }

    public class PassageStateChangeDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Passagestatechange>(resourceGraph, loggerFactory, Request)
    {
    }

    public class PlanDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Plan>(resourceGraph, loggerFactory, Request)
    {
    }

    public class ProjectDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Project>(resourceGraph, loggerFactory, Request)
    {
    }

    public class ProjectIntegrationDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Projectintegration>(resourceGraph, loggerFactory, Request)
    {
    }

    public class ResourceDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Resource>(resourceGraph, loggerFactory, Request)
    {
    }

    public class SectionPassageDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Sectionpassage>(resourceGraph, loggerFactory, Request)
    {
    }

    public class SectionResourceDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Sectionresource>(resourceGraph, loggerFactory, Request)
    {
    }

    public class SectionResourceUserDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Sectionresourceuser>(resourceGraph, loggerFactory, Request)
    {
    }
    public class SharedresourceDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request,
        MediafileService mediafileService
        ) : BaseDefinition<Sharedresource>(resourceGraph, loggerFactory, Request)
    {
        private readonly MediafileService _mediafileService = mediafileService;

        public override async Task OnWritingAsync(
                        Sharedresource resource,
                        WriteOperationKind writeOperation,
                        CancellationToken cancellationToken
                    )
        {
            _ = PublishMediafile(writeOperation, _mediafileService, PublishTitle, resource.TitleMediafileId);
            await base.OnWritingAsync(resource, writeOperation, cancellationToken);
        }
    }
    public class SharedresourcereferenceDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Sharedresourcereference>(resourceGraph, loggerFactory, Request)
    {
    }
    public class VWBiblebrainBibleDefinition(
        IResourceGraph resourceGraph
        ) : JsonApiResourceDefinition<Vwbiblebrainbible, int>(resourceGraph)
    {
    }
    public class VWBiblebrainLanguageDefinition(
        IResourceGraph resourceGraph
        ) : JsonApiResourceDefinition<Vwbiblebrainlanguage, int>(resourceGraph)
    {
    }
    public class VWChecksumDefinition(
        IResourceGraph resourceGraph
        ) : JsonApiResourceDefinition<VWChecksum, int>(resourceGraph)
    {
    }
    public class WorkflowstepDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<Workflowstep>(resourceGraph, loggerFactory, Request)
    {
    }
}
