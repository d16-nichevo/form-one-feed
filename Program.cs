using FluentFTP;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;

namespace FormOneFeed
{
    // Form One Feed
    // For more information, see:
    // https://github.com/d16-nichevo/form-one-feed
    internal class Program
    {
        // The collection of items from all the feeds we build up,
        // thread-safe for parallel use:
        private static ConcurrentBag<SyndicationItem> FeedItems = new ConcurrentBag<SyndicationItem>();
        static async Task Main(string[] args)
        {
            try
            {
                // Check usage.
                // https://learn.microsoft.com/en-us/windows/terminal/command-line-arguments?tabs=windows#options-and-commands
                if (args.Length == 0
                    || new string[] { "--help", "-h", "-?", "/?" }.Any(x => x.Equals(args[0], StringComparison.OrdinalIgnoreCase)))
                {
                    var appName = System.Diagnostics.Process.GetCurrentProcess().ProcessName.ToUpper();
                    Console.WriteLine("Fetches multiple RSS feeds and outputs a single combined feed.");
                    Console.WriteLine();
                    Console.WriteLine($"{appName} configs");
                    Console.WriteLine();
                    Console.WriteLine("\tconfigs\tA list of one or more locations of JSON configuration files, in ");
                    Console.WriteLine("\t\torder from lowest to highest precedence. Each config must be");
                    Console.WriteLine("\t\tseparated by a space. Configs can be on the local file system");
                    Console.WriteLine("\t\tor at a valid web URL.");
                    Console.WriteLine("");
                    Console.WriteLine("For more information, visit https://github.com/d16-nichevo/form-one-feed");
                    Environment.Exit(1);
                }

                // Read specified config files:
                HttpClient client = new HttpClient();
                var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory());
                foreach (var arg in args)
                {
                    // Local file or URL?
                    bool argIsFile = File.Exists(arg);
                    bool argIsUrl = Uri.IsWellFormedUriString(arg, UriKind.Absolute);
                    if (argIsFile)
                    {
                        // Read from a local file:
                        builder = builder.AddJsonFile(arg, optional: false);
                    }
                    else if (argIsUrl)
                    {
                        // Read from URL:
                        client.DefaultRequestHeaders.Add("User-Agent", "Other");
                        var stream = await client.GetStreamAsync(arg);
                        builder = builder.AddJsonStream(stream);
                    }
                    else
                    {
                        Console.WriteLine("Argument invalid:");
                        Console.WriteLine(arg);
                        Console.WriteLine();
                        Console.WriteLine("Is it a valid path to a local file or a valid web URL?");
                        Environment.Exit(1);
                    }
                }
                IConfiguration config = builder.Build();

                // Load all feeds, and collect items from them:
                var feeds = config.GetSection("SourceFeeds").Get<string[]>();
                DateTime feedItemOldest = DateTime.MinValue;
                if (config.CheckConfigVal("CombinedFeed:ItemMaxAgeInDays", typeof(int)))
                {
                    var feedItemMaxAgeInDays = config.GetSection("CombinedFeed:ItemMaxAgeInDays").Get<int>();
                    feedItemOldest = DateTime.Now.AddDays(-1 * feedItemMaxAgeInDays);
                }
                var prependFeedTitle = config.CheckConfigVal("CombinedFeed:PrefixFeedTitle", typeof(bool))
                                        && config.GetSection("CombinedFeed:PrefixFeedTitle").Get<bool>();
                // Because the bottleneck is likely internet bandwidth/latency
                // we do them in parallel to save time:
                Parallel.ForEach(feeds!, feed =>
                {
                    GetItemsFromFeed(feed, feedItemOldest, prependFeedTitle);
                });

                // Sort the feeds by date:
                List<SyndicationItem> sortedItems = FeedItems.OrderByDescending(x => x.PublishDate.UtcDateTime).ToList();
                // Truncate if needed:
                if (config.CheckConfigVal("CombinedFeed:MaxItemCount", typeof(int)))
                {
                    var feedMaxItemCount = config.GetSection("CombinedFeed:MaxItemCount").Get<int>();
                    sortedItems = sortedItems.Take(feedMaxItemCount).ToList();
                }

                // Output all items, now sorted, into a single feed:
                var feedTitle = config.GetSection("CombinedFeed:Title").Get<string>();
                var feedDescription = config.GetSection("CombinedFeed:Description").Get<string>();
                var feedAlternateLink = new Uri("http://www.contoso.com/");

                // Give the feed our custom title, description, etc:
                var combinedFeed = new SyndicationFeed(sortedItems);
                combinedFeed.Title = new TextSyndicationContent(config.GetSection("CombinedFeed:Title").Get<string>());
                combinedFeed.Description = new TextSyndicationContent(config.GetSection("CombinedFeed:Description").Get<string>());
                combinedFeed.LastUpdatedTime = DateTime.UtcNow;
                if (config.CheckConfigVal("CombinedFeed:ImageUrl", typeof(string)))
                {
                    combinedFeed.ImageUrl = new Uri(config.GetSection("CombinedFeed:ImageUrl").Get<string>()!);
                }

                // Prepare to write the combined feed:
                bool isAtom = config.CheckConfigVal("CombinedFeed:Format", typeof(string))
                                && config.GetSection("CombinedFeed:Format").Get<string>()!.Equals("atom", StringComparison.OrdinalIgnoreCase);
                bool isFtp = config.CheckConfigVal("CombinedFeed:OutputType", typeof(string))
                                && config.GetSection("CombinedFeed:OutputType").Get<string>()!.Equals("ftp", StringComparison.OrdinalIgnoreCase);
                var output = config.GetSection("CombinedFeed:Output").Get<string>() ?? String.Empty;
                Uri outputUri = new Uri(output);

                // Write to the desired location:
                if (outputUri.Scheme == Uri.UriSchemeFtp || outputUri.Scheme == Uri.UriSchemeFtps)
                {
                    // FTP or FTPS:
                    var username = config.GetSection("CombinedFeed:UploadUsername").Get<string>() ?? String.Empty;
                    var password = config.GetSection("CombinedFeed:UploadPassword").Get<string>() ?? String.Empty;
                    WriteToFtp(combinedFeed, isAtom, outputUri.Host, username, password, outputUri.AbsolutePath);
                }
                else if (outputUri.Scheme == Uri.UriSchemeFile)
                {
                    // Local filesystem:
                    WriteToFilesystem(combinedFeed, isAtom, outputUri.AbsolutePath);
                }
                else
                {
                    throw new InvalidOperationException($"Unknown output type '{output}'.");
                }
            }
            catch (Exception ex)
            {
                // On any error, throw a generic message:
                Console.WriteLine($"ERROR: {ex.Message}");
                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    Console.WriteLine($"INNER EXCEPTION: {ex.Message}");
                }
                // Console.WriteLine(ex.ToString());
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Populates the class variable FeedItems with items from this feed.
        /// </summary>
        /// <param name="feedUri">
        /// The URI this feed can be found at.
        /// Example: http://feeds.feedburner.com/OhNoPodcast
        /// </param>
        /// <param name="oldestAllowed">
        /// Feed items with a publish date older than this will be ignored.
        /// Null allows feed items from any date.
        /// </param>
        private static void GetItemsFromFeed(string feedUri, DateTime? oldestAllowed, bool prependFeedTitle)
        {
            XmlReader xml;
            SyndicationFeed? sf = null;

            try
            {
                xml = XmlReader.Create(feedUri);
                sf = SyndicationFeed.Load(xml);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ignoring feed {feedUri} due to error: {ex.Message}", ex);
                sf = null;
            }

            if (sf != null)
            {
                foreach (var item in sf.Items)
                {
                    try
                    {
                        if (oldestAllowed == null || item.PublishDate.DateTime >= oldestAllowed)
                        {
                            if (prependFeedTitle)
                            {
                                item.Title = new TextSyndicationContent($"{sf.Title.Text} — {item.Title.Text}");
                            }
                            FeedItems.Add(item);
                        }
                    }
                    catch (Exception ex)
                    {
                        string itemId;
                        try
                        {
                            itemId = item.Id;
                        }
                        catch
                        {
                            itemId = "(Unknown ID)";
                        }
                        Console.WriteLine($"Ignoring item {itemId} from feed {feedUri} due to error: {ex.Message}", ex);
                        sf = null;
                    }
                }
            }
        }

