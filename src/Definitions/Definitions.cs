using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Definitions
{
    public class ArtifactCategoryDefinition : BaseDefinition<Artifactcategory>
    {
        public ArtifactCategoryDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }

    public class ArtifactTypeDefinition : BaseDefinition<Artifacttype>
    {
        public ArtifactTypeDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class BibleDefinition : BaseDefinition<Bible>
    {
        private readonly MediafileService _mediafileService;
        public BibleDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request,
            MediafileService mediafileService
        ) : base(resourceGraph, loggerFactory, Request)
        {
            _mediafileService = mediafileService;

        }
        public override async Task OnWritingAsync(
    Bible resource,
    WriteOperationKind writeOperation,
    CancellationToken cancellationToken
)
        {
            if (resource.IsoMediafile != null)
            {
                await _mediafileService.MakePublic(resource.IsoMediafile);
            }
            if (resource.BibleMediafile != null)
            {
                await _mediafileService.MakePublic(resource.BibleMediafile);
            }

            await base.OnWritingAsync(resource, writeOperation, cancellationToken);
        }
    }

    public class CommentDefinition : BaseDefinition<Comment>
    {
        public CommentDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class CopyProjectDefinition : BaseDefinition<CopyProject>
    {
        public CopyProjectDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }

    public class DiscussionDefinition : BaseDefinition<Discussion>
    {
        public DiscussionDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }

    public class GraphicDefinition : BaseDefinition<Graphic>
    {
        public GraphicDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class GroupDefinition : BaseDefinition<Group>
    {
        public GroupDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }

    public class GroupMembershipDefinition : BaseDefinition<Groupmembership>
    {
        public GroupMembershipDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class IntegrationDefinition : BaseDefinition<Integration>
    {
        public IntegrationDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class IntellectualPropertyDefinition : BaseDefinition<Intellectualproperty>
    {
        public IntellectualPropertyDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class InvitationDefinition : BaseDefinition<Invitation>
    {
        public InvitationDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }

    public class OrganizationDefinition : BaseDefinition<Organization>
    {
        public OrganizationDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }

    public class OrganizationMembershipDefinition : BaseDefinition<Organizationmembership>
    {
        public OrganizationMembershipDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class OrgKeytermDefinition : BaseDefinition<Orgkeyterm>
    {
        public OrgKeytermDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class OrgKeytermreferenceDefinition : BaseDefinition<Orgkeytermreference>
    {
        public OrgKeytermreferenceDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class OrgKeytermTargetDefinition : BaseDefinition<Orgkeytermtarget>
    {
        public OrgKeytermTargetDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class OrgworkflowstepDefinition : BaseDefinition<Orgworkflowstep>
    {
        public OrgworkflowstepDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }

    public class PassageDefinition : BaseDefinition<Passage>
    {
        public PassageDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
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

    public class PassageStateChangeDefinition : BaseDefinition<Passagestatechange>
    {
        public PassageStateChangeDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }

    public class PlanDefinition : BaseDefinition<Plan>
    {
        public PlanDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }

    public class ProjectDefinition : BaseDefinition<Project>
    {
        public ProjectDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }

    public class ProjectIntegrationDefinition : BaseDefinition<Projectintegration>
    {
        public ProjectIntegrationDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }

    public class ResourceDefinition : BaseDefinition<Resource>
    {
        public ResourceDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }

    public class SectionDefinition : BaseDefinition<Section>
    {
        public SectionDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }

    public class SectionPassageDefinition : BaseDefinition<Sectionpassage>
    {
        public SectionPassageDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }

    public class SectionResourceDefinition : BaseDefinition<Sectionresource>
    {
        public SectionResourceDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }

    public class SectionResourceUserDefinition : BaseDefinition<Sectionresourceuser>
    {
        public SectionResourceUserDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class SharedresourceDefinition : BaseDefinition<Sharedresource>
    {
        public SharedresourceDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class SharedresourcereferenceDefinition : BaseDefinition<Sharedresourcereference>
    {
        public SharedresourcereferenceDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class VWChecksumDefinition : JsonApiResourceDefinition<VWChecksum, int>
    {
        public VWChecksumDefinition(
            IResourceGraph resourceGraph
        ) : base(resourceGraph) { }
    }
}
