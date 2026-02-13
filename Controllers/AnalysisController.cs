using Biogenom.Dto;
using Biogenom.Models;
using Biogenom.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Biogenom.Controllers
{
	public class AnalysisController : ControllerBase
	{
		private readonly AppDbContext _context;
		private readonly IGigaChatService _aiService;

		public AnalysisController(AppDbContext context, IGigaChatService aiService)
		{
			_context = context;
			_aiService = aiService;
		}

		// Метод 1: Initial Detection
		[HttpPost("detect-objects")]
		public async Task<ActionResult<DetectObjectsResponse>> DetectObjects([FromForm] DetectObjectsRequest request)
		{
			// a. & b. Скачиваем фото
			byte[] imageBytes;
			try
			{
				imageBytes = await _aiService.DownloadImageAsync(request.ImageUrl);
			}
			catch (Exception ex)
			{
				return BadRequest($"Не удалось скачать изображение: {ex.Message}");
			}

			// c. & d. Отправляем в GigaChat и парсим
			var detectedNames = await _aiService.DetectObjectsAsync(imageBytes, request.ImageUrl);

			// e. Сохраняем в БД
			var analysisRequest = new AnalysisRequest
			{
				Id = Guid.NewGuid(),
				ImageUrl = request.ImageUrl,
				RawDetectionResponse = JsonConvert.SerializeObject(detectedNames) // Для аудита
			};

			_context.AnalysisRequests.Add(analysisRequest);
			await _context.SaveChangesAsync();

			// f. Возвращаем ответ
			return Ok(new DetectObjectsResponse
			{
				RequestId = analysisRequest.Id,
				ProbableObjects = detectedNames
			});
		}

		// Метод 2: Material Analysis
		[HttpPost("detect-materials")]
		public async Task<ActionResult<List<MaterialResult>>> DetectMaterials([FromForm] AnalyzeMaterialsRequest request)
		{
			// Находим исходный запрос, чтобы получить URL
			var analysisRecord = await _context.AnalysisRequests
				.FirstOrDefaultAsync(r => r.Id == request.RequestId);

			if (analysisRecord == null)
				return NotFound("Запрос с таким ID не найден");

			// b. Снова скачиваем фото (stateless подход, чтобы не хранить блобы в БД)
			// (В продакшене лучше кэшировать или хранить в S3, но по ТЗ качаем по ссылке)
			byte[] imageBytes = await _aiService.DownloadImageAsync(analysisRecord.ImageUrl);

			// b. & c. Отправляем в AI скорректированный список предметов и фото
			var materialsResult = await _aiService.DetectMaterialsAsync(imageBytes,analysisRecord.ImageUrl, request.ConfirmedObjects);

			// d. Сохраняем в БД в нормализованном виде
			// Сначала удаляем старые объекты для этого реквеста, если был повторный вызов
			var existingObjects = await _context.DetectedObjects
				.Where(o => o.RequestId == request.RequestId)
				.ToListAsync();
			_context.DetectedObjects.RemoveRange(existingObjects);

			foreach (var item in materialsResult)
			{
				var newObj = new DetectedObject
				{
					RequestId = request.RequestId,
					Name = item.ObjectName
				};

				// Добавляем материалы
				foreach (var mat in item.Materials)
				{
					newObj.Materials.Add(new ObjectMaterial { MaterialName = mat });
				}

				_context.DetectedObjects.Add(newObj);
			}

			await _context.SaveChangesAsync();

			// e. Возвращаем результат
			return Ok(materialsResult);
		}
	}
}
