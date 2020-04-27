namespace TradfriModule
{
    public class CollectBatteryPowerResponse 
    {
        public BatteryPowerDevice[] devices {get; set;}

        public int responseState { get; set; }

        public string errorMessage { get; set; }
    }
}
