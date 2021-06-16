using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Security.Claims;
using SampleWebFormsAAD.Util;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Newtonsoft.Json;

using System.Collections.Concurrent;

using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Configuration;
using Microsoft.Owin.Security.OAuth;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using System.Net.Http.Headers;
using Microsoft.Owin.Security;
//using System.Net.Http.Headers;
using Microsoft.Owin.Security.OpenIdConnect;

namespace SampleWebFormsAAD
{
    public partial class _Default : Page
    {// For simplicity, this sample uses an in-memory data store instead of a db.
     //private ConcurrentDictionary<string, List<Group>> groupList = new ConcurrentDictionary<string, List<Group>>();
        protected void Page_Load(object sender, EventArgs e)
        {
            //var result =  Request.GetOwinContext().Authentication.AuthenticateAsync("Cookies").Result;
            //string token = result.Properties.Dictionary["access_token"];

            if (!Request.IsAuthenticated)
            {
                HttpContext.Current.GetOwinContext().Authentication.Challenge(
                    new AuthenticationProperties { RedirectUri = "/" },
                    OpenIdConnectAuthenticationDefaults.AuthenticationType);
            }


            // GetUserMemberDetails_CallGraph_UsingAPICall(); //Not working ..Getting exception Bad Request exception
            // MISCApproaches(); //Not working ..Getting exception
            //CheckGroups_using_authProvider_MSDN(); //Not working ..Getting exception
            //CheckGroups_DotnetcoreApproach(); //Not working ..Getting exception
            CheckGroups_using_authProvider_MSDN(); //Not working ..Getting exception
        }
       

        public void CheckGroups_using_authProvider_MSDN()
        {
            //This method is as per MSDN documentation
            //link to read the groups using graph api 
            // https://docs.microsoft.com/en-us/graph/api/user-checkmembergroups?view=graph-rest-1.0&tabs=http


            IConfidentialClientApplication confidentialClientApplication = MsalAppBuilder.BuildConfidentialClientApplication();
            AuthorizationCodeProvider authProvider = new AuthorizationCodeProvider(confidentialClientApplication, new[] { "User.Read" });
            GraphServiceClient graphClient = new GraphServiceClient(authProvider);

            var groupIds = new List<String>()
        {
           "1e587548-e2e3-48f4-b909-38d9316487bf", "ee48b026-276d-438d-bcaa-d322435d7a0c"
        };

            var i = graphClient.Me
                    .CheckMemberGroups(groupIds)
                    .Request()
                    .PostAsync().Result;

        }


        public void Test3()
        {
            string token = GetGraphAccessToken(new string[] { "User.Read" }).Result;

   //         HttpProvider httpProvider = new HttpProvider(
   //new DelegateAuthenticationProvider(requestMessage =>
   //{
   //    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);
   //   return Task.FromResult(0);
   //})
   //);


           //var client = new GraphServiceClient(null, httpProvider);
        }
        public void CheckGroups_DotnetcoreApproach()
        {
            string userToken = GetGraphAccessToken(new string[] { "User.Read" }).Result;

            var clientApp = ConfidentialClientApplicationBuilder
                .Create(ConfigurationManager.AppSettings["ida:ClientId"])
                .WithTenantId(ConfigurationManager.AppSettings["ida:TenantId"])
                .WithClientSecret(ConfigurationManager.AppSettings["ida:ClientSecret"])
                .Build();

            var authResult =  clientApp
                .AcquireTokenOnBehalfOf(new[] { "User.Read" }, new UserAssertion(userToken))
                .ExecuteAsync().Result;

            GraphServiceClient graphClient = new GraphServiceClient(
                "https://graph.microsoft.com/v1.0",
                new DelegateAuthenticationProvider(async (requestMessage) =>
                {
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", authResult.AccessToken);
                }));
        }
        public void CheckGroups_MISCApproaches()
        {
            //var result = Request.GetOwinContext().Authentication.AuthenticateAsync("Cookies").Result;
            //string token = result.Properties.Dictionary["access_token"];

            //        IConfidentialClientApplication confidentialClientApplication = ConfidentialClientApplicationBuilder
            //.Create(ConfigurationManager.AppSettings["ida:ClientId"])
            //.WithTenantId(ConfigurationManager.AppSettings["ida:TenantId"])
            //.WithRedirectUri(Globals.RedirectUri)
            //.WithClientSecret(ConfigurationManager.AppSettings["ida:ClientSecret"]) // or .WithCertificate(certificate)
            //.Build();
            string authority = "https://login.microsoftonline.com/organizations/";
            IConfidentialClientApplication confidentialClientApplication = ConfidentialClientApplicationBuilder
                .Create(ConfigurationManager.AppSettings["ida:ClientId"])
                .WithTenantId(ConfigurationManager.AppSettings["ida:TenantId"])
                // .WithRedirectUri(Globals.RedirectUri)
                .WithClientSecret(ConfigurationManager.AppSettings["ida:ClientSecret"])
                //.WithAuthority(authority)
                .Build();

            AuthorizationCodeProvider authProvider = new AuthorizationCodeProvider(confidentialClientApplication, new[] { "User.Read" });
            GraphServiceClient graphClient = new GraphServiceClient(authProvider);

            var groupIds = new List<String>()
{
   "1e587548-e2e3-48f4-b909-38d9316487bf", "ee48b026-276d-438d-bcaa-d322435d7a0c"
};

            //var i=     graphClient.Me
            //        .CheckMemberGroups(groupIds)
            //        .Request()
            //        .PostAsync().Result;

            var i = graphClient.Groups.Request().Select(x => new { x.Id, x.DisplayName }).GetAsync().Result;

            //var clientApp = ConfidentialClientApplicationBuilder
            //   .Create(ConfigurationManager.AppSettings["ida:ClientId"])
            //   .WithTenantId(ConfigurationManager.AppSettings["ida:TenantId"])
            //   .WithClientSecret(ConfigurationManager.AppSettings["ida:ClientSecret"])
            //   .Build();

            //var authResult =  clientApp
            //	.AcquireTokenOnBehalfOf(new[] { "User.Read" }, new UserAssertion(userToken))
            //	.ExecuteAsync().Result;
        }

