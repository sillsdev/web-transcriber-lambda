using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models;

public class VWProject : Identifiable<int>
{
    [Attr(PublicName = "organization-id")]
    public int OrganizationId { get; set; }
    [Attr(PublicName = "org-name")]
    public string OrgName { get; set; } = "";
    [Attr(PublicName = "project-id")]
    public int ProjectId { get; set; }
    [Attr(PublicName = "project-name")]
    public string ProjectName { get; set; } = "";
    [Attr(PublicName = "language")]
    public string Language { get; set; } = "";
    [Attr(PublicName = "section-id")]
    public int? SectionId { get; set; }
    [Attr(PublicName = "section-title")]
    public string? SectionTitle { get; set; }
    [Attr(PublicName = "section-num")]
    public decimal? SectionNum { get; set; }
    [Attr(PublicName = "published")]
    public bool? Published { get; set; }
    [Attr(PublicName = "section-level")]
    public int? SectionLevel { get; set; }
    [Attr(PublicName = "passage-id")]
    public int? PassageId { get; set; }
    [Attr(PublicName = "passage-num")]
    public decimal? PassageNum { get; set; }
    [Attr(PublicName = "book")]
    public string? Book { get; set; }
    [Attr(PublicName = "reference")]
    public string? Reference { get; set; }
    [Attr(PublicName = "shared-resource-id")]
    public int? SharedResourceId { get; set; }
    [Attr(PublicName = "start-chapter")]
    public int? StartChapter { get; set; }

    [Attr(PublicName = "start-verse")]
    public int? StartVerse { get; set; }

    [Attr(PublicName = "end-chapter")]
    public int? EndChapter { get; set; }

    [Attr(PublicName = "end-verse")]
    public int? EndVerse { get; set; }
}
