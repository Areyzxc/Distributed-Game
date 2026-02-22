using Microsoft.AspNetCore.SignalR;
using GameServer.Data;
using GameServer.Models;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Hubs
{
    /// <summary>
    /// GameHub handles real-time communication between the game server and connected clients.
    /// Broadcasts player scores, movement data, and manages player groups.
    /// </summary>
    public class GameHub : Hub
    {
        private readonly GameDbContext _context;
        private readonly ILogger<GameHub> _logger;

        public GameHub(GameDbContext context, ILogger<GameHub> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Called when a client connects to the hub.
        /// Adds the connection to appropriate groups based on client type.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            var clientType = httpContext?.Request.Query["clientType"].ToString() ?? "player";
            var playerId = httpContext?.Request.Query["playerId"].ToString();

            _logger.LogInformation($"Client connected: {Context.ConnectionId}, Type: {clientType}, PlayerId: {playerId}");

            // Add to group based on client type
            if (clientType == "dashboard")
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "WebDashboard");
                _logger.LogInformation($"Added {Context.ConnectionId} to WebDashboard group");
            }
            else if (clientType == "validator")
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Validators");
                _logger.LogInformation($"Added {Context.ConnectionId} to Validators group");
            }
            else // Default: player
            {
                if (!string.IsNullOrEmpty(playerId))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Player_{playerId}");
                    await Groups.AddToGroupAsync(Context.ConnectionId, "Players");
                    _logger.LogInformation($"Added {Context.ConnectionId} to Player_{playerId} and Players groups");
                }
            }

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when a client disconnects from the hub.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation($"Client disconnected: {Context.ConnectionId}. Exception: {exception?.Message}");
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Broadcasts a player's score update to the dashboard and all connected players.
        /// Called when a player finishes a game session.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player</param>
        /// <param name="playerName">The username of the player</param>
        /// <param name="score">The score achieved in this session</param>
        public async Task SendScore(string playerId, string playerName, int score)
        {
            try
            {
                _logger.LogInformation($"Score submitted: Player={playerName}, Score={score}");

                // Verify player exists in database
                var player = await _context.Players.FindAsync(Guid.Parse(playerId));
                if (player == null)
                {
                    _logger.LogWarning($"Player not found: {playerId}");
                    await Clients.Caller.SendAsync("Error", "Player not found");
                    return;
                }

                // Create game score record
                var gameScore = new GameScore
                {
                    Id = Guid.NewGuid(),
                    PlayerId = player.Id,
                    Score = score,
                    RecordedAt = DateTime.UtcNow
                };

                _context.Scores.Add(gameScore);
                player.TotalScore += score;
                await _context.SaveChangesAsync();

                // Broadcast to dashboard (real-time leaderboard update)
                await Clients.Group("WebDashboard").SendAsync("ScoreUpdate", new
                {
                    playerId = playerId,
                    playerName = playerName,
                    score = score,
                    totalScore = player.TotalScore,
                    timestamp = DateTime.UtcNow
                });

                // Notify all players of leaderboard change
                await Clients.Group("Players").SendAsync("LeaderboardUpdate", new
                {
                    playerId = playerId,
                    playerName = playerName,
                    totalScore = player.TotalScore
                });

                _logger.LogInformation($"Score broadcast successfully: {playerName} - {score}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in SendScore: {ex.Message}", ex);
                await Clients.Caller.SendAsync("Error", $"Failed to send score: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends player movement data for validation by the AI service.
        /// Called when a player performs an action (move, attack, ability use).
        /// </summary>
        /// <param name="playerId">The unique identifier of the player</param>
        /// <param name="moveData">Movement/action data containing position, velocity, rotation, etc.</param>
        public async Task SendMove(string playerId, object moveData)
        {
            try
            {
                _logger.LogInformation($"Move received: PlayerId={playerId}");

                // Forward movement data to validators for anti-cheat analysis
                await Clients.Group("Validators").SendAsync("ValidateMove", new
                {
                    playerId = playerId,
                    moveData = moveData,
                    timestamp = DateTime.UtcNow,
                    connectionId = Context.ConnectionId
                });

                // Broadcast normalized move to other players (for MMO synchronization)
                await Clients.Group("Players").SendAsync("PlayerMove", new
                {
                    playerId = playerId,
                    moveData = moveData,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in SendMove: {ex.Message}", ex);
                await Clients.Caller.SendAsync("Error", $"Failed to process move: {ex.Message}");
            }
        }

        /// <summary>
        /// Receives cheat detection result from the AI service and takes action.
        /// </summary>
        /// <param name="playerId">The player ID being validated</param>
        /// <param name="cheatProbability">Probability score (0.0 to 1.0) of cheating</param>
        /// <param name="confidence">Confidence level of the detection</param>
        public async Task CheatDetected(string playerId, double cheatProbability, string confidence)
        {
            try
            {
                _logger.LogWarning($"Potential cheat detected: PlayerId={playerId}, Probability={cheatProbability}");

                var player = await _context.Players.FindAsync(Guid.Parse(playerId));
                if (player == null)
                {
                    _logger.LogError($"Player not found for cheat detection: {playerId}");
                    return;
                }

                // Store detection result
                var movementPattern = new MovementPattern
                {
                    Id = Guid.NewGuid(),
                    PlayerId = player.Id,
                    PatternVector = Array.Empty<float>(), // Placeholder - would receive actual vector from AI
                    CheatProbability = cheatProbability,
                    DetectedAt = DateTime.UtcNow
                };

                _context.MovementPatterns.Add(movementPattern);

                // If high confidence cheat, ban the player
                if (cheatProbability > 0.85 && confidence == "high")
                {
                    var bannedPlayer = new BannedPlayer
                    {
                        Id = Guid.NewGuid(),
                        PlayerId = player.Id,
                        Reason = $"Automated detection: {confidence} confidence, {cheatProbability:P} probability",
                        BannedAt = DateTime.UtcNow,
                        UnbanAt = DateTime.UtcNow.AddDays(30) // 30-day ban
                    };

                    player.IsActive = false;
                    _context.BannedPlayers.Add(bannedPlayer);

                    // Notify player and all dashboards
                    await Clients.Caller.SendAsync("Banned", new
                    {
                        reason = bannedPlayer.Reason,
                        unbanAt = bannedPlayer.UnbanAt
                    });

                    _logger.LogWarning($"Player banned for cheating: {player.Username}");
                }

                await _context.SaveChangesAsync();

                // Notify dashboard of detection
                await Clients.Group("WebDashboard").SendAsync("CheatAlert", new
                {
                    playerId = playerId,
                    playerName = player.Username,
                    cheatProbability = cheatProbability,
                    confidence = confidence,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in CheatDetected: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Health check endpoint for monitoring client connection status.
        /// </summary>
        public async Task<string> Ping()
        {
            _logger.LogDebug($"Ping received from: {Context.ConnectionId}");
            return "pong";
        }

        /// <summary>
        /// Requests current leaderboard data from the server.
        /// </summary>
        /// <returns>Top 10 players by total score</returns>
        public async Task<IEnumerable<object>> GetLeaderboard()
        {
            try
            {
                var topPlayers = await _context.Players
                    .Where(p => p.IsActive)
                    .OrderByDescending(p => p.TotalScore)
                    .Take(10)
                    .Select(p => new
                    {
                        playerId = p.Id.ToString(),
                        playerName = p.Username,
                        totalScore = p.TotalScore,
                        games = p.Scores.Count,
                        lastLogin = p.LastLoginAt
                    })
                    .ToListAsync();

                return topPlayers;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetLeaderboard: {ex.Message}", ex);
                return Enumerable.Empty<object>();
            }
        }

        /// <summary>
        /// Retrieves all active sessions (connected players).
        /// Admin/dashboard only.
        /// </summary>
        public async Task<int> GetActiveSessions()
        {
            _logger.LogInformation($"Active sessions request from {Context.ConnectionId}");
            
            // Count active players connected to the server
            var activePlayers = await _context.Players.CountAsync(p => p.IsActive);
            return activePlayers;
        }
    }
}
