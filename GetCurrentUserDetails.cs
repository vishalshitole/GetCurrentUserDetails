using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
//using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;

namespace MMC.GetCurrentUserDetails
{
    public static class GetCurrentUserDetails
    {
        private static HttpClient httpClient = new HttpClient();

        [FunctionName("GetCurrentUserDetails")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("MiddleTierAPI function processed a request.");

            string tenantId = Environment.GetEnvironmentVariable("Azure_App_Tenant_Id");
            string clientId = Environment.GetEnvironmentVariable("Azure_App_Client_Id");
            string clientSecret = Environment.GetEnvironmentVariable("Azure_App_Client_Secret");
            string selectProperties = Environment.GetEnvironmentVariable("Graph_Api_Select_Properties");
            string[] downstreamApiScopes = { "https://graph.microsoft.com/.default" };

            try
            {
                if (string.IsNullOrEmpty(tenantId) ||
                string.IsNullOrEmpty(clientId) ||
                string.IsNullOrEmpty(clientSecret))
                {
                    throw new Exception("Configuration values are missing.");
                }

                string authority = $"https://login.microsoftonline.com/{tenantId}";
                string issuer = $"https://sts.windows.net/{tenantId}/";
                string audience = $"api://{clientId}";
                var app = ConfidentialClientApplicationBuilder.Create(clientId)
                   .WithAuthority(authority)
                   .WithClientSecret(clientSecret)
                   .Build();

                var headers = req.Headers;
                log.LogInformation("headers");
                log.LogInformation(headers.ToString());
                var token = string.Empty;
                if (headers.TryGetValue("Authorization", out var authHeader))
                {
                    if (authHeader[0].StartsWith("Bearer "))
                    {
                        token = authHeader[0].Substring(7, authHeader[0].Length - 7);
                        log.LogInformation("token");
                        log.LogInformation(token);
                    }
                    else
                    {
                        return new UnauthorizedResult();
                    }
                }


                //var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                //    issuer + "/.well-known/openid-configuration",
                //    new OpenIdConnectConfigurationRetriever(),
                //    new HttpDocumentRetriever());
//
                //bool validatedToken = await ValidateToken(token, issuer, audience, configurationManager);
//
                //if (!validatedToken)
                //{
                //    throw new Exception("Token validation failed.");
                //}
                UserAssertion userAssertion = new UserAssertion(token);
                AuthenticationResult result = await app.AcquireTokenOnBehalfOf(downstreamApiScopes, userAssertion).ExecuteAsync();
                string accessToken = result.AccessToken;
                if (accessToken == null)
                {
                    throw new Exception("Access Token could not be acquired.");
                }
                log.LogInformation("accessToken", accessToken);

                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var requestUri = string.IsNullOrWhiteSpace(selectProperties) ? $"https://graph.microsoft.com/v1.0/me" : $"https://graph.microsoft.com/v1.0/me?$select=" + selectProperties;
                var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

                var response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                var myProps = new Dictionary<string, string>();
                myProps.Add("Current User through Microsoft Graph", content);
                return new OkObjectResult(myProps);
            }
            catch (Exception ex)
            {
                log.LogInformation("Error:");
                log.LogInformation(ex.Message);
                return new BadRequestObjectResult(ex.Message);
            }
        }

        //private static async Task<bool> ValidateToken(
        //    string token,
        //    string issuer,
        //    string audience,
        //    IConfigurationManager<OpenIdConnectConfiguration> configurationManager)
        //{
        //    if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));
        //    if (string.IsNullOrEmpty(issuer)) throw new ArgumentNullException(nameof(issuer));
//
        //    var discoveryDocument = await configurationManager.GetConfigurationAsync(default(CancellationToken));
        //    var signingKeys = discoveryDocument.SigningKeys;
//
        //    var validationParameters = new TokenValidationParameters
        //    {
        //        RequireExpirationTime = true,
        //        RequireSignedTokens = true,
        //        ValidateIssuer = true,
        //        ValidIssuer = issuer,
        //        ValidateAudience = true,
        //        ValidAudience = audience,
        //        ValidateIssuerSigningKey = true,
        //        IssuerSigningKeys = signingKeys,
        //        ValidateLifetime = true,
        //        ClockSkew = TimeSpan.FromMinutes(2),
        //    };
//
        //    try
        //    {
        //        new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out var rawValidatedToken);
        //        return true;
        //    }
        //    catch (SecurityTokenValidationException)
        //    {
        //        return false;
        //    }
        //}
    }
}
