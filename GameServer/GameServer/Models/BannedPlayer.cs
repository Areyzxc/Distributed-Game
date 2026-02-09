namespace GameServer.Models
{
    public class BannedPlayer
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PlayerId { get; set; }
        public string Reason { get; set; }
        public DateTime BannedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UnbanAt { get; set; }

        // Foreign key
        public Player Player { get; set; }
    }
}
