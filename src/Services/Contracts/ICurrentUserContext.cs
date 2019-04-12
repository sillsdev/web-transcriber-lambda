using System;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Services
{
    public interface ICurrentUserContext
    {
        string Auth0Id { get; }
        string Email { get; }
        string GivenName { get; }
        string FamilyName { get; }
        string Name { get; }
    }
}
