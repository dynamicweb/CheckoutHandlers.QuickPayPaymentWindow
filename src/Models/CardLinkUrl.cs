using System.Runtime.Serialization;

namespace Dynamicweb.Ecommerce.CheckoutHandlers.QuickPayPaymentWindow.Models;

[DataContract]
internal sealed class CardLinkUrl
{
    [DataMember(Name = "url")]
    public string Url { get; set; }
}
