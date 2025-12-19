using Microsoft.Azure.Functions.Worker.Http;
using System.Net.Http.Headers;

namespace ThreadClear.Functions.Extensions
{
    public class FormDataCollection
    {
        public Dictionary<string, string> Fields { get; } = new();
        public FormFileCollection Files { get; } = new();

        public string this[string key] => Fields.TryGetValue(key, out var value) ? value : string.Empty;
    }

    public class FormFileCollection
    {
        private readonly List<FormFile> _files = new();

        public void Add(FormFile file) => _files.Add(file);

        public FormFile? GetFile(string name) => _files.FirstOrDefault(f => f.Name == name);

        public IEnumerable<FormFile> GetFiles(string name) => _files.Where(f => f.Name == name);
    }

    public class FormFile
    {
        public string Name { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Length { get; set; }
        private readonly byte[] _data;

        public FormFile(byte[] data)
        {
            _data = data;
            Length = data.Length;
        }

        public Stream OpenReadStream() => new MemoryStream(_data);
    }

    public static class HttpRequestDataExtensions
    {
        public static async Task<FormDataCollection> ReadFormDataAsync(this HttpRequestData req)
        {
            var result = new FormDataCollection();

            var contentType = req.Headers.TryGetValues("Content-Type", out var values)
                ? values.FirstOrDefault()
                : null;

            if (string.IsNullOrEmpty(contentType) || !contentType.Contains("multipart/form-data"))
            {
                throw new InvalidOperationException("Request is not multipart/form-data");
            }

            var boundary = GetBoundary(contentType);
            var body = await req.ReadAsStringAsync() ?? string.Empty;

            // For binary data, we need to read as bytes
            req.Body.Position = 0;
            using var ms = new MemoryStream();
            await req.Body.CopyToAsync(ms);
            var bodyBytes = ms.ToArray();

            ParseMultipartFormData(bodyBytes, boundary, result);

            return result;
        }

        private static string GetBoundary(string contentType)
        {
            var elements = contentType.Split(';');
            var boundaryElement = elements.FirstOrDefault(e => e.Trim().StartsWith("boundary="));
            if (boundaryElement == null)
                throw new InvalidOperationException("No boundary found in Content-Type");

            var boundary = boundaryElement.Split('=')[1].Trim('"', ' ');
            return boundary;
        }

        private static void ParseMultipartFormData(byte[] data, string boundary, FormDataCollection result)
        {
            var boundaryBytes = System.Text.Encoding.UTF8.GetBytes("--" + boundary);
            var parts = SplitByteArray(data, boundaryBytes);

            foreach (var part in parts)
            {
                if (part.Length < 10) continue;

                var partString = System.Text.Encoding.UTF8.GetString(part);

                // Skip if it's just the closing boundary
                if (partString.Trim() == "--" || string.IsNullOrWhiteSpace(partString)) continue;

                // Find header/content separator
                var headerEndIndex = partString.IndexOf("\r\n\r\n");
                if (headerEndIndex < 0) continue;

                var headers = partString.Substring(0, headerEndIndex);

                // Parse Content-Disposition
                var nameMatch = System.Text.RegularExpressions.Regex.Match(headers, @"name=""([^""]+)""");
                var fileNameMatch = System.Text.RegularExpressions.Regex.Match(headers, @"filename=""([^""]+)""");
                var contentTypeMatch = System.Text.RegularExpressions.Regex.Match(headers, @"Content-Type:\s*(.+?)(\r\n|$)");

                if (!nameMatch.Success) continue;

                var name = nameMatch.Groups[1].Value;

                if (fileNameMatch.Success)
                {
                    // It's a file
                    var fileName = fileNameMatch.Groups[1].Value;
                    var fileContentType = contentTypeMatch.Success ? contentTypeMatch.Groups[1].Value.Trim() : "application/octet-stream";

                    // Find content start in bytes
                    var headerBytes = System.Text.Encoding.UTF8.GetBytes(partString.Substring(0, headerEndIndex + 4));
                    var contentStart = headerBytes.Length;

                    // Content ends before \r\n at the end
                    var contentLength = part.Length - contentStart;
                    if (contentLength > 2 && part[part.Length - 2] == '\r' && part[part.Length - 1] == '\n')
                    {
                        contentLength -= 2;
                    }

                    var fileData = new byte[contentLength];
                    Array.Copy(part, contentStart, fileData, 0, contentLength);

                    var formFile = new FormFile(fileData)
                    {
                        Name = name,
                        FileName = fileName,
                        ContentType = fileContentType
                    };
                    result.Files.Add(formFile);
                }
                else
                {
                    // It's a field
                    var value = partString.Substring(headerEndIndex + 4).TrimEnd('\r', '\n');
                    result.Fields[name] = value;
                }
            }
        }

        private static List<byte[]> SplitByteArray(byte[] data, byte[] delimiter)
        {
            var result = new List<byte[]>();
            var start = 0;

            for (var i = 0; i <= data.Length - delimiter.Length; i++)
            {
                var match = true;
                for (var j = 0; j < delimiter.Length; j++)
                {
                    if (data[i + j] != delimiter[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    if (i > start)
                    {
                        var part = new byte[i - start];
                        Array.Copy(data, start, part, 0, part.Length);
                        result.Add(part);
                    }
                    start = i + delimiter.Length;
                    i += delimiter.Length - 1;
                }
            }

            if (start < data.Length)
            {
                var part = new byte[data.Length - start];
                Array.Copy(data, start, part, 0, part.Length);
                result.Add(part);
            }

            return result;
        }
    }
}