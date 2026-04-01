using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using TofuPilot;
using TofuPilot.Models.Errors;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

[Collection("API")]
public class AttachmentsTests
{
    private readonly TofuPilot _client;

    public AttachmentsTests(TestFixture fixture)
    {
        _client = fixture.Client;
    }

    private string Uid() => Guid.NewGuid().ToString("N")[..8];

    [Fact]
    public async Task Initialize_ReturnsIdAndUploadUrl()
    {
        var result = await _client.Attachments.InitializeAsync(new AttachmentInitializeRequest
        {
            Name = $"test-{Uid()}.txt",
        });
        Assert.False(string.IsNullOrEmpty(result.Id));
        Assert.False(string.IsNullOrEmpty(result.UploadUrl));
    }

    [Fact]
    public async Task FullLifecycle_InitUploadFinalize()
    {
        var initialized = await _client.Attachments.InitializeAsync(new AttachmentInitializeRequest
        {
            Name = $"lifecycle-{Uid()}.txt",
        });
        Assert.False(string.IsNullOrEmpty(initialized.UploadUrl));

        using var httpClient = new HttpClient();
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes("test content"));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        var uploadResponse = await httpClient.PutAsync(initialized.UploadUrl, content);
        uploadResponse.EnsureSuccessStatusCode();

        var finalized = await _client.Attachments.FinalizeAsync(initialized.Id);
        Assert.False(string.IsNullOrEmpty(finalized.Url));
    }

    [Fact]
    public async Task Finalize_Nonexistent_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _client.Attachments.FinalizeAsync(Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task Upload_ReturnsAttachmentId()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "upload helper test content");
            var attachmentId = await _client.Attachments.UploadAsync(tempFile);
            Assert.False(string.IsNullOrEmpty(attachmentId));
            Assert.Equal(36, attachmentId.Length); // UUID format
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Upload_NonexistentFile_ThrowsFileNotFound()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _client.Attachments.UploadAsync("/nonexistent/file.txt"));
    }

    [Fact]
    public async Task UploadAndDownload_Roundtrip()
    {
        var tempUpload = Path.GetTempFileName();
        var tempDownload = Path.Combine(Path.GetTempPath(), $"download-{Uid()}.txt");
        try
        {
            var originalContent = $"roundtrip test {Uid()}";
            await File.WriteAllTextAsync(tempUpload, originalContent);

            // Upload
            var attachmentId = await _client.Attachments.UploadAsync(tempUpload);
            Assert.False(string.IsNullOrEmpty(attachmentId));

            // Get download URL via initialize+finalize (attachment is already finalized)
            // We need to get the URL — it was returned by finalize during upload
            // Re-initialize a new file to test download separately
            var init = await _client.Attachments.InitializeAsync(new AttachmentInitializeRequest
            {
                Name = $"download-test-{Uid()}.txt",
            });
            using var httpClient = new HttpClient();
            var content = new ByteArrayContent(Encoding.UTF8.GetBytes(originalContent));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            await httpClient.PutAsync(init.UploadUrl, content);
            var finalized = await _client.Attachments.FinalizeAsync(init.Id);

            // Download
            var downloadedPath = await _client.Attachments.DownloadAsync(finalized.Url, tempDownload);
            Assert.Equal(tempDownload, downloadedPath);
            Assert.True(File.Exists(tempDownload));

            var downloadedContent = await File.ReadAllTextAsync(tempDownload);
            Assert.Equal(originalContent, downloadedContent);
        }
        finally
        {
            File.Delete(tempUpload);
            if (File.Exists(tempDownload)) File.Delete(tempDownload);
        }
    }

    [Fact]
    public async Task Download_EmptyUrl_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _client.Attachments.DownloadAsync("", "/tmp/test.txt"));
    }
}
