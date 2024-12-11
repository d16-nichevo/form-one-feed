# Form One Feed

C# console app that can:

1. Read one or more RSS feeds located on the internet.
2. Combine those feeds usings settings you choose.
3. Output an RSS file that blends those feeds.

The broad idea is that this app can be run on a schedule (e.g. as a [Windows Scheduled Task](https://learn.microsoft.com/en-us/windows/win32/taskschd/task-scheduler-start-page)), and the resulting output file can be deployed to a website where it can be accessed.

# Why Merge Feeds?

I have a very particular setup desired for my podcast listening. But I also want to be lightly attached to the podcast app on my phone. I don't want to spend hours configuring it only to have that work invalidated by data loss, a change of phone, or a change of app. I also don't much like the convoluted inferface most podcast apps have.

By merging feeds together, it greatly cuts down on the number of feeds I need to input and configure for any app.

(I know there are other solutions to this problem, like OPML. This is how I chose to solve my problem.)

For a time, I used [Feed Informer](http://feed.informer.com/) to do this. But there was a long period where it simply stopped working, and I could find no competitor that I liked that was both simple-to-use and free. (Feed Informer is operating again, I think, but I've written this now anyway! 😅)

The name "Form One Feed" comes from the [road sign to `Form 1 Lane`](https://globalspill.com.au/product/form-one-lane-sign/). Not highly creative, I know.

# Installation

Form One Feed is targets .NET 8 and so should be able to run on Windows, Linux, and Mac. I've only tested on Windows.

At the current time, if you want to run Form One Feed, you'll have to download and compile the code yourself. I may make releases to download compiled software if there is demand (see below).

# Usage

Usage is pretty simple. From the command line, run:

```bash
formonefeed config
```

Where `config` is the path to a JSON file that contains various settings, located either on the local filesystem or an HTTP server.

Example 1 -- A Local File:

```bash
formonefeed c:/mydirectory/funny-podcasts.json
```

Example 2 -- An HTTP server (i.e. on the internet):

```bash
formonefeed http://my-fake-site.org/history-podcasts.json
```

# JSON Config File

You need to supply a JSON file the tells the program what feeds to merge and how.

Fields are explained below, but the actual code might out-pace this documentation. For the most up-to-date collection of fields, check the example [here](https://github.com/d16-nichevo/form-one-feed/blob/main/sample-config.json). Hopefully the names are self-evident.

```json
{
  "CombinedFeed": {
    "Format": "rss",
    "Title": "My Feed Mix",
    "Description": "This is my mix of feeds.",
    "ImageUrl": "https://i.imgur.com/MsDJpcz.png",
    "MaxItemCount": 1000,
    "ItemMaxAgeInDays": 30,
    "PrefixFeedTitle": true,
    "Output": "c:/temp/combined-feed.rss",
    "UploadUsername": "merisiel",
    "UploadPassword": "d4gg3r5"
  },
  "SourceFeeds": [
    "http://feeds.feedburner.com/OhNoPodcast",
    "https://audioboom.com/channels/2399216.rss"
  ]
}
```

Fields:

* `CombinedFeed`
  * These fields are channel elements as-per the [RSS specification](https://www.rssboard.org/rss-specification#requiredChannelElements):
    * `Title`, `Description`, `ImageUrl`
  * `Format`: either `rss` or `atom`. Defaults to `rss`.
  * `ItemMaxAgeInDays`: Any feed item older than this many days will be ignored.
  * `MaxItemCount`: The combined feed will stop after this many items.  
  * `Output`: Path to the merged feed file that Form One Feed will create. Existing files will be overwritten. Currently supports local filesystem and FTP.
    * **Filesystem**: Example: `c:/temp/myfeed.rss`
    * **FTP**: Example: `ftp://myserver.net/htdocs/rss/myfeed.rss` 
  * `PrefixFeedTitle`: Prefix the channel title to each feed item's title.
  * `UploadPassword`: The password for the FTP account. Only required when using FTP (see `Output`). 
  * `UploadUsername`: The username for the FTP account. Only required when using FTP (see `Output`). 
* `SourceFeeds`: A list of feeds you want merged.

💡 **Useful Tips**

1. Add a [query string](https://en.wikipedia.org/wiki/Query%5Fstring) to your `SourceFeeds` URLs if they have obscure URLs so you can identify them later. For example:
   * `https://audioboom.com/channels/2399216.rss` doesn't tell you what podcast this was for.
   * `https://audioboom.com/channels/2399216.rss?NoSuchThingAsAFish` is better, it helps identify the podcast, and should still work fine as a feed URL.
1. When uploading to FTP, ensure the full directory path already exists on the server, as FormOneFeed will not create any directories.

## Multiple Configs

You can accually specify more than one config, like this:

```bash
formonefeed c:/mydirectory/funny-podcasts.json http://my-fake-site.org/history-podcasts.json
```

Each config should be separated by a space. 

This can be useful if you wish to set common settings in one file, but override them with partial settings in another file.

Items later in the list "overwrite" settings from items earlier in the list. I'm not sure exactly how this works, I just let [`Microsoft.Extensions.Configuration.ConfigurationBuilder`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.configuration.configurationbuilder) do its thing.

# Future Development

For now, this is an app for my own usage. If others find it useful, great.

If there's enough demand I may make changes and improvements. Some possible ideas:

1. Add compiled releases to this project.
1. Better instructions for less-technical users.
1. More robust and informative error-handling.
1. Add more options to how feeds are merged.
1. Create a GUI application rather than a console app.
