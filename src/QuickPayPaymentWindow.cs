﻿using Dynamicweb.Caching;
using Dynamicweb.Configuration;
using Dynamicweb.Core;
using Dynamicweb.Ecommerce.Cart;
using Dynamicweb.Ecommerce.Orders;
using Dynamicweb.Ecommerce.Orders.Gateways;
using Dynamicweb.Ecommerce.Prices;
using Dynamicweb.Extensibility.AddIns;
using Dynamicweb.Extensibility.Editors;
using Dynamicweb.Frontend;
using Dynamicweb.Rendering;
using Dynamicweb.Security.UserManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dynamicweb.Ecommerce.CheckoutHandlers.QuickPayPaymentWindow;

/// <summary>
/// QuickPay Payment Window Checkout Handler
/// </summary>
[AddInName("QuickPay Payment Window"),
 AddInDescription("QuickPay Payment Window Checkout Handler")]
public class QuickPayPaymentWindow : CheckoutHandlerWithStatusPage, IParameterOptions, IRemotePartialCapture, ISavedCard, IRecurring, IPartialReturn, IFullReturn
{
    private PostModes postMode = PostModes.Auto;
    private static object lockObject = new object();
    private string postTemplate;
    private string cancelTemplate;
    private string errorTemplate;
    private const string orderCacheKey = "CheckoutHandler:Order.";
    private const string PostTemplateFolder = "eCom7/CheckoutHandler/QuickPayPaymentWindow/Post";
    private const string CancelTemplateFolder = "eCom7/CheckoutHandler/QuickPayPaymentWindow/Cancel";
    private const string ErrorTemplateFolder = "eCom7/CheckoutHandler/QuickPayPaymentWindow/Error";

    #region Addin parameters

    /// <summary>
    /// Gets or sets QuickPay Payment Window Merchant ID
    /// </summary>
    [AddInParameter("Merchant ID"), AddInParameterEditor(typeof(TextParameterEditor), "NewGUI=true; ")]
    public string Merchant { get; set; }

    /// <summary>
    /// Gets or sets QuickPay Payment Window User Agreement id
    /// </summary>
    [AddInParameter("Agreement ID"), AddInParameterEditor(typeof(TextParameterEditor), "NewGUI=true; ")]
    public string Agreement { get; set; }

    /// <summary>
    /// Gets or sets QuickPay Payment Window Api Key
    /// </summary>
    [AddInParameter("Api key"), AddInParameterEditor(typeof(TextParameterEditor), "NewGUI=true;size=150")]
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets QuickPay Payment Window Private key
    /// </summary>
    [AddInParameter("Private key"), AddInParameterEditor(typeof(TextParameterEditor), "NewGUI=true;size=150")]
    public string PrivateKey { get; set; }

    /// <summary>
    /// Gets or sets payment methods allowed to be used in Quick Pay service
    /// </summary> 
    [AddInParameter("Card type"), AddInParameterEditor(typeof(CheckListParameterEditor), "NewGUI=true; none=false; SortBy=Value;")]
    public string PaymentMethods { get; set; }

    /// <summary>
    /// Gets or sets post mode indicates how user will be redirected to Quick Pay service
    /// </summary>
    [AddInParameter("Post mode"), AddInParameterEditor(typeof(DropDownParameterEditor), "NewGUI=true; none=false; SortBy=Value;")]
    public string PostModeSelection
    {
        get => postMode.ToString();
        set
        {
            postMode = value switch
            {
                "Auto" => PostModes.Auto,
                "Template" => PostModes.Template,
                "Inline" => PostModes.Inline,
                _ => throw new NotSupportedException($"Unknown value of post mode was used. The value: {value}")
            };
        }
    }

    /// <summary>
    /// Gets or sets path to template that renders before user will be redirected to Quick Pay service
    /// </summary>
    [AddInParameter("Post template"), AddInParameterEditor(typeof(TemplateParameterEditor), $"folder=Templates/{PostTemplateFolder}; infoText=The Post template is used to post data to QuickPay when the render mode is Render template or Render inline form.;")]
    public string PostTemplate
    {
        get => TemplateHelper.GetTemplateName(postTemplate);
        set => postTemplate = value;
    }

    /// <summary>
    /// Gets or sets path to template that renders when user canceled payment on Quick Pay service
    /// </summary>
    [AddInParameter("Cancel template"), AddInParameterEditor(typeof(TemplateParameterEditor), $"folder=Templates/{CancelTemplateFolder}; infoText=The Cancel template is shown if the user cancels payment at some point during the checkout process\"Error template\".;")]
    public string CancelTemplate
    {
        get => TemplateHelper.GetTemplateName(cancelTemplate);
        set => cancelTemplate = value;
    }

    /// <summary>
    /// Gets or sets path to template that renders when error happened during Quick Pay service work
    /// </summary>
    [AddInParameter("Error template"), AddInParameterEditor(typeof(TemplateParameterEditor), $"folder=Templates/{ErrorTemplateFolder}; infoText=The Error template is shown if an error occurs.;")]
    public string ErrorTemplate
    {
        get => TemplateHelper.GetTemplateName(errorTemplate);
        set => errorTemplate = value;
    }

    /// <summary>
    /// Gets or sets QuickPay Payment Window Branding ID
    /// </summary>
    [AddInParameter("Branding id"), AddInParameterEditor(typeof(TextParameterEditor), "NewGUI=true; ")]
    public string Branding { get; set; }

    /// <summary>
    /// Gets or sets Google analitics tracking ID
    /// </summary>
    [AddInParameter("Google analytics tracking id"), AddInParameterEditor(typeof(TextParameterEditor), "NewGUI=true; ")]
    public string GoogleAnalyticsTracking { get; set; }

    /// <summary>
    /// Gets or sets Google analitics client ID
    /// </summary>
    [AddInParameter("Google analytics client id"), AddInParameterEditor(typeof(TextParameterEditor), "NewGUI=true; ")]
    public string GoogleAnalyticsClient { get; set; }

    /// <summary>
    /// Gets or sets value indicates if CheckoutHandler supports autocapture
    /// </summary>
    [AddInParameter("Auto capture"), AddInParameterEditor(typeof(YesNoParameterEditor), "infoText=Auto capture to capture orders as soon as they have been authorized.;")]
    public bool AutoCapture { get; set; }

    /// <summary>
    /// Gets or sets value indicates if CheckoutHandler supports autofee
    /// </summary>
    [AddInParameter("AutoFee"), AddInLabel("Add fee in payment gateway"), AddInParameterEditor(typeof(YesNoParameterEditor), "")]
    public bool AutoFee { get; set; }

    [AddInParameter("Test Mode"), AddInParameterEditor(typeof(YesNoParameterEditor), "")]
    public bool TestMode { get; set; }

    #endregion

    #region Form properties

    private static string LanguageCode
    {
        get
        {
            string currentLanguageCode = Environment.ExecutingContext.GetCulture(true).TwoLetterISOLanguageName;
            string[] supportedLanguageCodes =
            [
                "da",
                "de",
                "es",
                "fo",
                "fi",
                "fr",
                "kl",
                "it",
                "nl",
                "pl",
                "pt",
                "ru",
                "sv",
                "nb",
                "nn"
            ];
            if (!supportedLanguageCodes.Contains(currentLanguageCode))
                return "en";
            else
            {
                // TFS#19794  MVA: "I have talked to QuickPay supporter again, and finally he said that finnish UI is not translated (hence english UI).Swedish is translated, but it doesn't work with sv apparently - he said that we should try se."
                return currentLanguageCode switch
                {
                    "sv" => "se",
                    "nb" or "nn" => "no",
                    _ => currentLanguageCode
                };
            }
        }
    }

