using System;

namespace TradfriModule
{
    public class RoutedMessage
    {
        public long id { get; set; }
        public string  name { get; set; }
        public string state { get; set; }
        public long brightness { get; set; }
        public string colorHex { get; set; }
        public long groupId { get; set; }
        public string groupName { get; set; }
        public DateTime lastSeen {get; set;}
    }
}
