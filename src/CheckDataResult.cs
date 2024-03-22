namespace Dynamicweb.Ecommerce.CheckoutHandlers.QuickPayPaymentWindow;

internal enum CheckDataResult
{
    Error,
    CallbackSucceed,
    SplitCaptureSucceed,
    FinalCaptureSucceed,
    PartialReturnSucceed,
    FullReturnSucceed
}