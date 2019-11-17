namespace TradfriModule
{
    using System.Collections.Generic;

    public class CollectedInformation
    {
        public CollectedInformation()
        {
            groups = new Dictionary<long, Group>();
        }      

        public Dictionary<long, Group> groups {get; private set;}
    }
}
