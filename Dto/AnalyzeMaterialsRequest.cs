namespace Biogenom.Dto
{
	public class AnalyzeMaterialsRequest
	{
		public Guid RequestId { get; set; }
		public List<string> ConfirmedObjects { get; set; } = new();
	}
}
