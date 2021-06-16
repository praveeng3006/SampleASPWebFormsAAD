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
                        //SecurityTokenValidated = OnSecurityTokenValidated,
                        AuthorizationCodeReceived = OnAuthorizationCodeReceived,
                        AuthenticationFailed = (context) =>
                        {
                            return System.Threading.Tasks.Task.FromResult(0);
                        },
                        
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
            context.OwinContext.Response.Redirect("/Error?message=" + context.Exception.Message);
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

    }
}
