using System.Runtime.Serialization;

namespace Dynamicweb.Ecommerce.CheckoutHandlers.QuickPayPaymentWindow.Models.Frontend;

//This is a model for ajax-interactions with our templates only
[DataContract]
internal sealed class CallbackError
{
    [DataMember(Name = "errorMessage")]
    public string ErrorMessage { get; set; }
}
