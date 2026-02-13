using System.Net.Http.Headers;
using System.Text;
using Biogenom.Dto;
using Biogenom.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Biogenom.Helpers;


namespace Biogenom.Services
{
	public class GigaChatService : IGigaChatService
	{
		private readonly HttpClient _httpClient;
		private readonly string _authKey; // Постоянный ключ (Basic Auth)
		private readonly string _scope;

		// Кэширование токена
		private string? _accessToken;
		private DateTime _tokenExpiration = DateTime.MinValue;
		private readonly SemaphoreSlim _tokenLock = new(1, 1); // Для потокобезопасности

		private const string AuthUrl = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
		private const string BaseApiUrl = "https://gigachat.devices.sberbank.ru/api/v1";
		private const string ModelName = "GigaChat-Pro"; // Убедитесь, что модель доступна

		public GigaChatService(HttpClient httpClient, IConfiguration config)
		{
			_httpClient = httpClient;
			_authKey = config["GigaChat:AuthKey"] ?? throw new ArgumentNullException("GigaChat:AuthKey is missing");
			_scope = config["GigaChat:Scope"] ?? "GIGACHAT_API_PERS";
		}

		public async Task<byte[]> DownloadImageAsync(string url)
		{
			// 1. Добавляем User-Agent, чтобы хостинг картинок не считал нас ботом
			var request = new HttpRequestMessage(HttpMethod.Get, url);
			request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

			var response = await _httpClient.SendAsync(request);

			if (!response.IsSuccessStatusCode)
			{
				throw new Exception($"Не удалось скачать картинку. Status: {response.StatusCode}");
			}

			// Проверка, что это реально картинка
			var contentType = response.Content.Headers.ContentType?.MediaType;
			if (contentType != null && !contentType.StartsWith("image/"))
			{
				throw new Exception($"По ссылке находится не картинка, а {contentType}. Возможно, прямая ссылка протухла или ведет на HTML страницу.");
			}

			return await response.Content.ReadAsByteArrayAsync();
		}

		// 1. Определение предметов
		public async Task<List<string>> DetectObjectsAsync(byte[] imageBytes, string fileName)
		{
			string base64Image = Convert.ToBase64String(imageBytes);
			string mimeType = Biogenom.Helpers.GigaHelpers.GetMimeType(fileName); // Определяем тип (jpg/png)

			string systemPrompt = "Ты - эксперт по анализу изображений. Твоя задача - перечислить основные физические предметы на фото. Верни ТОЛЬКО JSON массив строк. Например: [\"стол\", \"стул\"]. Не пиши ничего кроме JSON.";

			var result = await CallGigaChatVisionApi(imageBytes, mimeType, systemPrompt);

			try
			{
				var cleanJson = Biogenom.Helpers.GigaHelpers.CleanJson(result);
				return JsonConvert.DeserializeObject<List<string>>(cleanJson) ?? new List<string>();
			}
			catch
			{
				return new List<string> { result };
			}
		}


		// 2. Определение материалов
		public async Task<List<Dto.MaterialResult>> DetectMaterialsAsync(byte[] imageBytes, string fileName, List<string> objects)
		{
			string base64Image = Convert.ToBase64String(imageBytes);
			string mimeType = Biogenom.Helpers.GigaHelpers.GetMimeType(fileName);
			string objectsList = string.Join(", ", objects);

			string prompt = $"На изображении присутствуют следующие предметы: {objectsList}. " +
							"Для каждого предмета определи материал, из которого он сделан (например: металл, дерево, пластик). " +
							"Верни ТОЛЬКО JSON массив объектов с полями 'objectName' и 'materials' (массив строк). " +
							"Пример: [{\"objectName\": \"стол\", \"materials\": [\"металл\"]}]";

			var result = await CallGigaChatVisionApi(imageBytes, mimeType, prompt);

			try
			{
				var cleanJson = Biogenom.Helpers.GigaHelpers.CleanJson(result);
				return JsonConvert.DeserializeObject<List<Dto.MaterialResult>>(cleanJson) ?? new();
			}
			catch
			{
				return new List<Dto.MaterialResult>();
			}
		}

