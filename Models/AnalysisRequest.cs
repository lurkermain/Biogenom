using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Biogenom.Models;

namespace Biogenom.Models
{
	public class AnalysisRequest
	{
		[Key]
		public Guid Id { get; set; }
		public required string ImageUrl { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		// Raw ответ от первого этапа (для истории)
		public string? RawDetectionResponse { get; set; }

		public List<DetectedObject> DetectedObjects { get; set; } = new();
	}
}
