using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Web;
using Owin;
using Microsoft.Owin.Extensions;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;

using System.Threading.Tasks;
using Microsoft.Owin.Security.Notifications;
using Microsoft.IdentityModel.Protocols;
using Microsoft.Identity.Client;
using SampleWebFormsAAD.Util;
//using Microsoft.Iden
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.Owin.Security.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Owin.Host.SystemWeb;
using System.Security.Claims;
using Microsoft.Graph.Auth;
using Microsoft.Graph;
using System.Net.Http.Headers;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;
using System.Security.Claims;
using System.Collections.Specialized;

namespace SampleWebFormsAAD
{
    public partial class Startup
    {// The Client ID is used by the application to uniquely identify itself to Microsoft identity platform.
        private static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        private static string aadInstance = EnsureTrailingSlash(ConfigurationManager.AppSettings["ida:AADInstance"]);
        // Tenant is the tenant ID (e.g. contoso.onmicrosoft.com, or 'common' for multi-tenant)
        private static string tenantId = ConfigurationManager.AppSettings["ida:TenantId"];

        private static string postLogoutRedirectUri = ConfigurationManager.AppSettings["ida:PostLogoutRedirectUri"];

        string authority = aadInstance + tenantId;

        public void ConfigureAuth(IAppBuilder app)
        {
            app.SetDefaultSignInAsAuthenticationType(CookieAuthenticationDefaults.AuthenticationType);

            app.UseCookieAuthentication(new CookieAuthenticationOptions());

            app.UseOpenIdConnectAuthentication(
                new OpenIdConnectAuthenticationOptions
                {
                    ClientId = clientId,
                    Authority = authority,
                    PostLogoutRedirectUri = postLogoutRedirectUri,


                    Notifications = new OpenIdConnectAuthenticationNotifications()
                    {
                        // SecurityTokenValidated = OnSecurityTokenValidated,
                        AuthorizationCodeReceived = OnAuthorizationCodeReceived,
                        AuthenticationFailed = (context) =>
                        {
                            return System.Threading.Tasks.Task.FromResult(0);
                        },
                        RedirectToIdentityProvider=(context)=>
                        {
               // NOTE:IF User is authenticated & Not have access to any page then Redirect him to UnAuthorized page instead of login page to "AVOID INFINITE LOOP ISSUE" (In case of Role based Authorization)
                            if(context.OwinContext.Authentication.User.Identity.IsAuthenticated && context.OwinContext.Response.StatusCode==401)
                            {
                                context.OwinContext.Response.Redirect("/HttpErrors/unauthorized.aspx");
                                context.HandleResponse();
                            }
                            //if(context.OwinContext.Request.Path.Value != "/Account/SignInWithOpenId")
                            
                            return Task.FromResult(0);
                        }

                    }

                }
                );


            // This makes any middleware defined above this line run before the Authorization rule is applied in web.config
            app.UseStageMarker(PipelineStage.Authenticate);
        }

        private static string EnsureTrailingSlash(string value)
        {
            if (value == null)
            {
                value = string.Empty;
            }

            if (!value.EndsWith("/", StringComparison.Ordinal))
            {
                return value + "/";
            }

            return value;
        }
        private Task OnAuthenticationFailed(AuthenticationFailedNotification<Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectMessage, OpenIdConnectAuthenticationOptions> context)
        {
            // Handle any unexpected errors during sign in
            context.OwinContext.Response.Redirect("HttpErrors/InternalServerError?message=" + context.Exception.Message);
            context.HandleResponse(); // Suppress the exception
            return Task.FromResult(0);
        }

        private async Task OnAuthorizationCodeReceived(AuthorizationCodeReceivedNotification context)
        {
            /*
			 The `MSALPerUserMemoryTokenCache` is created and hooked in the `UserTokenCache` used by `IConfidentialClientApplication`.
			 At this point, if you inspect `ClaimsPrinciple.Current` you will notice that the Identity is still unauthenticated and it has no claims,
			 but `MSALPerUserMemoryTokenCache` needs the claims to work properly. Because of this sync problem, we are using the constructor that
			 receives `ClaimsPrincipal` as argument and we are getting the claims from the object `AuthorizationCodeReceivedNotification context`.
			 This object contains the property `AuthenticationTicket.Identity`, which is a `ClaimsIdentity`, created from the token received from 
			 Azure AD and has a full set of claims.
			 */
            IConfidentialClientApplication confidentialClient = MsalAppBuilder.BuildConfidentialClientApplication(new ClaimsPrincipal(context.AuthenticationTicket.Identity));

            // Upon successful sign in, get & cache a token using MSAL
            AuthenticationResult result = await confidentialClient.AcquireTokenByAuthorizationCode(new[] { "User.Read" }, context.Code).ExecuteAsync();
            var accessToken = result.AccessToken;
            var authGroupsSettings = ConfigurationManager.GetSection("AADAuthorizationGroups") as NameValueCollection;
            Dictionary<string, string> roleGroups = new Dictionary<string, string>();
            if (authGroupsSettings.Count == 0)
            {
                HttpContext.Current.GetOwinContext().Response.Redirect("HttpErrors/PermissionsRequired?message=AADAuthorizationGroups section not found in the web.conifg . Please specify the same");
            }
            else
            {
                foreach (var key in authGroupsSettings.AllKeys)
                {
                    Console.WriteLine(key + " = " + authGroupsSettings[key]);
                    roleGroups.Add(key, authGroupsSettings[key]);

                }
            }
            var groupIds= await GetUserMemberDetails_CallGraph_UsingAPICall(accessToken, roleGroups);
            var identityUser = new ClaimsIdentity(
        context.AuthenticationTicket.Identity.Claims,
        context.AuthenticationTicket.Identity.AuthenticationType,
        ClaimTypes.Name,
        ClaimTypes.Role);
            identityUser.AddClaim(new Claim(ClaimTypes.Role, "SampleRoleClaimStartup"));
            var claims = groupIds.Select(grpId => new Claim(ClaimTypes.Role, roleGroups[grpId]));
            identityUser.AddClaims(claims);
            context.AuthenticationTicket = new AuthenticationTicket(identityUser, context.AuthenticationTicket.Properties);

        }
        private Task OnSecurityTokenValidated(SecurityTokenValidatedNotification<Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectMessage, OpenIdConnectAuthenticationOptions> context)
        {




            // Verify the user signing in is a business user, not a consumer user. Microsoft.IdentityModel.Protocols.OpenIdConnect
            string[] issuer = context.AuthenticationTicket.Identity.FindFirst(Globals.IssuerClaim).Value.Split('/');
            string tenantId = issuer[(issuer.Length - 2)];
            if (tenantId == Globals.ConsumerTenantId)
            {
                throw new SecurityTokenValidationException("Consumer accounts are not supported for the Group Manager App.  Please log in with your work account.");
            }

            return Task.FromResult(0);
        }

