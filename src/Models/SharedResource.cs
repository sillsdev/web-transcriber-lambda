﻿using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models;
[Table("sharedresources")]

public partial class Sharedresource : BaseModel, IArchive
{
    [Attr(PublicName = "passage-id")]
    public int? PassageId { get; set; }
    [HasOne(PublicName = "passage")]
    public Passage? Passage { get; set; }

    [Attr(PublicName = "cluster-id")]
    public int? ClusterId { get; set; }
    [HasOne(PublicName = "cluster")]
    public Organization? Cluster { get; set; }

    [Attr(PublicName = "title")]
    public string? Title { get; set; }
    [Attr(PublicName = "description")]
    public string? Description { get; set; }
    [Attr(PublicName = "languagebcp47")]
    public string? Languagebcp47 { get; set; }

    [Attr(PublicName = "terms-of-use")]
    public string? TermsOfUse { get; set; }

    [Attr(PublicName = "keywords")]
    public string? Keywords { get; set; }

    public int? ArtifactCategoryId { get; set; }

    [HasOne(PublicName = "artifact-category")]
    public Artifactcategory? ArtifactCategory { get; set; }
    public bool Archived { get; set; }
}
