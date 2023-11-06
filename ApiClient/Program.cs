using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Net.WebRequestMethods;

namespace ApiClient 
{
    internal class Program
    {
        internal static HttpClient HttpClient { get; } = new HttpClient();

        private static string CustomApiUrlTemplate { get; } = "https://api.businesscentral.dynamics.com/v2.0/<ENVIRONMENTNAME>/api/<APIPUBLISHER>/<APIGROUP>/<APIVERSION>/companies(<COMPANYNAME>)";
        private static IConfiguration? Configuration;
        private static ClientConfiguration? ClientConfig { get; set; }
        private static ApiConfiguration? ApiConfig;
        private static string? InstanceJson { get; set; }

        static async Task Main(string[] args)
        {
            try
            {
                if (args.Length == 0) 
                {
                    var instancePath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!, "instance.json");
                    if (System.IO.File.Exists(instancePath))
                        InstanceJson = System.IO.File.ReadAllText(instancePath);
                    else
                    {
                        Console.Error.WriteLine($"No document instance specified, and no \"{instancePath}\" found");
                        return;
                    }
                }
                else
                {
                    if (System.IO.File.Exists(args[0]))
                        InstanceJson = System.IO.File.ReadAllText(args[0]);
                    else
                    {
                        Console.Error.WriteLine($"Document instance \"{args[0]}\" not found");
                        return;
                    }
                }
                JsonSerializer.Deserialize<ConfirmedPriceModel>(InstanceJson!); // Throws if json is not ConfirmedPriceModel (or not json)

                Configuration = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json")
                        .Build();
                ClientConfig = Configuration.GetSection("Client").Get<ClientConfiguration>() ?? new ClientConfiguration();
                ApiConfig = Configuration.GetSection("Api").Get<ApiConfiguration>() ?? new ApiConfiguration();

                await PostConfirmedPriceDoc(await GetToken());

                Console.Write("Strike a key");
                Console.ReadKey();

            }
            catch (Exception x)
            {
                Console.Error.WriteLine(x.Message);
            }
        }

        static async Task<string> GetToken()
        {            
            var authority = $"{ClientConfig!.Instance}{ClientConfig.TenantId}/oauth2/v2.0/token";
            var app = ConfidentialClientApplicationBuilder.Create(ClientConfig.ClientId)
                .WithClientSecret(ClientConfig.ClientSecret)
                .WithAuthority(new Uri(authority))
                .Build();
            string[] ResourceIds = new string[] {ClientConfig.ResourceId ?? string.Empty };
            var result = await app.AcquireTokenForClient(ResourceIds).ExecuteAsync();
            return result.AccessToken;
        }

        static async Task PostConfirmedPriceDoc(string token)
        {
            var httpRequest = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                Version = new Version("1.1"),
                RequestUri = new Uri($"{GetCustomApiUrl()}/{ApiConfig!.Name}"),
                Content = new StringContent(InstanceJson!,Encoding.UTF8,"application/json")
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