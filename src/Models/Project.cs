using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using SIL.Transcriber.Utility.Extensions;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SIL.Transcriber.Models
{
    [Table(Tables.Projects)]
    public partial class Project : BaseModel, IArchive, IBelongsToOrganization
    {
        public Project() : base()
        {
            Name = "";
            IsPublic = false;
        }
        [Attr(PublicName = "name")]
        public string Name { get; set; }

        [Attr(PublicName = "slug")]
        public string? Slug { get; set; }

        [HasOne(PublicName = "projecttype")]
        public virtual Projecttype Projecttype { get; set; } = null!;
        [Attr(PublicName = "projecttype-id")]
        public int ProjecttypeId { get; set; }

        [Attr(PublicName = "description")]
        public string? Description { get; set; }

        [HasOne(PublicName = "owner")]
        public virtual User? Owner { get; set; }
        [Attr(PublicName = "owner-id")]
        public int? OwnerId { get; set; }

        [HasOne(PublicName = "organization")]
        public virtual Organization Organization { get; set; } = null!;
        [Attr(PublicName = "organization-id")]
        public int OrganizationId { get; set; }

        [HasOne(PublicName = "group")]
        public virtual Group Group { get; set; } = null!;
        [Attr(PublicName = "group-id")]
        public int GroupId { get; set; }

        //settings
        [Attr(PublicName = "uilanguagebcp47")]
        public string? Uilanguagebcp47 { get; set; }

        [Attr(PublicName = "language")]
        public string? Language { get; set; }

        [Attr(PublicName = "language-name")]
        public string? LanguageName { get; set; }

        [Attr(PublicName = "default-font")]
        public string? DefaultFont { get; set; }

        [Attr(PublicName = "default-font-size")]
        public string? DefaultFontSize { get; set; }

        [Attr(PublicName = "rtl")]
        public bool? Rtl { get; set; } = true;

        [Attr(PublicName = "allow-claim")]
        public bool? AllowClaim { get; set; } = true;

        [Attr(PublicName = "is-public")]
        public bool IsPublic { get; set; } = true;

        [Attr(PublicName = "spell-check")]
        public bool? SpellCheck { get; set; } = true;

        [Attr(PublicName = "default-params")]
        [Column(TypeName = "jsonb")]
        public string? DefaultParams { get; set; }

        private DateTime? _archived;
        [Attr(PublicName = "date-archived")]
        public DateTime? DateArchived { get { return _archived; } set { _archived = value.SetKindUtc(); } }

        [HasOne(PublicName = "editsheetgroup")]
        public virtual Group? EditsheetGroup { get; set; }
        public int? EditsheetGroupId { get; set; }
        [HasOne(PublicName = "editsheetuser")]
        public virtual User? EditsheetUser { get; set; }
        public int? EditsheetUserId { get; set; }

        [HasOne(PublicName = "publishgroup")]
        public virtual Group? PublishGroup { get; set; }
        public int? PublishGroupId { get; set; }
        [HasOne(PublicName = "publishuser")]
        public virtual User? PublishUser { get; set; }
        public int? PublishUserId { get; set; }

        [JsonIgnore]
        [HasMany(PublicName = "plans")]
        public virtual List<Plan>? Plans { get; set; }
        //[HasManyThrough("tasks")]
        //public virtual List<Task> Tasks { get; set; }
        public bool Archived { get; set; }

        public dynamic? GetDefaultParam(string key)
        {
            if (DefaultParams == null)
            {
                return null;
            }
            dynamic? x = Newtonsoft.Json.JsonConvert.DeserializeObject(DefaultParams);
            return x?[key];
        }
        public bool AddSectionNumbers()
        {
            string? value = GetDefaultParam("exportNumbers");
            return value != null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("\"true\"", StringComparison.OrdinalIgnoreCase));
        }
        public SectionMap[] GetSectionMap()
        {
            SectionMap [] ret = [];
            dynamic? sectionMapstr = GetDefaultParam("sectionMap"); //"[[0.01,\\"M1\\"],[1,\\"M1 S1\\"],[2,\\"M1 S2\\"],[3,\\"M1 S3\\"]]"    
            if (typeof(string) == sectionMapstr?.GetType()) //if it's a string, it's escaped
            {
                if (sectionMapstr.Contains("\\"))
                    sectionMapstr = sectionMapstr.Replace("\\", "");
            }
            else
                sectionMapstr = sectionMapstr?.ToString();
            List<List<object>>? tmp = sectionMapstr == null ? null : Newtonsoft.Json.JsonConvert.DeserializeObject<List<List<object>>>(sectionMapstr.ToString());
            tmp?.ForEach((List<object> item) => {
                if (!decimal.TryParse(item[0]?.ToString(), out decimal num))
                    num = 0;
                ret = [.. ret, new SectionMap { Sequencenum = num, Label = item[1]?.ToString() ?? "" }];
            });
            return ret;
        }
    }
}
