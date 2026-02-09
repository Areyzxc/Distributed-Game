namespace GameServer.Models
{
    public class GameScore
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PlayerId { get; set; }
        public int Score { get; set; }
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

        // Foreign key
        public Player Player { get; set; }
    }
}
