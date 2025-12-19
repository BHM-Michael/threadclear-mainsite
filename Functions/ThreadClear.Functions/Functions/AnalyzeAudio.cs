using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ThreadClear.Functions.Extensions;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class AnalyzeAudio
    {
        private readonly ILogger<AnalyzeAudio> _logger;
        private readonly IConversationParser _parser;
        private readonly IConversationAnalyzer _analyzer;
        private readonly IThreadCapsuleBuilder _builder;
        private readonly HttpClient _httpClient;
        private readonly string _openAiApiKey;

        public AnalyzeAudio(
            ILogger<AnalyzeAudio> logger,
            IConversationParser parser,
            IConversationAnalyzer analyzer,
            IThreadCapsuleBuilder builder)
        {
            _logger = logger;
            _parser = parser;
            _analyzer = analyzer;
            _builder = builder;
            _httpClient = new HttpClient();
            _openAiApiKey = Environment.GetEnvironmentVariable("OpenAI__ApiKey")
                ?? Environment.GetEnvironmentVariable("OpenAI:ApiKey")
                ?? throw new InvalidOperationException("OpenAI API key not configured");
        }

        [Function("AnalyzeAudio")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "analyze-audio")] HttpRequestData req)
        {
            _logger.LogInformation("Processing audio analysis request");

            try
            {
                var formData = await req.ReadFormDataAsync();

                var audioFile = formData.Files.GetFile("audio");
                var sourceType = formData["sourceType"].ToString() ?? "simple";
                var parsingModeStr = formData["parsingMode"].ToString() ?? "2";
                int.TryParse(parsingModeStr, out int parsingMode);

                if (audioFile == null || audioFile.Length == 0)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "No audio file provided" });
                    return badRequest;
                }

                _logger.LogInformation("Received audio file: {Name}, Size: {Size} bytes",
                    audioFile.FileName, audioFile.Length);

                // Transcribe audio using OpenAI Whisper
                var transcribedText = await TranscribeAudio(audioFile);

                if (string.IsNullOrWhiteSpace(transcribedText))
                {
                    var noTextResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await noTextResponse.WriteAsJsonAsync(new { error = "Could not transcribe audio" });
                    return noTextResponse;
                }

                _logger.LogInformation("Transcribed {Length} characters from audio", transcribedText.Length);

                // Analyze the transcribed text using existing pipeline
                var mode = (ParsingMode)parsingMode;
                var capsule = await _parser.ParseConversation(transcribedText, sourceType, mode);

                var modeUsed = capsule.Metadata["ParsingMode"];
                _logger.LogInformation("Parsed conversation {Id} using {Mode} mode", capsule.CapsuleId, modeUsed);

                await _analyzer.AnalyzeConversation(capsule);
                await _builder.EnrichWithLinguisticFeatures(capsule);
                await _builder.CalculateMetadata(capsule);
                var summary = await _builder.GenerateSummary(capsule);
                capsule.Summary = summary;

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    capsule = capsule,
                    parsingMode = modeUsed,
                    transcribedText = transcribedText
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing audio analysis");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = "Error processing audio", details = ex.Message });
                return errorResponse;
            }
        }

        private async Task<string> TranscribeAudio(FormFile audioFile)
        {
            using var content = new MultipartFormDataContent();

            // Read file into memory
            using var memoryStream = new MemoryStream();
            await audioFile.OpenReadStream().CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(audioFile.ContentType ?? "audio/mpeg");

            content.Add(fileContent, "file", audioFile.FileName ?? "audio.mp3");
            content.Add(new StringContent("whisper-1"), "model");
            content.Add(new StringContent("text"), "response_format");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_openAiApiKey}");

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/audio/transcriptions", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Whisper API error: {Response}", responseBody);
                throw new Exception($"Whisper API error: {response.StatusCode} - {responseBody}");
            }

            return responseBody.Trim();
        }
    }
}