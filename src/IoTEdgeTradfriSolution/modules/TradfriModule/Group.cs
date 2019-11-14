namespace TradfriModule
{
    using System.Collections.Generic;

    public class Group
    {
        public string name { get; set; }
        public long lightState { get; set; }
        public long activeMood {get; set;}
        public Dictionary<string, Device> devices {get; private set;}

        public Group()
        {
            devices = new Dictionary<string, Device>();
        }
    }
}
