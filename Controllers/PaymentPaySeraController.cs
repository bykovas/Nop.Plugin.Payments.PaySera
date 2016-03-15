using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.PaySera.Models;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Payments.PaySera.Controllers
{
    public class PaymentPaySeraController : BasePaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IStoreContext _storeContext;
        private readonly ILogger _logger;
        private readonly IWebHelper _webHelper;
        private readonly PaymentSettings _paymentSettings;
        private readonly PaySeraPaymentSettings _paySeraPaymentSettings;

        public PaymentPaySeraController(IWorkContext workContext,
            IStoreService storeService,
            ISettingService settingService,
            IPaymentService paymentService,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            IStoreContext storeContext,
            ILogger logger,
            IWebHelper webHelper,
            PaymentSettings paymentSettings,
            PaySeraPaymentSettings payPalStandardPaymentSettings)
        {
            this._workContext = workContext;
            this._storeService = storeService;
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._storeContext = storeContext;
            this._logger = logger;
            this._webHelper = webHelper;
            this._paymentSettings = paymentSettings;
            this._paySeraPaymentSettings = payPalStandardPaymentSettings;
        }

        [ValidateInput(false)]
        public ActionResult CallBackData()
        {
            Dictionary<string, string> strs;
            string str;
            int num;
            string str1;
            ActionResult actionResult;
            if (base.Request.Params.Count > 0)
            {
                PaySeraPaymentProcessor PaySeraPaymentProcessor = this._paymentService.LoadPaymentMethodBySystemName("Payments.PaySera") as PaySeraPaymentProcessor;
                if ((PaySeraPaymentProcessor == null ||
                                    !PaySeraPaymentProcessor.IsPaymentMethodActive(_paymentSettings)
                                    || !PaySeraPaymentProcessor.PluginDescriptor.Installed))
                    throw new NopException("PaySera module cannot be loaded");
                {
                    string item = base.Request.Params["data"];
                    string item1 = base.Request.Params["ss1"];
                    string item2 = base.Request.Params["ss2"];
                    if ((string.IsNullOrEmpty(item) || string.IsNullOrEmpty(item1) ? false : !string.IsNullOrEmpty(item2)))
                    {
                        if (PaySeraPaymentProcessor.CallBackDataValid(item, item1, item2, out strs))
                        {
                            strs.TryGetValue("orderid", out str);
                            //if (!string.IsNullOrEmpty(str) && str.Length > 3)
                            //{
                            //    str = str.Substring(3);
                            //}
                            if (int.TryParse(str, out num))
                            {
                                Order orderById = this._orderService.GetOrderById(num);
                                var order = _orderService.GetOrderById(num);
                                if (orderById != null)
                                {
                                    StringBuilder stringBuilder = new StringBuilder();
                                    foreach (KeyValuePair<string, string> keyValuePair in strs)
                                    {
                                        stringBuilder.AppendLine(string.Concat(keyValuePair.Key, ": ", keyValuePair.Value));
                                    }
                                    int num1 = Convert.ToInt32(strs["amount"]);
                                    int num2 = Convert.ToInt32(strs["payamount"]);
                                    if (num2 >= num1)
                                    {
                                        order.OrderNotes.Add(new OrderNote()
                                        {
                                            Note = stringBuilder.ToString(),
                                            DisplayToCustomer = false,
                                            CreatedOnUtc = DateTime.UtcNow
                                        });
                                        _orderService.UpdateOrder(order);
                                        strs.TryGetValue("status", out str1);
                                        if ((str1 != "1" ? false : this._orderProcessingService.CanMarkOrderAsPaid(orderById)))
                                        {
                                            orderById.AuthorizationTransactionId = strs["requestid"];
                                            this._orderService.UpdateOrder(orderById);
                                            this._orderProcessingService.MarkOrderAsPaid(orderById);
                                            actionResult = base.Content("OK");
                                            return actionResult;
                                        }
                                    }
                                    else
                                    {
                                        string str2 = string.Format("PaySera. Returned order total {0} doesn't equal order total {1}", Math.Round(Convert.ToDouble(num2) / 100, 2), Math.Round(Convert.ToDouble(num1) / 100, 2));
                                        LoggingExtensions.Error(this._logger, str2, null, null);
                                        actionResult = base.Content("errorStr");
                                        return actionResult;
                                    }
                                }
                            }
                        }
                        actionResult = base.Content(string.Concat("Wrong call back data: ", base.Request.Params));
                        return actionResult;
                    }
                    else
                    {
                        LoggingExtensions.Error(this._logger, "PaySera call back data is invalid", null, null);
                        actionResult = base.Content("PaySera call back data is invalid");
                    }
                }
            }
            else
            {
                actionResult = base.Content(string.Concat("Wrong call back data: ", base.Request.Params));
                return actionResult;
            }
            return actionResult;
        }

        public ActionResult CancelOrder()
        {
            ActionResult action = base.RedirectToAction("Index", "Home", new { area = "" });
            return action;
        }

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var paySeraStandardPaymentSettings = _settingService.LoadSetting<PaySeraPaymentSettings>(storeScope);

            var model = new ConfigurationModel();

            model.UseSandbox = paySeraStandardPaymentSettings.UseSandbox;
            model.ProjectId = paySeraStandardPaymentSettings.ProjectId;
            model.ProjectPassword = paySeraStandardPaymentSettings.ProjectPassword;
            model.AdditionalFee = paySeraStandardPaymentSettings.AdditionalFee;
            model.AdditionalFeePercentage = paySeraStandardPaymentSettings.AdditionalFeePercentage;
            model.PayText = paySeraStandardPaymentSettings.PayText;

            model.ActiveStoreScopeConfiguration = storeScope;
            if (storeScope > 0)
            {
                model.UseSandbox_OverrideForStore = _settingService.SettingExists(paySeraStandardPaymentSettings, x => x.UseSandbox, storeScope);
                model.ProjectId_OverrideForStore = _settingService.SettingExists(paySeraStandardPaymentSettings, x => x.ProjectId, storeScope);
                model.ProjectPassword_OverrideForStore = _settingService.SettingExists(paySeraStandardPaymentSettings, x => x.ProjectPassword, storeScope);
                model.AdditionalFee_OverrideForStore = _settingService.SettingExists(paySeraStandardPaymentSettings, x => x.AdditionalFee, storeScope);
                model.AdditionalFeePercentage_OverrideForStore = _settingService.SettingExists(paySeraStandardPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
                model.PayText_OverrideForStore = _settingService.SettingExists(paySeraStandardPaymentSettings, x => x.PayText, storeScope);
            }
            return base.View("Nop.Plugin.Payments.PaySera.Views.PaymentPaySera.Configure", model);
        }

        [AdminAuthorize]
        [ChildActionOnly]
        [HttpPost]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var paySeraPaymentSettings = _settingService.LoadSetting<PaySeraPaymentSettings>(storeScope);

            //save settings
            paySeraPaymentSettings.UseSandbox = model.UseSandbox;
            paySeraPaymentSettings.ProjectId = model.ProjectId;
            paySeraPaymentSettings.ProjectPassword = model.ProjectPassword;
            paySeraPaymentSettings.AdditionalFee = model.AdditionalFee;
            paySeraPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
            paySeraPaymentSettings.PayText = model.PayText;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            if (model.UseSandbox_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(paySeraPaymentSettings, x => x.UseSandbox, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(paySeraPaymentSettings, x => x.UseSandbox, storeScope);

            if (model.ProjectId_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(paySeraPaymentSettings, x => x.ProjectId, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(paySeraPaymentSettings, x => x.ProjectId, storeScope);

            if (model.ProjectPassword_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(paySeraPaymentSettings, x => x.ProjectPassword, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(paySeraPaymentSettings, x => x.ProjectPassword, storeScope);

            if (model.AdditionalFee_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(paySeraPaymentSettings, x => x.AdditionalFee, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(paySeraPaymentSettings, x => x.AdditionalFee, storeScope);

            if (model.AdditionalFeePercentage_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(paySeraPaymentSettings, x => x.AdditionalFeePercentage, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(paySeraPaymentSettings, x => x.AdditionalFeePercentage, storeScope);

            if (model.PayText_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(paySeraPaymentSettings, x => x.PayText, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(paySeraPaymentSettings, x => x.PayText, storeScope);

            //now clear settings cache
            _settingService.ClearCache();

            return Configure();
        }

        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            return new ProcessPaymentRequest();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            return base.View("Nop.Plugin.Payments.PaySera.Views.PaymentPaySera.PaymentInfo");
        }

        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            return new List<string>();
        }
    }
}
