using Dynamicweb.Core;
using Dynamicweb.Ecommerce.CheckoutHandlers.QuickPayPaymentWindow.Models;
using Dynamicweb.Ecommerce.Orders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Dynamicweb.Ecommerce.CheckoutHandlers.QuickPayPaymentWindow;

internal sealed class QuickPayRequest
{
    private static readonly string BaseAddress = "https://api.quickpay.net";

    public Order Order { get; set; }

    public string ApiKey { get; set; }

    public QuickPayRequest(string apiKey)
    {
        ApiKey = apiKey;
    }

    public QuickPayRequest(string apiKey, Order order) : this(apiKey)
    {
        Order = order;
    }

    public string SendRequest(CommandConfiguration configuration)
    {
        if (configuration.CommandType is not ApiService.DeleteCard)
        {
            Ensure.NotNull(Order, "Order not set");
            Ensure.Not(string.IsNullOrEmpty(Order.Id), "Order id not set");
        }

        using (var handler = GetHandler())
        {
            using (var client = new HttpClient(handler))
            {
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

                string base64Key = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Format(":{0}", ApiKey)));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Key);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                client.DefaultRequestHeaders.Add("Accept-Version", "v10");

                string commandLink = GetCommandLink(configuration.CommandType, configuration.OperatorId, configuration.OperatorSecondId, configuration.QueryParameters);

                Task<HttpResponseMessage> requestTask = configuration.CommandType switch
                {
                    //PUT
                    ApiService.GetCardLink => client.PutAsync(commandLink, GetContent()),
                    //GET
                    ApiService.GetCard or
                    ApiService.GetPayment => client.GetAsync(commandLink),
                    //POST
                    ApiService.CreatePayment or
                    ApiService.AuthorizePayment or
                    ApiService.CapturePayment or
                    ApiService.CreateCardToken or
                    ApiService.CreateCard or
                    ApiService.DeleteCard or
                    ApiService.RefundPayment => client.PostAsync(commandLink, GetContent()),
                    _ => throw new NotSupportedException($"Unknown operation was used. The operation code: {configuration.CommandType}.")
                };

                try
                {
                    using (HttpResponseMessage response = requestTask.GetAwaiter().GetResult())
                    {
                        Log(Order, $"Remote server response: HttpStatusCode = {response.StatusCode}, HttpStatusDescription = {response.ReasonPhrase}");
                        string responseText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        Log(Order, $"Remote server ResponseText: {responseText}");

                        if (!response.IsSuccessStatusCode)
                        {
                            var error = Converter.Deserialize<ServiceError>(responseText);
                            if (error.ErrorCode > 0 || !string.IsNullOrWhiteSpace(error.Message))
                            {
                                string errorMessage = error.ErrorCode > 0
                                    ? $"Error code: {error.ErrorCode}. Message: {error.Message}."
                                    : $"Message: {error.Message}.";
                                throw new Exception(errorMessage);
                            }
                        }

                        return responseText;
                    }
                }
                catch (HttpRequestException requestException)
                {
                    throw new Exception($"An error occurred during QuickPay request. Error code: {requestException.StatusCode}");
                }
            }
        }

        HttpClientHandler GetHandler() => new()
        {
            AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
        };

        HttpContent GetContent()
        {
            Dictionary<string, string> parameters = configuration.Parameters?.ToDictionary(x => x.Key, y => configuration.Parameters[y.Key]?.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            if (configuration.CommandType is ApiService.AuthorizePayment)
                return new FormUrlEncodedContent(parameters ?? new());

            string content = parameters?.Any() is true ? Converter.Serialize(parameters) : string.Empty;
            return new StringContent(content, Encoding.UTF8, "application/json");
        }
    }

    private static void Log(Order order, string message)
    {
        if (order is null)
            return;

        Services.OrderDebuggingInfos.Save(order, message, typeof(QuickPayPaymentWindow).FullName, DebuggingInfoType.Undefined);
    }

    private static string GetCommandLink(ApiService command, string operatorId = "", string operatorSecondId = "", Dictionary<string, string> queryParameters = null)
    {
        return command switch
        {
            ApiService.CreatePayment => GetCommandLink("payments"),
            ApiService.GetPayment => GetCommandLink($"payments/{operatorId}", queryParameters),
            ApiService.AuthorizePayment => GetCommandLink($"payments/{operatorId}/authorize", queryParameters),
            ApiService.CapturePayment => GetCommandLink($"payments/{operatorId}/capture"),
            ApiService.RefundPayment => GetCommandLink($"payments/{operatorId}/refund", queryParameters),
            ApiService.CreateCard => GetCommandLink("cards"),
            ApiService.GetCard => GetCommandLink($"cards/{operatorId}"),
            ApiService.GetCardLink => GetCommandLink($"cards/{operatorId}/link"),
            ApiService.CreateCardToken => GetCommandLink($"cards/{operatorId}/tokens"),
            ApiService.DeleteCard => GetCommandLink($"cards/{operatorId}/cancel"),
            _ => throw new NotSupportedException($"The api command is not supported. Command: {command}")
        };

        string GetCommandLink(string gateway, Dictionary<string, string> queryParameters = null)
        {
            string link = $"{BaseAddress}/{gateway}";

            if (queryParameters?.Count is 0 or null)
                return link;

            string parameters = string.Join("&", queryParameters.Select(parameter => $"{parameter.Key}={parameter.Value}"));

            return $"{link}?{parameters}";
        }
    }
}
