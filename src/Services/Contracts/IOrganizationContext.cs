using System;
namespace SIL.Transcriber.Services
{
    public interface IOrganizationContext
    {
        bool HasOrganization { get; }
        bool SpecifiedOrganizationDoesNotExist { get; }
        bool IsOrganizationHeaderPresent { get; }
        int OrganizationId { get; }
    }
}
