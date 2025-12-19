using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using ThreadClear.Functions.Extensions;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class AnalyzeImage
    {
        private readonly ILogger<AnalyzeImage> _logger;
        private readonly IConversationParser _parser;
        private readonly IConversationAnalyzer _analyzer;
        private readonly IThreadCapsuleBuilder _builder;
        private readonly IAIService _aiService;

        public AnalyzeImage(
            ILogger<AnalyzeImage> logger,
            IConversationParser parser,
            IConversationAnalyzer analyzer,
            IThreadCapsuleBuilder builder,
            IAIService aiService)
        {
            _logger = logger;
            _parser = parser;
            _analyzer = analyzer;
            _builder = builder;
            _aiService = aiService;
        }

        [Function("AnalyzeImage")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "analyze-image")] HttpRequestData req)
        {
            _logger.LogInformation("Processing image analysis request");

            try
            {
                // Parse multipart form data
                var formData = await req.ReadFormDataAsync();

                var imageFile = formData.Files.GetFile("image");
                var sourceType = formData["sourceType"].ToString() ?? "simple";
                var parsingModeStr = formData["parsingMode"].ToString() ?? "2";
                int.TryParse(parsingModeStr, out int parsingMode);

                if (imageFile == null || imageFile.Length == 0)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "No image provided" });
                    return badRequest;
                }

                // Read image to base64
                using var memoryStream = new MemoryStream();
                await imageFile.OpenReadStream().CopyToAsync(memoryStream);
                var imageBytes = memoryStream.ToArray();
                var base64Image = Convert.ToBase64String(imageBytes);
                var mimeType = imageFile.ContentType ?? "image/png";

                // Extract conversation text from image using Claude Vision
                var extractedText = await _aiService.ExtractTextFromImage(base64Image, mimeType);

                _logger.LogInformation("Extracted text from image: {Length} characters", extractedText.Length);

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    var noTextResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await noTextResponse.WriteAsJsonAsync(new { error = "Could not extract conversation from image" });
                    return noTextResponse;
                }

                // Now analyze the extracted text using existing pipeline
                var mode = (ParsingMode)parsingMode;
                var capsule = await _parser.ParseConversation(extractedText, sourceType, mode);

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
                    extractedText = extractedText  // Include so user can see what was extracted
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image analysis");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = "Error processing image", details = ex.Message });
                return errorResponse;
            }
        }
    }
}