		private async Task<string> CallGigaChatVisionApi(byte[] imageBytes, string mimeType, string prompt)
		{
			// 1. Получаем актуальный токен
			string token = await GetAccessTokenAsync();

			// 2. Загружаем файл (чтобы избежать ошибки 400 Bad Request из-за Base64)
			string fileId = await UploadFileToGigaChat(imageBytes, mimeType, token);

			// 3. Отправляем запрос в чат
			var requestBody = new
			{
				model = ModelName,
				messages = new[]
				{
					new
					{
						role = "user",
						content = prompt,
						attachments = new[] { fileId } // Ссылка на ID файла
                    }
				},
				temperature = 0.1
			};

			var requestContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

			using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseApiUrl}/chat/completions");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			request.Content = requestContent;

			var response = await _httpClient.SendAsync(request);

			if (!response.IsSuccessStatusCode)
			{
				var errorBody = await response.Content.ReadAsStringAsync();
				throw new HttpRequestException($"GigaChat API Error: {response.StatusCode}. {errorBody}");
			}

			var jsonResponse = await response.Content.ReadAsStringAsync();
			var parsed = JObject.Parse(jsonResponse);

			return parsed["choices"]?[0]?["message"]?["content"]?.ToString() ?? string.Empty;
		}

		// Новый метод для предварительной загрузки изображения
		private async Task<string> UploadFileToGigaChat(byte[] imageBytes, string mimeType, string token)
		{
			using var content = new MultipartFormDataContent();
			var fileContent = new ByteArrayContent(imageBytes);
			fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mimeType);

			content.Add(fileContent, "file", "image_upload");
			content.Add(new StringContent("general"), "purpose");

			using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseApiUrl}/files");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			request.Content = content;

			var response = await _httpClient.SendAsync(request);

			if (!response.IsSuccessStatusCode)
			{
				var error = await response.Content.ReadAsStringAsync();
				throw new Exception($"Ошибка загрузки файла: {error}");
			}

			var json = await response.Content.ReadAsStringAsync();
			return JObject.Parse(json)["id"]?.ToString() ?? throw new Exception("No file id returned");
		}


		private async Task<string> GetAccessTokenAsync()
		{
			// Если токен есть и он жив еще хотя бы 2 минуты - возвращаем его
			if (!string.IsNullOrEmpty(_accessToken) && _tokenExpiration > DateTime.UtcNow.AddMinutes(2))
			{
				return _accessToken;
			}

			// Блокируем поток, чтобы только один запрос обновлял токен
			await _tokenLock.WaitAsync();
			try
			{
				// Проверяем еще раз (Double-check locking pattern)
				if (!string.IsNullOrEmpty(_accessToken) && _tokenExpiration > DateTime.UtcNow.AddMinutes(2))
				{
					return _accessToken;
				}

				// Делаем запрос на получение токена
				var requestId = Guid.NewGuid().ToString();
				using var request = new HttpRequestMessage(HttpMethod.Post, AuthUrl);

				// Basic Auth с вашим ключом
				request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _authKey);
				request.Headers.Add("RqUID", requestId);

				// Body: x-www-form-urlencoded
				var content = new StringContent($"scope={_scope}", Encoding.UTF8, "application/x-www-form-urlencoded");
				request.Content = content;

				var response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					var error = await response.Content.ReadAsStringAsync();
					throw new Exception($"Ошибка авторизации GigaChat ({response.StatusCode}): {error}");
				}

				var json = await response.Content.ReadAsStringAsync();
				var parsed = JObject.Parse(json);

				_accessToken = parsed["access_token"]?.ToString();

				// Вычисляем время жизни (обычно expires_at в Unix timestamp ms)
				long expiresAtMs = parsed["expires_at"]?.ToObject<long>() ?? 0;
				_tokenExpiration = DateTimeOffset.FromUnixTimeMilliseconds(expiresAtMs).UtcDateTime;

				return _accessToken ?? throw new Exception("Access token is null");
			}
			finally
			{
				_tokenLock.Release();
			}
		}
	}
}
