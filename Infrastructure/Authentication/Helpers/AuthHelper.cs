using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace HoTeach_Functions_API.Infrastructure.Authentication.Helpers
{
    public static class AuthHelper
    {
        private static IConfiguration _configuration;
        
        static AuthHelper()
        {
            // Initialize configuration if needed
            // This is a simple approach; in a real application, you might want to use dependency injection
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
            
            _configuration = configBuilder.Build();
        }

        public static ClaimsPrincipal ValidateToken(HttpRequestData request)
        {
            try
            {
                // Get the token from the Authorization header
                string authHeader = request.Headers.GetValues("Authorization").FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                string token = authHeader.Substring("Bearer ".Length).Trim();
                if (string.IsNullOrEmpty(token))
                {
                    return null;
                }

                // Define token validation parameters
                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidIssuer = $"https://{_configuration["Auth0:Domain"]}/",
                    ValidAudience = _configuration["Auth0:Audience"],
                    IssuerSigningKeys = OpenIdConnectConfigurationRetriever
                    .GetAsync($"https://{_configuration["Auth0:Domain"]}/.well-known/openid-configuration", default)
                    .GetAwaiter().GetResult()
                    .SigningKeys
                };

                // Validate the token
                var handler = new JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(token, tokenValidationParameters, out var securityToken);

                return principal;
            }
            catch (Exception)
            {
                // Token validation failed
                return null;
            }
        }

        public static bool HasScope(ClaimsPrincipal principal, string scope)
        {
            if (principal == null)
            {
                return false;
            }

            // Check if the 'scope' claim exists and contains the required scope
            var scopeClaim = principal.FindFirst("scope")?.Value;
            if (string.IsNullOrEmpty(scopeClaim))
            {
                return false;
            }

            // The scope claim usually contains space-separated scopes
            var scopes = scopeClaim.Split(' ');
            return scopes.Contains(scope);
        }
    }
}
