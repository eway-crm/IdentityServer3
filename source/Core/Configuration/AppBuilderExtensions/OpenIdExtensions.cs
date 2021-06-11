﻿using IdentityModel;
using IdentityServer3.Core.Extensions;
using Microsoft.Owin;
using Microsoft.Owin.Security.OpenIdConnect;
using Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IdentityServer3.Core.Configuration.AppBuilderExtensions
{
    public static class OpenIdExtensions
    {
        public static IAppBuilder UseAzureAdAuthentication(this IAppBuilder app, AzureAdAuthenticationOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var openIdOptions = new OpenIdConnectAuthenticationOptions
            {
                AuthenticationType = "aad",
                Caption = "AzureAD",
                Scope = "openid profile",
                SignInAsAuthenticationType = options.SignInAsAuthenticationType,

                Authority = $"https://login.microsoftonline.com/{options.TenantId}",
                ClientId = options.ClientId,
                RedirectUri = options.RedirectUri,

                Notifications = new OpenIdConnectAuthenticationNotifications
                {
                    RedirectToIdentityProvider = n =>
                    {
                        if (n.ProtocolMessage.RequestType == Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectRequestType.Authentication)
                        {
                            var signInMessage = n.OwinContext.Environment.GetSignInMessage();
                            string webServiceUrl = n.OwinContext.Environment.GetIdentityServerWebServiceUri();
                            if (signInMessage != null)
                            {
                                n.ProtocolMessage.Prompt = signInMessage.PromptMode;
                                n.ProtocolMessage.State = $"{Base64Url.Encode(Encoding.UTF8.GetBytes(webServiceUrl))}.{n.ProtocolMessage.State}";
                            }
                        }

                        return Task.FromResult(0);
                    }
                }
            };


            if (!string.IsNullOrEmpty(options.CallbackUri))
            {
                openIdOptions.CallbackPath = PathString.FromUriComponent(new Uri(options.CallbackUri));
            }

            return app.UseOpenIdConnectAuthentication(openIdOptions);
        }
    }
}
