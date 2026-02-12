namespace Biogenom.Dto
{
	public class DetectObjectsResponse
	{
		public Guid RequestId { get; set; }
		public List<string> ProbableObjects { get; set; } = new();
	}
}
