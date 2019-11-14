namespace TradfriModule
{
    public class CollectInformationResponse : CollectedInformation
    {
        public CollectInformationResponse() : base()
        {
        }

        public int responseState { get; set; }

        public string errorMessage { get; set; }
    }
}
