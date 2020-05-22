﻿using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Console_DeviceCodeFlow_MultiTarget
{
    internal class Program
    {
        private static PublicClientApplicationOptions appConfiguration = null;
        private static IConfiguration configuration;
        private static string _authority;

        private static async Task Main(string[] args)
        {
            // Using appsettings.json to load the configuration settings
            var builder = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            configuration = builder.Build();

            appConfiguration = configuration.Get<PublicClientApplicationOptions>();

            // build the AAd authority Url
            _authority = string.Concat(appConfiguration.Instance, appConfiguration.TenantId);

            // Initialize MSAL
            var app = PublicClientApplicationBuilder.Create(appConfiguration.ClientId)
                                                    .WithAuthority(_authority)
                                                    .WithDefaultRedirectUri()
                                                    .Build();

            // Scopes for MS graph that we'd be requesting a token for
            string[] scopes = new[] { "user.read" };

            AuthenticationResult result;

            try
            {
                var accounts = await app.GetAccountsAsync();
                // Try to acquire an access token from the cache. If device code is required, Exception will be thrown.
                result = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                result = await app.AcquireTokenWithDeviceCode(scopes, deviceCodeResult =>
                   {
                       // This will print the message on the console which tells the user where to go sign-in using
                       // a separate browser and the code to enter once they sign in.
                       // The AcquireTokenWithDeviceCode() method will poll the server after firing this
                       // device code callback to look for the successful login of the user via that browser.
                       // This background polling (whose interval and timeout data is also provided as fields in the
                       // deviceCodeCallback class) will occur until:
                       // * The user has successfully logged in via browser and entered the proper code
                       // * The timeout specified by the server for the lifetime of this code (typically ~15 minutes) has been reached
                       // * The developing application calls the Cancel() method on a CancellationToken sent into the method.
                       //   If this occurs, an OperationCanceledException will be thrown (see catch below for more details).
                       Console.WriteLine(deviceCodeResult.Message);
                       return Task.FromResult(0);
                   }).ExecuteAsync();
            }

            string graphApiUrl = configuration.GetValue<string>("GraphApiUrl");

            var graphClient = GetGraphServiceClient(result.AccessToken, graphApiUrl);

            // Call the MS Graph /me endpoint
            var me = await graphClient.Me.Request().GetAsync();


            Console.Write(Environment.NewLine);
            Console.WriteLine($"Hello {result.Account.Username}");
            Console.Write(Environment.NewLine);

            // Print the signed-in user's fetched from  MS Graph
            Console.WriteLine("-------- User's info from MS Graph --------");
            Console.Write(Environment.NewLine);
            Console.WriteLine($"Id: {me.Id}");
            Console.WriteLine($"Display Name: {me.DisplayName}");
            Console.WriteLine($"Email: {me.Mail}");
        }

        /// <summary>
        /// Initializes the Graph SDK
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="graphApiUrl"></param>
        /// <returns></returns>
        private static GraphServiceClient GetGraphServiceClient(string accessToken, string graphApiUrl)
        {
            GraphServiceClient graphServiceClient = new GraphServiceClient(graphApiUrl,
                                                        new DelegateAuthenticationProvider(
                                                            async (requestMessage) =>
                                                            {
                                                                await Task.Run(() =>
                                                                {
                                                                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                                                                });
                                                            }));
            return graphServiceClient;
        }
    }
}