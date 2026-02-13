namespace Biogenom.Helpers
{
	public class GigaHelpers
	{
		public static string CleanJson(string input)
		{
			input = input.Trim();
			if (input.StartsWith("```json")) input = input.Substring(7);
			if (input.StartsWith("```")) input = input.Substring(3);
			if (input.EndsWith("```")) input = input.Substring(0, input.Length - 3);
			return input.Trim();
		}
		public static string GetMimeType(string urlOrFileName)
		{
			var ext = Path.GetExtension(urlOrFileName).ToLower();
			return ext switch
			{
				".png" => "image/png",
				".gif" => "image/gif",
				".webp" => "image/webp",
				_ => "image/jpeg" // Default
			};
		}

	}
}
