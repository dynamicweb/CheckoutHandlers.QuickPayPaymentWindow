namespace Dynamicweb.Ecommerce.CheckoutHandlers.QuickPayPaymentWindow;

internal sealed class CheckedData
{
    public CheckDataResult Result { get; set; }

    public string Message { get; set; }

    public CheckedData(CheckDataResult result)
    {
        Result = result;
    }

    public CheckedData(CheckDataResult result, string message) : this(result)
    {
        Message = message;
    }
}