        #region GraphAPICall_ToGetListAzureADGroupsofUser
        public async Task<List<string>> GetUserMemberDetails_CallGraph_UsingAPICall(string accessToken, Dictionary<string, string> configuredRoleGroups)
        {
            GraphResponse result = new GraphResponse();
            try
            {
                // Source: https://docs.microsoft.com/en-us/graph/api/user-checkmembergroups?view=graph-rest-1.0&tabs=http

                // Get a token for our admin-restricted set of scopes Microsoft Graph
                //string token = await GetGraphAccessToken(new string[] { "group.read.all" });

                //string accessToken = GetGraphAccessToken(new string[] { "User.Read" }).Result;

                //Get list of AD Groups Ids configured in Web.config these will be sent in API Call
                var groupIds = ConfigurationManager.AppSettings["ida:AuthorizationGroups"].ToString().Split(',').ToList();
                //var authGroupsSettings = ConfigurationManager.GetSection("AADAuthorizationGroups") as NameValueCollection;
                //Dictionary<string, string> roleGroups = new Dictionary<string, string>();
                //if (authGroupsSettings.Count == 0)
                //{
                //    Console.WriteLine("Application Settings are not defined");
                //}
                //else
                //{
                //    foreach (var key in authGroupsSettings.AllKeys)
                //    {
                //        Console.WriteLine(key + " = " + authGroupsSettings[key]);
                //        roleGroups.Add(key, authGroupsSettings[key]);

                //    }
                //}
                // var graphAPIRequestGroupIDs = new GraphRequest() { groupIds = groupIds };
                var graphAPIRequestGroupIDs = new GraphRequest() { groupIds = configuredRoleGroups.Keys.ToList() };
                var content = new StringContent(JsonConvert.SerializeObject(graphAPIRequestGroupIDs), Encoding.UTF8, "application/json");
                HttpClient client = new HttpClient();
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Globals.MicrosoftGraphCheckMembersAPi);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = content;

                //HttpResponseMessage response = await client.SendAsync(request);
                //TODO :: Implement async operation as in the above line
                HttpResponseMessage response = await client.SendAsync(request);
                // Ensure a successful response
                response.EnsureSuccessStatusCode();

                // Populate the data store with the first page of groups
                string json = response.Content.ReadAsStringAsync().Result;
                result = JsonConvert.DeserializeObject<GraphResponse>(json);
                return result.value;
                //Create the roles with AD GroupId's and Assign it to user
                //var identity = ClaimsPrincipal.Current.Identity as ClaimsIdentity;
                //var claims=result.value.Select(grpId => new Claim(ClaimTypes.Role, grpId));
                //identity.AddClaims(claims);

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
                    // Response.Redirect("/Account/SignIn/?redirectUrl=/Groups");

                    HttpContext.Current.GetOwinContext().Authentication.Challenge(
                   new AuthenticationProperties { RedirectUri = "/" },
                   OpenIdConnectAuthenticationDefaults.AuthenticationType);

                }
                else if (ex.ErrorCode == "invalid_grant")
                {
                    // If we got a token for the basic scopes, but not the admin-restricted scopes,
                    // then we need to ask the admin to grant permissions by by connecting their tenant.
                    //return new RedirectResult("/Account/PermissionsRequired");

                    //*****************TODO:: LOG THE EXCEPTION DETAILS HERE*********************
                    HttpContext.Current.GetOwinContext().Response.Redirect("HttpErrors/PermissionsRequired?message=" + ex.Message);
                    // Suppress the exception

                }
                else
                { //*****************TODO:: LOG THE EXCEPTION DETAILS HERE*********************
                    HttpContext.Current.GetOwinContext().Response.Redirect("HttpErrors/InternalServerError?message=" + ex.Message);
                }
                return result?.value;

            }
            // Handle unexpected errors.
            catch (Exception ex)
            {
                //*****************TODO:: LOG THE EXCEPTION DETAILS HERE*********************
                HttpContext.Current.GetOwinContext().Response.Redirect("HttpErrors/InternalServerError?message=" + ex.Message);
                return result?.value;
            }

        }
        #endregion


    }
}
