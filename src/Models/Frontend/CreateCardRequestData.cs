using System.Runtime.Serialization;

namespace Dynamicweb.Ecommerce.CheckoutHandlers.QuickPayPaymentWindow.Models.Frontend;

//This is a model for ajax-interactions with our templates only
[DataContract]
internal sealed class CreateCardRequestData
{
    [DataMember(Name = "agreementId")]
    public string AgreementId { get; set; }

    [DataMember(Name = "brandingId")]
    public string BrandingId { get; set; }

    [DataMember(Name = "languageCode")]
    public string LanguageCode { get; set; }

    [DataMember(Name = "paymentMethods")]
    public string PaymentMethods { get; set; }

    [DataMember(Name = "googleAnalyticsTrackingId")]
    public string GoogleAnalyticsTrackingId { get; set; }

    [DataMember(Name = "googleAnalyticsClientId")]
    public string GoogleAnalyticsClientId { get; set; }

    [DataMember(Name = "receiptUrl")]
    public string ReceiptUrl { get; set; }

    [DataMember(Name = "cancelUrl")]
    public string CancelUrl { get; set; }

    [DataMember(Name = "callbackUrl")]
    public string СallbackUrl { get; set; }
}
