using System.Runtime.Serialization;

namespace Dynamicweb.Ecommerce.CheckoutHandlers.QuickPayPaymentWindow.Models;

internal sealed class Error
{
    [DataMember(Name = "message")]
    public string Message { get; set; }

    [DataMember(Name = "error_code")]
    public int ErrorCode { get; set; }
}
