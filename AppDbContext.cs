using Biogenom.Models;
using Microsoft.EntityFrameworkCore;

namespace Biogenom
{
	public class AppDbContext : DbContext
	{
		public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

		public DbSet<AnalysisRequest> AnalysisRequests { get; set; }
		public DbSet<DetectedObject> DetectedObjects { get; set; }
		public DbSet<ObjectMaterial> ObjectMaterials { get; set; }
	}
}
