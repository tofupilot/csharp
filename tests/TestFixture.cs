using System;
using System.IO;
using TofuPilot;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

public class TestFixture : IDisposable
{
    public TofuPilot Client { get; }
    public string ProcedureId { get; private set; } = "";

    public TestFixture()
    {
        var envPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env.local");
        if (File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                var idx = trimmed.IndexOf('=');
                if (idx > 0)
                    Environment.SetEnvironmentVariable(trimmed[..idx], trimmed[(idx + 1)..]);
            }
        }

        var url = Environment.GetEnvironmentVariable("TOFUPILOT_URL") ?? "http://localhost:3000";
        var apiKey = Environment.GetEnvironmentVariable("TOFUPILOT_API_KEY_USER")
            ?? throw new Exception("TOFUPILOT_API_KEY_USER not set");

        Client = new TofuPilot(apiKey: apiKey, serverUrl: $"{url}/api");

        var proc = Client.Procedures.CreateAsync(new ProcedureCreateRequest
        {
            Name = $"CSharp Test {DateTime.UtcNow:yyyyMMddHHmmss}"
        }).GetAwaiter().GetResult();

        ProcedureId = proc.Id;
    }

    public void Dispose() { }
}

[CollectionDefinition("API")]
public class ApiCollection : ICollectionFixture<TestFixture> { }
