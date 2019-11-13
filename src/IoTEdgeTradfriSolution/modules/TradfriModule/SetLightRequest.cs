namespace TradfriModule
{
    public class SetLightRequest
    {
        public long id { get; set; }
        public bool? turnLightOn { get; set; }
        public string color { get; set; }
        public int? brightness { get; set; }
    }
}