    private static string BaseUrl(Order order, bool headless = false)
    {
        bool disablePortNumber = SystemConfiguration.Instance.GetValue("/Globalsettings/System/http/DisableBaseHrefPort") == "True";
        string portString = Context.Current.Request.Url.IsDefaultPort || disablePortNumber ? string.Empty : $":{Context.Current.Request.Url.Port}";
        string pageId = Context.Current.Request["ID"] is null ? string.Empty : $"ID={Context.Current.Request["ID"]}&";

        if (headless)
            return $"{Context.Current.Request.Url.Scheme}://{Context.Current.Request.Url.Host}{portString}/dwapi/ecommerce/carts/callback?{OrderIdRequestName}={order.Id}";

        return $"{Context.Current.Request.Url.Scheme}://{Context.Current.Request.Url.Host}{portString}/Default.aspx?{pageId}{OrderIdRequestName}={order.Id}";
    }

    private static string CardSavedUrl(Order order, string cardId, string cardName) => $"{BaseUrl(order)}&QuickPayState=CardSaved&CardId={cardId}&CardName={cardName}";

    private static string ContinueUrl(Order order) => $"{BaseUrl(order)}&QuickPayState=Ok";

    private static string CancelUrl(Order order) => string.Format("{0}&QuickPayState=Cancel", BaseUrl(order));

    private static string CallbackUrl(Order order, bool headless = false) => string.Format("{0}&QuickPayState=Callback&redirect=false", BaseUrl(order, headless));

    private string GetServiceLink(ApiService service, string operationID = "", string parameters = "") => service switch
    {
        ApiService.CreatePayment => string.Format("https://api.quickpay.net/payments"),
        ApiService.CreateCard => string.Format("https://api.quickpay.net/cards"),
        ApiService.GetCardLink => string.Format("https://api.quickpay.net/cards/{0}/link", operationID),
        ApiService.GetCardData => string.Format("https://api.quickpay.net/cards/{0}", operationID),
        ApiService.GetCardToken => string.Format("https://api.quickpay.net/cards/{0}/tokens", operationID),
        ApiService.AuthorizePayment => string.Format("https://api.quickpay.net/payments/{0}/authorize{1}", operationID, parameters),
        ApiService.CapturePayment => string.Format("https://api.quickpay.net/payments/{0}/capture", operationID),
        ApiService.DeleteCard => string.Format("https://api.quickpay.net/cards/{0}/cancel", operationID),
        ApiService.RefundPayment => string.Format("https://api.quickpay.net/payments/{0}/refund{1}", operationID, parameters),
        ApiService.GetPaymentStatus => string.Format("https://api.quickpay.net/payments/{0}{1}", operationID, parameters),
        _ => string.Empty
    };

    #endregion

    /// <summary>
    /// Post values based on order to QuickPay
    /// </summary>
    /// <param name="order">The order to checkout</param>
    /// <param name="parameters">Checkout parameters</param>
    /// <remarks>
    ///		These are the fields that QuickPay should get
    ///		merchant_id 	                /[^d]$/  	            This is your Merchant Account id.
    ///		agreement_id 	                /[^d]$/  	            This is the User Agreement id. The checksum must be signed with the API-key belonging to this Agreement.
    ///		order_id 	                    /^[a-zA-Z0-9]{4,20}$/ 	This is the order is generated in your system.
    ///		language                        /^[a-z]{2}$/            Set the language of the user interface.
    ///		amount 	                        /^[0-9]{1,10}$/ 	    The amount defined in the request in its smallest unit. In example, 1 EUR is written 100.
    ///		currency 	                    /^[A-Z]{3}$/ 	        The payment currency as the 3-letter ISO 4217 alphabetical code.
    ///		continueurl                     /^https?://!            The customer will be redirected to this URL upon a succesful payment.
    ///		cancelurl                       /^https?://!            The customer will be redirected to this URL if the custo,er cancels the payment.
    ///		callbackurl                     /^https?://!            QuickPay will make a call back to this URL with the result of the payment. Overwrites the default callback url.
    ///		autocapture                     /^[0-1]{1}$/            If set to 1, the payment will be captured automatically.
    ///		autofee                         /^[0-1]{1}$/            If set to 1 the fee charged by the acquirer will be culculated and added to the transaction amount.
    ///		subscription                    /^[0-1]{1}$/            Create a subscription instead of a standart payment.
    ///		description                     /^[\w\s\-\.]{1-20}%/    A value by the merchant's own choise. Used for indentifying a subscription payment. It is required if "subscribe" is set.
    ///		payment_methods                 /^[a-zA-Z,\-]$/         Lock to some payment method(s). Multiple cart types allowed by comma separation.
    ///		branding_id                     /[^d]$/                 Use this brandidng. Overwrites the default branding.
    ///		google_analytics_tracking_id    /[^d]$/                 Your Google Analytics tracking ID.
    ///		google_analytics_client_id      /[^d]$/                 Your Google Analytics client ID.
    ///		checksum 	                    /^[a-z0-9]{32}$/ 	    The calculated checksum of your data./ 	
    /// </remarks>
    public override OutputResult BeginCheckout(Order order, CheckoutParameters parameters)
    {
        bool headless = parameters is not null ? true : false;
        string receiptUrl = parameters?.ReceiptUrl;
        string cancelUrl = parameters?.CancelUrl;

        try
        {
            LogEvent(order, "Checkout started");

            string cardName = order.SavedCardDraftName;
            string token = Converter.ToString(Context.Current.Request["card_token"]);

            if (!string.IsNullOrEmpty(token))
            {
                ProcessPayment(order, token, true);
                return PassToCart(order);
            }
            else if (order.DoSaveCardToken || !string.IsNullOrEmpty(cardName) || order.IsRecurringOrderTemplate)
                return CreateCard(cardName, order, headless, receiptUrl, cancelUrl);
            else
            {
                var formValues = new Dictionary<string, string>
                {
                    {"version", "v10"},
                    {"merchant_id", Merchant.Trim()},
                    {"agreement_id", Agreement.Trim()},
                    {"order_id", order.Id},
                    {"language", LanguageCode},
                    {"amount", order.Price.PricePIP.ToString()},
                    {"currency", order.Price.Currency.Code},
                    {"continueurl", receiptUrl ?? ContinueUrl(order)},
                    {"cancelurl", cancelUrl ?? CancelUrl(order)},
                    {"callbackurl", CallbackUrl(order, headless)},
                    {"autocapture", AutoCapture ? "1" : "0"},
                    {"autofee", AutoFee ? "1" : "0"},
                    {"payment_methods", PaymentMethods},
                    {"branding_id", Branding},
                    {"google_analytics_tracking_id", GoogleAnalyticsTracking},
                    {"google_analytics_client_id", GoogleAnalyticsClient}
                };

                formValues.Add("checksum", ComputeHash(ApiKey, GetMacString(formValues)));

                switch (postMode)
                {
                    case PostModes.Auto:
                        LogEvent(order, "Autopost to QuickPay");
                        return GetSubmitFormResult("https://payment.quickpay.net", formValues);

                    case PostModes.Template:
                        LogEvent(order, "Render template");

                        var formTemplate = new Template(TemplateHelper.GetTemplatePath(PostTemplate, PostTemplateFolder));
                        foreach (var formValue in formValues)
                            formTemplate.SetTag(string.Format("QuickPayPaymentWindow.{0}", formValue.Key), formValue.Value);

                        return new ContentOutputResult { Content = formTemplate.Output() };

                    default:
                        var errorMessage = string.Format("Unhandled post mode: '{0}'", postMode);
                        LogError(order, errorMessage);
                        return PrintErrorTemplate(order, errorMessage);

                }
            }
        }
        catch (ThreadAbortException ex)
        {
            return PrintErrorTemplate(order, $"Thread abort exception: {ex.Message}");
        }
        catch (Exception ex)
        {
            LogError(order, ex, "Unhandled exception with message: {0}", ex.Message);
            return PrintErrorTemplate(order, ex.Message);
        }
    }

