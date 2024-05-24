using Dynamicweb.Updates;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Dynamicweb.Ecommerce.CheckoutHandlers.QuickPayPaymentWindow.Updates;

public class QuickPayUpdateProvider : UpdateProvider
{
    private static Stream GetResourceStream(string name)
    {
        string resourceName = $"Dynamicweb.Ecommerce.CheckoutHandlers.QuickPayPaymentWindow.Updates.{name}";

        return Assembly.GetAssembly(typeof(QuickPayUpdateProvider)).GetManifestResourceStream(resourceName);
    }

    public override IEnumerable<Update> GetUpdates()
    {
        return new List<Update>()
        {
            new FileUpdate("ab0730a8-f5fa-4427-80b2-2c7635ab2c5c", this, "/Files/Templates/eCom7/CheckoutHandler/QuickPayPaymentWindow/Post/Card.cshtml", () => GetResourceStream("Card.cshtml")),
            new FileUpdate("fdc187af-6c9e-417a-9671-848a8c3a8a0d", this, "/Files/Templates/eCom7/CheckoutHandler/QuickPayPaymentWindow/Post/InlineForm.cshtml", () => GetResourceStream("InlineForm.cshtml")),
            new FileUpdate("c30f6547-1722-4cc1-a581-03ddcdf97540", this, "/Files/Templates/eCom7/CheckoutHandler/QuickPayPaymentWindow/Post/Post.cshtml", () => GetResourceStream("Post.cshtml")),
        };
    }

    /*
     * IMPORTANT!
     * Use a generated GUID string as id for an update
     * - Execute command in C# interactive window: Guid.NewGuid().ToString()
     */
}
