using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Biogenom.Models
{
	public class DetectedObject
	{
		[Key]
		public int Id { get; set; }

		public Guid RequestId { get; set; }
		[ForeignKey("RequestId")]
		public AnalysisRequest Request { get; set; } = null!;

		public required string Name { get; set; }

		public List<ObjectMaterial> Materials { get; set; } = new();
	}
}
