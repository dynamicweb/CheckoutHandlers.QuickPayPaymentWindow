namespace Dynamicweb.Ecommerce.CheckoutHandlers.QuickPayPaymentWindow;

internal enum ApiService
{
    /// <summary>
    /// Create payment
    /// POST /payments
    /// </summary>
    CreatePayment,

    /// <summary>
    /// Get payment
    /// GET /payments/{operatorId}
    /// </summary>
    GetPayment,

    /// <summary>
    /// Authorize payment
    /// POST /payments/{operatorId}/authorize
    /// </summary>
    AuthorizePayment,

    /// <summary>
    /// Capture payment
    /// POST /payments/{operatorId}/capture
    /// </summary>
    CapturePayment,

    /// <summary>
    /// Refund payment
    /// POST /payments/{operatorId}/refund
    /// </summary>
    RefundPayment,

    /// <summary>
    /// Create saved card
    /// POST /cards
    /// </summary>
    CreateCard,

    /// <summary>
    /// Get saved card
    /// GET /cards/{operatorId}
    /// </summary>
    GetCard,

    /// <summary>
    /// Create or update a card link
    /// PUT /cards/{operatorId}/link
    /// </summary>
    GetCardLink,

    /// <summary>
    /// Create card token
    /// POST /cards/{operatorId}/tokens
    /// </summary>
    CreateCardToken,

    /// <summary>
    /// Delete card link
    /// POST /cards/{operatorId}/cancel
    /// </summary>
    DeleteCard,
}
