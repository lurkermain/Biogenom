
using Biogenom.Services;
using Microsoft.EntityFrameworkCore;

namespace Biogenom
{
    public class Program
    {
        public static void Main(string[] args)
        {
			var builder = WebApplication.CreateBuilder(args);

			// Регистрация сервисов
			builder.Services.AddControllers();

			// Настройка БД (PostgreSQL)
			builder.Services.AddDbContext<AppDbContext>(options =>
				options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

			// Регистрация AI сервиса и HTTP клиента
			// Отключаем валидацию SSL для GigaChat (частая проблема с сертификатами минцифры)
			builder.Services.AddHttpClient<IGigaChatService, GigaChatService>()
				.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
				{
					ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
				});

			var app = builder.Build();

			// Middleware
			app.UseHttpsRedirection();
			app.UseAuthorization();
			app.MapControllers();

			// Автоматическое создание БД при старте (для удобства проверки)
			using (var scope = app.Services.CreateScope())
			{
				var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
				db.Database.EnsureCreated();
			}

			app.Run();
		}
    }
}
