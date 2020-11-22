using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace mltd_img_gen
{
    public static class PicGen
    {
        static Page page;
        static PicGen()
        {
            new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision).Wait();
            var browser = Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = new[]
                {
                    "--proxy-server=winip:1080"
                }
            }).Result;
            page = browser.NewPageAsync().Result;
        }

        [FunctionName("PicGen")]
        public static async Task Run(
            [QueueTrigger("html-queue")] string html,
            ILogger log,
            [Blob("mltd-img/{DateTime}.png", FileAccess.Write)] Stream imgStream
        )
        {
            log.LogInformation("start to process html content");
            await page.SetContentAsync(html, new NavigationOptions
            {
                Timeout = 5000,
                WaitUntil = new[] { WaitUntilNavigation.Networkidle2 }
            });
            var root = await page.QuerySelectorAsync(".root");
            var img = await root.ScreenshotStreamAsync();
            await img.CopyToAsync(imgStream);
        }
    }
}
