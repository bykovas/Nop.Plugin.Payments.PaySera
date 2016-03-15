using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.PaySera.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PaySera.Fields.ProjectId")]
        public string ProjectId { get; set; }
        public bool ProjectId_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PaySera.Fields.ProjectPassword")]
        public string ProjectPassword { get; set; }
        public bool ProjectPassword_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PaySera.Fields.UseSandbox")]
        public bool UseSandbox { get; set; }
        public bool UseSandbox_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PaySera.Fields.AdditionalFee")]
        public decimal AdditionalFee { get; set; }
        public bool AdditionalFee_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PaySera.Fields.AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }
        public bool AdditionalFeePercentage_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PaySera.Fields.PayText")]
        public string PayText { get; set; }
        public bool PayText_OverrideForStore { get; set; }
    }
}
