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
            var downloadsFolder = Path.GetTempPath();
            var option = new BrowserFetcherOptions
            {
                Path = downloadsFolder
            };
            var fetcher = new BrowserFetcher(option);
            fetcher.DownloadAsync(BrowserFetcher.DefaultRevision).Wait();
            var browser = Puppeteer.LaunchAsync(new LaunchOptions
            {
                ExecutablePath = fetcher.RevisionInfo(BrowserFetcher.DefaultRevision).ExecutablePath,
                Headless = true,
                Args = new[]
                {
                    !isProd ? "--proxy-server=winip:1080" : ""
                }
            }).Result;
            page = browser.NewPageAsync().Result;
            page.SetViewportAsync(new ViewPortOptions
            {
                Height = 800,
                Width = 1300
            }).Wait();
        }
        static bool isProd = Environment.GetEnvironmentVariable("DOTNET_ENV") == "prod";
        static string PWD = isProd ? "/home/site/wwwroot" : Directory.GetCurrentDirectory();

        [FunctionName("PicGen")]
        public static async Task Run(
            [QueueTrigger("html-queue")] string context,
            ILogger log,
            IBinder binder
        )
        {
            log.LogInformation("start to process html content");
            var location = Path.Combine(PWD, "index.html");
            var obj = JsonDocument.Parse(context).RootElement;
            var html = obj.GetProperty("src").GetString();
            await File.WriteAllTextAsync(location, html);
            await page.GoToAsync($"file://{location}", new NavigationOptions
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
