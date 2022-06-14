using JsonApiDotNetCore.Configuration;
using SIL.Transcriber.Models;
using JsonApiDotNetCore.Middleware;

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

    public class CommentDefinition : BaseDefinition<Comment>
    {
        public CommentDefinition(
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
}
