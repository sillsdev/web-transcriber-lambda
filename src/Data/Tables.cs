namespace SIL.Transcriber.Data;

public static class Tables
{
    public const string ActivityStates = "activitystates";
    public const string ArtifactCategorys  = "artifactcategorys";
    public const string ArtifactTypes = "artifacttypes";
    public const string Comments = "comments";
    public const string CurrentVersions= "currentversions";
    public const string Discussions = "discussions";  
    public const string GroupMemberships = "groupmemberships";
    public const string Groups = "groups";
    public const string Integrations = "integrations";
    public const string IntellectualPropertys = "intellectualpropertys";
    public const string Invitations = "invitations";
    public const string Mediafiles = "mediafiles";
    public const string Organizations = "organizations";
    public const string OrganizationMemberships = "organizationmemberships";
    public const string OrgKeyTermReferences =  "orgkeytermreferences";
    public const string OrgKeyTerms = "orgkeyterms";
    public const string OrgKeyTermTargets = "orgkeytermtargets";
    public const string OrgWorkflowSteps =  "orgworkflowsteps";
    public const string ParatextTokens = "paratexttokens";
    public const string Passages = "passages";
    public const string PassageStateChanges = "passagestatechanges";
    public const string Plans =     "plans";
    public const string PlanTypes = "plantypes";
    public const string ProjectIntegrations = "projectintegrations";
    public const string Projects = "projects";
    public const string ProjectTypes = "projecttypes";
    public const string Roles = "roles";
    public const string SectionPassages = "sectionpassages";
    public const string SectionResources = "sectionresources";
    public const string SectionResourceUsers = "sectionresourceusers";
    public const string Sections = "sections";
    public const string SharedResourceReferences = "sharedresourcereferences";
    public const string SharedResources = "sharedresources";
    public const string Users = "users";
    public const string UserVersions = "userversions";
    public const string WorkflowSteps = "workflowsteps";

    public static string ToType(string table) => table[..^1];
}
