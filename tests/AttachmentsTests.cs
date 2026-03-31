using System;
using System.Collections.Generic;
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
    private readonly TofuPilotSDK _client;

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
        await Assert.ThrowsAsync<ErrorNOTFOUND>(() =>
            _client.Attachments.FinalizeAsync(Guid.NewGuid().ToString()));
    }
}
