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

    private string Uid() => E2E.Uid();

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
                        MeasuredValue = measuredValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
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
                Value = "42",
                Outcome = RunCreateMeasurementsAggregationsOutcome.Pass,
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
                Value = "72.3",
                Outcome = RunCreateMeasurementsAggregationsOutcome.Pass,
            },
            new RunCreateMeasurementsAggregations
            {
                Type = "max",
                Value = "80.1",
            },
            new RunCreateMeasurementsAggregations
            {
                Type = "min",
                Value = "65",
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
                Value = "nominal",
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
                Value = "true",
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
                        MeasuredValue = "true",
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
                Value = "72.3",
                Outcome = RunCreateMeasurementsAggregationsOutcome.Pass,
                Validators = new List<RunCreateMeasurementsAggregationsValidators>
                {
                    new RunCreateMeasurementsAggregationsValidators
                    {
                        Operator = ">=",
                        ExpectedValue = "60",
                        Outcome = RunCreateMeasurementsAggregationsValidatorsOutcome.Pass,
                    },
                    new RunCreateMeasurementsAggregationsValidators
                    {
                        Operator = "<=",
                        ExpectedValue = "90",
                        Outcome = RunCreateMeasurementsAggregationsValidatorsOutcome.Pass,
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
                Value = "50",
                Outcome = RunCreateMeasurementsAggregationsOutcome.Pass,
            },
        };
        var req = WithAggregations(uid, "agg_pass", 50.0, aggs);
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.Equal(RunGetAggregationsOutcome.Pass, fetched.Phases![0].Measurements[0].Aggregations![0].Outcome);
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
                Value = "50",
                Outcome = RunCreateMeasurementsAggregationsOutcome.Fail,
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
                        MeasuredValue = "50",
                        Aggregations = aggs,
                    },
                },
            },
        };
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.Equal(RunGetAggregationsOutcome.Fail, fetched.Phases![0].Measurements[0].Aggregations![0].Outcome);
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
                Value = "95",
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
                Value = "-15.5",
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
                Value = "72",
                Outcome = RunCreateMeasurementsAggregationsOutcome.Fail,
                Validators = new List<RunCreateMeasurementsAggregationsValidators>
                {
                    new RunCreateMeasurementsAggregationsValidators
                    {
                        Operator = ">=",
                        ExpectedValue = "80",
                        Outcome = RunCreateMeasurementsAggregationsValidatorsOutcome.Fail,
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
