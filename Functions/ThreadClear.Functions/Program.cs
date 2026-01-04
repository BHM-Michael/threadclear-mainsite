using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Implementations;
using ThreadClear.Functions.Services.Interfaces;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // ⭐ Get SQL Connection String
        var sqlConnectionString = configuration["SqlConnectionString"]
            ?? configuration.GetConnectionString("SqlConnectionString")
            ?? throw new InvalidOperationException("SqlConnectionString is required");

        // ============================================
        // EXISTING SERVICES (your original code)
        // ============================================

        // ⭐ Register UserService
        services.AddSingleton<IUserService>(sp =>
            new UserService(sqlConnectionString, sp.GetRequiredService<ILogger<UserService>>()));

        // ⭐ Register AI Service based on configuration
        var aiProvider = configuration["AI:Provider"] ?? "Anthropic";
        if (aiProvider == "Anthropic")
        {
            services.AddSingleton<IAIService>(sp =>
            {
                var apiKey = configuration["Anthropic:ApiKey"]
                    ?? throw new InvalidOperationException("Anthropic:ApiKey is required");
                var model = configuration["Anthropic:Model"] ?? "claude-sonnet-4-20250514";
                return new AnthropicAIService(apiKey, model);
            });
        }
        else if (aiProvider == "OpenAI")
        {
            services.AddSingleton<IAIService>(sp =>
            {
                var apiKey = configuration["OpenAI:ApiKey"]
                    ?? throw new InvalidOperationException("OpenAI:ApiKey is required");
                var model = configuration["OpenAI:Model"] ?? "gpt-4o";
                return new OpenAIService(apiKey, model);
            });
        }
        else if (aiProvider == "Gemini")
        {
            services.AddSingleton<IAIService>(sp =>
            {
                var apiKey = configuration["Gemini:ApiKey"]
                    ?? throw new InvalidOperationException("Gemini:ApiKey is required");
                var model = configuration["Gemini:Model"] ?? "gemini-pro";
                return new GeminiAIService(apiKey, model);
            });
        }

        // ⭐ Register ConversationParser with mode selection
        services.AddScoped<IConversationParser>(sp =>
        {
            var modeString = configuration["Parsing:DefaultMode"] ?? "Auto";

            if (!Enum.TryParse<ParsingMode>(modeString, true, out var mode))
            {
                mode = ParsingMode.Auto;
            }

            if (mode == ParsingMode.Basic)
            {
                return new ConversationParser();
            }
            else
            {
                try
                {
                    var aiService = sp.GetRequiredService<IAIService>();
                    return new ConversationParser(aiService, mode);
                }
                catch (InvalidOperationException)
                {
                    return new ConversationParser();
                }
            }
        });

        services.AddScoped<IThreadCapsuleBuilder, ThreadCapsuleBuilder>();
        services.AddScoped<IConversationAnalyzer, ConversationAnalyzer>();

        // ============================================
        // NEW SERVICES (Phase 3-7)
        // ============================================

        // ⭐ Register Repositories
        services.AddSingleton<IOrganizationRepository>(sp =>
            new OrganizationRepository(sqlConnectionString, sp.GetRequiredService<ILogger<OrganizationRepository>>()));

        services.AddSingleton<ITaxonomyRepository>(sp =>
            new TaxonomyRepository(sqlConnectionString, sp.GetRequiredService<ILogger<TaxonomyRepository>>()));

        services.AddSingleton<IInsightRepository>(sp =>
            new InsightRepository(sqlConnectionString, sp.GetRequiredService<ILogger<InsightRepository>>()));

        // ⭐ Register Organization Service
        services.AddSingleton<IOrganizationService>(sp =>
            new OrganizationService(
                sp.GetRequiredService<IOrganizationRepository>(),
                sp.GetRequiredService<IUserService>(),
                sp.GetRequiredService<ILogger<OrganizationService>>()));

        // ⭐ Register Taxonomy Service
        services.AddSingleton<ITaxonomyService>(sp =>
            new TaxonomyService(
                sp.GetRequiredService<ITaxonomyRepository>(),
                sp.GetRequiredService<IOrganizationRepository>(),
                sp.GetRequiredService<ILogger<TaxonomyService>>()));

        // ⭐ Register Insight Service
        services.AddSingleton<IInsightService>(sp =>
            new InsightService(
                sp.GetRequiredService<IInsightRepository>(),
                sp.GetRequiredService<ITaxonomyService>(),
                sp.GetRequiredService<IOrganizationRepository>(),
                sp.GetRequiredService<ILogger<InsightService>>()));

        // ⭐ Register Registration Service
        services.AddSingleton<IRegistrationService>(sp =>
            new RegistrationService(
                sp.GetRequiredService<IUserService>(),
                sp.GetRequiredService<IOrganizationService>(),
                sp.GetRequiredService<IOrganizationRepository>(),
                sp.GetRequiredService<ILogger<RegistrationService>>()));

        // ============================================
        // APPLICATION INSIGHTS
        // ============================================
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
    })
    .Build();

host.Run();