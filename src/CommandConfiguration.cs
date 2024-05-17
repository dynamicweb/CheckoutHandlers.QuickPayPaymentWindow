using System;
using System.Collections.Generic;

namespace Dynamicweb.Ecommerce.CheckoutHandlers.QuickPayPaymentWindow;

internal sealed class CommandConfiguration
{
    /// <summary>
    /// Quick pay command. See operation urls in <see cref="QuickPayRequest"/> and <see cref="ApiService"/>
    /// </summary>
    public ApiService CommandType { get; set; }

    /// <summary>
    /// Command operator id, like /cards/{OperatorId}
    /// </summary>
    public string OperatorId { get; set; }

    /// <summary>
    /// Command operator second id, like /cards/{OperatorId}/operations/{OperatorSecondId}
    /// </summary>
    public string OperatorSecondId { get; set; }

    /// <summary>
    /// Command query parameters, like /payments/{OperatorId}/refund?{QueryParameters}
    /// </summary>
    public Dictionary<string, string> QueryParameters { get; set; }

    /// <summary>
    /// Parameters for request
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
}