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
        private readonly IAttachments? _attachments;

        internal RunAttachments(IRuns runs)
        {
            _runs = runs;
            // Finalize lives on the attachments API; the concrete Runs shares
            // its config. A wrapped or mocked IRuns cannot reach it — the
            // upload path warns when it actually needs finalize.
            _attachments = runs is Runs concrete ? new Attachments(concrete.SDKConfiguration) : null;
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
            await FinalizeBestEffort(_attachments, result.Id, cancellationToken);
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
        /// The PUT stores the bytes but records no metadata; finalize stamps
        /// size and content type from the stored object. Best-effort: the
        /// attachment is already stored and linked, so a metadata failure
        /// must not fail the upload — the caller would retry and duplicate
        /// it. The warning is the signal that size will read 0.
        /// </summary>
        internal static async Task FinalizeBestEffort(IAttachments? attachments, string uploadId, CancellationToken cancellationToken)
        {
            if (attachments == null)
            {
                // Warned here, not in the constructor: only uploads need
                // finalize, so download-only use of a wrapped client stays
                // quiet, and the CWT factory re-running can't duplicate it.
                Console.Error.WriteLine(
                    $"tofupilot: attachment {uploadId} cannot be finalized (client is not the SDK's concrete Runs/Units) and will show size 0.");
                return;
            }
            try
            {
                await attachments.FinalizeAsync(uploadId, cancellationToken);
            }
            // Cancellation is the caller's request, not a finalize failure —
            // it must keep its normal semantics and propagate.
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Console.Error.WriteLine(
                    $"tofupilot: attachment {uploadId} uploaded but not finalized (size will read 0): {e.Message}");
            }
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
        private readonly IAttachments? _attachments;

        internal UnitAttachments(IUnits units)
        {
            _units = units;
            // See RunAttachments: finalize needs the attachments API.
            _attachments = units is Units concrete ? new Attachments(concrete.SDKConfiguration) : null;
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
            await RunAttachments.FinalizeBestEffort(_attachments, result.Id, cancellationToken);
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
        // Keyed on object identity, not GetHashCode: a 32-bit identity hash
        // can collide across clients, which would hand one tenant's cached
        // sub-resource (and its credentials) to another. ConditionalWeakTable
        // is also thread-safe and lets entries die with their client.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IRuns, RunAttachments> _runAttachments = new();
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IUnits, UnitAttachments> _unitAttachments = new();

        public static RunAttachments Attachments(this IRuns runs)
        {
            return _runAttachments.GetValue(runs, static r => new RunAttachments(r));
        }

        public static UnitAttachments Attachments(this IUnits units)
        {
            return _unitAttachments.GetValue(units, static u => new UnitAttachments(u));
        }
    }
}
