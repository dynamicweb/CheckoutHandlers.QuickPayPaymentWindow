using Dynamicweb.Ecommerce.Orders;
using Dynamicweb.Rendering;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Dynamicweb.Ecommerce.CheckoutHandlers.QuickPayPaymentWindow;

/// <summary>
/// The class to help set template tags and create form values for Quickpay Form
/// </summary>
internal sealed class QuickpayTemplateHelper
{
    public Order Order { get; set; }

    public string Merchant { get; set; }

    public string Agreement { get; set; }

    public string LanguageCode { get; set; }

    public string[] AvailableLanguages { get; set; }

    public string ReceiptUrl { get; set; }

    public string CancelUrl { get; set; }

    public string CallbackUrl { get; set; }

    public string ContinueUrl { get; set; }

    public bool AutoCapture { get; set; }

    public bool AutoFee { get; set; }

    public string PaymentMethods { get; set; }

    public Dictionary<string, string> AvailablePaymentMethods { get; set; }

    public int Branding { get; set; }

    public string GoogleAnalyticsTracking { get; set; }

    public string GoogleAnalyticsClient { get; set; }


    /// <summary>
    /// Gets values for Quickpay Form: https://learn.quickpay.net/tech-talk/payments/form/   
    /// </summary>
    /// <param name="apiKey">The api key</param>
    public Dictionary<string, string> GetQuickpayFormValues(string apiKey)
    {
        var quickpayValues = GetCommonValues();
        quickpayValues["version"] = "v10";
        quickpayValues["merchant_id"] = Merchant.Trim();
        quickpayValues["order_id"] = Order.Id;
        quickpayValues["amount"] = Order.Price.PricePIP.ToString();
        quickpayValues["currency"] = Order.Price.Currency.Code;
        quickpayValues["autocapture"] = AutoCapture ? "1" : "0";
        quickpayValues["autofee"] = AutoFee ? "1" : "0";
        quickpayValues["checksum"] = Hash.ComputeHash(apiKey, GetMacString(quickpayValues));

        return quickpayValues;
    }

    /// <summary>
    /// Sets tags for Quickpay Form template:  https://learn.quickpay.net/tech-talk/payments/form/     
    /// </summary>
    /// <param name="apiKey">The api key</param>
    /// <param name="template">Quickpay Form template (see: Post.cshtml as example)</param>
    public void SetQuickpayFormTemplateTags(string apiKey, Template template)
    {
        Dictionary<string, string> formValues = GetQuickpayFormValues(apiKey);
        SetTemplateTags(template, formValues);
    }

    /// <summary>
    /// Sets tags for Card template (see: Card.cshtml as example) 
    /// </summary>
    /// <param name="template">Card template</param>
    public void SetCardTemplateTags(Template template)
    {
        var quickpayValues = GetCommonValues();
        quickpayValues["receipturl"] = ReceiptUrl;

        var cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
        Dictionary<string, string> availableLanguages = AvailableLanguages.ToDictionary(
            code => code,
            code => cultures.FirstOrDefault(culture => culture.TwoLetterISOLanguageName.Equals(code, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? string.Empty
        );
        quickpayValues["availableLanguages"] = GetStringValue(availableLanguages);
        quickpayValues["availablePaymentMethods"] = GetStringValue(AvailablePaymentMethods);

        SetTemplateTags(template, quickpayValues);

        string GetStringValue(Dictionary<string, string> data) => string.Join(',', data.Select(pair => $"{pair.Key}|{pair.Value}"));
    }

    private void SetTemplateTags(Template template, Dictionary<string, string> values)
    {
        foreach ((string key, string value) in values)
            template.SetTag(string.Format("QuickPayPaymentWindow.{0}", key), value);
    }

    /// <summary>
    /// Gets common values for both QuickPay Form and Card
    /// </summary>
    private Dictionary<string, string> GetCommonValues()
    {
        var values = new Dictionary<string, string>
        {
            ["agreement_id"] = Agreement.Trim(),
            ["language"] = LanguageCode,
            ["continueurl"] = ContinueUrl,
            ["cancelurl"] = CancelUrl,
            ["callbackurl"] = CallbackUrl,
            ["payment_methods"] = PaymentMethods,
            ["google_analytics_tracking_id"] = GoogleAnalyticsTracking,
            ["google_analytics_client_id"] = GoogleAnalyticsClient
        };

        if (Branding > 0)
            values["branding_id"] = Branding.ToString();

        return values;
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
                continue;

            if (message.Length > 0)
                message.Append(" ");

            var value = formValues[key];
            message.Append(value);
        }

        return message.ToString();
    }
}
