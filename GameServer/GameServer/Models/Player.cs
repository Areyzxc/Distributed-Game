namespace GameServer.Models
{
    public class Player
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public int TotalScore { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public ICollection<GameScore> Scores { get; set; } = new List<GameScore>();
        public ICollection<MovementPattern> MovementPatterns { get; set; } = new List<MovementPattern>();
        public ICollection<BannedPlayer> Bans { get; set; } = new List<BannedPlayer>();
    }
}