    /// <summary>
    ///  Handles redirect from QuickPay with state
    /// </summary>
    /// <param name="order">Order for processing</param>
    /// <returns>String representation of template output</returns>
    public override OutputResult HandleRequest(Order order)
    {
        try
        {
            LogEvent(null, "Redirected to QuickPay CheckoutHandler");

            switch (Converter.ToString(Context.Current.Request["QuickPayState"]))
            {
                case "Ok":
                    return StateOk(order);
                case "CardSaved":
                    return StateCardSaved(order);
                case "Cancel":
                    return StateCancel(order);
                default:
                    Callback(order);
                    return ContentOutputResult.Empty;
            }
        }
        catch (ThreadAbortException ex)
        {
            return PrintErrorTemplate(order, $"Thread abort exception: {ex.Message}");
        }
        catch (Exception ex)
        {
            LogError(order, ex, "Unhandled exception with message: {0}", ex.Message);
            return PrintErrorTemplate(order, ex.Message);
        }
    }

    private OutputResult StateOk(Order order)
    {
        LogEvent(order, "State ok");

        if (!order.Complete)
        {
            OperationStatus status;
            int maxAttempts = 62;
            int attempts = 0;
            do
            {
                Thread.Sleep(1000);
                status = GetLastOperationStatus(order);
                attempts++;
            }
            while (status.IsPending && attempts < maxAttempts);
        }

        lock (lockObject)
        {
            if (order.Complete)
                return PassToCart(order);

            if (Common.Context.Cart is null)
                CheckoutDone(order);
        }

        var errorMessage = "Called State ok, but order is not set complete - this should have happened in the callback.";
        LogError(order, errorMessage);

        return PrintErrorTemplate(order, errorMessage);
    }

    private OutputResult StateCardSaved(Order order)
    {
        LogEvent(order, "QuickPay Card Authorized successfully");

        string cardId = Context.Current.Request["CardId"] ?? "";
        string cardName = Context.Current.Request["CardName"] ?? "";

        var cardData = Converter.Deserialize<Dictionary<string, object>>(ExecuteRequest(order, ApiService.GetCardData, cardId));
        if (cardData.ContainsKey("metadata"))
        {
            var metadata = Converter.Deserialize<Dictionary<string, object>>(cardData["metadata"].ToString());
            var cardType = Converter.ToString(metadata["brand"]);
            var cardNubmer = order.TransactionCardNumber = Converter.ToString(metadata["last4"]).PadLeft(16, 'X');
            int? expirationMonth = Converter.ToNullableInt32(metadata["exp_month"]);
            int? expirationYear = Converter.ToNullableInt32(metadata["exp_year"]);

            if (UserContext.Current.User is User user)
            {
                var paymentCard = new PaymentCardToken
                {
                    UserID = user.ID,
                    PaymentID = order.PaymentMethodId,
                    Name = cardName,
                    CardType = cardType,
                    Identifier = cardNubmer,
                    Token = cardId,
                    UsedDate = DateTime.Now,
                    ExpirationMonth = expirationMonth,
                    ExpirationYear = expirationYear
                };
                Services.PaymentCard.Save(paymentCard);
                order.SavedCardId = paymentCard.ID;
                Services.Orders.Save(order);
                LogEvent(order, "Saved Card created");
                UseSavedCardInternal(order, paymentCard);
            }
            else
            {
                order.TransactionToken = cardId;
                Services.Orders.Save(order);
                ProcessPayment(order, cardId);
            }

            if (!order.Complete)
                return PrintErrorTemplate(order, "Some error happened on creating payment using saved card", ErrorType.SavedCard);

            return PassToCart(order);
        }
        else
        {
            LogError(order, "Unable to get card meta data from QuickPay");
        }

        CheckoutDone(order);

        var errorMessage = "Card saved but payment failed";
        LogError(order, errorMessage);
        return PrintErrorTemplate(order, errorMessage);
    }

    private OutputResult StateCancel(Order order)
    {
        LogEvent(order, "State cancel");

        lock (lockObject)
        {
            order.TransactionStatus = "Cancelled";
            Services.Orders.Save(order);

            CheckoutDone(order);
        }

        var cancelTemplate = new Template(TemplateHelper.GetTemplatePath(CancelTemplate, CancelTemplateFolder));
        var orderRenderer = new Frontend.Renderer();
        orderRenderer.RenderOrderDetails(cancelTemplate, order, true);

        return new ContentOutputResult { Content = cancelTemplate.Output() };
    }

    private void SetOrderSucceeded(Order order, bool success)
    {
        if (success)
        {
            order.TransactionAmount = order.Price.PricePIP / 100d;
            order.TransactionStatus = TestMode ? "TEST Succeeded" : "Succeeded";
        }
        else
        {
            order.TransactionStatus = "Failed";
        }
        Services.Orders.Save(order);
    }

    private void Callback(Order order)
    {
        lock (lockObject)
        {
            LogEvent(order, "Callback started");
            string callbackResponce;

            using (StreamReader reader = new StreamReader(Context.Current.Request.InputStream, Encoding.UTF8))
            {
                callbackResponce = reader.ReadToEndAsync().GetAwaiter().GetResult();
            }

            CheckDataResult result = CheckData(order, callbackResponce ?? string.Empty, order.Price.PricePIP);
            string resultInfo = result switch
            {
                //ViaBill autocapture starts callback.
                CheckDataResult.FinalCaptureSucceed or CheckDataResult.SplitCaptureSucceed => "Autocapture callback completed successfully",
                CheckDataResult.CallbackSucceed => "Callback completed successfully",
                _ => "Some error occurred during callback process, check error logs"
            };

            LogEvent(order, resultInfo);
        }
    }

    #region IDropDownOptions
    public IEnumerable<ParameterOption> GetParameterOptions(string parameterName)
    {
        try
        {
            switch (parameterName)
            {
                case "Post mode":
                    return new List<ParameterOption>
                    {
                        new("Auto post (does not use the template)", "Auto") { Hint = "do not use the template." },
                        new("Render template", "Template") { Hint = "this allows you to customize the information sent to QuickPay via the post template." },
                        new("Render inline form", "Inline")
                    };
                case "Card type":
                    return GetCardTypes(false, true).Select(pair => new ParameterOption(pair.Value, pair.Key)).ToList();

                default:
                    throw new ArgumentException(string.Format("Unknown dropdown name: '{0}'", parameterName));
            }
        }
        catch (Exception ex)
        {
            LogError(null, ex, "Unhandled exception with message: {0}", ex.Message);
            return null;
        }
    }

    #endregion

    #region IRemoteCapture

    /// <summary>
    /// Send capture request to transaction service
    /// </summary>
    /// <param name="order">Order to be captured</param>
    /// <returns><see cref="OrderCaptureInfo">Order Cupture Info object</see> represents capture information about order</returns>
    public OrderCaptureInfo Capture(Order order)
    {
        return Capture(order, order.Price.PricePIP, false);
    }

