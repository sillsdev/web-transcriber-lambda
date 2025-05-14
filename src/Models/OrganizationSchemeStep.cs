using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models;
[Table(Tables.OrganizationSchemeSteps)]
public partial class Organizationschemestep : BaseModel, IArchive
{
    [HasOne(PublicName = "organizationscheme")]
    public virtual Organizationscheme OrganizationScheme { get; set; } = null!;

    [Attr(PublicName = "organizationscheme-id")]
    public int OrganizationschemeId { get; set; }

    public int OrgWorkflowStepId { get; set; }

    [HasOne(PublicName = "org-workflow-step")]
    public Orgworkflowstep? OrgWorkflowStep { get; set; }


    [HasOne(PublicName = "user")]
    public User? User { get; set; }

    public int? UserId { get; set; }


    [HasOne(PublicName = "group")]
    public Group? Group { get; set; } = null!;
    public int? GroupId { get; set; }

    public bool Archived { get; set; }
}
