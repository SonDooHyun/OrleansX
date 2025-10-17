using Example.Grains.Interfaces;
using Example.Grains.Models;
using OrleansX.Abstractions;
using OrleansX.Abstractions.Options;
using OrleansX.Client.Extensions;

var builder = WebApplication.CreateBuilder(args);

// OrleansX Client 등록
builder.Services.AddOrleansXClient(new OrleansClientOptions
{
    ClusterId = "game-cluster",
    ServiceId = "party-service",
    Retry = new RetryOptions
    {
        MaxAttempts = 3,
        BaseDelayMs = 200,
        MaxDelayMs = 5000
    }
});

var app = builder.Build();

// Health Check
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// 파티 생성
app.MapPost("/api/parties", async (CreatePartyRequest request, IGrainInvoker invoker) =>
{
    var partyId = Guid.NewGuid().ToString();
    var partyGrain = invoker.GetGrain<IPartyGrain>(partyId);

    var success = await partyGrain.CreateAsync(
        request.LeaderId,
        request.LeaderName,
        request.MaxMembers);

    if (!success)
    {
        return Results.BadRequest(new { error = "Failed to create party" });
    }

    return Results.Ok(new { partyId });
});

// 파티 정보 조회
app.MapGet("/api/parties/{partyId}", async (string partyId, IGrainInvoker invoker) =>
{
    var partyGrain = invoker.GetGrain<IPartyGrain>(partyId);
    var state = await partyGrain.GetStateAsync();

    if (state == null)
    {
        return Results.NotFound(new { error = "Party not found" });
    }

    return Results.Ok(state);
});

// 파티 참가
app.MapPost("/api/parties/{partyId}/join", async (string partyId, JoinPartyRequest request, IGrainInvoker invoker) =>
{
    var partyGrain = invoker.GetGrain<IPartyGrain>(partyId);
    var success = await partyGrain.JoinAsync(request.PlayerId, request.PlayerName, request.Level);

    if (!success)
    {
        return Results.BadRequest(new { error = "Failed to join party" });
    }

    return Results.Ok(new { message = "Joined successfully" });
});

// 파티 탈퇴
app.MapPost("/api/parties/{partyId}/leave", async (string partyId, LeavePartyRequest request, IGrainInvoker invoker) =>
{
    var partyGrain = invoker.GetGrain<IPartyGrain>(partyId);
    var success = await partyGrain.LeaveAsync(request.PlayerId);

    if (!success)
    {
        return Results.BadRequest(new { error = "Failed to leave party" });
    }

    return Results.Ok(new { message = "Left successfully" });
});

// 파티 해산
app.MapPost("/api/parties/{partyId}/disband", async (string partyId, DisbandPartyRequest request, IGrainInvoker invoker) =>
{
    var partyGrain = invoker.GetGrain<IPartyGrain>(partyId);
    var success = await partyGrain.DisbandAsync(request.RequesterId);

    if (!success)
    {
        return Results.BadRequest(new { error = "Failed to disband party" });
    }

    return Results.Ok(new { message = "Disbanded successfully" });
});

// 매칭 시작
app.MapPost("/api/parties/{partyId}/matchmaking/start", async (string partyId, IGrainInvoker invoker) =>
{
    var partyGrain = invoker.GetGrain<IPartyGrain>(partyId);
    var success = await partyGrain.StartMatchmakingAsync();

    if (!success)
    {
        return Results.BadRequest(new { error = "Failed to start matchmaking" });
    }

    return Results.Ok(new { message = "Matchmaking started" });
});

// 매칭 취소
app.MapPost("/api/parties/{partyId}/matchmaking/cancel", async (string partyId, IGrainInvoker invoker) =>
{
    var partyGrain = invoker.GetGrain<IPartyGrain>(partyId);
    var success = await partyGrain.CancelMatchmakingAsync();

    if (!success)
    {
        return Results.BadRequest(new { error = "Failed to cancel matchmaking" });
    }

    return Results.Ok(new { message = "Matchmaking cancelled" });
});

// 매칭 큐 상태 조회
app.MapGet("/api/matchmaking/queue", async (IGrainInvoker invoker) =>
{
    var matchmakingGrain = invoker.GetGrain<IMatchmakingGrain>("default");
    var queueSize = await matchmakingGrain.GetQueueSizeAsync();

    return Results.Ok(new { queueSize });
});

// 매치 정보 조회
app.MapGet("/api/matchmaking/matches/{matchId}", async (string matchId, IGrainInvoker invoker) =>
{
    var matchmakingGrain = invoker.GetGrain<IMatchmakingGrain>("default");
    var matchInfo = await matchmakingGrain.GetMatchInfoAsync(matchId);

    if (matchInfo == null)
    {
        return Results.NotFound(new { error = "Match not found" });
    }

    return Results.Ok(matchInfo);
});

app.Run();

// DTOs
record CreatePartyRequest(string LeaderId, string LeaderName, int MaxMembers = 4);
record JoinPartyRequest(string PlayerId, string PlayerName, int Level);
record LeavePartyRequest(string PlayerId);
record DisbandPartyRequest(string RequesterId);
