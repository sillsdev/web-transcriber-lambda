using JsonApiDotNetCore.Configuration;
using SIL.Transcriber.Models;
using JsonApiDotNetCore.Middleware;

namespace SIL.Transcriber.Definitions
{
    public class GroupDefinition : BaseDefinition<Group>
    {
        public GroupDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory,
            IJsonApiRequest Request) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class GroupMembershipDefinition : BaseDefinition<Groupmembership>
    {
        public GroupMembershipDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory,
            IJsonApiRequest Request) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class InvitationDefinition : BaseDefinition<Invitation>
    {
        public InvitationDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory,
            IJsonApiRequest Request) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class MediafileDefinition : BaseDefinition<Mediafile>
    {
        public MediafileDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory,
            IJsonApiRequest Request) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class OrganizationDefinition : BaseDefinition<Organization>
    {
        public OrganizationDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory,
            IJsonApiRequest Request) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class OrganizationMembershipDefinition : BaseDefinition<Organizationmembership>
    {
        public OrganizationMembershipDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory,
            IJsonApiRequest Request) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class PassageDefinition : BaseDefinition<Passage> 
    {
        public PassageDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory,
            IJsonApiRequest Request) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class PassageStateChangeDefinition : BaseDefinition<Passagestatechange>
    {
        public PassageStateChangeDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory,
            IJsonApiRequest Request) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class PlanDefinition : BaseDefinition<Plan>
    {
       public PlanDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory,
            IJsonApiRequest Request) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class ProjectDefinition : BaseDefinition<Project>
    {
        public ProjectDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory,
            IJsonApiRequest Request) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class ProjectIntegrationDefinition : BaseDefinition<Projectintegration>
    {
        public ProjectIntegrationDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory,
            IJsonApiRequest Request) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class SectionDefinition : BaseDefinition<Section>
    {
        public SectionDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory,
            IJsonApiRequest Request) : base(resourceGraph, loggerFactory, Request) { }
    }
    public class SectionPassageDefinition : BaseDefinition<Sectionpassage>
    {
        public SectionPassageDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory,
            IJsonApiRequest Request) : base(resourceGraph, loggerFactory, Request) { }
    }

}
