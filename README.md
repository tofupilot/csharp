# TofuPilot C# Client

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Tests](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/upview/3c4792a8e7e8e8d0b37e141e95cc885e/raw/csharp-client-tests.json)](https://github.com/tofupilot/csharp)

The official C# client for [TofuPilot](https://tofupilot.com). Integrate your hardware test runs into one app with just a few lines of C#.

## Installation

Add a project reference:

```bash
dotnet add reference path/to/TofuPilot.csproj
```

## Quick Start

```csharp
using TofuPilot;
using TofuPilot.Models.Requests;

var client = new TofuPilot(apiKey: Environment.GetEnvironmentVariable("TOFUPILOT_API_KEY")!);

var run = await client.Runs.CreateAsync(new RunCreateRequest
{
    ProcedureId = "your-procedure-id",
    SerialNumber = "SN001",
    PartNumber = "PN001",
    Outcome = RunCreateOutcome.Pass,
    StartedAt = DateTime.UtcNow.AddMinutes(-5),
    EndedAt = DateTime.UtcNow,
});

Console.WriteLine($"Run created: {run.Id}");
```

All async methods support `CancellationToken`:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var run = await client.Runs.CreateAsync(request, cts.Token);
```

## Authentication

Set your API key as an environment variable:

```bash
export TOFUPILOT_API_KEY="your-api-key"
```

Or pass it directly:

```csharp
var client = new TofuPilot(apiKey: "your-api-key");
```

To point to a different server (e.g. self-hosted):

```csharp
var client = new TofuPilot(
    apiKey: "your-api-key",
    serverUrl: "https://your-instance.com/api"
);
```

### Custom certificates

```csharp
using System.Security.Cryptography.X509Certificates;
using TofuPilot.Utils;

var handler = new HttpClientHandler();
handler.ClientCertificates.Add(new X509Certificate2("client.pfx", "password"));

var client = new TofuPilot(
    apiKey: "your-api-key",
    client: new TofuPilotHttpClient(handler)
);
```

## Available Resources

| Resource | Operations |
|----------|-----------|
| `client.Runs` | List, Create, Get, Delete, Update |
| `client.Units` | List, Create, Get, Delete, Update, AddChild, RemoveChild |
| `client.Parts` | List, Create, Get, Delete, Update |
| `client.Parts.Revisions` | Create, Get, Delete, Update |
| `client.Procedures` | List, Create, Get, Delete, Update |
| `client.Procedures.Versions` | Create, Get, Delete |
| `client.Batches` | List, Create, Get, Delete, Update |
| `client.Stations` | List, Create, Get, GetCurrent, Remove, Update |
| `client.Attachments` | Initialize, Finalize, Delete |
| `client.User` | List |

## Usage Examples

### Create a run with measurements

```csharp
var run = await client.Runs.CreateAsync(new RunCreateRequest
{
    ProcedureId = procedureId,
    SerialNumber = "SN-001",
    PartNumber = "PCB-V1",
    Outcome = RunCreateOutcome.Pass,
    StartedAt = DateTime.UtcNow.AddMinutes(-5),
    EndedAt = DateTime.UtcNow,
    Phases = new List<RunCreatePhases>
    {
        new()
        {
            Name = "Voltage Test",
            Outcome = RunCreatePhasesOutcome.Pass,
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            EndedAt = DateTime.UtcNow,
            Measurements = new List<RunCreateMeasurements>
            {
                new()
                {
                    Name = "Output Voltage",
                    Outcome = RunCreateMeasurementsOutcome.Pass,
                    MeasuredValue = 3.3,
                    Units = new RunCreateUnits { Value = "V" },
                    Validators = new List<RunCreateValidators>
                    {
                        new() { Operator = ">=", ExpectedValue = RunCreateExpectedValue.CreateNumber(3.0) },
                        new() { Operator = "<=", ExpectedValue = RunCreateExpectedValue.CreateNumber(3.6) },
                    },
                },
            },
        },
    },
});
```

### List and filter runs

```csharp
var result = await client.Runs.ListAsync(
    partNumbers: new List<string> { "PCB-V1" },
    outcomes: new List<RunListQueryParamOutcome> { RunListQueryParamOutcome.Pass },
    limit: 10
);

foreach (var run in result.Data)
    Console.WriteLine($"{run.Id} — {run.Unit.SerialNumber}");
```

### Manage units and sub-units

```csharp
// Create part and revision
await client.Parts.CreateAsync(new PartCreateRequest { Number = "PCB-V1", Name = "Main Board" });
await client.Parts.Revisions.CreateAsync("PCB-V1", new PartCreateRevisionRequestBody { Number = "REV-A" });

// Create units
await client.Units.CreateAsync(new UnitCreateRequest
{
    SerialNumber = "PARENT-001",
    PartNumber = "PCB-V1",
    RevisionNumber = "REV-A",
});
await client.Units.CreateAsync(new UnitCreateRequest
{
    SerialNumber = "CHILD-001",
    PartNumber = "PCB-V1",
    RevisionNumber = "REV-A",
});

// Link parent-child
await client.Units.AddChildAsync("PARENT-001", new UnitAddChildRequestBody
{
    ChildSerialNumber = "CHILD-001",
});
```

### Upload and download attachments

```csharp
// Upload a file (one line)
var attachmentId = await client.Attachments.UploadAsync("report.pdf");

// Link to a run
await client.Runs.UpdateAsync(runId, new RunUpdateRequestBody
{
    Attachments = new List<string> { attachmentId },
});

// Download an attachment
await client.Attachments.DownloadAsync(downloadUrl, "local-copy.pdf");
```

## Error Handling

API errors throw typed exceptions:

```csharp
using TofuPilot.Models.Errors;

try
{
    await client.Runs.GetAsync("nonexistent-id");
}
catch (NotFoundException ex)
{
    Console.WriteLine($"Not found: {ex.Message}");
}
catch (BadRequestException ex)
{
    Console.WriteLine($"Bad request: {ex.Message}");
}
catch (ApiException ex)
{
    Console.WriteLine($"API error {ex.StatusCode}: {ex.Body}");
}
```

| Exception | Status Code |
|-----------|------------|
| `BadRequestException` | 400 |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| `UnprocessableContentException` | 422 |
| `InternalServerErrorException` | 500 |
| `ApiException` | Any other |

## Running Tests

```bash
cd clients/csharp/tests
# Create .env.local with your API key and URL:
# TOFUPILOT_URL=http://localhost:3000
# TOFUPILOT_API_KEY_USER=your-api-key
dotnet test
```

## Documentation

- [Getting Started](https://tofupilot.com/docs/dashboard)
- [API Reference](https://tofupilot.com/docs/dashboard/api/v2)
- [Changelog](https://tofupilot.com/changelog)

## Acknowledgments

This package builds on the original C# client created by [@Hylaean](https://github.com/Hylaean) (versions 1.x).

## License

MIT
