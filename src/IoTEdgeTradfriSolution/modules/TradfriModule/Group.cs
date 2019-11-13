namespace TradfriModule
{
    using System.Collections.Generic;

    public class Group
    {
        public long id { get; set; }
        public string name { get; set; }
        public long lightState { get; set; }
        public long activeMood {get; set;}
        public List<Device> devices {get; private set;}

        public Group()
        {
            devices = new List<Device>();
        }
    }
}
