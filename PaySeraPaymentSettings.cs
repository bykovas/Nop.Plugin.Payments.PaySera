using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.PaySera
{
    public class PaySeraPaymentSettings : ISettings
    {
        public string ProjectId { get; set; }
        public string ProjectPassword { get; set; }
        public bool UseSandbox { get; set; }
        public decimal AdditionalFee { get; set; }
        public bool AdditionalFeePercentage { get; set; }
        public string PayText { get; set; }
    }
}
