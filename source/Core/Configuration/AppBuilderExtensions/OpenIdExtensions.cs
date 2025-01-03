﻿using IdentityModel;
using IdentityServer3.Core.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Owin;
using Microsoft.Owin.Security.OpenIdConnect;
using Owin;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static IdentityServer3.Core.Constants;

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

                Authority = $"https://login.microsoftonline.com/{options.TenantId}/v2.0",
                ClientId = options.ClientId,
                RedirectUri = options.RedirectUri,

                Notifications = new OpenIdConnectAuthenticationNotifications
                {
                    RedirectToIdentityProvider = context =>
                    {
                        if (context.ProtocolMessage.RequestType == Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectRequestType.Authentication)
                        {
                            var signInMessage = context.OwinContext.Environment.GetSignInMessage();
                            string webServiceUrl = context.OwinContext.Environment.GetIdentityServerWebServiceUri();
                            if (signInMessage != null)
                            {
                                context.ProtocolMessage.Prompt = signInMessage.PromptMode;
                                context.ProtocolMessage.State = $"{Base64Url.Encode(Encoding.UTF8.GetBytes(webServiceUrl))}.{context.ProtocolMessage.State}";

                                if (!context.OwinContext.Authentication.AuthenticationResponseChallenge.Properties.Dictionary.TryGetValue("login_hint", out string loginHint))
                                {
                                    loginHint = signInMessage.LoginHint;
                                }

                                context.ProtocolMessage.LoginHint = loginHint;
                            }
                        }

                        return Task.FromResult(0);
                    },
                    AuthenticationFailed = context =>
                    {
                        if (string.Equals(context.ProtocolMessage.Error, AuthorizeErrors.AccessDenied, StringComparison.Ordinal))
                        {
                            context.HandleResponse();
                            context.Response.Redirect(context.OwinContext.Environment.GetIdentityServerBaseUrl() + $"error?message={AuthorizeErrors.AccessDenied}");
                        }
                        else if (context.Exception.Message.StartsWith("IDX21323"))
                        {
                            context.HandleResponse();
                            context.Response.Redirect(context.OwinContext.Environment.GetIdentityServerBaseUrl() + $"error?message={AuthorizeErrors.InvalidNonce}");
                        }

                        return Task.FromResult(0);
                    }
                }
            };

            if (options.TenantId.StartsWith("common"))
            {
                openIdOptions.TokenValidationParameters.IssuerValidator = ValidateIssuerWithPlaceholder;
            }

            if (!string.IsNullOrEmpty(options.CallbackUri))
            {
                openIdOptions.CallbackPath = PathString.FromUriComponent(new Uri(options.CallbackUri));
            }

            return app.UseOpenIdConnectAuthentication(openIdOptions);
        }

        private static string ValidateIssuerWithPlaceholder(string issuer, SecurityToken token, TokenValidationParameters parameters)
        {
            // Accepts any issuer of the form "https://login.microsoftonline.com/{tenantid}/v2.0",
            // where tenantid is the tid from the token.

            if (token is JwtSecurityToken jwt)
            {
                if (jwt.Payload.TryGetValue("tid", out var value) && value is string tokenTenantId)
                {
                    var validIssuers = new List<string>(parameters.ValidIssuers ?? Enumerable.Empty<string>());
                    validIssuers.Add(parameters.ValidIssuer);
                    validIssuers.RemoveAll(i => string.IsNullOrEmpty(i));

                    if (validIssuers.Any(i => i.Replace("{tenantid}", tokenTenantId) == issuer))
                        return issuer;
                }
            }

            return Validators.ValidateIssuer(issuer, token, parameters);
        }
    }
}
