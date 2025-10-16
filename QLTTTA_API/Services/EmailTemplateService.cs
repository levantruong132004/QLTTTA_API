using System.Text;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace QLTTTA_API.Services
{
    public interface IEmailTemplateRenderer
    {
        Task<string> RenderAsync(string templateName, IDictionary<string, string> model, CancellationToken ct = default);
    }

    public class FileEmailTemplateRenderer : IEmailTemplateRenderer
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileEmailTemplateRenderer> _logger;

        public FileEmailTemplateRenderer(IWebHostEnvironment env, ILogger<FileEmailTemplateRenderer> logger)
        {
            _env = env;
            _logger = logger;
        }

        public async Task<string> RenderAsync(string templateName, IDictionary<string, string> model, CancellationToken ct = default)
        {
            var root = _env.ContentRootPath;
            var path = Path.Combine(root, "Templates", "Email", templateName + ".html");
            if (!File.Exists(path))
            {
                _logger.LogWarning("Email template not found: {Path}", path);
                return string.Empty;
            }

            string html;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, Encoding.UTF8))
            {
                html = await sr.ReadToEndAsync();
            }

            if (model != null)
            {
                foreach (var kv in model)
                {
                    html = html.Replace("{{" + kv.Key + "}}", kv.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                }
            }

            // Built-in common placeholders
            html = html.Replace("{{Year}}", DateTime.UtcNow.Year.ToString());
            return html;
        }
    }
}
