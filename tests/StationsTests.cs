using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TofuPilot;
using TofuPilot.Models.Errors;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

[Collection("API")]
public class StationsTests
{
    private readonly TofuPilot _client;

    public StationsTests(TestFixture fixture)
    {
        _client = fixture.Client;
    }

    private string Uid() => Guid.NewGuid().ToString("N")[..8];

    [Fact]
    public async Task CreateStation_ReturnsId()
    {
        var result = await _client.Stations.CreateAsync(new StationCreateRequest
        {
            Name = $"Station-CRE-{Uid()}",
        });
        Assert.False(string.IsNullOrEmpty(result.Id));
    }

    [Fact]
    public async Task GetStation_ReturnsMatchingData()
    {
        var name = $"Station-GET-{Uid()}";
        var created = await _client.Stations.CreateAsync(new StationCreateRequest
        {
            Name = name,
        });
        var fetched = await _client.Stations.GetAsync(created.Id);
        Assert.Equal(name, fetched.Name);
        Assert.Equal(created.Id, fetched.Id);
    }

    [Fact]
    public async Task GetStation_Nonexistent_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _client.Stations.GetAsync(Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task ListStations_ReturnsList()
    {
        await _client.Stations.CreateAsync(new StationCreateRequest
        {
            Name = $"Station-LST-{Uid()}",
        });
        var result = await _client.Stations.ListAsync();
        Assert.NotEmpty(result.Data);
    }

    [Fact]
    public async Task ListStations_WithSearchQuery()
    {
        var name = $"Station-SRQ-{Uid()}";
        await _client.Stations.CreateAsync(new StationCreateRequest
        {
            Name = name,
        });
        var result = await _client.Stations.ListAsync(searchQuery: name);
        Assert.NotEmpty(result.Data);
        Assert.Contains(result.Data, s => s.Name == name);
    }

    [Fact]
    public async Task ListStations_Pagination()
    {
        for (int i = 0; i < 3; i++)
        {
            await _client.Stations.CreateAsync(new StationCreateRequest
            {
                Name = $"Station-PAG{i}-{Uid()}",
            });
        }

        var page1 = await _client.Stations.ListAsync(limit: 1);
        Assert.Single(page1.Data);
        Assert.True(page1.Meta.HasMore);
        Assert.NotNull(page1.Meta.NextCursor);

        var page2 = await _client.Stations.ListAsync(limit: 1, cursor: page1.Meta.NextCursor);
        Assert.Single(page2.Data);
        Assert.NotEqual(page1.Data[0].Id, page2.Data[0].Id);
    }

    [Fact]
    public async Task RemoveStation_ReturnsId()
    {
        var created = await _client.Stations.CreateAsync(new StationCreateRequest
        {
            Name = $"Station-REM-{Uid()}",
        });
        var removed = await _client.Stations.RemoveAsync(created.Id);
        Assert.Equal(created.Id, removed.Id);
    }

    [Fact]
    public async Task RemoveStation_Nonexistent_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _client.Stations.RemoveAsync(Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task RemoveStation_Twice_Fails()
    {
        var created = await _client.Stations.CreateAsync(new StationCreateRequest
        {
            Name = $"Station-RMT-{Uid()}",
        });
        await _client.Stations.RemoveAsync(created.Id);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _client.Stations.RemoveAsync(created.Id));
    }

    [Fact]
    public async Task UpdateStation_Name()
    {
        var created = await _client.Stations.CreateAsync(new StationCreateRequest
        {
            Name = $"Station-UPD-{Uid()}",
        });
        var newName = $"Station-UPDATED-{Uid()}";
        await _client.Stations.UpdateAsync(created.Id, new StationUpdateRequestBody
        {
            Name = newName,
        });
        var fetched = await _client.Stations.GetAsync(created.Id);
        Assert.Equal(newName, fetched.Name);
    }

    [Fact]
    public async Task UpdateStation_Nonexistent_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _client.Stations.UpdateAsync(Guid.NewGuid().ToString(), new StationUpdateRequestBody
            {
                Name = "whatever",
            }));
    }

    [Fact]
    public async Task CreateStation_DuplicateName_ThrowsConflict()
    {
        var name = $"Station-DUPE-{Uid()}";
        await _client.Stations.CreateAsync(new StationCreateRequest
        {
            Name = name,
        });
        await Assert.ThrowsAsync<ConflictException>(() =>
            _client.Stations.CreateAsync(new StationCreateRequest
            {
                Name = name,
            }));
    }

    [Fact]
    public async Task GetCurrent_WithUserKey_ThrowsForbidden()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _client.Stations.GetCurrentAsync());
    }
}
