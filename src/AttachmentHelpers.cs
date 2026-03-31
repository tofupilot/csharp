using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using TofuPilot.Models.Requests;

namespace TofuPilot
{
    /// <summary>
    /// Convenience methods for attachment upload and download.
    /// </summary>
    public static class AttachmentHelpers
    {
        /// <summary>
        /// Upload a file and return its attachment ID.
        /// Handles the full workflow: initialize → upload to storage → finalize.
        /// </summary>
        /// <param name="attachments">The attachments resource.</param>
        /// <param name="filePath">Path to the file to upload.</param>
        /// <returns>The attachment ID (use with Units.UpdateAsync or Runs.UpdateAsync).</returns>
        public static async Task<string> UploadAsync(this IAttachments attachments, string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}", filePath);

            var fileName = Path.GetFileName(filePath);
            var init = await attachments.InitializeAsync(new AttachmentInitializeRequest { Name = fileName });

            var fileBytes = await File.ReadAllBytesAsync(filePath);
            using var httpClient = new HttpClient();
            var content = new ByteArrayContent(fileBytes);

            var contentType = GetContentType(filePath);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            var uploadResponse = await httpClient.PutAsync(init.UploadUrl, content);
            if (!uploadResponse.IsSuccessStatusCode)
                throw new InvalidOperationException($"File upload failed with status {(int)uploadResponse.StatusCode}");

            await attachments.FinalizeAsync(init.Id);
            return init.Id;
        }

        /// <summary>
        /// Download an attachment to a local file.
        /// </summary>
        /// <param name="attachments">The attachments resource.</param>
        /// <param name="downloadUrl">The download URL from an attachment object.</param>
        /// <param name="destinationPath">Destination file path.</param>
        /// <returns>The path to the downloaded file.</returns>
        public static async Task<string> DownloadAsync(this IAttachments attachments, string downloadUrl, string destinationPath)
        {
            if (string.IsNullOrEmpty(downloadUrl))
                throw new ArgumentException("Download URL cannot be null or empty.", nameof(downloadUrl));

            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(downloadUrl);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Download failed with status {(int)response.StatusCode}");

            var bytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(destinationPath, bytes);
            return destinationPath;
        }

        private static string GetContentType(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".csv" => "text/csv",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".zip" => "application/zip",
                ".txt" => "text/plain",
                ".html" or ".htm" => "text/html",
                _ => "application/octet-stream",
            };
        }
    }
}
