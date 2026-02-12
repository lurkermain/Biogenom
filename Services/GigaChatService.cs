using System.Net.Http.Headers;
using System.Text;
using Biogenom.Dto;
using Biogenom.Models;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace Biogenom.Services
{
	public class GigaChatService
	{
		private readonly HttpClient _httpClient;
		private readonly string _accessToken; // В реальном проекте получать через Auth сервис
		private const string GigaChatApiUrl = "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";

		public GigaChatService(HttpClient httpClient, IConfiguration config)
		{
			_httpClient = httpClient;
			// В .NET 10 конфигурация такая же. Предполагаем, что токен валиден.
			_accessToken = config["GigaChat:AccessToken"] ?? "YOUR_TOKEN";
		}

		public async Task<byte[]> DownloadImageAsync(string url)
		{
			return await _httpClient.GetByteArrayAsync(url);
		}

		// 1. Определение предметов
		public async Task<List<string>> DetectObjectsAsync(byte[] imageBytes)
		{
			string base64Image = Convert.ToBase64String(imageBytes);

			// Промпт для нейросети
			string systemPrompt = "Ты - эксперт по анализу изображений. Твоя задача - перечислить основные физические предметы на фото. Верни ТОЛЬКО JSON массив строк. Например: [\"стол\", \"стул\"]. Не пиши ничего кроме JSON.";

			var result = await CallGigaChatVisionApi(base64Image, systemPrompt);

			try
			{
				// Очистка от markdown блоков ```json ... ``` если они есть
				var cleanJson = CleanJson(result);
				return JsonConvert.DeserializeObject<List<string>>(cleanJson) ?? new List<string>();
			}
			catch
			{
				// Fallback, если AI вернул текст
				return new List<string> { result };
			}
		}

		// 2. Определение материалов
		public async Task<List<Dto.MaterialResult>> DetectMaterialsAsync(byte[] imageBytes, List<string> objects)
		{
			string base64Image = Convert.ToBase64String(imageBytes);
			string objectsList = string.Join(", ", objects);

			// Промпт для материалов
			string prompt = $"На изображении присутствуют следующие предметы: {objectsList}. " +
							"Для каждого предмета определи материал, из которого он сделан (например: металл, дерево, пластик). " +
							"Верни ТОЛЬКО JSON массив объектов с полями 'objectName' и 'materials' (массив строк). " +
							"Пример: [{\"objectName\": \"стол\", \"materials\": [\"металл\"]}]";

			var result = await CallGigaChatVisionApi(base64Image, prompt);

			try
			{
				var cleanJson = CleanJson(result);
				return JsonConvert.DeserializeObject<List<MaterialResult>>(cleanJson) ?? new();
			}
			catch
			{
				return new List<MaterialResult>();
			}
		}

		private async Task<string> CallGigaChatVisionApi(string base64Image, string prompt)
		{
			// Формирование тела запроса под GigaChat / OpenAI совместимый формат
			var requestBody = new
			{
				model = "GigaChat-Pro", // Или актуальная модель с Vision
				messages = new[]
				{
					new
					{
						role = "user",
						content = new object[]
						{
							new { type = "text", text = prompt },
							new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{base64Image}" } }
						}
					}
				},
				temperature = 0.1 // Низкая температура для детерминированного JSON
			};

			var requestContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

			using var request = new HttpRequestMessage(HttpMethod.Post, GigaChatApiUrl);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
			// Отключение проверки сертификатов (специфика GigaChat иногда требует)
			request.Headers.Add("X-Request-ID", Guid.NewGuid().ToString());
			request.Content = requestContent;

			var response = await _httpClient.SendAsync(request);
			response.EnsureSuccessStatusCode();

			var jsonResponse = await response.Content.ReadAsStringAsync();
			var parsed = JObject.Parse(jsonResponse);

			// Извлекаем контент
			return parsed["choices"]?[0]?["message"]?["content"]?.ToString() ?? string.Empty;
		}
	}
}
