using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TofuPilot;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

[Collection("API")]
public class RunsCreateAggregationsTests
{
    private readonly TofuPilot _client;
    private readonly string _procedureId;

    public RunsCreateAggregationsTests(TestFixture fixture)
    {
        _client = fixture.Client;
        _procedureId = fixture.ProcedureId;
    }

    private string Uid() => Guid.NewGuid().ToString("N")[..8];

    private RunCreateRequest BaseRequest(string? uid = null)
    {
        uid ??= Uid();
        var now = DateTime.UtcNow;
        return new RunCreateRequest
        {
            SerialNumber = $"SN-A-{uid}",
            ProcedureId = _procedureId,
            PartNumber = $"PART-A-{uid}",
            StartedAt = now.AddMinutes(-5),
            EndedAt = now,
            Outcome = RunCreateOutcome.Pass,
        };
    }

    private RunCreateRequest WithAggregations(string uid, string measName, double measuredValue, List<RunCreateMeasurementsAggregations> aggregations)
    {
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "agg_phase",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = measName,
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        MeasuredValue = measuredValue,
                        Aggregations = aggregations,
                    },
                },
            },
        };
        return req;
    }

    [Theory]
    [InlineData("avg")]
    [InlineData("min")]
    [InlineData("max")]
    [InlineData("sum")]
    [InlineData("count")]
    [InlineData("std")]
    [InlineData("median")]
    public async Task CreateRun_AggregationType(string aggType)
    {
        var uid = Uid();
        var aggs = new List<RunCreateMeasurementsAggregations>
        {
            new RunCreateMeasurementsAggregations
            {
                Type = aggType,
                Value = RunCreateMeasurementsValue.CreateNumber(42.0),
                Outcome = "PASS",
            },
        };
        var req = WithAggregations(uid, $"test_{aggType}", 50.0, aggs);
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.NotNull(fetched.Phases);
        var measurement = fetched.Phases[0].Measurements[0];
        Assert.NotNull(measurement.Aggregations);
        Assert.Single(measurement.Aggregations);
        Assert.Equal(aggType, measurement.Aggregations[0].Type, ignoreCase: true);
    }

    [Fact]
    public async Task CreateRun_MultipleAggregationsOnSingleMeasurement()
    {
        var uid = Uid();
        var aggs = new List<RunCreateMeasurementsAggregations>
        {
            new RunCreateMeasurementsAggregations
            {
                Type = "avg",
                Value = RunCreateMeasurementsValue.CreateNumber(72.3),
                Outcome = "PASS",
            },
            new RunCreateMeasurementsAggregations
            {
                Type = "max",
                Value = RunCreateMeasurementsValue.CreateNumber(80.1),
            },
            new RunCreateMeasurementsAggregations
            {
                Type = "min",
                Value = RunCreateMeasurementsValue.CreateNumber(65.0),
            },
        };
        var req = WithAggregations(uid, "multi_agg", 75.5, aggs);
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.Equal(3, fetched.Phases![0].Measurements[0].Aggregations!.Count);
    }

    [Fact]
    public async Task CreateRun_AggregationWithStringValue()
    {
        var uid = Uid();
        var aggs = new List<RunCreateMeasurementsAggregations>
        {
            new RunCreateMeasurementsAggregations
            {
                Type = "mode",
                Value = RunCreateMeasurementsValue.CreateStr("nominal"),
            },
        };
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "str_agg_phase",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "status_mode",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        MeasuredValue = "nominal",
                        Aggregations = aggs,
                    },
                },
            },
        };
        var created = await _client.Runs.CreateAsync(req);
        Assert.False(string.IsNullOrEmpty(created.Id));
    }

    [Fact]
    public async Task CreateRun_AggregationWithBooleanValue()
    {
        var uid = Uid();
        var aggs = new List<RunCreateMeasurementsAggregations>
        {
            new RunCreateMeasurementsAggregations
            {
                Type = "all",
                Value = RunCreateMeasurementsValue.CreateBoolean(true),
            },
        };
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "bool_agg_phase",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "all_pass",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        MeasuredValue = true,
                        Aggregations = aggs,
                    },
                },
            },
        };
        var created = await _client.Runs.CreateAsync(req);
        Assert.False(string.IsNullOrEmpty(created.Id));
    }

    [Fact]
    public async Task CreateRun_AggregationWithValidators()
    {
        var uid = Uid();
        var aggs = new List<RunCreateMeasurementsAggregations>
        {
            new RunCreateMeasurementsAggregations
            {
                Type = "avg",
                Value = RunCreateMeasurementsValue.CreateNumber(72.3),
                Outcome = "PASS",
                Validators = new List<RunCreateMeasurementsAggregationsValidators>
                {
                    new RunCreateMeasurementsAggregationsValidators
                    {
                        Operator = ">=",
                        ExpectedValue = RunCreateMeasurementsAggregationsExpectedValue.CreateNumber(60.0),
                        Outcome = "PASS",
                    },
                    new RunCreateMeasurementsAggregationsValidators
                    {
                        Operator = "<=",
                        ExpectedValue = RunCreateMeasurementsAggregationsExpectedValue.CreateNumber(90.0),
                        Outcome = "PASS",
                    },
                },
            },
        };
        var req = WithAggregations(uid, "agg_with_val", 75.0, aggs);
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        var agg = fetched.Phases![0].Measurements[0].Aggregations![0];
        Assert.NotNull(agg.Validators);
        Assert.Equal(2, agg.Validators.Count);
    }

    [Fact]
    public async Task CreateRun_AggregationOutcomePass()
    {
        var uid = Uid();
        var aggs = new List<RunCreateMeasurementsAggregations>
        {
            new RunCreateMeasurementsAggregations
            {
                Type = "avg",
                Value = RunCreateMeasurementsValue.CreateNumber(50.0),
                Outcome = "PASS",
            },
        };
        var req = WithAggregations(uid, "agg_pass", 50.0, aggs);
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.Equal("PASS", fetched.Phases![0].Measurements[0].Aggregations![0].Outcome);
    }

    [Fact]
    public async Task CreateRun_AggregationOutcomeFail()
    {
        var uid = Uid();
        var aggs = new List<RunCreateMeasurementsAggregations>
        {
            new RunCreateMeasurementsAggregations
            {
                Type = "avg",
                Value = RunCreateMeasurementsValue.CreateNumber(50.0),
                Outcome = "FAIL",
            },
        };
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Outcome = RunCreateOutcome.Fail;
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "fail_agg",
                Outcome = RunCreatePhasesOutcome.Fail,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "agg_fail",
                        Outcome = RunCreateMeasurementsOutcome.Fail,
                        MeasuredValue = 50.0,
                        Aggregations = aggs,
                    },
                },
            },
        };
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.Equal("FAIL", fetched.Phases![0].Measurements[0].Aggregations![0].Outcome);
    }

    [Fact]
    public async Task CreateRun_AggregationWithSpecialCharType()
    {
        var uid = Uid();
        var aggs = new List<RunCreateMeasurementsAggregations>
        {
            new RunCreateMeasurementsAggregations
            {
                Type = "percentile_95",
                Value = RunCreateMeasurementsValue.CreateNumber(95.0),
            },
        };
        var req = WithAggregations(uid, "special_type", 90.0, aggs);
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.Equal("percentile_95", fetched.Phases![0].Measurements[0].Aggregations![0].Type, ignoreCase: true);
    }

    [Fact]
    public async Task CreateRun_AggregationWithNegativeValue()
    {
        var uid = Uid();
        var aggs = new List<RunCreateMeasurementsAggregations>
        {
            new RunCreateMeasurementsAggregations
            {
                Type = "min",
                Value = RunCreateMeasurementsValue.CreateNumber(-15.5),
            },
        };
        var req = WithAggregations(uid, "neg_agg", -10.0, aggs);
        var created = await _client.Runs.CreateAsync(req);
        Assert.False(string.IsNullOrEmpty(created.Id));
    }

    [Fact]
    public async Task CreateRun_AggregationValidatorWithIsDecisive()
    {
        var uid = Uid();
        var aggs = new List<RunCreateMeasurementsAggregations>
        {
            new RunCreateMeasurementsAggregations
            {
                Type = "avg",
                Value = RunCreateMeasurementsValue.CreateNumber(72.0),
                Outcome = "FAIL",
                Validators = new List<RunCreateMeasurementsAggregationsValidators>
                {
                    new RunCreateMeasurementsAggregationsValidators
                    {
                        Operator = ">=",
                        ExpectedValue = RunCreateMeasurementsAggregationsExpectedValue.CreateNumber(80.0),
                        Outcome = "FAIL",
                        IsDecisive = false,
                    },
                },
            },
        };
        var req = WithAggregations(uid, "agg_decisive", 72.0, aggs);
        var created = await _client.Runs.CreateAsync(req);
        Assert.False(string.IsNullOrEmpty(created.Id));
    }
}
