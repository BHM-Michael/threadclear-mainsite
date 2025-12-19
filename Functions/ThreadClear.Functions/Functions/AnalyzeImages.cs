using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Net;
using System.Text;
using ThreadClear.Functions.Extensions;
using ThreadClear.Functions.Models;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class AnalyzeImages
    {
        private readonly ILogger<AnalyzeImages> _logger;
        private readonly IConversationParser _parser;
        private readonly IConversationAnalyzer _analyzer;
        private readonly IThreadCapsuleBuilder _builder;
        private readonly IAIService _aiService;

        public AnalyzeImages(
            ILogger<AnalyzeImages> logger,
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

        [Function("AnalyzeImages")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "analyze-images")] HttpRequestData req)
        {
            _logger.LogInformation("Processing multi-image analysis request");

            try
            {
                var formData = await req.ReadFormDataAsync();

                var sourceType = formData["sourceType"].ToString() ?? "simple";
                var parsingModeStr = formData["parsingMode"].ToString() ?? "2";
                int.TryParse(parsingModeStr, out int parsingMode);

                // Get all images
                var imageFiles = formData.Files.GetFiles("images");

                if (imageFiles == null || !imageFiles.Any())
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "No images provided" });
                    return badRequest;
                }

                _logger.LogInformation("Processing {Count} images", imageFiles.Count());

                // Extract text from each image in order
                var extractedTexts = new List<string>();

                foreach (var imageFile in imageFiles)
                {
                    using var memoryStream = new MemoryStream();
                    await imageFile.OpenReadStream().CopyToAsync(memoryStream);
                    var imageBytes = memoryStream.ToArray();
                    var base64Image = Convert.ToBase64String(imageBytes);
                    var mimeType = imageFile.ContentType ?? "image/png";

                    var extractedText = await _aiService.ExtractTextFromImage(base64Image, mimeType);

                    if (!string.IsNullOrWhiteSpace(extractedText))
                    {
                        extractedTexts.Add(extractedText);
                    }
                }

                if (!extractedTexts.Any())
                {
                    var noTextResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await noTextResponse.WriteAsJsonAsync(new { error = "Could not extract conversation from any images" });
                    return noTextResponse;
                }

                // Combine all extracted texts
                var combinedText = string.Join("\n\n", extractedTexts);

                _logger.LogInformation("Combined extracted text: {Length} characters from {Count} images",
                    combinedText.Length, extractedTexts.Count);

                // Analyze the combined conversation
                var mode = (ParsingMode)parsingMode;
                var capsule = await _parser.ParseConversation(combinedText, sourceType, mode);

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
                    imagesProcessed = extractedTexts.Count,
                    extractedText = combinedText
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing multi-image analysis");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = "Error processing images", details = ex.Message });
                return errorResponse;
            }
        }
    }
}