using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using TofuPilot;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

[Collection("API")]
public class AttachmentsTests
{
    private readonly TofuPilot _client;
    private readonly string _procedureId;

    public AttachmentsTests(TestFixture fixture)
    {
        _client = fixture.Client;
        _procedureId = fixture.ProcedureId;
    }

    private string Uid() => Guid.NewGuid().ToString("N")[..8];

    [Fact]
    public async Task RunAttachments_Create_ReturnsId()
    {
        var run = await _client.Runs.CreateAsync(new RunCreateRequest
        {
            ProcedureId = _procedureId,
            SerialNumber = $"ATTACH-{Uid()}",
            PartNumber = "PCB-001",
            Outcome = RunCreateOutcome.Pass,
            StartedAt = DateTime.UtcNow.AddMinutes(-1),
            EndedAt = DateTime.UtcNow,
        });

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "run attachment test");
            var attachmentId = await _client.Runs.Attachments().UploadAsync(run.Id, tempFile);
            Assert.False(string.IsNullOrEmpty(attachmentId));
            Assert.Equal(36, attachmentId.Length);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task RunAttachments_CreateAndVerify()
    {
        var run = await _client.Runs.CreateAsync(new RunCreateRequest
        {
            ProcedureId = _procedureId,
            SerialNumber = $"ATTACH-{Uid()}",
            PartNumber = "PCB-001",
            Outcome = RunCreateOutcome.Pass,
            StartedAt = DateTime.UtcNow.AddMinutes(-1),
            EndedAt = DateTime.UtcNow,
        });

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "verify test");
            var attachmentId = await _client.Runs.Attachments().UploadAsync(run.Id, tempFile);

            var fetched = await _client.Runs.GetAsync(run.Id);
            Assert.NotNull(fetched.Attachments);
            Assert.Contains(fetched.Attachments, a => a.Id == attachmentId);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task RunAttachments_Create_FileNotFound()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _client.Runs.Attachments().UploadAsync("00000000-0000-0000-0000-000000000000", "/nonexistent/file.txt"));
    }

    [Fact]
    public async Task UnitAttachments_CreateAndDelete()
    {
        var serial = $"DELATT-{Uid()}";
        var partNumber = $"DELPART-{Uid()}";
        var revNumber = $"DELREV-{Uid()}";

        await _client.Parts.CreateAsync(new PartCreateRequest { Number = partNumber, Name = $"Part {Uid()}" });
        await _client.Parts.Revisions.CreateAsync(partNumber, new PartCreateRevisionRequestBody { Number = revNumber });
        await _client.Units.CreateAsync(new UnitCreateRequest { SerialNumber = serial, PartNumber = partNumber, RevisionNumber = revNumber });

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "file to delete");
            var attachmentId = await _client.Units.Attachments().UploadAsync(serial, tempFile);
            Assert.False(string.IsNullOrEmpty(attachmentId));

            var result = await _client.Units.Attachments().DeleteAsync(serial, new List<string> { attachmentId });
            Assert.Contains(attachmentId, result.Ids);

            var fetched = await _client.Units.GetAsync(serial);
            Assert.DoesNotContain(fetched.Attachments ?? new List<UnitGetAttachments>(), a => a.Id == attachmentId);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task RunAttachments_Download_EmptyUrl()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _client.Runs.Attachments().DownloadAsync("", "/tmp/test.txt"));
    }
}