    /// <summary>
    /// Send capture request to transaction service
    /// </summary>
    /// <param name="order">Order to be captured</param>
    /// <param name="amount">Amount to be transferred. The amount must be given in smallest possible unit, e.g. cents 100 = USD 1.</param>
    /// <param name="final">Shows that this step is final</param>
    /// <returns><see cref="OrderCaptureInfo">Order Cupture Info object</see> represents capture information about order</returns>
    public OrderCaptureInfo Capture(Order order, long amount, bool final)
    {
        try
        {
            // Check order
            if (order is null)
            {
                LogError(null, "Order not set");
                return new OrderCaptureInfo(OrderCaptureInfo.OrderCaptureState.Failed, "Order not set");
            }
            else if (string.IsNullOrEmpty(order.Id))
            {
                LogError(null, "Order id not set");
                return new OrderCaptureInfo(OrderCaptureInfo.OrderCaptureState.Failed, "Order id not set");
            }
            else if (string.IsNullOrEmpty(order.TransactionNumber))
            {
                LogError(null, "Transaction number not set");
                return new OrderCaptureInfo(OrderCaptureInfo.OrderCaptureState.Failed, "Transaction number not set");
            }
            else if (order.Price.PricePIP < amount)
            {
                LogError(null, "Amount to capture should be less of order total");
                return new OrderCaptureInfo(OrderCaptureInfo.OrderCaptureState.Failed, "Amount to capture should be less of order total");
            }

            string content = string.Format(@"{{""amount"": {0}}}", amount);
            var responseText = ExecuteRequest(order, ApiService.CapturePayment, order.TransactionNumber, content);

            order.GatewayResult = responseText;
            switch (CheckData(order, responseText, amount))
            {
                case CheckDataResult.FinalCaptureSucceed:
                    {
                        LogEvent(order, "Capture successful", DebuggingInfoType.CaptureResult);
                        return new OrderCaptureInfo(OrderCaptureInfo.OrderCaptureState.Success, "Capture successful");
                    }
                case CheckDataResult.SplitCaptureSucceed:
                    if (final)
                    {
                        LogEvent(order, string.Format("Message=\"{0}\" Amount=\"{1:f2}\"", "Split capture(final)", amount / 100f), DebuggingInfoType.CaptureResult);
                        return new OrderCaptureInfo(OrderCaptureInfo.OrderCaptureState.Success, "Split capture successful");
                    }
                    else
                    {
                        LogEvent(order, string.Format("Message=\"{0}\" Amount=\"{1:f2}\"", "Split capture", amount / 100f), DebuggingInfoType.CaptureResult);
                        return new OrderCaptureInfo(OrderCaptureInfo.OrderCaptureState.Split, "Split capture successful");
                    }
                default:
                    LogError(order, "Incorrect response received from QuickPay");
                    return new OrderCaptureInfo(OrderCaptureInfo.OrderCaptureState.Failed, "Incorrect response received from QuickPay");
            }

        }
        catch (Exception ex)
        {
            LogError(order, ex, "Unexpected error during capture: {0}", ex.Message);
            return new OrderCaptureInfo(OrderCaptureInfo.OrderCaptureState.Failed, "Unexpected error during capture");
        }
    }

    /// <summary>
    /// Shows if capture supported
    /// </summary>
    /// <param name="order">This order object</param>
    /// <returns>'true' if order transaction card type is defined</returns>
    public bool CaptureSupported(Order order)
    {
        return true;
    }

    /// <summary>
    /// Shows if partial capture of the order supported
    /// </summary>
    /// <param name="order">Instance of order</param>
    /// <returns>True, if partial capture of the order is supported</returns>
    public bool SplitCaptureSupported(Order order)
    {
        return true;
    }

    #endregion

    #region ISavedCard interface

