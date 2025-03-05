namespace SIL.Transcriber.Models
{
    public class OrbitId
    {
        public OrbitId(string mytype)
        {
            Type = mytype;
            Ids = [];
        }
        public OrbitId(string mytype, List<int> newIds)
        {
            Type = mytype;
            Ids = newIds;
        }
        public string Type { get; set; }
        public List<int> Ids { get; set; }

        public void AddUnique(List<int> newIds)
        {
            newIds.ForEach(i => {
                if (!Ids.Exists(id => id == i))
                    Ids.Add(i);
            });
        }
    }
}
