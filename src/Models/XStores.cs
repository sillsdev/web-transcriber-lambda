using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Stores
    {
        public Stores()
        {
            Organizationstores = new HashSet<Organizationstores>();
            Products = new HashSet<Products>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Storetypeid { get; set; }

        public Storetypes Storetype { get; set; }
        public ICollection<Organizationstores> Organizationstores { get; set; }
        public ICollection<Products> Products { get; set; }
    }
}
