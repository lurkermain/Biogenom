using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Biogenom.Models
{
	public class ObjectMaterial
	{
		[Key]
		public int Id { get; set; }

		public int DetectedObjectId { get; set; }
		[ForeignKey("DetectedObjectId")]
		public DetectedObject DetectedObject { get; set; } = null!;

		public required string MaterialName { get; set; }
	}
}
