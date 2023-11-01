using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ApiClient 
{
    internal class Program
    {
        internal static HttpClient HttpClient { get; } = new HttpClient();

        private static string CustomApiUrlTemplate { get; } = "https://api.businesscentral.dynamics.com/v2.0/<ENVIRONMENTNAME>/api/<APIPUBLISHER>/<APIGROUP>/<APIVERSION>/companies(<COMPANYNAME>)";
        private static IConfiguration? Configuration;
        private static ClientConfiguration? ClientConfig { get; set; }
        private static ApiConfiguration? ApiConfig;

        static async Task Main(string[] args)
        {
            try
            {
                Configuration = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json")
                        .Build();
                ClientConfig = Configuration.GetSection("Client").Get<ClientConfiguration>() ?? new ClientConfiguration();
                ApiConfig = Configuration.GetSection("Api").Get<ApiConfiguration>() ?? new ApiConfiguration();

                await PostConfirmedPriceDoc(await GetToken());
            }
            catch (Exception x)
            {
                Console.WriteLine(x.Message);
            }
        }

        static async Task<string> GetToken()
        {
            var app = ConfidentialClientApplicationBuilder.Create(ClientConfig!.ClientId)
                .WithClientSecret(ClientConfig.ClientSecret)
                .WithAuthority(new Uri($"{ClientConfig.Instance}{ClientConfig.TenantId}/oauth2/v2.0/token"))
                .Build();
            string[] ResourceIds = new string[] {ClientConfig.ResourceId ?? string.Empty };
            var result = await app.AcquireTokenForClient(ResourceIds).ExecuteAsync();
            return result.AccessToken;
        }

        static async Task PostConfirmedPriceDoc(string token)
        {
            var confirmedPrice = new ConfirmedPriceModel()
            {
                externalId = "ORDER_818f78e3-db46-ee11-be72-6045bdc8a892", // As received in the consignor doc
                confirmedPrice = 1234.56M
            };
            var httpRequest = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                Version = new Version("1.1"),
                RequestUri = new Uri($"{GetCustomApiUrl()}/{ApiConfig!.Name}"),
                Content = new StringContent(JsonSerializer.Serialize(confirmedPrice),Encoding.UTF8,"application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await HttpClient.SendAsync(httpRequest);
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine(response.ReasonPhrase);
            if (!response.IsSuccessStatusCode)
                Console.WriteLine($"WwwAuthenticate:\"{response.Headers.WwwAuthenticate}\"");
            Console.WriteLine(responseContent ?? "nada");
        }
        static string GetCustomApiUrl() =>
            CustomApiUrlTemplate
                .Replace("<ENVIRONMENTNAME>", ApiConfig!.EnvironmentName)
                .Replace("<APIPUBLISHER>", ApiConfig.ApiPublisher)
                .Replace("<APIGROUP>", ApiConfig.ApiGroup)
                .Replace("<APIVERSION>", ApiConfig.ApiVersion)
                .Replace("<COMPANYNAME>", ApiConfig.CompanyName);
    }
}