using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Productdefinitions
    {
        public Productdefinitions()
        {
            Organizationproductdefinitions = new HashSet<XOrganizationproductdefinitions>();
            Products = new HashSet<Products>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public int Typeid { get; set; }
        public string Description { get; set; }
        public int Workflowid { get; set; }

        public ProjectType ProjectType { get; set; }
        public Workflowdefinitions Workflow { get; set; }
        public ICollection<XOrganizationproductdefinitions> Organizationproductdefinitions { get; set; }
        public ICollection<Products> Products { get; set; }
    }
}
