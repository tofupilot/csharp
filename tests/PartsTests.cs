using System;
using System.Threading.Tasks;
using TofuPilot;
using TofuPilot.Models.Errors;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

[Collection("API")]
public class PartsTests
{
    private readonly TofuPilotSDK _client;

    public PartsTests(TestFixture fixture)
    {
        _client = fixture.Client;
    }

    private string Uid() => Guid.NewGuid().ToString("N")[..8];

    [Fact]
    public async Task CreatePart_ReturnsId()
    {
        var uid = Uid();
        var result = await _client.Parts.CreateAsync(new PartCreateRequest
        {
            Number = $"PART-CS-{uid}",
            Name = $"Test Part {uid}",
        });
        Assert.False(string.IsNullOrEmpty(result.Id));
    }

    [Fact]
    public async Task GetPart_ReturnsMatchingData()
    {
        var uid = Uid();
        var partNumber = $"PART-GET-{uid}";
        var partName = $"Get Part {uid}";
        await _client.Parts.CreateAsync(new PartCreateRequest
        {
            Number = partNumber,
            Name = partName,
        });

        var fetched = await _client.Parts.GetAsync(partNumber);
        Assert.Equal(partNumber, fetched.Number);
        Assert.Equal(partName, fetched.Name);
        Assert.NotEqual(default, fetched.CreatedAt);
    }

    [Fact]
    public async Task GetPart_Nonexistent_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<ErrorNOTFOUND>(
            () => _client.Parts.GetAsync($"PART-NONE-{Uid()}"));
    }

    [Fact]
    public async Task ListParts_ReturnsList()
    {
        await _client.Parts.CreateAsync(new PartCreateRequest
        {
            Number = $"PART-LST-{Uid()}",
            Name = "List Part",
        });

        var result = await _client.Parts.ListAsync();
        Assert.NotEmpty(result.Data);
        Assert.NotNull(result.Meta);
    }

    [Fact]
    public async Task ListParts_WithSearchQuery()
    {
        var uid = Uid();
        var partNumber = $"PART-SRQ-{uid}";
        await _client.Parts.CreateAsync(new PartCreateRequest
        {
            Number = partNumber,
            Name = $"Searchable Part {uid}",
        });

        var result = await _client.Parts.ListAsync(searchQuery: partNumber);
        Assert.NotEmpty(result.Data);
    }

    [Fact]
    public async Task ListParts_Pagination()
    {
        for (int i = 0; i < 3; i++)
        {
            await _client.Parts.CreateAsync(new PartCreateRequest
            {
                Number = $"PART-PG-{Uid()}",
                Name = $"Paginated Part {i}",
            });
        }

        var page1 = await _client.Parts.ListAsync(limit: 1);
        Assert.Single(page1.Data);

        if (page1.Meta.HasMore)
        {
            var page2 = await _client.Parts.ListAsync(limit: 1, cursor: page1.Meta.NextCursor);
            Assert.Single(page2.Data);
            Assert.NotEqual(page1.Data[0].Id, page2.Data[0].Id);
        }
    }

    [Fact]
    public async Task DeletePart_ReturnsId()
    {
        var uid = Uid();
        var partNumber = $"PART-DEL-{uid}";
        var created = await _client.Parts.CreateAsync(new PartCreateRequest
        {
            Number = partNumber,
            Name = $"Del Part {uid}",
        });

        var deleted = await _client.Parts.DeleteAsync(partNumber);
        Assert.Equal(created.Id, deleted.Id);
    }

    [Fact]
    public async Task DeletePart_Nonexistent_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<ErrorNOTFOUND>(
            () => _client.Parts.DeleteAsync($"PART-NONE-{Uid()}"));
    }

    [Fact]
    public async Task UpdatePart_Name()
    {
        var uid = Uid();
        var partNumber = $"PART-UPN-{uid}";
        await _client.Parts.CreateAsync(new PartCreateRequest
        {
            Number = partNumber,
            Name = $"Old Name {uid}",
        });

        var newName = $"New Name {uid}";
        var updated = await _client.Parts.UpdateAsync(partNumber, new PartUpdateRequestBody
        {
            Name = newName,
        });
        Assert.False(string.IsNullOrEmpty(updated.Id));

        var fetched = await _client.Parts.GetAsync(partNumber);
        Assert.Equal(newName, fetched.Name);
    }

    [Fact]
    public async Task UpdatePart_Number()
    {
        var uid = Uid();
        var oldNumber = $"PART-UPO-{uid}";
        var newNumber = $"PART-UPN2-{uid}";
        await _client.Parts.CreateAsync(new PartCreateRequest
        {
            Number = oldNumber,
            Name = $"Rename Part {uid}",
        });

        await _client.Parts.UpdateAsync(oldNumber, new PartUpdateRequestBody
        {
            NewNumber = newNumber,
        });

        var fetched = await _client.Parts.GetAsync(newNumber);
        Assert.Equal(newNumber, fetched.Number);

        await Assert.ThrowsAsync<ErrorNOTFOUND>(
            () => _client.Parts.GetAsync(oldNumber));
    }

    [Fact]
    public async Task UpdatePart_DuplicateNumber_ThrowsConflict()
    {
        var uid = Uid();
        var number1 = $"PART-DUP1-{uid}";
        var number2 = $"PART-DUP2-{uid}";
        await _client.Parts.CreateAsync(new PartCreateRequest { Number = number1, Name = "Dup1" });
        await _client.Parts.CreateAsync(new PartCreateRequest { Number = number2, Name = "Dup2" });

        await Assert.ThrowsAsync<ErrorCONFLICT>(
            () => _client.Parts.UpdateAsync(number2, new PartUpdateRequestBody
            {
                NewNumber = number1,
            }));
    }

    [Fact]
    public async Task CreatePart_DuplicateNumber_ThrowsConflict()
    {
        var uid = Uid();
        var partNumber = $"PART-DDUP-{uid}";
        await _client.Parts.CreateAsync(new PartCreateRequest
        {
            Number = partNumber,
            Name = "First",
        });

        await Assert.ThrowsAsync<ErrorCONFLICT>(
            () => _client.Parts.CreateAsync(new PartCreateRequest
            {
                Number = partNumber,
                Name = "Second",
            }));
    }

    [Fact]
    public async Task CreatePart_EmptyNumber_Fails()
    {
        await Assert.ThrowsAnyAsync<Exception>(
            () => _client.Parts.CreateAsync(new PartCreateRequest
            {
                Number = "",
                Name = "Bad Part",
            }));
    }
}
