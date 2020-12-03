using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public class OrbitId
    {
        public OrbitId(string mytype)
        {
            type = mytype;
            ids = new List<int>();
        }
        public string type { get; set; }
        public List<int> ids { get; set; }

        public void AddUnique(List<int> newIds)
        {
            newIds.ForEach(i =>
            {
                if (!ids.Exists(id => id == i)) ids.Add(i);
            });
        }
    }
}
