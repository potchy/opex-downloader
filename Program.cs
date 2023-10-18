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
            //driverOptions.AddArgument("--headless");
            driverOptions.SetPreference("browser.download.folderList", 2);
            driverOptions.SetPreference("browser.download.dir", actualDownloadDirectory);
            driverOptions.SetPreference("browser.helperApps.neverAsk.saveToDisk", "video/mp4");
            driverOptions.BrowserExecutableLocation = firefoxLocation;

            using var driver = new FirefoxDriver(driverOptions);
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

                (int Left, int Top) cursorPosition = Console.GetCursorPosition();

                while (true)
                {
                    try
                    {
                        IWebElement downloadLink = driver.FindElement(By.XPath("//a[@id=\"link-final\" and @data-clicked=\"true\"]"));
                        
                        // if app reaches this line, it means that the JavaScript code has finished running.
                        // now, we need to wait until we acquire an exclusive lock to the downloaded file, which means the browser has finished writting it.
                        string fileName = downloadLink.GetAttribute("download");
                        string filePath = Path.Combine(actualDownloadDirectory, fileName);
                        
                        async Task AcquireExclusiveLock()
                        {
                            while (true)
                            {
                                try
                                {
                                    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    await Task.Delay(1000);
                                    continue;
                                }
                            }
                        }

                        await AcquireExclusiveLock();
                        break;
                    }
                    catch (NoSuchElementException ex)
                    {
                    }

                    try
                    {
                        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
                        wait.IgnoreExceptionTypes(typeof(NoSuchElementException));

                        // if app reaches this line, it means the download is still ongoing.
                        IWebElement downloadedElement = wait.Until(a => a.FindElement(By.Id("downloaded")));
                        IWebElement totalSizeElement = wait.Until(a => a.FindElement(By.Id("totalSize")));
                        IWebElement progressLabelElement = wait.Until(a => a.FindElement(By.Id("progressLabel")));

                        if (new[] { downloadedElement.Text, totalSizeElement.Text, progressLabelElement.Text }.All(a => !string.IsNullOrWhiteSpace(a)))
                        {
                            Console.SetCursorPosition(cursorPosition.Left, cursorPosition.Top);
                            Console.Write(new string(' ', Console.BufferWidth - cursorPosition.Left));
                            Console.SetCursorPosition(cursorPosition.Left, cursorPosition.Top);
                            Console.Write($"{downloadedElement.Text} / {totalSizeElement.Text} = {progressLabelElement.Text}");
                        }

                        await Task.Delay(1000);
                    }
                    catch (WebDriverTimeoutException ex)
                    {
                        Console.WriteLine("Link has expired. Reloading...");

                        driver.Close();
                        driver.SwitchTo().Window(driver.WindowHandles.First());
                        goto linkHasExpired;
                    }
                }

                driver.Close();
                driver.SwitchTo().Window(driver.WindowHandles.First());

                Console.WriteLine();
            }
        }
    }
}