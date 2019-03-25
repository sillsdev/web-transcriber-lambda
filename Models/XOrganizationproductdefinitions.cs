using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class XOrganizationproductdefinitions
    {
        public int Id { get; set; }
        public int Organizationid { get; set; }
        public int Productdefinitionid { get; set; }

        public Organization Organization { get; set; }
        public Productdefinitions Productdefinition { get; set; }
    }
}
