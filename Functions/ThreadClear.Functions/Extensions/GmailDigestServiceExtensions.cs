using Microsoft.Extensions.DependencyInjection;
using ThreadClear.Functions.Services;

namespace ThreadClear.Functions.Extensions
{
    public static class GmailDigestServiceExtensions
    {
        /// <summary>
        /// Register all Gmail Digest services for dependency injection
        /// </summary>
        public static IServiceCollection AddGmailDigestServices(this IServiceCollection services)
        {
            // Register services
            services.AddSingleton<GmailTokenService>();
            services.AddSingleton<DigestEmailService>();
            
            // GmailService needs HttpClient
            services.AddHttpClient<GmailService>();

            return services;
        }
    }
}