    /// <summary>
    /// Deletes saved card
    /// </summary>
    /// <param name="savedCardID">Identifier of saved card to be deleted</param>
    public void DeleteSavedCard(int savedCardID)
    {
        var savedCard = Services.PaymentCard.GetById(savedCardID);
        if (savedCard is not null)
        {
            var cardID = savedCard.Token;
            try
            {
                ExecuteRequest(null, ApiService.DeleteCard, cardID);
            }
            catch (Exception ex)
            {
                LogError(null, ex, "Delete saved card exception: {0}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Directs checkout handler to use saved card
    /// </summary>
    /// <param name="order">Order that should be processed using saved card information</param>
    /// <returns>Empty string, if operation succeeded, otherwise string template with exception mesage</returns>
    public string UseSavedCard(Order order)
    {
        try
        {
            UseSavedCardInternal(order);
            if (!order.Complete)
                return PrintErrorTemplate(order, "Some error happened on creating payment using saved card", ErrorType.SavedCard).Content;

            /*PassToCart part doesn't work because of changes in Redirect behavior.
            * We need to return RedirectOutputResult as OutputResult, and handle output result to make it work.
            * It means, that we need to change ISavedCard.UseSavedCard method, probably create new one (with OutputResult as returned type)
            * To make it work (temporarily), we use Response.Redirect here                 
            */
            if (PassToCart(order) is RedirectOutputResult redirectResult)
                Context.Current.Response.Redirect(redirectResult.RedirectUrl, redirectResult.IsPermanent);

            return string.Empty;
        }
        catch (Exception ex)
        {
            LogEvent(order, ex.Message, DebuggingInfoType.UseSavedCard);
            return PrintErrorTemplate(order, ex.Message).Content;
        }
    }

    /// <summary>
    /// Shows if order supports saving card
    /// </summary>
    /// <param name="order">Instance of order</param>
    /// <returns>True, if saving card is supported</returns>
    public bool SavedCardSupported(Order order)
    {
        Dictionary<string, string> recurringTypes = GetCardTypes(true, false);
        var selectedMethods = new HashSet<string>(PaymentMethods.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
        return selectedMethods.All(method => recurringTypes.ContainsKey(method));
    }

    private void UseSavedCardInternal(Order order, PaymentCardToken savedCard = null)
    {
        savedCard ??= Services.PaymentCard.GetById(order.SavedCardId);
        if (savedCard is null || order.CustomerAccessUserId != savedCard.UserID)
            throw new PaymentCardTokenException("Token is incorrect.");

        if (order.IsRecurringOrderTemplate)
        {
            // Redirect to cart
            order.TransactionCardType = savedCard.CardType;
            order.TransactionCardNumber = savedCard.Identifier;
            SetOrderComplete(order);
            CheckoutDone(order);
        }
        else
        {
            ProcessPayment(order, savedCard.Token);
        }
    }

    #endregion         

    #region IRecurring

    /// <summary>
    /// Creates new payment for recurring order
    /// </summary>
    /// <param name="order">recurring order to be used for payment</param>
    /// <param name="initialOrder">Base order, used for creating current recurring order</param>
    public void Recurring(Order order, Order initialOrder)
    {
        if (order is not null)
        {
            try
            {
                UseSavedCardInternal(order);
                LogEvent(order, "Recurring succeeded");
            }
            catch (Exception ex)
            {
                LogError(order, ex, "Recurring failed with the message: {0}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Shows if order supports recurring payments
    /// </summary>
    /// <param name="order">Instance of order</param>
    /// <returns>True, if recurring payments are supported</returns>
    public bool RecurringSupported(Order order) => SavedCardSupported(order);

    #endregion

    #region Private methods

    private ContentOutputResult PrintErrorTemplate(Order order, string errorMessage, ErrorType errorType = ErrorType.Undefined)
    {
        LogEvent(order, "Printing error template");
        var errorTemplate = new Template(TemplateHelper.GetTemplatePath(ErrorTemplate, ErrorTemplateFolder));
        var orderRenderer = new Frontend.Renderer();
        errorTemplate.SetTag("CheckoutHandler:ErrorType", errorType.ToString());
        errorTemplate.SetTag("CheckoutHandler:ErrorMessage", errorMessage);
        orderRenderer.RenderOrderDetails(errorTemplate, order, true);

        return new ContentOutputResult
        {
            Content = errorTemplate.Output()
        };
    }

    private OutputResult CreateCard(string cardName, Order order, bool headless, string receiptUrl, string cancelUrl)
    {
        string errorMessage = "Error happened during creating QuickPay card";

        if (string.IsNullOrEmpty(cardName))
            cardName = order.Id;

        var response = Converter.Deserialize<Dictionary<string, object>>(ExecuteRequest(order, ApiService.CreateCard));
        LogEvent(order, "QuickPay Card created");

        if (response.ContainsKey("id"))
        {
            var cardID = Converter.ToString(response["id"]);
            if (!string.IsNullOrEmpty(cardID))
            {
                string reqbody = string.Format(@"{{""agreement_id"": ""{0}"", ""language"": ""{1}"", ""continueurl"": ""{2}"", ""cancelurl"": ""{3}"", ""callbackurl"": ""{4}"", ""payment_methods"": ""{5}"", ""google_analytics_tracking_id"": ""{6}"", ""google_analytics_client_id"": ""{7}""}}",
                    Agreement.Trim(), LanguageCode, receiptUrl ?? CardSavedUrl(order, cardID, cardName), cancelUrl ?? CancelUrl(order), CallbackUrl(order, headless), PaymentMethods, GoogleAnalyticsTracking, GoogleAnalyticsClient);
                int brandingId = Converter.ToInt32(Branding);
                if (brandingId > 0)
                    reqbody = string.Format("{0},\"branding_id\": {1}}}", reqbody.Substring(0, reqbody.Length - 1), brandingId);

                response = Converter.Deserialize<Dictionary<string, object>>(ExecuteRequest(order, ApiService.GetCardLink, cardID, reqbody));
                LogEvent(order, "QuickPay Card authorize link received");

                if (response.ContainsKey("url"))
                {
                    Services.Orders.Save(order);
                    string redirectUrl = Converter.ToString(response["url"]);
                    return new RedirectOutputResult { RedirectUrl = redirectUrl };
                }
                else
                {
                    errorMessage = string.Format("Bad QuickPay response on getting payment url. Response text{0}", response.ToString());
                    LogError(order, errorMessage);
                }
            }
            else
            {
                errorMessage = string.Format("QuickPay response doesn't contains value for card id. Response text:{0}", response.ToString());
                LogError(order, "QuickPay response doesn't contains value for card id. Response text:{0}", response.ToString());
            }
        }
        else
        {
            errorMessage = string.Format("Bad QuickPay response on creating card. Response text{0}", response.ToString());
            LogError(order, "Bad QuickPay response on creating card. Response text{0}", response.ToString());
        }
        return PrintErrorTemplate(order, errorMessage);
    }

    private void ProcessPayment(Order order, string savedCardToken, bool isRawToken = false)
    {
        if (order.Complete)
            return;

        Dictionary<string, object> response;

        var token = savedCardToken;
        if (!isRawToken)
        {
            response = Converter.Deserialize<Dictionary<string, object>>(ExecuteRequest(order, ApiService.GetCardToken, savedCardToken));
            token = Converter.ToString(response["token"]);
        }
        LogEvent(order, "QuickPay card token recieved");

        string formValues = string.Format(@"{{""order_id"": ""{0}"", ""currency"": ""{1}""}}", order.Id, order.CurrencyCode);
        response = Converter.Deserialize<Dictionary<string, object>>(ExecuteRequest(order, ApiService.CreatePayment, "", formValues));
        LogEvent(order, "QuickPay new payment created");
        var paymentID = Converter.ToString(response["id"]);

        formValues = string.Format("amount={0}&card[token]={1}&auto_capture={2}", order.Price.PricePIP, token, (AutoCapture ? "1" : "0"));
        var respText = ExecuteRequest(order, ApiService.AuthorizePayment, paymentID, formValues, "?synchronized");
        LogEvent(order, "QuickPay payment authorized");

        CheckDataResult result = CheckData(order, respText, order.Price.PricePIP, false);

        if (result == CheckDataResult.CallbackSucceed)
        {
            LogEvent(order, "Callback completed successfully");
        }
        else
        {
            LogEvent(order, "Some error occurred during callback process, check error logs");
            order.TransactionStatus = "Failed";
            CheckoutDone(order);
        }
    }

    private string ExecuteRequest(Order order, ApiService apiService, string serviceObjID = "", string body = "", string serviceParameters = "")
    {
        try
        {
            if (apiService is not ApiService.DeleteCard)
            {
                // Check order
                if (order is null)
                {
                    LogError(null, "Order not set");
                    throw new Exception("Order not set");
                }
                else if (string.IsNullOrEmpty(order.Id))
                {
                    LogError(null, "Order id not set");
                    throw new Exception("Order id not set");
                }
            }

            using (var handler = GetHandler())
            {
                using (var client = new HttpClient(handler))
                {
                    handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                    handler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

                    string apiKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Format(":{0}", ApiKey)));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", apiKey);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                    client.DefaultRequestHeaders.Add("Accept-Version", "v10");

                    string contentType = apiService is ApiService.AuthorizePayment
                        ? "application/x-www-form-urlencoded"
                        : "application/json";
                    var content = new StringContent(body, Encoding.UTF8, contentType);

                    string requestUri = GetServiceLink(apiService, serviceObjID, serviceParameters);
                    Task<HttpResponseMessage> requestTask = apiService switch
                    {
                        ApiService.GetCardLink => client.PutAsync(requestUri, content),
                        ApiService.GetCardData or ApiService.GetPaymentStatus => client.GetAsync(requestUri),
                        _ => client.PostAsync(requestUri, content)
                    };

                    try
                    {
                        using (HttpResponseMessage response = requestTask.GetAwaiter().GetResult())
                        {
                            LogEvent(order, "Remote server response: HttpStatusCode = {0}, HttpStatusDescription = {1}",
                                response.StatusCode, response.ReasonPhrase);
                            string responseText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            LogEvent(order, "Remote server ResponseText: {0}", responseText);

                            return responseText;
                        }
                    }
                    catch (Exception ex)
                    {
                        string errorMessage = "Unable to make http request to QuickPay";
                        LogError(order, ex, $"{errorMessage}: {ex.Message}");

                        if (ex is HttpRequestException requestException)
                            throw new Exception($"{errorMessage}. Error: {requestException.StatusCode}");
                        throw new Exception(errorMessage);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            string errorMessage = "Unexpected error during request to QuickPay";
            LogError(order, ex, $"{errorMessage}: {ex.Message}");

            throw new Exception(errorMessage);
        }

        HttpClientHandler GetHandler() => new()
        {
            AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
        };
    }

    /// <remarks>
    ///  These are the fields that QuickPay should send
    ///	      headers
    ///	 QuickPay-Checksum-SHA256             Checksum of the entire raw callback request body - using HMAC with SHA256 as the cryptographic hash function. The checksum is signed using the Account's private key. We strongly recommend that you validate the checksum to ensure that the request is authentic.
    ///	 QuickPay-Resource-Type               The type of resource that was created, changed or deleted
    ///	 QuickPay-Account-ID                  The account id of the resource owner - useful if receiving callbacks for several systems on the same url
    ///	 QuickPay-API-Version                 API version of the callback-generating request
    ///	      body
    ///	 id                                   id
    ///	 order_id                             The order id that is proceeding
    ///	 accepted                             If transaction accepted
    ///	 test_mode                            If is in test mode
    ///	 branding_id                          The branding id
    ///	 variables                            Variables?
    ///	 acquirer                             The accuirer system
    ///	 operations                           An array of objects with operations
    ///	      id                              Operation id
    ///	      type                            Operation type
    ///	      amount                          The amont
    ///	      pending                         If is in pending status
    ///	      qp_status_code                  Quick pay status code
    ///	      qp_status_msg                   Quick pay status message
    ///	      aq_status_code                  Accuirer status code
    ///	      aq_status_msg                   Accuirer status message
    ///	      data                            Data object
    ///	      created_at                      Created date
    ///	 metadata                             An object with metadata
    ///	      type                            Card or phone
    ///	      brand                           Brand of operation
    ///	      last4                           Last 4 digits
    ///	      exp_month                       Expiration month
    ///	      exp_year                        Expiration year
    ///	      country                         Country
    ///	      is_3d_secure                    If is 3d secure
    ///	      customer_ip                     Customer ip
    ///	      customer_country                Customer county
    ///	 created_at                           Transaction creation time
    ///	 balance                              Captured balance
    ///	 currency                             Currency code
    /// </remarks>
    private CheckDataResult CheckData(Order order, string responsetext, long transactionAmount, bool doCheckSum = true)
    {
        LogEvent(order, "Response validation started");

        var quickpayResponse = Converter.Deserialize<Dictionary<string, object>>(responsetext);
        var operations = Converter.Deserialize<Dictionary<string, object>[]>(Converter.ToString(quickpayResponse["operations"]));
        var metadata = Converter.Deserialize<Dictionary<string, object>>(Converter.ToString(quickpayResponse["metadata"]));

        Dictionary<string, object> operation;
        if (!order.Complete)
        {
            operation = operations.LastOrDefault(op => Converter.ToString(op["type"]) == "authorize");
        }
        else
        {
            operation = operations.Last();
        }

        if (operation is null)
        {
            LogError(order, "QuickPay returned no transaction information");
            return CheckDataResult.Error;
        }

        var isAccepted = Converter.ToBoolean(quickpayResponse["accepted"]);
        var quickPayStatusCode = Converter.ToString(operation["qp_status_code"]);

        // Skip "Forced 3DS test operation" callback - it have no information we need to know / update
        // A new callback will be initiated after user passed the 3D Secure
        if (!isAccepted && "30100".Equals(quickPayStatusCode, StringComparison.OrdinalIgnoreCase))
        {
            return CheckDataResult.CallbackSucceed;
        }

        var requiredFields = new List<string>
        {
            "id",
            "order_id",
            "accepted",
            "operations",
            "metadata",
            "created_at"
        };
        var result = CheckDataResult.Error;

        foreach (var key in requiredFields.Where(x => !quickpayResponse.ContainsKey(x) || quickpayResponse[x] == null))
        {
            LogError(
                order,
                "The expected parameter from QuickPay '{0}' was not send",
                key
            );
            return CheckDataResult.Error;
        }

        if (!isAccepted)
        {
            LogError(
                order,
                "The quick pay did not accept the transaction"
            );
            return CheckDataResult.Error;
        }

        if (Converter.ToString(quickpayResponse["order_id"]) != order.Id)
        {
            LogError(
                order,
                "The ordernumber returned from callback does not match with the ordernumber set on the order: Callback: '{0}', order: '{1}'",
                quickpayResponse["order_id"], order.Id
            );
            return CheckDataResult.Error;
        }

        LogEvent(
            order,
            string.Format("Current QuickPay operation is {0}",
            Converter.ToString(operation["type"]))
        );

        switch (Converter.ToString(operation["type"]))
        {
            case "authorize":
                if (Converter.ToString(quickpayResponse["type"]) != "Payment")
                {
                    LogError(
                        order,
                        "Unsupported transaction type: {0}",
                        Converter.ToString(quickpayResponse["type"])
                    );
                    return CheckDataResult.Error;
                }

                if (Converter.ToString(quickpayResponse["currency"]) != order.Price.Currency.Code)
                {
                    LogError(
                        order,
                            "The currency return from callback does not match the amount set on the order: Callback: {0}, order: {1}",
                            quickpayResponse["currency"], order.Price.Currency.Code
                    );
                    return CheckDataResult.Error;
                }

                if (doCheckSum)
                {
                    var calculatedHash = ComputeHash(PrivateKey, responsetext);
                    var callbackCheckSum = Context.Current.Request.Headers["QuickPay-Checksum-Sha256"];

                    if (!calculatedHash.Equals(callbackCheckSum, StringComparison.CurrentCultureIgnoreCase))
                    {
                        LogError(
                            order,
                            "The HMAC checksum returned from callback does not match: Callback: {0}, calculated: {1}",
                            callbackCheckSum, calculatedHash
                        );
                        return CheckDataResult.Error;
                    }
                }

                if (Converter.ToString(operation["amount"]) != order.Price.PricePIP.ToString())
                {
                    LogError(
                        order,
                        "The amount returned from callback does not match the amount set on the order: Callback: {0}, order: {1}",
                        Converter.ToString(operation["amount"]), order.Price.PricePIP
                    );
                    return CheckDataResult.Error;
                }

                if (Converter.ToBoolean(quickpayResponse["test_mode"]) && !TestMode)
                {
                    LogError(
                        order,
                        "Test card info was used for payment. To make test payment enable test mode in backoffice"
                    );
                    return CheckDataResult.Error;
                }

                // Check the state of the callback
                // 20000	Approved
                // 40000	Rejected By Acquirer
                // 40001	Request Data Error
                // 50000	Gateway Error
                // 50300	Communications Error (with Acquirer)
                switch (quickPayStatusCode)
                {
                    case "20000":
                        break;

                    case "40000":
                        LogEvent(
                            order,
                            "Not approved: QuickPay response: 'Rejected by acquirer', qp_status_code: {0}, qp_status_msg: {1}, aq_status_code: '{2}', aq_status_msg: '{3}'.",
                            quickPayStatusCode, Converter.ToString(operation["qp_status_msg"]), Converter.ToString(operation["aq_status_code"]), Converter.ToString(operation["aq_status_msg"])
                        );
                        return CheckDataResult.Error;

                    case "40001":
                        LogEvent(
                            order,
                            "Not approved: QuickPay response: 'Request Data Error', qp_status_code: {0}, qp_status_msg: {1}, aq_status_code: '{2}', aq_status_msg: '{3}'.",
                            quickPayStatusCode, Converter.ToString(operation["qp_status_msg"]), Converter.ToString(operation["aq_status_code"]), Converter.ToString(operation["aq_status_msg"])
                        );
                        return CheckDataResult.Error;

                    case "50000":
                        LogEvent(
                            order,
                            "Not approved: QuickPay response: 'Gateway Error', qp_status_code: {0}, qp_status_msg: {1}, aq_status_code: '{2}', aq_status_msg: '{3}'.",
                            quickPayStatusCode, Converter.ToString(operation["qp_status_msg"]), Converter.ToString(operation["aq_status_code"]), Converter.ToString(operation["aq_status_msg"])
                        );
                        return CheckDataResult.Error;

                    case "50300":
                        LogEvent(
                            order,
                            "Not approved: QuickPay response: 'Communications Error (with Acquirer)', qp_status_code: {0}, qp_status_msg: {1}, aq_status_code: '{2}', aq_status_msg: '{3}'.",
                            quickPayStatusCode, Converter.ToString(operation["qp_status_msg"]), Converter.ToString(operation["aq_status_code"]), Converter.ToString(operation["aq_status_msg"])
                        );
                        return CheckDataResult.Error;

                    default:
                        LogEvent(
                            order,
                            "Not approved: Unexpected status code. QuickPay response: , qp_status_code: {0}, qp_status_msg: {1}, aq_status_code: '{2}', aq_status_msg: '{3}'.",
                            quickPayStatusCode, Converter.ToString(operation["qp_status_msg"]), Converter.ToString(operation["aq_status_code"]), Converter.ToString(operation["aq_status_msg"])
                        );
                        return CheckDataResult.Error;
                }

                if (AutoFee && operation.ContainsKey("fee"))
                {
                    var fee = Converter.ToString(operation["fee"]);
                    LogEvent(order, "Checking card fee from QuickPay");

                    long feeAmount;
                    if (long.TryParse(fee, out feeAmount))
                    {
                        LogEvent(order, "Saving card fee '{0}'", fee);
                        order.ExternalPaymentFee = feeAmount / 100d;
                    }
                }

                if (AutoCapture)
                {
                    long autoCaptureAmount;
                    if (long.TryParse(Converter.ToString(operation["amount"]), out autoCaptureAmount))
                    {
                        LogEvent(order, "Autocapturing order", DebuggingInfoType.CaptureResult);
                        order.CaptureInfo = new OrderCaptureInfo(OrderCaptureInfo.OrderCaptureState.Success, "Autocapture successful");
                        order.CaptureAmount = autoCaptureAmount / 100d;
                    }
                }

                LogEvent(
                    order,
                    "Payment{1} succeeded with transaction number {0}",
                    Converter.ToString(quickpayResponse["id"]), TestMode ? "[TEST]" : ""
                );
                var cardType = Converter.ToString(metadata["brand"]);
                order.TransactionCardType = !string.IsNullOrWhiteSpace(cardType) ? cardType : Converter.ToString(quickpayResponse["acquirer"]);
                order.TransactionCardNumber = Converter.ToString(metadata["last4"]).PadLeft(16, 'X');
                SetOrderComplete(order, Converter.ToString(quickpayResponse["id"]));

                SetOrderSucceeded(order, true);
                CheckoutDone(order);

                if (!order.Complete)
                {
                    SetOrderSucceeded(order, false);
                }
                result = CheckDataResult.CallbackSucceed;
                break;

            case "capture":
                long captureAmount, balance;
                if (long.TryParse(Converter.ToString(operation["amount"]), out captureAmount) && long.TryParse(Converter.ToString(quickpayResponse["balance"]), out balance))
                {

                    if (transactionAmount != captureAmount)
                    {
                        LogError(
                            order,
                            "The amount returned from response does not match the amount set to capture: Response: {0}, Amount to capture: {1}",
                            Converter.ToString(operation["amount"]), transactionAmount
                        );
                        return CheckDataResult.Error;
                    }

                    var qpStatusCode = Converter.ToString(operation["qp_status_code"]);
                    if (Converter.ToBoolean(operation["pending"]))
                    {
                        LogEvent(order, "The payment has not yet been verified by QuickPay. We will try again shortly.");

                        OperationStatus captureStatus = null;
                        int maxAttempts = 62;
                        int attempts = 0;
                        do
                        {
                            Thread.Sleep(1000);
                            captureStatus = GetLastOperationStatus(order, "capture");
                            attempts++;
                        }
                        while (captureStatus.IsPending && attempts < maxAttempts);

                        if (!captureStatus.Succeded)
                        {
                            qpStatusCode = captureStatus.StatusCode;
                        }

                        if (attempts == maxAttempts && captureStatus.IsPending)
                        {
                            LogError(order, $"Capture was not completed within {attempts} seconds. Try again later");
                            return CheckDataResult.Error;
                        }
                    }

                    if (!string.IsNullOrEmpty(qpStatusCode) && qpStatusCode != "20000")
                    {
                        LogError(order, $"Capture failed with error message: {qpStatusCode}");
                        return CheckDataResult.Error;
                    }

                    if (order.Price.PricePIP == captureAmount + balance)
                    {
                        return CheckDataResult.FinalCaptureSucceed;
                    }
                    else
                    {
                        return CheckDataResult.SplitCaptureSucceed;
                    }
                }
                else
                {
                    LogError(order, "Error with handle amounts from quickpay data");
                    return CheckDataResult.Error;
                }

            case "refund":
                long returnAmount;
                if (long.TryParse(Converter.ToString(operation["amount"]), out returnAmount) && long.TryParse(Converter.ToString(quickpayResponse["balance"]), out balance))
                {

                    if (transactionAmount != returnAmount)
                    {
                        LogError(
                            order,
                            "The amount returned from response does not match the amount set to return: Response: {0}, Amount to return: {1}",
                            Converter.ToString(operation["amount"]), transactionAmount
                        );
                        return CheckDataResult.Error;
                    }
                    //Nets does not allow capture on previously refunded
                    if (order.CaptureInfo.State == OrderCaptureInfo.OrderCaptureState.Split)
                    {
                        order.CaptureInfo.State = OrderCaptureInfo.OrderCaptureState.Success;
                        order.CaptureInfo.Message = "Split capture finalized by return operation.";
                    }

                    var returned = PriceHelper.ConvertToPIP(order.Currency, order.ReturnOperations.Where(returnOperation => returnOperation.State == OrderReturnOperationState.PartiallyReturned).Sum(x => x.Amount));
                    if (PriceHelper.ConvertToPIP(order.Currency, order.CaptureAmount) == (returnAmount + returned))
                    {
                        return CheckDataResult.FullReturnSucceed;
                    }
                    else
                    {
                        return CheckDataResult.PartialReturnSucceed;
                    }
                }
                else
                {
                    LogError(order, "Error with handle amounts from quickpay data");
                    return CheckDataResult.Error;
                }

            default:
                LogError(order, "Unsuported transaction type");
                return CheckDataResult.Error;
        }

        Cache.Current.Set(orderCacheKey + order.Id, order, new CacheItemPolicy { AbsoluteExpiration = DateTime.Now.AddMinutes(10) });
        return result;
    }

    private OperationStatus GetLastOperationStatus(Order order, string operationTypeLock = "")
    {
        var operationStatus = new OperationStatus();

        var serviceParameters = "";
        if (string.IsNullOrWhiteSpace(order.TransactionNumber))
        {
            serviceParameters = $"?order_id={order.Id}";
        }

        var responsetext = ExecuteRequest(order, ApiService.GetPaymentStatus, order.TransactionNumber, serviceParameters: serviceParameters);
        Dictionary<string, object> paymentModel;
        if (string.IsNullOrWhiteSpace(order.TransactionNumber))
        {
            paymentModel = Converter.Deserialize<Dictionary<string, object>[]>(responsetext).FirstOrDefault();
            if (paymentModel == null)
            {
                LogError(order, $"QuickPay returned no transaction information on get status. DW order id - {order.Id}, transaction number - {order.TransactionNumber}");
                operationStatus.Succeded = false;
                return operationStatus;
            }
        }
        else
        {
            paymentModel = Converter.Deserialize<Dictionary<string, object>>(responsetext);
        }
        var operations = Converter.Deserialize<Dictionary<string, object>[]>(Converter.ToString(paymentModel["operations"]));

        Dictionary<string, object> operation;
        operation = operations.Last(o => string.IsNullOrEmpty(operationTypeLock) ||
            string.Equals(operationTypeLock, Converter.ToString(o["type"]), StringComparison.OrdinalIgnoreCase));

        if (operation is null)
        {
            LogError(order, $"QuickPay returned no transaction information on get status. DW order id - {order.Id}, transaction number - {order.TransactionNumber}");
            operationStatus.Succeded = false;
            return operationStatus;
        }

        operationStatus.IsPending = Converter.ToBoolean(operation["pending"]);
        operationStatus.StatusCode = Converter.ToString(operation["qp_status_code"]);
        operationStatus.Succeded = !operationStatus.IsPending & operationStatus.StatusCode.Equals("20000");
        return operationStatus;
    }

    private Dictionary<string, string> GetCardTypes(bool recurringOnly, bool translate)
    {
        var cardTypes = new Dictionary<string, string>
        {
            {"creditcard", "All card type payments"},
            {"american-express", "American Express credit card"},
            {"american-express-dk", "American Express (Danish card)"},
            {"dankort", "Dankort credit card"},
            {"diners", "Diners Club credit card"},
            {"diners-dk", "Diners Club (Danish card)"},
            {"fbg1886", "Forbrugsforeningen af 1886"},
            {"jcb", "JCB credit card"},
            {"mastercard", "Mastercard credit card"},
            {"mastercard-dk", "Mastercard (Danish card)"},
            {"mastercard-debet", "Mastercard debet card"},
            {"mastercard-debet-dk", "Mastercard-Debet"},
            {"mobilepay-subscriptions", "MobilePay Subscriptions"},
            {"visa", "Visa credit card"},
            {"visa-dk", "Visa (Danish card)"},
            {"visa-electron", "Visa debet (former Visa Electron) card"},
            {"visa-electron-dk", "Visa Electron (Danish card)"},
            {"3d-creditcard", "3D-secure payments"},
            {"3d-jcb", "JCB 3D-Secure"},
            {"3d-maestro", "Maestro 3D-Secure"},
            {"3d-maestro-dk", "Maestro 3D-Secure (Danish card)"},
            {"3d-mastercard", "Mastercard 3D-Secure"},
            {"3d-mastercard-dk", "Mastercard 3D-Secure (Danish card)"},
            {"3d-mastercard-debet", "Mastercard-Debet 3D-Secure"},
            {"3d-mastercard-debet-dk", "Mastercard-Debet 3D-Secure (Danish card)"},
            {"3d-visa", "Visa 3D-Secure"},
            {"3d-visa-dk", "Visa 3D-Secure (Danish card)"},
            {"3d-visa-electron", "Visa Electron 3D-Secure"},
            {"3d-visa-electron-dk", "Visa Electron 3D-Secure (Danish card)"}
        };

        if (!recurringOnly)
        {
            var acquirers = new Dictionary<string, string>
            {
                {"apple-pay", "Apple pay"},
                {"anyday-split", "ANYDAY Split"},
                {"google-pay", "Google pay"},
                {"ideal", "iDEAL"},
                {"resurs", "Resurs Bank"},
                {"klarna-payments", "Klarna Payments"},
                {"mobilepay", "MobilePay Online"},
                {"paypal", "PayPal"},
                {"sofort", "Sofort"},
                {"viabill", "ViaBill"},
                {"klarna", "Klarna"},
                {"bitcoin", "Bitcoin through Coinify"},
                {"swish", "Swish"},
                {"trustly", "Trustly"},
                {"vipps", "Vipps"},
                {"vippspsp", "Vipps via Quickpay"},
                {"paysafecard", "Paysafecard"}
            };

            cardTypes = cardTypes.Union(acquirers).ToDictionary(x => x.Key, y => y.Value);
        }
        return translate ? cardTypes.ToDictionary(x => x.Key, y => y.Value) : cardTypes;
    }

    private string GetMacString(IDictionary<string, string> formValues)
    {
        var excludeList = new List<string> { "MAC" };
        var keysSorted = formValues.Keys.ToArray();
        Array.Sort(keysSorted, StringComparer.Ordinal);

        var message = new StringBuilder();
        foreach (string key in keysSorted)
        {
            if (excludeList.Contains(key))
            {
                continue;
            }

            if (message.Length > 0)
            {
                message.Append(" ");
            }

            var value = formValues[key];
            message.Append(value);
        }

        return message.ToString();
    }

    private string ByteArrayToHexString(byte[] bytes)
    {
        var result = new StringBuilder();
        foreach (byte b in bytes)
        {
            result.Append(b.ToString("x2"));
        }

        return result.ToString();
    }

    private string ComputeHash(string key, Stream message)
    {
        var encoding = new System.Text.UTF8Encoding();
        var byteKey = encoding.GetBytes(key);

        using (HMACSHA256 hmac = new HMACSHA256(byteKey))
        {
            var hashedBytes = hmac.ComputeHash(message);
            return ByteArrayToHexString(hashedBytes);
        }
    }

    private string ComputeHash(string key, string message)
    {
        var encoding = new System.Text.UTF8Encoding();
        var byteKey = encoding.GetBytes(key);

        using (HMACSHA256 hmac = new HMACSHA256(byteKey))
        {
            var messageBytes = encoding.GetBytes(message);
            var hashedBytes = hmac.ComputeHash(messageBytes);

            return ByteArrayToHexString(hashedBytes);
        }
    }

    #endregion

    #region IPartialReturn, IFullReturn
    public void PartialReturn(Order order, Order originalOrder)
    {
        ProceedReturn(originalOrder, order.Price.PricePIP);
    }

    public void FullReturn(Order order)
    {
        ProceedReturn(order, order.Price.PricePIP);
    }

    private void ProceedReturn(Order order, long amount)
    {
        var doubleAmount = Converter.ToDouble(amount) / 100;

        if (string.IsNullOrEmpty(order?.Id))
        {
            OrderReturnInfo.SaveReturnOperation(OrderReturnOperationState.Failed, "No valid Order object set", doubleAmount, order);
            LogError(order, "QuickPay payment refund operation failed. No valid Order object set.");
            return;
        }

        if (string.IsNullOrWhiteSpace(order.TransactionNumber))
        {
            OrderReturnInfo.SaveReturnOperation(OrderReturnOperationState.Failed, "No transaction number set on the order", doubleAmount, order);
            LogError(order, "QuickPay payment refund operation failed. No transaction number set on the order.");
            return;
        }

        if (!new OrderCaptureInfo.OrderCaptureState[2] { OrderCaptureInfo.OrderCaptureState.Success, OrderCaptureInfo.OrderCaptureState.Split }.Contains(order.CaptureInfo.State) || order.CaptureAmount <= 0.00)
        {
            OrderReturnInfo.SaveReturnOperation(OrderReturnOperationState.Failed, "Order not yet captured.", doubleAmount, order);
            LogError(order, "QuickPay payment refund operation failed. Order not yet captured.");
            return;
        }

        var fullReturnOperation = order.ReturnOperations.FirstOrDefault(returnOperation => returnOperation.State == OrderReturnOperationState.FullyReturned);
        var returned = order.ReturnOperations.Where(returnOperation => returnOperation.State == OrderReturnOperationState.PartiallyReturned || returnOperation.State == OrderReturnOperationState.FullyReturned).Sum(x => x.Amount);
        if (fullReturnOperation != null || (order.CaptureAmount - returned) < 0.01)
        {
            OrderReturnInfo.SaveReturnOperation(OrderReturnOperationState.Failed, "Order already returned", doubleAmount, order);
            LogError(order, "QuickPay payment refund operation failed. Order already returned.");
            return;
        }

        var notReturnedAmount = order.CaptureAmount - returned;
        if (notReturnedAmount < doubleAmount && (doubleAmount - notReturnedAmount) >= 0.01)
        {
            OrderReturnInfo.SaveReturnOperation(OrderReturnOperationState.Failed, $"Order remaining captured amount to return ({Services.Currencies.Format(order.Currency, notReturnedAmount)}) less than amount requested for return{Services.Currencies.Format(order.Currency, doubleAmount)}.", doubleAmount, order);
            LogError(order, "QuickPay payment refund operation failed. Order captured amount less then amount requested for return.");
            return;
        }

        var formValues = $@"{{""id"": ""{order.Id}"", ""amount"": {amount}}}";

        try
        {
            var responseText = ExecuteRequest(order, ApiService.RefundPayment, order.TransactionNumber, formValues, "?synchronized");
            LogEvent(order, "QuickPay has refunded payment", DebuggingInfoType.ReturnResult);
            var result = CheckData(order, responseText, amount, false);
            switch (result)
            {
                case CheckDataResult.Error:
                    OrderReturnInfo.SaveReturnOperation(OrderReturnOperationState.Failed, "QuickPay response validation failed. Check order logs for details.", doubleAmount, order);
                    break;
                case CheckDataResult.PartialReturnSucceed:
                    OrderReturnInfo.SaveReturnOperation(OrderReturnOperationState.PartiallyReturned, "QuickPay has partial refunded payment.", doubleAmount, order);
                    break;
                case CheckDataResult.FullReturnSucceed:
                    OrderReturnInfo.SaveReturnOperation(OrderReturnOperationState.FullyReturned, "QuickPay has full refunded payment.", doubleAmount, order);
                    break;
            }
        }
        catch
        {
            OrderReturnInfo.SaveReturnOperation(OrderReturnOperationState.Failed, "QuickPay refund request failed. Check order logs for details.", doubleAmount, order);
            return;
        }
    }

    #endregion

    #region RenderInlineForm

    public override string RenderInlineForm(Order order)
    {
        if (postMode == PostModes.Inline)
        {
            LogEvent(order, "Render inline form");
            var formTemplate = new Template(TemplateHelper.GetTemplatePath(PostTemplate, PostTemplateFolder));
            formTemplate.SetTag("QuickPayPaymentWindow.merchant_id", Merchant.Trim());
            formTemplate.SetTag("QuickPayPaymentWindow.agreement_id", Agreement.Trim());
            return Render(order, formTemplate);
        }

        return string.Empty;
    }
    #endregion
}
