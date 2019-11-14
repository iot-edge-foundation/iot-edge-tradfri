namespace TradfriModule
{
    using System.Collections.Generic;

    public class CollectedInformation
    {
        public CollectedInformation()
        {
            groups = new Dictionary<string, Group>();
        }      

        public Dictionary<string, Group> groups {get; private set;}
    }
}
