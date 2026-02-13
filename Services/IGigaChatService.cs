using Biogenom.Dto;
namespace Biogenom.Services
{
	public interface IGigaChatService
	{
		Task<byte[]> DownloadImageAsync(string url);
        Task<List<string>> DetectObjectsAsync(byte[] imageBytes, string fileName); // Добавил fileName для определения типа
        Task<List<Dto.MaterialResult>> DetectMaterialsAsync(byte[] imageBytes, string fileName, List<string> objects);
	}
}
