using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Payments.PaySera
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            routes.MapRoute("Plugin.Payments.PaySera.Configure",
                 "Plugins/PaymentPaySera/Configure",
                 new { controller = "PaymentPaySera", action = "Configure" },
                 new[] { "Nop.Plugin.Payments.PaySera.Controllers" }
            );

            routes.MapRoute("Plugin.Payments.PaySera.PaymentInfo",
                 "Plugins/PaymentPaySera/PaymentInfo",
                 new { controller = "PaymentPaySera", action = "PaymentInfo" },
                 new[] { "Nop.Plugin.Payments.PaySera.Controllers" }
            );

            //Cancel
            routes.MapRoute("Plugin.Payments.PaySera.CancelOrder",
                 "Plugins/PaymentPaySera/CancelOrder",
                 new { controller = "PaymentPaySera", action = "CancelOrder" },
                 new[] { "Nop.Plugin.Payments.PaySera.Controllers" }
            );

            //CallBackData
            routes.MapRoute("Plugin.Payments.PaySera.CallBackData",
                 "Plugins/PaymentPaySera/CallBackData",
                 new { controller = "PaymentPaySera", action = "CallBackData" },
                 new[] { "Nop.Plugin.Payments.PaySera.Controllers" }
            );
        }

        public int Priority
        {
            get
            {
                return 0;
            }
        }
    }
}
