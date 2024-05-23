using Dynamicweb.Core;

namespace Dynamicweb.Ecommerce.CheckoutHandlers.QuickPayPaymentWindow;

internal sealed class SavedCardInformation
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string PaymentUrl { get; set; }

    public SavedCardInformation(string id, string name, string paymentUrl)
    {
        Ensure.NotNullOrEmpty(id, nameof(id));
        Ensure.NotNullOrEmpty(name, nameof(name));
        Ensure.NotNullOrEmpty(paymentUrl, nameof(paymentUrl));

        Id = id;
        Name = name;
        PaymentUrl = paymentUrl;
    }
}
