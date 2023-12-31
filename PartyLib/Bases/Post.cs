﻿using HtmlAgilityPack;
using PartyLib.Config;
using PartyLib.Helpers;
using RestSharp;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;

namespace PartyLib.Bases;

public class Post : HttpAssetCore
{
    /// <summary>
    /// Post constructor. Most fields are filled here.
    /// </summary>
    /// <param name="url">The post's partysite URL</param>
    /// <param name="creator">The creator of the post</param>
    public Post(string url, Creator creator)
    {
        this.URL = url;
        this.Creator = creator;

        // Perform HTTP request
        RestResponse? response = HttpHelper.HttpGet(new RestRequest(), url);
        if (response == null)
        {
            // No response was recieved and something went very wrong
            this.SuccessfulFetch = false;
            this.StatusCode = HttpStatusCode.MethodNotAllowed;
        }
        else if (response.IsSuccessful == false)
        {
            // Response was recieved but failed
            this.SuccessfulFetch = false;
            this.StatusCode = response.StatusCode;
        }
        else
        {
            // Successful HTTP fetch
            this.SuccessfulFetch = true;
            this.StatusCode = response.StatusCode;

            // Load HTML
            HtmlDocument responseDocument = new HtmlDocument();
            responseDocument.LoadHtml(response.Content);
            this.PostHtml = responseDocument;

            // Fetch post ID
            Regex postIDFinder = new Regex("/post/(.*)");
            Match postIDMatch = postIDFinder.Match(url);
            string postId = postIDMatch.Groups[1].Value;
            ID = int.Parse(postId);

            // Fetch post title
            HtmlNode? titleParent = responseDocument.DocumentNode.Descendants().FirstOrDefault(x => x.HasClass("post__title") && x.Name == "h1");
            HtmlNode? titleSpan = titleParent?.ChildNodes.FirstOrDefault(x => x.Name == "span");
            string? postTitle = HttpUtility.HtmlDecode(titleSpan?.InnerText);

            // Fetch post upload date
            HtmlNode? dateParent = responseDocument.DocumentNode.Descendants().FirstOrDefault(x => x.HasClass("post__published") && x.Name == "div");
            HtmlNode? divChild = dateParent?.ChildNodes.FirstOrDefault(x => x.Name == "div");
            string? TimeText = dateParent?.InnerHtml.Replace(divChild.OuterHtml, "").Replace("\n", "").Trim();
            DateTime uploadDate = DateTime.ParseExact(TimeText, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            this.UploadDate = uploadDate;

            // Handle empty post titles
            if (postTitle == "Untitled")
            {
                postTitle = postTitle + " (Post ID∶ " + ID + ")";
            }

            // Translate post title if applicable
            if (PartyConfig.TranslationConfig.TranslateTitles)
            {
                try
                {
                    string translatedTitle = PartyConfig.TranslationConfig.Translator.TranslateAsync(postTitle, PartyConfig.TranslationConfig.TranslationLocaleCode).Result.Translation;
                    Title = translatedTitle;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception occurred during post title translation: {ex.Message}. Disabling translations for all future jobs. To re-enable, set the global variable back to true.");
                    PartyConfig.TranslationConfig.TranslateTitles = false;
                    Title = postTitle.Trim();
                }
            }
            else
            {
                Title = postTitle.Trim();
            }

            // Text for posts
            var contentNode = responseDocument.DocumentNode.Descendants().FirstOrDefault(x => x.HasClass("post__content") && x.Name == "div");
            if (contentNode != null)
            {
                // MEGA links
                if (PartyConfig.MegaOptions.EnableMegaSupport)
                {
                    List<HtmlNode> megaLinks = contentNode.Descendants().Where(x => x.Attributes["href"] != null && x.Attributes["href"].Value.Contains("https://mega.nz")).ToList();
                    foreach (var megaLink in megaLinks)
                    {
                        this.MegaUrls.Add(megaLink.Attributes["href"].Value);
                    }
                }

                // Content stuff
                var scrDesc = contentNode.InnerText;
                if (scrDesc.StartsWith("\n"))
                {
                    scrDesc = scrDesc.Remove(0);
                }
                foreach (var child in contentNode.ChildNodes) scrDesc = scrDesc + child.InnerText + "\n";
                if (PartyConfig.TranslationConfig.TranslateDescriptions)
                {
                    try
                    {
                        string translatedDescription = PartyConfig.TranslationConfig.Translator.TranslateAsync(scrDesc, PartyConfig.TranslationConfig.TranslationLocaleCode).Result.Translation;
                        Description = translatedDescription.Trim();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception occurred during post description translation: {ex.Message}. Disabling translations for all future jobs. To re-enable, set the global variable back to true.");
                        PartyConfig.TranslationConfig.TranslateDescriptions = false;
                        Description = scrDesc.Trim();
                    }
                }
                else
                {
                    Description = scrDesc.Trim();
                }
            }

            // Files
            var filesNode = responseDocument.DocumentNode.Descendants().FirstOrDefault(x => x.HasClass("post__files") && x.Name == "div");
            if (filesNode != null)
            {
                List<HtmlNode> files = filesNode.Descendants().Where(x => x.Attributes["href"] != null && x.Attributes["download"] != null).ToList();
                foreach (var file in files)
                {
                    var filey = new Attachment(file, this);
                    Files?.Add(filey);
                }
            }

            // Attachment posts
            var attachmentNode = responseDocument.DocumentNode.Descendants().FirstOrDefault(x => x.HasClass("post__attachments") && x.Name == "ul");
            if (attachmentNode != null)
            {
                List<HtmlNode> rawAttachments = attachmentNode.Descendants().Where(x => x.Attributes["href"] != null && x.Attributes["download"] != null).ToList();
                foreach (var attachment in rawAttachments)
                {
                    var attachy = new Attachment(attachment, this);
                    Attachments?.Add(attachy);
                }
            }
        }
    }

    /// <summary>
    /// The post's HTML DOM representation
    /// </summary>
    public HtmlDocument PostHtml { get; set; } = new HtmlDocument();

    /// <summary>
    /// The post's human-friendly title
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// The post's human-friendly description
    /// </summary>
    public string? Description { get; private set; } = string.Empty;

    /// <summary>
    /// The post's associated creator
    /// </summary>
    public Creator Creator { get; private set; }

    /// <summary>
    /// The post's upload time, as referenced from the partysite
    /// </summary>
    public DateTime UploadDate { get; private set; }

    /// <summary>
    /// Post's URL
    /// </summary>
    public string? URL { get; set; } = string.Empty;

    /// <summary>
    /// The post's internal ID
    /// </summary>
    public int ID { get; }

    /// <summary>
    /// Post iteration relative to the total posts number
    /// </summary>
    public int Iteration { get; set; }

    /// <summary>
    /// Reversed post iteration
    /// </summary>
    public int ReverseIteration { get; set; }

    /// <summary>
    /// A list of any discovered MEGA urls inside the post
    /// </summary>
    public List<string> MegaUrls { get; private set; } = new List<string>();

    /// <summary>
    /// A list of images attached to the post
    /// </summary>
    public List<Attachment>? Files { get; } = new();

    /// <summary>
    /// A list of attachments attached to the post
    /// </summary>
    public List<Attachment>? Attachments { get; } = new();
}