        /// <summary>
        /// Write a feed to a filename.
        /// </summary>
        /// <param name="feed">The feed to write.</param>
        /// <param name="filename">A full path to a filename on the local file system.</param>
        /// <param name="isAtom">If true, write in Atom format. RSS otherwise.</param>
        private static void WriteToFilesystem(SyndicationFeed feed, bool isAtom, string filename)
        {
            var rssWriter = new XmlTextWriter(filename!, Encoding.UTF8);
            rssWriter.Formatting = Formatting.Indented;
            rssWriter.WriteToFormatter(feed, isAtom);
            rssWriter.Close();
        }

        /// <summary>
        /// Write a feed to an FTP location.
        /// </summary>
        /// <param name="feed">The feed to write.</param>
        /// <param name="isAtom">If true, write in Atom format. RSS otherwise.</param>
        /// <param name="host">The FTP host.</param>
        /// <param name="username">The FTP username.</param>
        /// <param name="password">The FTP password.</param>
        /// <param name="remotePath">The remote path on the FTP server.</param>
        private static void WriteToFtp(SyndicationFeed feed, bool isAtom, string host, string username, string password, string remotePath)
        {
            var stream = new MemoryStream();
            var rssWriter = new XmlTextWriter(stream, Encoding.UTF8);
            rssWriter.Formatting = Formatting.Indented;
            rssWriter.WriteToFormatter(feed, isAtom);

            FtpClient client = new FtpClient(host, username, password);
            client.AutoConnect();
            client.UploadStream(stream, remotePath, FtpRemoteExists.Overwrite, false);
            rssWriter.Close();
            stream.Close();
        }
    }
}
