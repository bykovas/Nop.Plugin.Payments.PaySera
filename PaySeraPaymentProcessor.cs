using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Web;
using System.Web.Routing;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.PaySera.Controllers;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Tax;
namespace Nop.Plugin.Payments.PaySera
{
    class PaySeraPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly PaySeraPaymentSettings _paySeraPaymentSettings;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly HttpContextBase _httpContext;
        private readonly IWorkContext _workContext;

        #endregion

        #region Properies

        public bool SupportCapture
        {
            get { return false; }
        }

        public bool SupportPartiallyRefund
        {
            get { return false; }
        }

        public bool SupportRefund
        {
            get { return false; }
        }

        public bool SupportVoid
        {
            get { return false; }
        }

        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.NotSupported; }
        }

        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Redirection; }
        }

        public bool SkipPaymentInfo { get; private set; }

        #endregion

        #region Ctor

        public PaySeraPaymentProcessor(PaySeraPaymentSettings paySeraPaymentSettings,
            ISettingService settingService, ICurrencyService currencyService,
            CurrencySettings currencySettings, IWebHelper webHelper,
            ICheckoutAttributeParser checkoutAttributeParser, ITaxService taxService,
            IOrderTotalCalculationService orderTotalCalculationService, HttpContextBase httpContext, IWorkContext workContext)
        {
            this._paySeraPaymentSettings = paySeraPaymentSettings;
            this._settingService = settingService;
            this._webHelper = webHelper;
            this._orderTotalCalculationService = orderTotalCalculationService;
            this._httpContext = httpContext;
            this._workContext = workContext;
        }

        #endregion

        #region Utilities

        private string GetPaySeraUrl()
        {
            return "https://www.mokejimai.lt/pay/";
        }

        private string GetPaySeraAcceptUrl()
        {
            return _webHelper.GetStoreLocation(false) + "orderdetails/";
        }

        private string GetPaySeraCancelUrl()
        {
            return _webHelper.GetStoreLocation(false) + "Plugins/PaymentPaySera/CancelOrder";
        }

        private string GetPaySeraCallBackUrl()
        {
            return _webHelper.GetStoreLocation(false) + "Plugins/PaymentPaySera/CallBackData";
        }

        private string GetPaySeraLanguageCode()
        {
            string language = _workContext.WorkingLanguage.UniqueSeoCode;
            switch (language)
            {
                case "en":
                    return "ENG";

                case "lv":
                    return "LAV";

                case "es":
                    return "EST";

                case "ru":
                    return "RUS";

                case "de":
                    return "GER";

                case "pl":
                    return "POL";

                case "lt":
                    return "LIT";

                default:
                    return "END";

            }
        }

        private static string GetMd5Hash(HashAlgorithm md5Hash, string input)
        {
            var data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sBuilder = new StringBuilder();
            foreach (var t in data)
            {
                sBuilder.Append(t.ToString("x2"));
            }
            return sBuilder.ToString();
        }

        private static string CalculateMd5(string text)
        {
            MD5 mD5 = MD5.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            byte[] numArray = mD5.ComputeHash(bytes);
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < (int)numArray.Length; i++)
            {
                stringBuilder.Append(numArray[i].ToString("x2"));
            }
            return stringBuilder.ToString();
        }

        public bool CallBackDataValid(string data, string ss1, string ss2, out Dictionary<string, string> values)
        {
            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string str = HttpUtility.UrlDecode(PaySeraPaymentProcessor.DecodeBase64UrlSafe(data));
            if (str != null)
            {
                string[] strArrays = str.Split(new char[] { '&' });
                for (int i = 0; i < (int)strArrays.Length; i++)
                {
                    string str1 = strArrays[i].Trim();
                    int num = str1.IndexOf('=');
                    if (num >= 0)
                    {
                        values.Add(str1.Substring(0, num), str1.Substring(num + 1));
                    }
                }
            }
            return (VerifySs1(data, this._paySeraPaymentSettings.ProjectPassword, ss1) &&
                    PaySeraPaymentProcessor.VerifySs2(data, PaySeraPaymentProcessor.DecodeBase64UrlSafeAsByteArray(ss2),
                        PaySeraPaymentProcessor.GetPublicKeyRawDataFromPemFile(
                            PaySeraPaymentProcessor.DownloadPublicKey())));
        }

        private static string DecodeBase64(string encodedText)
        {
            byte[] numArray = Convert.FromBase64String(encodedText);
            return Encoding.UTF8.GetString(numArray);
        }

        public static string DecodeBase64UrlSafe(string encodedText)
        {
            encodedText = encodedText.Replace('-', '+');
            encodedText = encodedText.Replace('\u005F', '/');
            encodedText = encodedText.Replace("%3D", "=");
            return PaySeraPaymentProcessor.DecodeBase64(encodedText);
        }

        public static byte[] DecodeBase64UrlSafeAsByteArray(string encodedData)
        {
            encodedData = encodedData.Replace('-', '+');
            encodedData = encodedData.Replace('\u005F', '/');
            encodedData = encodedData.Replace("%3D", "=");
            return Convert.FromBase64String(encodedData);
        }

        public static bool VerifySs1(string data, string password, string signatureSs1)
        {
            bool flag = PaySeraPaymentProcessor.CalculateMd5(string.Concat(data, password)) == signatureSs1;
            return flag;
        }

        public static bool VerifySs2(string data, byte[] signature, byte[] publicKeyRawData)
        {
            X509Certificate2 x509Certificate2 = new X509Certificate2(publicKeyRawData);
            RSACryptoServiceProvider rSACryptoServiceProvider = new RSACryptoServiceProvider();
            rSACryptoServiceProvider.FromXmlString(x509Certificate2.PublicKey.Key.ToXmlString(false));
            bool flag = rSACryptoServiceProvider.VerifyData(Encoding.UTF8.GetBytes(data), CryptoConfig.MapNameToOID("SHA1"), signature);
            return flag;
        }

        public static string DownloadPublicKey()
        {
            string str;
            WebClient webClient = new WebClient();
            try
            {
                try
                {
                    str = webClient.DownloadString("http://downloads.webtopay.com/download/public.key");
                }
                catch (Exception exception1)
                {
                    Exception exception = exception1;
                    throw new Exception(string.Concat("Enable to download public key file :", exception.Message), exception);
                }
            }
            finally
            {
                if (webClient != null)
                {
                    ((IDisposable)webClient).Dispose();
                }
            }
            return str;
        }

        public static byte[] GetPublicKeyRawDataFromPemFile(string pemFileContents)
        {
            int num = pemFileContents.IndexOf("-----BEGIN CERTIFICATE-----", StringComparison.Ordinal);
            int num1 = pemFileContents.IndexOf("-----END CERTIFICATE-----", StringComparison.Ordinal);
            string str = pemFileContents.Substring(num + "-----BEGIN CERTIFICATE-----".Length, num1 - num - "-----END CERTIFICATE-----".Length - 2);
            return Convert.FromBase64String(str.Trim());
        }

        #endregion

        #region Methods

        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult { NewPaymentStatus = PaymentStatus.Pending };
            return result;
        }

        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var requestBuilder = new StringBuilder();

            //pass system info
            requestBuilder.AppendFormat("&projectid={0}", _paySeraPaymentSettings.ProjectId);
            requestBuilder.AppendFormat("&test={0}", _paySeraPaymentSettings.UseSandbox ? 1 : 0);
            requestBuilder.AppendFormat("&version={0}", HttpUtility.UrlEncode("1.6"));
            requestBuilder.AppendFormat("&developerid={0}", HttpUtility.UrlEncode("197522"));

            //pass urls
            requestBuilder.AppendFormat("&cancelurl={0}", HttpUtility.UrlEncode(GetPaySeraCancelUrl()));
            requestBuilder.AppendFormat("&accepturl={0}", HttpUtility.UrlEncode(GetPaySeraAcceptUrl() + postProcessPaymentRequest.Order.Id));
            requestBuilder.AppendFormat("&callbackurl={0}", HttpUtility.UrlEncode(GetPaySeraCallBackUrl()));

            //pass order total
            requestBuilder.AppendFormat("&orderid={0}", postProcessPaymentRequest.Order.Id);
            var orderTotal = Math.Round(postProcessPaymentRequest.Order.OrderTotal * postProcessPaymentRequest.Order.CurrencyRate, 2) * 100;
            requestBuilder.AppendFormat("&amount={0}", orderTotal.ToString("0.00", CultureInfo.InvariantCulture));

            //pass customer info
            if (!String.IsNullOrEmpty(HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.FirstName)))
                requestBuilder.AppendFormat("&p_firstname={0}",
                    HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.FirstName));

            if (!String.IsNullOrEmpty(HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.LastName)))
                requestBuilder.AppendFormat("&p_lastname={0}",
                    HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.LastName));

            if (!String.IsNullOrEmpty(HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.Email)))
                requestBuilder.AppendFormat("&p_email={0}",
                    HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.Email));

            if (!String.IsNullOrEmpty(HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.Address1)))
                requestBuilder.AppendFormat("&p_street={0}",
                    HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.Address1));

            if (!String.IsNullOrEmpty(HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.City)))
                requestBuilder.AppendFormat("&p_city={0}",
                    HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.City));

            if (!String.IsNullOrEmpty(postProcessPaymentRequest.Order.BillingAddress.ZipPostalCode))
                requestBuilder.AppendFormat("&p_zip={0}", postProcessPaymentRequest.Order.BillingAddress.ZipPostalCode);

            if (!String.IsNullOrEmpty(postProcessPaymentRequest.Order.BillingAddress.Country.TwoLetterIsoCode))
                requestBuilder.AppendFormat("&p_countrycode={0}",
                    postProcessPaymentRequest.Order.BillingAddress.Country.TwoLetterIsoCode);

            //pass payment variables
            if (!String.IsNullOrEmpty(GetPaySeraLanguageCode()))
                requestBuilder.AppendFormat("&lang={0}", GetPaySeraLanguageCode());

            if (!String.IsNullOrEmpty(postProcessPaymentRequest.Order.CustomerCurrencyCode))
                requestBuilder.AppendFormat("&currency={0}", postProcessPaymentRequest.Order.CustomerCurrencyCode);

            //create data for system
            var encbuff = Encoding.UTF8.GetBytes(requestBuilder.ToString());
            var data = Convert.ToBase64String(encbuff).Replace('/', '_').Replace('+', '-');
            var sign = GetMd5Hash(MD5.Create(), data + _paySeraPaymentSettings.ProjectPassword);

            //redirect to payment page
            _httpContext.Response.Redirect(GetPaySeraUrl() + "?data=" + data + "&sign=" + sign);
        }

        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            var result = this.CalculateAdditionalFee(_orderTotalCalculationService, cart,
                                                     _paySeraPaymentSettings.AdditionalFee,
                                                     _paySeraPaymentSettings.AdditionalFeePercentage);
            return result;
        }

        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return result;
        }

        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return result;
        }

        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return result;
        }

        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return result; ;
        }

        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");
            return !((DateTime.UtcNow - order.CreatedOnUtc).TotalMinutes < 1);
        }

        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentPaySera";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.PaySera.Controllers" }, { "area", null } };
        }

        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentPaySera";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.PaySera.Controllers" }, { "area", null } };
        }

        public Type GetControllerType()
        {
            return typeof(PaymentPaySeraController);
        }

        public override void Install()
        {
            //settings
            var settings = new PaySeraPaymentSettings()
            {
                UseSandbox = true,
                ProjectId = "123",
                ProjectPassword = "xxxxxxxxxxxxxxx",
                PayText = "Payment for goods or services on site [site_name] (order# [order_nr])."
            };
            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaySera.Fields.RedirectionTip", "After order confirmation you will be redirected to Mokejimai.lt site to complete the payment.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaySera.Fields.UseSandbox", "Use Sandbox");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaySera.Fields.UseSandbox.Hint", "Check to enable Sandbox (testing environment).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaySera.Fields.ProjectId", "Project ID:");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaySera.Fields.ProjectId.Hint", "Specify your Mokejimai.lt project ID.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaySera.Fields.ProjectPassword", "Project password");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaySera.Fields.ProjectPassword.Hint", "Specify you Mokejimai.lt project password");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaySera.Fields.AdditionalFee", "Additional fee");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaySera.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaySera.Fields.AdditionalFeePercentage", "Additinal fee. Use percentage");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaySera.Fields.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaySera.Fields.PayText", "Payment text");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaySera.Fields.PayText.Hint", "This text will be displayed and used as payment description, fields [site_name] and [order_nr] are mandatory, maximum lenght is 255 characters.");

            base.Install();
        }

        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<PaySeraPaymentSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.PaySera.Fields.RedirectionTip");
            this.DeletePluginLocaleResource("Plugins.Payments.PaySera.Fields.UseSandbox");
            this.DeletePluginLocaleResource("Plugins.Payments.PaySera.Fields.UseSandbox.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PaySera.Fields.ProjectId");
            this.DeletePluginLocaleResource("Plugins.Payments.PaySera.Fields.ProjectId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PaySera.Fields.ProjectPassword");
            this.DeletePluginLocaleResource("Plugins.Payments.PaySera.Fields.ProjectPassword.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PaySera.Fields.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.PaySera.Fields.AdditionalFee.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PaySera.Fields.AdditionalFeePercentage");
            this.DeletePluginLocaleResource("Plugins.Payments.PaySera.Fields.AdditionalFeePercentage.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PaySera.Fields.PayText");
            this.DeletePluginLocaleResource("Plugins.Payments.PaySera.Fields.PayText.Hint");

            base.Uninstall();
        }
        #endregion
    }
}
