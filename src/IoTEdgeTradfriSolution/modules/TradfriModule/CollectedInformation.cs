namespace TradfriModule
{
    using System.Collections.Generic;

    public class CollectedInformation
    {
        public CollectedInformation()
        {
            groups = new List<Group>();
        }      

        public List<Group> groups {get; private set;}
    }
}
