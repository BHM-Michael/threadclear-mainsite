using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Implementations;
using ThreadClear.Functions.Services.Interfaces;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

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
            
            // Try to parse the mode
            if (!Enum.TryParse<ParsingMode>(modeString, true, out var mode))
            {
                mode = ParsingMode.Auto; // Default fallback
            }
            
            if (mode == ParsingMode.Basic)
            {
                // Basic mode - no AI service needed
                return new ConversationParser();
            }
            else
            {
                // Advanced or Auto mode - needs AI service
                try
                {
                    var aiService = sp.GetRequiredService<IAIService>();
                    return new ConversationParser(aiService, mode);
                }
                catch (InvalidOperationException)
                {
                    // AI service not configured, fall back to Basic mode
                    return new ConversationParser();
                }
            }
        });
        
        // TODO: Add your other service registrations here
        services.AddScoped<IThreadCapsuleBuilder, ThreadCapsuleBuilder>();
        services.AddScoped<IConversationAnalyzer, ConversationAnalyzer>();
        // services.AddScoped<IAuthService, AuthService>();
        
        // Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
    })
    .Build();

host.Run();
