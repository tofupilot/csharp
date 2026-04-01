using System;
using System.Linq;
using System.Threading.Tasks;
using TofuPilot;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

[Collection("API")]
public class UsersTests
{
    private readonly TofuPilot _client;

    public UsersTests(TestFixture fixture)
    {
        _client = fixture.Client;
    }

    private string Uid() => Guid.NewGuid().ToString("N")[..8];

    [Fact]
    public async Task ListUsers_ReturnsList()
    {
        var result = await _client.User.ListAsync();
        Assert.NotEmpty(result);
        Assert.All(result, u => Assert.False(string.IsNullOrEmpty(u.Id)));
    }

    [Fact]
    public async Task ListUsers_Current()
    {
        var result = await _client.User.ListAsync(current: true);
        Assert.Single(result);
        Assert.False(string.IsNullOrEmpty(result[0].Id));
        Assert.False(string.IsNullOrEmpty(result[0].Email));
    }
}
