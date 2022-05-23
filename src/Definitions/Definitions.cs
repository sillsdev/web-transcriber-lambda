using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Resources;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility.Extensions.JSONAPI;

namespace SIL.Transcriber.Definitions
{
    public class GroupDefinition : BaseDefinition<Group>
    {
        public GroupDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory) : base(resourceGraph, loggerFactory) { }
    }
    public class GroupMembershipDefinition : BaseDefinition<GroupMembership>
    {
        public GroupMembershipDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory) : base(resourceGraph, loggerFactory) { }
    }
    public class InvitationDefinition : BaseDefinition<Invitation>
    {
        public InvitationDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory) : base(resourceGraph, loggerFactory) { }
    }
    public class MediafileDefinition : BaseDefinition<Mediafile>
    {
        public MediafileDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory) : base(resourceGraph, loggerFactory) { }
    }
    public class OrganizationDefinition : BaseDefinition<Organization>
    {
        public OrganizationDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory) : base(resourceGraph, loggerFactory) { }
    }
    public class OrganizationMembershipDefinition : BaseDefinition<OrganizationMembership>
    {
        public OrganizationMembershipDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory) : base(resourceGraph, loggerFactory) { }
    }
    public class PassageDefinition : BaseDefinition<Passage> 
    {
        public PassageDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory) : base(resourceGraph, loggerFactory) { }
    }
    public class PassageStateChangeDefinition : BaseDefinition<PassageStateChange>
    {
        public PassageStateChangeDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory) : base(resourceGraph, loggerFactory) { }
    }
    public class PlanDefinition : BaseDefinition<Plan>
    {
       public PlanDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory) : base(resourceGraph, loggerFactory) { }
    }
    public class ProjectDefinition : BaseDefinition<Project>
    {
        public ProjectDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory) : base(resourceGraph, loggerFactory) { }
    }
    public class ProjectIntegrationDefinition : BaseDefinition<ProjectIntegration>
    {
        public ProjectIntegrationDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory) : base(resourceGraph, loggerFactory) { }
    }
    public class SectionDefinition : BaseDefinition<Section>
    {
        public SectionDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory) : base(resourceGraph, loggerFactory) { }
    }
    public class SectionPassageDefinition : BaseDefinition<SectionPassage>
    {
        public SectionPassageDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory) : base(resourceGraph, loggerFactory) { }
    }

}
