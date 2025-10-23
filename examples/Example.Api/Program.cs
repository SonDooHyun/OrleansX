using Example.Grains.Interfaces;
using Example.Grains.Models;
using OrleansX.Abstractions;
using OrleansX.Abstractions.Options;
using OrleansX.Client.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// OrleansX Client 등록
builder.Services.AddOrleansXClient(new OrleansClientOptions
{
    ClusterId = "game-cluster",
    ServiceId = "game-service",
    Retry = new RetryOptions
    {
        MaxAttempts = 3,
        BaseDelayMs = 200,
        MaxDelayMs = 5000
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ============================================================================
// Health Check
// ============================================================================
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithTags("Health");

// ============================================================================
// Player APIs
// ============================================================================
app.MapPost("/api/players", async (CreatePlayerRequest request, IGrainInvoker invoker) =>
{
    var playerGrain = invoker.GetGrain<IPlayerGrain>(request.PlayerId);
    await playerGrain.CreateAsync(request.Name, request.Level, request.Mmr);
    
    return Results.Ok(new { playerId = request.PlayerId, message = "Player created" });
})
.WithTags("Players")
.WithName("CreatePlayer");

app.MapGet("/api/players/{playerId}", async (string playerId, IGrainInvoker invoker) =>
{
    var playerGrain = invoker.GetGrain<IPlayerGrain>(playerId);
    var player = await playerGrain.GetInfoAsync();
    
    if (player == null)
        return Results.NotFound(new { error = "Player not found" });
    
    return Results.Ok(player);
})
.WithTags("Players")
.WithName("GetPlayer");

app.MapPut("/api/players/{playerId}/mmr", async (string playerId, UpdateMmrRequest request, IGrainInvoker invoker) =>
{
    var playerGrain = invoker.GetGrain<IPlayerGrain>(playerId);
    await playerGrain.UpdateMmrAsync(request.NewMmr);
    
    return Results.Ok(new { message = "MMR updated", newMmr = request.NewMmr });
})
.WithTags("Players")
.WithName("UpdatePlayerMmr");

// ============================================================================
// Party APIs
// ============================================================================
app.MapPost("/api/parties", async (CreatePartyRequest request, IGrainInvoker invoker) =>
{
    var partyId = Guid.NewGuid().ToString();
    var partyGrain = invoker.GetGrain<IPartyGrain>(partyId);
    
    await partyGrain.CreateAsync(
        request.LeaderId,
        request.LeaderName,
        request.LeaderLevel,
        request.LeaderMmr,
        request.MaxMembers);
    
    return Results.Ok(new { partyId, message = "Party created" });
})
.WithTags("Party")
.WithName("CreateParty");

app.MapGet("/api/parties/{partyId}", async (string partyId, IGrainInvoker invoker) =>
{
    var partyGrain = invoker.GetGrain<IPartyGrain>(partyId);
    var party = await partyGrain.GetInfoAsync();
    
    if (party == null)
        return Results.NotFound(new { error = "Party not found" });
    
    return Results.Ok(party);
})
.WithTags("Party")
.WithName("GetParty");

app.MapPost("/api/parties/{partyId}/join", async (string partyId, JoinPartyRequest request, IGrainInvoker invoker) =>
{
    var partyGrain = invoker.GetGrain<IPartyGrain>(partyId);
    var success = await partyGrain.JoinAsync(
        request.PlayerId,
        request.PlayerName,
        request.Level,
        request.Mmr);
    
    if (!success)
        return Results.BadRequest(new { error = "Failed to join party (full or invalid state)" });
    
    return Results.Ok(new { message = "Joined party successfully" });
})
.WithTags("Party")
.WithName("JoinParty");

app.MapPost("/api/parties/{partyId}/leave", async (string partyId, LeavePartyRequest request, IGrainInvoker invoker) =>
{
    var partyGrain = invoker.GetGrain<IPartyGrain>(partyId);
    var success = await partyGrain.LeaveAsync(request.PlayerId);
    
    if (!success)
        return Results.BadRequest(new { error = "Failed to leave party" });
    
    return Results.Ok(new { message = "Left party successfully" });
})
.WithTags("Party")
.WithName("LeaveParty");

app.MapPost("/api/parties/{partyId}/disband", async (string partyId, IGrainInvoker invoker) =>
{
    var partyGrain = invoker.GetGrain<IPartyGrain>(partyId);
    await partyGrain.DisbandAsync();
    
    return Results.Ok(new { message = "Party disbanded" });
})
.WithTags("Party")
.WithName("DisbandParty");

// ============================================================================
// Matchmaking APIs
// ============================================================================
app.MapPost("/api/matchmaking/solo", async (EnqueueSoloRequest request, IGrainInvoker invoker) =>
{
    var matchmakingGrain = invoker.GetGrain<IMatchmakingGrain>("global");
    await matchmakingGrain.EnqueueSoloAsync(request.PlayerId, request.Mmr);
    
    return Results.Ok(new { message = "Enqueued for solo matchmaking" });
})
.WithTags("Matchmaking")
.WithName("EnqueueSolo");

app.MapPost("/api/parties/{partyId}/matchmaking/start", async (string partyId, IGrainInvoker invoker) =>
{
    var partyGrain = invoker.GetGrain<IPartyGrain>(partyId);
    var success = await partyGrain.StartMatchmakingAsync();
    
    if (!success)
        return Results.BadRequest(new { error = "Failed to start matchmaking" });
    
    return Results.Ok(new { message = "Party matchmaking started" });
})
.WithTags("Matchmaking")
.WithName("StartPartyMatchmaking");

app.MapPost("/api/parties/{partyId}/matchmaking/cancel", async (string partyId, IGrainInvoker invoker) =>
{
    var partyGrain = invoker.GetGrain<IPartyGrain>(partyId);
    await partyGrain.CancelMatchmakingAsync();
    
    return Results.Ok(new { message = "Matchmaking cancelled" });
})
.WithTags("Matchmaking")
.WithName("CancelPartyMatchmaking");

app.MapPost("/api/matchmaking/cancel/{playerId}", async (string playerId, IGrainInvoker invoker) =>
{
    var matchmakingGrain = invoker.GetGrain<IMatchmakingGrain>("global");
    await matchmakingGrain.DequeueAsync(playerId);
    
    return Results.Ok(new { message = "Dequeued from matchmaking" });
})
.WithTags("Matchmaking")
.WithName("CancelSoloMatchmaking");

app.MapGet("/api/matchmaking/queue", async (IGrainInvoker invoker) =>
{
    var matchmakingGrain = invoker.GetGrain<IMatchmakingGrain>("global");
    var (soloCount, partyCount) = await matchmakingGrain.GetQueueStatusAsync();
    
    return Results.Ok(new
    {
        soloQueue = soloCount,
        partyQueue = partyCount,
        total = soloCount + partyCount
    });
})
.WithTags("Matchmaking")
.WithName("GetQueueStatus");

app.MapGet("/api/matchmaking/history", async (IGrainInvoker invoker, int count = 10) =>
{
    var matchmakingGrain = invoker.GetGrain<IMatchmakingGrain>("global");
    var history = await matchmakingGrain.GetMatchHistoryAsync(count);
    
    return Results.Ok(history);
})
.WithTags("Matchmaking")
.WithName("GetMatchHistory");

app.MapPost("/api/matchmaking/try-match", async (IGrainInvoker invoker) =>
{
    var matchmakingGrain = invoker.GetGrain<IMatchmakingGrain>("global");
    var matches = await matchmakingGrain.TryMatchAsync();
    
    return Results.Ok(new
    {
        matchesCreated = matches.Count,
        matches
    });
})
.WithTags("Matchmaking")
.WithName("TryMatch");

// ============================================================================
// Room APIs
// ============================================================================
app.MapGet("/api/rooms/{roomId}", async (string roomId, IGrainInvoker invoker) =>
{
    var roomGrain = invoker.GetGrain<IRoomGrain>(roomId);
    var room = await roomGrain.GetInfoAsync();
    
    if (room == null)
        return Results.NotFound(new { error = "Room not found" });
    
    return Results.Ok(room);
})
.WithTags("Room")
.WithName("GetRoom");

app.MapPost("/api/rooms/{roomId}/select-character", async (string roomId, SelectCharacterRequest request, IGrainInvoker invoker) =>
{
    var roomGrain = invoker.GetGrain<IRoomGrain>(roomId);
    var success = await roomGrain.SelectCharacterAsync(request.PlayerId, request.CharacterId);
    
    if (!success)
        return Results.BadRequest(new { error = "Failed to select character (invalid or duplicate)" });
    
    return Results.Ok(new { message = "Character selected" });
})
.WithTags("Room")
.WithName("SelectCharacter");

app.MapPost("/api/rooms/{roomId}/ready", async (string roomId, SetReadyRequest request, IGrainInvoker invoker) =>
{
    var roomGrain = invoker.GetGrain<IRoomGrain>(roomId);
    var success = await roomGrain.SetReadyAsync(request.PlayerId, request.IsReady);
    
    if (!success)
        return Results.BadRequest(new { error = "Failed to set ready status" });
    
    return Results.Ok(new { message = $"Ready status set to {request.IsReady}" });
})
.WithTags("Room")
.WithName("SetReady");

app.MapGet("/api/rooms/{roomId}/can-start", async (string roomId, IGrainInvoker invoker) =>
{
    var roomGrain = invoker.GetGrain<IRoomGrain>(roomId);
    var canStart = await roomGrain.CanStartGameAsync();
    
    return Results.Ok(new { canStart });
})
.WithTags("Room")
.WithName("CanStartGame");

app.MapPost("/api/rooms/{roomId}/start", async (string roomId, IGrainInvoker invoker) =>
{
    var roomGrain = invoker.GetGrain<IRoomGrain>(roomId);
    var success = await roomGrain.StartGameAsync();
    
    if (!success)
        return Results.BadRequest(new { error = "Cannot start game (not all players ready)" });
    
    return Results.Ok(new { message = "Game started!" });
})
.WithTags("Room")
.WithName("StartGame");

app.MapPost("/api/rooms/{roomId}/leave", async (string roomId, LeaveRoomRequest request, IGrainInvoker invoker) =>
{
    var roomGrain = invoker.GetGrain<IRoomGrain>(roomId);
    await roomGrain.LeaveAsync(request.PlayerId);
    
    return Results.Ok(new { message = "Left room" });
})
.WithTags("Room")
.WithName("LeaveRoom");

// ============================================================================
// Characters APIs
// ============================================================================
app.MapGet("/api/characters", () =>
{
    return Results.Ok(Characters.All);
})
.WithTags("Characters")
.WithName("GetAllCharacters");

app.MapGet("/api/characters/{characterId}", (string characterId) =>
{
    var character = Characters.GetCharacter(characterId);
    
    if (character == null)
        return Results.NotFound(new { error = "Character not found" });
    
    return Results.Ok(character);
})
.WithTags("Characters")
.WithName("GetCharacter");

Console.WriteLine("=".PadRight(80, '='));
Console.WriteLine("OrleansX Game Matchmaking API");
Console.WriteLine("Listening on: http://localhost:5000");
Console.WriteLine("Swagger UI: http://localhost:5000/swagger");
Console.WriteLine("=".PadRight(80, '='));

app.Run();

// ============================================================================
// DTOs
// ============================================================================
record CreatePlayerRequest(string PlayerId, string Name, int Level, int Mmr = 1000);
record UpdateMmrRequest(int NewMmr);

record CreatePartyRequest(string LeaderId, string LeaderName, int LeaderLevel, int LeaderMmr, int MaxMembers = 5);
record JoinPartyRequest(string PlayerId, string PlayerName, int Level, int Mmr);
record LeavePartyRequest(string PlayerId);

record EnqueueSoloRequest(string PlayerId, int Mmr);

record SelectCharacterRequest(string PlayerId, string CharacterId);
record SetReadyRequest(string PlayerId, bool IsReady);
record LeaveRoomRequest(string PlayerId);
