using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class Storelanguage : BaseModel
    {
        public Storelanguage()
        {
            Products = new HashSet<Products>();
        }

        public string Name { get; set; }
        public string Description { get; set; }
        public int Storetypeid { get; set; }

        public Storetypes Storetype { get; set; }
        public ICollection<Products> Products { get; set; }
    }
}
