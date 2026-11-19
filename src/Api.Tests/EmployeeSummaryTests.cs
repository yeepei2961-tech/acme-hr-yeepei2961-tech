using System.Net;
using System.Net.Http.Json;
using Api.Data;
using Api.Models;
using Api.Services;

namespace Api.Tests;

/// <summary>Days taken this year counts only approved leave (pending and rejected are not taken).</summary>
public class DaysTakenThisYearTests
{
    // Employee 1 has no seeded leave, so each test controls exactly what is counted.
    const int Emp = 1;
    static readonly DateOnly Mon = Seed.MondayOnOrAfter(new DateOnly(DateTime.UtcNow.Year, 9, 7));

    static async Task<decimal?> DaysTakenWith(LeaveStatus status)
    {
        using var db = TestDb.Create();
        db.LeaveRequests.Add(new LeaveRequest { EmployeeId = Emp, StartDate = Mon, EndDate = Mon.AddDays(2), Status = status }); // Mon-Wed: 3 days
        await db.SaveChangesAsync();
        return await new LeaveService(db).DaysTakenThisYearAsync(Emp);
    }

    [Fact]
    public async Task Approved_leave_is_counted() => Assert.Equal(3m, await DaysTakenWith(LeaveStatus.Approved));

    [Fact]
    public async Task Pending_leave_is_not_counted() => Assert.Equal(0m, await DaysTakenWith(LeaveStatus.Pending));

    [Fact]
    public async Task Rejected_leave_is_not_counted() => Assert.Equal(0m, await DaysTakenWith(LeaveStatus.Rejected));

    [Fact]
    public async Task Unknown_employee_is_null()
    {
        using var db = TestDb.Create();
        Assert.Null(await new LeaveService(db).DaysTakenThisYearAsync(99999));
    }
}

/// <summary>GET /employees/{id}/summary over HTTP: same access rules as /balance.</summary>
public class EmployeeSummaryApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    record Summary(int EmployeeId, decimal DaysTakenThisYear);

    [Fact]
    public async Task Employee_sees_own_summary()
    {
        var farah = await api.SignedInAs(People.Farah);
        var res = await farah.GetAsync("/employees/6/summary");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<Summary>();
        Assert.Equal(6, body!.EmployeeId);
        Assert.Equal(3m, body.DaysTakenThisYear); // seed: 3 approved days
    }

    [Fact]
    public async Task Another_employee_gets_403()
    {
        var farah = await api.SignedInAs(People.Farah);
        Assert.Equal(HttpStatusCode.Forbidden, (await farah.GetAsync("/employees/7/summary")).StatusCode);
    }

    [Fact]
    public async Task Unknown_employee_is_404()
    {
        var hr = await api.SignedInAs(People.Dina);
        Assert.Equal(HttpStatusCode.NotFound, (await hr.GetAsync("/employees/99999/summary")).StatusCode);
    }
}
