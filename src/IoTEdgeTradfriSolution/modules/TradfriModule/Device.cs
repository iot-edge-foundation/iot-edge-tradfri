namespace TradfriModule
{
    using System;

    public class Device
    {
        public long id { get; set; }
        public string deviceType { get; set; }
        public string deviceTypeExt { get; set; }
        public string name { get; set; }
        public long battery { get; set; }
        public DateTime lastSeen { get; set; }
        public string reachableState { get; set; }
        public long dimmer { get; set; }
        public string state { get; set; }
        public string colorHex { get; set; }
    }
}
