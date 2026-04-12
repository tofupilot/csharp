using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TofuPilot.Models.Requests;

namespace TofuPilot
{
    /// <summary>
    /// Sub-resource for run attachments: client.Runs.Attachments.CreateAsync() / .DownloadAsync()
    /// </summary>
    public class RunAttachments
    {
        private readonly IRuns _runs;

        internal RunAttachments(IRuns runs)
        {
            _runs = runs;
        }

        /// <summary>
        /// Upload a file and attach it to a run. Returns the attachment ID.
        /// </summary>
        public async Task<string> UploadAsync(string runId, string filePath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}", filePath);

            var fileName = Path.GetFileName(filePath);
            var result = await _runs.CreateAttachmentAsync(runId, new RunCreateAttachmentRequestBody { Name = fileName }, cancellationToken);

            await UploadToPresignedUrl(filePath, result.UploadUrl, cancellationToken);
            return result.Id;
        }

        /// <summary>
        /// Download an attachment to a local file.
        /// </summary>
        public async Task<string> DownloadAsync(string downloadUrl, string destinationPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(downloadUrl))
                throw new ArgumentException("Download URL cannot be null or empty.", nameof(downloadUrl));

            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(downloadUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Download failed with status {(int)response.StatusCode}");

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            await File.WriteAllBytesAsync(destinationPath, bytes, cancellationToken);
            return destinationPath;
        }

        private static async Task UploadToPresignedUrl(string filePath, string uploadUrl, CancellationToken cancellationToken)
        {
            var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            using var httpClient = new HttpClient();
            var content = new ByteArrayContent(fileBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(filePath));

            var response = await httpClient.PutAsync(uploadUrl, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"File upload failed with status {(int)response.StatusCode}");
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
                ".txt" or ".log" => "text/plain",
                ".html" or ".htm" => "text/html",
                _ => "application/octet-stream",
            };
        }
    }

    /// <summary>
    /// Sub-resource for unit attachments: client.Units.Attachments.CreateAsync() / .DownloadAsync() / .DeleteAsync()
    /// </summary>
    public class UnitAttachments
    {
        private readonly IUnits _units;

        internal UnitAttachments(IUnits units)
        {
            _units = units;
        }

        /// <summary>
        /// Upload a file and attach it to a unit. Returns the attachment ID.
        /// </summary>
        public async Task<string> UploadAsync(string serialNumber, string filePath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}", filePath);

            var fileName = Path.GetFileName(filePath);
            var result = await _units.CreateAttachmentAsync(serialNumber, new UnitCreateAttachmentRequestBody { Name = fileName }, cancellationToken);

            await UploadToPresignedUrl(filePath, result.UploadUrl, cancellationToken);
            return result.Id;
        }

        /// <summary>
        /// Download an attachment to a local file.
        /// </summary>
        public async Task<string> DownloadAsync(string downloadUrl, string destinationPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(downloadUrl))
                throw new ArgumentException("Download URL cannot be null or empty.", nameof(downloadUrl));

            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(downloadUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Download failed with status {(int)response.StatusCode}");

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            await File.WriteAllBytesAsync(destinationPath, bytes, cancellationToken);
            return destinationPath;
        }

        /// <summary>
        /// Delete attachments from a unit by their IDs.
        /// </summary>
        public async Task<UnitDeleteAttachmentResponse> DeleteAsync(string serialNumber, List<string> ids, CancellationToken cancellationToken = default)
        {
            return await _units.DeleteAttachmentAsync(serialNumber, ids, cancellationToken);
        }

        private static async Task UploadToPresignedUrl(string filePath, string uploadUrl, CancellationToken cancellationToken)
        {
            var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            using var httpClient = new HttpClient();
            var content = new ByteArrayContent(fileBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(filePath));

            var response = await httpClient.PutAsync(uploadUrl, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"File upload failed with status {(int)response.StatusCode}");
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
                ".txt" or ".log" => "text/plain",
                ".html" or ".htm" => "text/html",
                _ => "application/octet-stream",
            };
        }
    }

    /// <summary>
    /// Extension methods to expose Attachments sub-resources on Runs and Units.
    /// </summary>
    public static class AttachmentSubResourceExtensions
    {
        private static readonly Dictionary<int, RunAttachments> _runAttachments = new();
        private static readonly Dictionary<int, UnitAttachments> _unitAttachments = new();

        public static RunAttachments Attachments(this IRuns runs)
        {
            var key = runs.GetHashCode();
            if (!_runAttachments.TryGetValue(key, out var attachments))
            {
                attachments = new RunAttachments(runs);
                _runAttachments[key] = attachments;
            }
            return attachments;
        }

        public static UnitAttachments Attachments(this IUnits units)
        {
            var key = units.GetHashCode();
            if (!_unitAttachments.TryGetValue(key, out var attachments))
            {
                attachments = new UnitAttachments(units);
                _unitAttachments[key] = attachments;
            }
            return attachments;
        }
    }
}