        public void GetUserMemberDetails_CallGraph_UsingAPICall()
        {
            string tenantId = ClaimsPrincipal.Current.FindFirst(Globals.TenantIdClaimType).Value;

            try
            {
                // Get a token for our admin-restricted set of scopes Microsoft Graph
                //string token = await GetGraphAccessToken(new string[] { "group.read.all" });
                //commented above line ..changed scope"User.Read" in the below line
                string token = GetGraphAccessToken(new string[] { "User.Read" }).Result;
                string[] groupkeys = new string[] { "1e587548-e2e3-48f4-b909-38d9316487bf", "ee48b026-276d-438d-bcaa-d322435d7a0c" };
                var jsonPayload = new JArray(groupkeys);
                var content = new StringContent(jsonPayload.ToString(), Encoding.UTF8, "application/json");

                // Construct the groups query
                HttpClient client = new HttpClient();
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Globals.MicrosoftGraphCheckMembersAPi);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                request.Content = content;
                // Ensure a successful response
                //HttpResponseMessage response = await client.SendAsync(request);
                //TODO :: Implement async operation as in the above line
                HttpResponseMessage response = client.SendAsync(request).Result;
                response.EnsureSuccessStatusCode();

                // Populate the data store with the first page of groups
                string json = response.Content.ReadAsStringAsync().Result;
                //GroupResponse result = JsonConvert.DeserializeObject<GroupResponse>(json);
                //groupList[tenantId] = result.value;
                //return "Sampleresponse"+ json;
            }
            catch (MsalUiRequiredException ex)
            {
                if (ex.ErrorCode == "user_null")
                {
                    /*
					  If the tokens have expired or become invalid for any reason, ask the user to sign in again.
					  Another cause of this exception is when you restart the app using InMemory cache.
					  It will get wiped out while the user will be authenticated still because of their cookies, requiring the TokenCache to be initialized again
					  through the sign in flow.
					*/
                    Response.Redirect("/Account/SignIn/?redirectUrl=/Groups");
                }
                else if (ex.ErrorCode == "invalid_grant")
                {
                    // If we got a token for the basic scopes, but not the admin-restricted scopes,
                    // then we need to ask the admin to grant permissions by by connecting their tenant.
                    //return new RedirectResult("/Account/PermissionsRequired");
                }
                else
                    Response.Redirect("/Error?message=" + ex.Message);

            }
            // Handle unexpected errors.
            catch (Exception ex)
            {
                Response.Redirect("/Error?message=" + ex.Message);
            }

        }
        /// <summary>
		/// We obtain access token for Microsoft Graph with the scope "group.read.all". Since this access token was not obtained during the initial sign in process 
		/// (OnAuthorizationCodeReceived), the user will be prompted to consent again.
		/// </summary>
		/// <returns></returns>
		private async Task<string> GetGraphAccessToken(string[] scopes)
        {
            IConfidentialClientApplication cc = MsalAppBuilder.BuildConfidentialClientApplication();
            IAccount userAccount = await cc.GetAccountAsync(ClaimsPrincipal.Current.GetMsalAccountId());

            AuthenticationResult result = await cc.AcquireTokenSilent(scopes, userAccount).ExecuteAsync();
            return result.AccessToken;
        }
    }
}