namespace TradfriModule
{
    public class GatewayInfoResponse
    {
        public int responseState { get; set; }

        public string errorMessage { get; set; }

        public long commissioningMode { get; set; }

        public string currentTimeISO8601 { get; set; }

        public string firmware { get; set; }

        public string gatewayID { get; set; }

        public long gatewayTimeSource { get; set; }

        public long gatewayUpdateProgress { get; set; }

        public string homekitID { get; set; }

        public string ntp { get; set; }

        public long otaType { get; set; }

        public long OtaUpdateState { get; set; }
    }
}
