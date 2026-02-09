namespace GameServer.Models
{
    public class MovementPattern
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PlayerId { get; set; }
        public float[] PatternVector { get; set; } // 768-dimensional vector from TensorFlow
        public double CheatProbability { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

        // Foreign key
        public Player Player { get; set; }
    }
}
