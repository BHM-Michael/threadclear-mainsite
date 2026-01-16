using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using ThreadClear.Functions.Extensions;
using ThreadClear.Functions.Services.Interfaces;

namespace ThreadClear.Functions.Functions
{
    public class ExtractImageText
    {
        private readonly ILogger _logger;
        private readonly IAIService _aiService;

        public ExtractImageText(ILoggerFactory loggerFactory, IAIService aiService)
        {
            _logger = loggerFactory.CreateLogger<ExtractImageText>();
            _aiService = aiService;
        }

        [Function("ExtractImageText")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "images/extract-text")]
            HttpRequestData req)
        {
            _logger.LogInformation("Extracting text from images");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var formData = await req.ReadFormDataAsync();
                var imageFiles = formData.Files.GetFiles("images");

                if (imageFiles == null || !imageFiles.Any())
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { success = false, error = "No images provided" });
                    return badRequest;
                }

                _logger.LogInformation("Extracting text from {Count} images", imageFiles.Count());

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
                    await noTextResponse.WriteAsJsonAsync(new { success = false, error = "Could not extract text from images" });
                    return noTextResponse;
                }

                var combinedText = string.Join("\n\n", extractedTexts);

                _logger.LogInformation("Extracted {Length} chars from {Count} images in {Ms}ms",
                    combinedText.Length, extractedTexts.Count, sw.ElapsedMilliseconds);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    text = combinedText,
                    imageCount = extractedTexts.Count,
                    extractTimeMs = sw.ElapsedMilliseconds
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from images");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { success = false, error = ex.Message });
                return errorResponse;
            }
        }
    }
}