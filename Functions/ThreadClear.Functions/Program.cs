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

        // ⭐ Register SQL Connection String and UserService
        var sqlConnectionString = configuration["SqlConnectionString"]
            ?? configuration.GetConnectionString("SqlConnectionString")
            ?? throw new InvalidOperationException("SqlConnectionString is required");

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

        // Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
    })
    .Build();

host.Run();