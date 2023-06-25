using ByteSizeLib;
using Flurl;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;
using System.Text.RegularExpressions;

namespace OpexDownloader
{
    /// <summary></summary>
    public class Program
    {
        /// <summary></summary>
        /// <param name="firefoxLocation">Path to the Firefox executable.</param>
        /// <param name="seasonUrl">OPEX season URL to download. Example: https://onepieceex.net/episodios/t15/</param>
        /// <param name="checkDownloadDirectory">Directory where you store all your One Piece episodes. Example: "C:\One Piece"</param>
        /// <param name="actualDownloadDirectory">Subdirectory where the specified season will be downloaded. Example: "C:\One Piece\Season 21"</param>
        static async Task Main(string firefoxLocation, string seasonUrl, string checkDownloadDirectory, string actualDownloadDirectory)
        {
            if (new[] { firefoxLocation, seasonUrl, checkDownloadDirectory, actualDownloadDirectory }.Any(string.IsNullOrWhiteSpace))
            {
                Console.WriteLine("Please provide all arguments.");
                return;
            }

            var driverOptions = new FirefoxOptions();
            driverOptions.AddArgument("--headless");
            driverOptions.BrowserExecutableLocation = firefoxLocation;
            using var driver = new FirefoxDriver(driverOptions);

            using var httpClient = new HttpClient();
            int count = 0;

        linkHasExpired:
            driver.Navigate().GoToUrl(seasonUrl);

            IEnumerable<IWebElement> episodeElements = driver.FindElements(By.CssSelector("article.episodiov5"));
            episodeElements = episodeElements.Skip(count);

            foreach (IWebElement episodeElement in episodeElements)
            {
                IWebElement episodeNumberElement = episodeElement.FindElement(By.XPath("a/header/h1/strong"));
                string episodeNumber = episodeNumberElement.Text.TrimStart('0');
                Console.Write($"Episode {episodeNumber}: ");

                bool alreadyDownloaded = Directory.EnumerateFiles(checkDownloadDirectory, "*", SearchOption.AllDirectories)
                    .Any(delegate (string filePath)
                    {
                        if (filePath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                            return false;

                        string fileName = Path.GetFileName(filePath);
                        string pattern = $"[ _-]0*{episodeNumber}[ _-]";
                        return Regex.IsMatch(fileName, pattern);
                    });

                if (alreadyDownloaded)
                {
                    Console.WriteLine("Skipped.");

                    count++;
                    continue;
                }

                IWebElement qualityGroupElement = episodeElement.FindElement(By.XPath("nav/ul/li[position() > 1]"));
                IWebElement qualityElement = qualityGroupElement.FindElement(By.TagName("a"));
                Console.Write($"Downloading {qualityElement.Text} quality... ");

                IWebElement downloadRedirectElement = qualityGroupElement.FindElement(By.XPath("div/a[@class = \"opex-server\"]"));
                driver.ExecuteScript("arguments[0].target=\"_blank\";", downloadRedirectElement);
                driver.ExecuteScript("arguments[0].click();", downloadRedirectElement);
                driver.SwitchTo().Window(driver.WindowHandles.Last());

                IWebElement downloadButtonElement;
                try
                {
                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
                    wait.IgnoreExceptionTypes(typeof(NoSuchElementException));

                    downloadButtonElement = wait.Until(a => a.FindElement(By.XPath("//a[text() = \"Baixar\" and string(@href)]")));
                }
                catch (WebDriverTimeoutException ex)
                {
                    Console.WriteLine("Link has expired. Reloading...");

                    driver.Close();
                    driver.SwitchTo().Window(driver.WindowHandles.First());
                    goto linkHasExpired;
                }

                Url downloadLink = downloadButtonElement.GetAttribute("href");
                using HttpResponseMessage response = await httpClient.GetAsync(downloadLink, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                string filePath = Path.Combine(actualDownloadDirectory, downloadLink.PathSegments.Last());
                string temporaryFilePath = filePath + ".tmp";

                (int Left, int Top) cursorPosition = Console.GetCursorPosition();
                long totalSize = response.Content.Headers.ContentLength.GetValueOrDefault();
                long totalBytesRead = 0;

                var buffer = new byte[(int)ByteSize.FromMegaBytes(1).Bytes];
                using (var fileStream = new FileStream(temporaryFilePath, FileMode.Create, FileAccess.Write, FileShare.Read, buffer.Length, useAsync: true))
                using (Stream contentStream = await response.Content.ReadAsStreamAsync())
                {
                    int bytesRead;

                    do
                    {
                        bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);

                        if (bytesRead > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;
                        }

                        double percentage = (double)totalBytesRead / totalSize;
                        Console.SetCursorPosition(cursorPosition.Left, cursorPosition.Top);
                        Console.Write($"{ByteSize.FromBytes(totalBytesRead)} / {ByteSize.FromBytes(totalSize)} = {percentage:0.00%}");
                    }
                    while (bytesRead > 0);
                }

                File.Move(temporaryFilePath, filePath);
                driver.Close();
                driver.SwitchTo().Window(driver.WindowHandles.First());

                Console.WriteLine();
                count++;
            }
        }
    }
}