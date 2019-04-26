namespace SIL.Transcriber.Models
{
    public interface IBelongsToOrganization
    {
        int OrganizationId { get; set; }
        Organization Organization { get; set; }

    }
}
