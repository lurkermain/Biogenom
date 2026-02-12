using Biogenom.Dto;
namespace Biogenom.Services
{
	public interface IGigaChatService
	{
		Task<byte[]> DownloadImageAsync(string url);
		Task<List<string>> DetectObjectsAsync(byte[] imageBytes);
		Task<List<MaterialResult>> DetectMaterialsAsync(byte[] imageBytes, List<string> objects);
	}
}
