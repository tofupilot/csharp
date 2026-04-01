using System;
using System.Threading.Tasks;
using TofuPilot;
using TofuPilot.Models.Errors;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

[Collection("API")]
public class ProcedureVersionsTests
{
    private readonly TofuPilot _client;

    public ProcedureVersionsTests(TestFixture fixture)
    {
        _client = fixture.Client;
    }

    private string Uid() => Guid.NewGuid().ToString("N")[..8];

    private async Task<string> CreateProcedureAsync(string? uid = null)
    {
        uid ??= Uid();
        var proc = await _client.Procedures.CreateAsync(new ProcedureCreateRequest
        {
            Name = $"Proc Ver {uid}",
        });
        return proc.Id;
    }

    [Fact]
    public async Task CreateVersion_ReturnsId()
    {
        var uid = Uid();
        var procId = await CreateProcedureAsync(uid);

        var version = await _client.Procedures.Versions.CreateAsync(procId, new ProcedureCreateVersionRequestBody
        {
            Tag = $"v{uid}",
        });
        Assert.False(string.IsNullOrEmpty(version.Id));
    }

    [Fact]
    public async Task GetVersion_ReturnsMatchingData()
    {
        var uid = Uid();
        var procId = await CreateProcedureAsync(uid);
        var tag = $"v-g-{uid}";

        var created = await _client.Procedures.Versions.CreateAsync(procId, new ProcedureCreateVersionRequestBody
        {
            Tag = tag,
        });

        var fetched = await _client.Procedures.Versions.GetAsync(procId, tag);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(tag, fetched.Tag);
        Assert.NotEqual(default, fetched.CreatedAt);
        Assert.NotNull(fetched.Procedure);
    }

    [Fact]
    public async Task GetVersion_Nonexistent_ThrowsNotFound()
    {
        var procId = await CreateProcedureAsync();

        await Assert.ThrowsAsync<NotFoundException>(
            () => _client.Procedures.Versions.GetAsync(procId, $"v-none-{Uid()}"));
    }

    [Fact]
    public async Task DeleteVersion_ReturnsId()
    {
        var uid = Uid();
        var procId = await CreateProcedureAsync(uid);
        var tag = $"v-d-{uid}";

        var created = await _client.Procedures.Versions.CreateAsync(procId, new ProcedureCreateVersionRequestBody
        {
            Tag = tag,
        });

        var deleted = await _client.Procedures.Versions.DeleteAsync(procId, tag);
        Assert.Equal(created.Id, deleted.Id);
    }

    [Fact]
    public async Task DeleteVersion_Nonexistent_ThrowsNotFound()
    {
        var procId = await CreateProcedureAsync();

        await Assert.ThrowsAsync<NotFoundException>(
            () => _client.Procedures.Versions.DeleteAsync(procId, $"v-none-{Uid()}"));
    }
}
