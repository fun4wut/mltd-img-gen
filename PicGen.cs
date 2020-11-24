using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using System.Text.Json;
namespace mltd_img_gen
{
    static class PicGen
    {
        static Page page;
        static PicGen()
        {
            var option = new BrowserFetcherOptions
            {
                // Path = prodEnv == "true" ? "/home/site/wwwroot/.local-chrome" : "/home/fun4/Codes/mltd-img-gen/.local-chrome"
            };
            var fetcher = new BrowserFetcher(option);
            fetcher.DownloadAsync(BrowserFetcher.DefaultRevision).Wait();
            var browser = Puppeteer.LaunchAsync(new LaunchOptions
            {
                // ExecutablePath = $"{option.Path}/Linux-706915/chrome-linux/chrome",
                Headless = true,
                Args = new[]
                {
                    "--proxy-server=winip:1080"
                }
            }).Result;
            page = browser.NewPageAsync().Result;
            page.SetViewportAsync(new ViewPortOptions
            {
                Height = 750,
                Width = 1300
            }).Wait();
        }

        static string HTML_PATH = Path.Combine(Directory.GetCurrentDirectory(), "index.html");

        [FunctionName("PicGen")]
        public static async Task Run(
            [QueueTrigger("html-queue")] string content,
            ILogger log,
            IBinder binder
        )
        {
            log.LogInformation("start to process html content");
            var obj = JsonDocument.Parse(content).RootElement;
            Console.WriteLine(HTML_PATH);
            var html = obj.GetProperty("src").GetString();
            await File.WriteAllTextAsync(HTML_PATH, html);
            await page.GoToAsync($"file://{HTML_PATH}", new NavigationOptions
            {
                Timeout = 5000,
                WaitUntil = new[] { WaitUntilNavigation.Networkidle2 }
            });
            var root = await page.QuerySelectorAsync(".root");
            var img = await root.ScreenshotStreamAsync();
            var dt = DateTime.Parse(obj.GetProperty("time").GetString()).ToUniversalTime();
            using (var imgStream = binder.Bind<Stream>(
                new BlobAttribute($"mltd-img/{dt.ToString("yyyy-MM-ddTHH-mmZ")}.png", FileAccess.Write))
            )
            {
                await img.CopyToAsync(imgStream);
            }
        }
    }
}
