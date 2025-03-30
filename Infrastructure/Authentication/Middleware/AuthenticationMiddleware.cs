using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HoTeach_Functions_API.Infrastructure.Authentication.Helpers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using HoTeach_Functions_API.Infrastructure.Authentication.Attributes;

namespace HoTeach_Functions_API.Infrastructure.Authentication.Middleware
{
    public class AuthenticationMiddleware : IFunctionsWorkerMiddleware
    {
        private readonly ILogger<AuthenticationMiddleware> _logger;

        public AuthenticationMiddleware(ILogger<AuthenticationMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            // Get the HTTP request data from the context
            var httpRequestData = await context.GetHttpRequestDataAsync();
            if (httpRequestData == null)
            {
                _logger.LogWarning("No HTTP request data found in the function context");
                await next(context);
                return;
            }

            // Get the target function
            var entryPoint = context.FunctionDefinition.EntryPoint;
            var methodName = entryPoint.Split('.').Last();
            var functionType = Type.GetType(entryPoint.Substring(0, entryPoint.Length - methodName.Length - 1));
            
            if (functionType == null)
            {
                _logger.LogWarning("Could not find function type for entry point: {EntryPoint}", entryPoint);
                await next(context);
                return;
            }

            // Get the method info to check for AuthorizeAttribute
            var methodInfo = functionType.GetMethod(methodName);
            if (methodInfo == null)
            {
                _logger.LogWarning("Could not find method {MethodName} in type {TypeName}", methodName, functionType.Name);
                await next(context);
                return;
            }

            // Check if the method has the AuthorizeAttribute
            var authorizeAttributes = methodInfo.GetCustomAttributes<AuthorizeAttribute>().ToList();
            if (!authorizeAttributes.Any())
            {
                // No authentication required for this function
                await next(context);
                return;
            }

            try
            {
                // Validate the token with HttpRequestData
                var principal = AuthHelper.ValidateToken(httpRequestData);
                if (principal == null)
                {
                    await SetUnauthorizedResponse(httpRequestData, context, "Invalid token");
                    return;
                }

                // Check if the user has at least one of the required scopes
                var requiredScopes = authorizeAttributes.Select(a => a.Scope).ToList();
                if (requiredScopes.Any(s => !string.IsNullOrEmpty(s)) && 
                    !requiredScopes.Any(scope => string.IsNullOrEmpty(scope) || AuthHelper.HasScope(principal, scope)))
                {
                    await SetUnauthorizedResponse(httpRequestData, context, "Insufficient permissions");
                    return;
                }

                // Store the principal in function context items for later use
                context.Items["Principal"] = principal;
                
                // Continue with the request
                await next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authentication: {Message}", ex.Message);
                await SetUnauthorizedResponse(httpRequestData, context, "Authentication error");
            }
        }

        private async Task SetUnauthorizedResponse(HttpRequestData request, FunctionContext context, string message)
        {
            var response = request.CreateResponse();
            response.StatusCode = System.Net.HttpStatusCode.Unauthorized;
            await response.WriteAsJsonAsync(new { error = message });
            
            // Set the response in the function context to be returned
            context.GetInvocationResult().Value = response;
        }
    }
}
