﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using IO.Swagger.Api;
using IO.Swagger.Client;
using IO.Swagger.Model;
using Newtonsoft.Json;

namespace ConsoleApp1
{
    internal class Program
    {
        private static async Task<Tokens> RefreshCodeAsync(
            Creds2 creds)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                using (HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("POST"), "https://www.strava.com/api/v3/oauth/token"))
                {
                    List<string> contentList = new List<string>
                    {
                        $"client_id={creds.ClientId}",
                        $"client_secret={creds.ClientSecret}",
                        "grant_type=refresh_token",
                        $"refresh_token={creds.Tokens.RefreshToken}"
                    };
                    request.Content = new StringContent(string.Join("&", contentList));
                    request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");

                    HttpResponseMessage response = await httpClient.SendAsync(request);

                    return JsonConvert.DeserializeObject<Tokens>(await response.Content.ReadAsStringAsync());
                }
            }
        }

        private static async Task Main(string[] args)
        {
            Console.WriteLine(args[0].Length);

            string azureFileName = "creds2.txt";
            BlobServiceClient blobServiceClient = new BlobServiceClient(args[0]);

            //Create a unique name for the container
            string containerName = "leaderboards";

            // Create the container and return a container client object
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            // Get a reference to a blob
            BlobClient blobClient = containerClient.GetBlobClient(azureFileName);

            // Download the blob's contents and save it to a file
            BlobDownloadInfo download = await blobClient.DownloadAsync();
            string fc;
            using (StreamReader reader = new StreamReader(download.Content))
            {
                fc = await reader.ReadToEndAsync();
            }

            Creds2 creds = JsonConvert.DeserializeObject<Creds2>(fc);

            if (DateTime.UtcNow > creds.Tokens.GetExpiresAt())
            {
                Tokens tokens = await RefreshCodeAsync(creds);

                creds = new Creds2
                {
                    ClientId = creds.ClientId,
                    ClientSecret = creds.ClientSecret,
                    Tokens = tokens
                };

                Console.WriteLine("Uploading to Blob storage as blob:\n\t {0}\n", blobClient.Uri);

                // Open the file and upload its data
                MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(creds) ?? ""));
                await blobClient.UploadAsync(stream, true);
                stream.Close();
            }

            IEnumerable<SummaryActivity> data = await FetchData(containerClient, creds.Tokens.AccessToken);
            IEnumerable<UserSummary> collated = CollateData(data);
            StringBuilder distanceRows = new StringBuilder();
            UserSummary[] distance = collated.OrderByDescending(d => d.TotalDistance()).ToArray();
            for (int i = 0; i < distance.Length; i++)
            {
                UserSummary summary = distance[i];
                StringBuilder builder = new StringBuilder();
                builder.Append("<tr>");
                AddCell(builder, $"{i + 1}. {summary.User}");
                AddCell(builder, summary.DistanceWalked.InMiles());
                AddCell(builder, summary.DistanceRan.InMiles());
                AddCell(builder, summary.DistanceBiked.InMiles());
                AddCell(builder, summary.TotalDistance().InMiles());
                builder.Append("</tr>");

                distanceRows.AppendLine(builder.ToString());
            }

            StringBuilder timeRows = new StringBuilder();
            UserSummary[] time = collated.OrderByDescending(d => d.TotalMovingTime).ToArray();
            for (int i = 0; i < time.Length; i++)
            {
                UserSummary summary = time[i];
                StringBuilder builder = new StringBuilder();
                builder.Append("<tr>");
                AddCell(builder, $"{i + 1}. {summary.User}");
                AddCell(builder, summary.TotalTime.InHours());
                AddCell(builder, summary.TotalMovingTime.InHours());
                builder.Append("</tr>");

                timeRows.AppendLine(builder.ToString());
            }

            string template = File.ReadAllText("ConsoleApp1/ConsoleApp1/Template.html");
            string content = template.Replace("{{distanceRows}}", distanceRows.ToString());
            content = content.Replace("{{timeRows}}", timeRows.ToString());
            content = content.Replace("{{updated}}", DateTime.Now.ToString("dddd, MMMM dd, hh:mm tt ET"));

            File.WriteAllText("index.html", content);
        }

        private static void AddCell(
            StringBuilder builder,
            string value)
        {
            builder.Append("<td>");
            builder.Append(value);
            builder.Append("</td>");
        }

        private static IEnumerable<UserSummary> CollateData(IEnumerable<SummaryActivity> data)
        {
            Dictionary<string, UserSummary> summaries = new Dictionary<string, UserSummary>();
            foreach (SummaryActivity activity in data)
            {
                if (!summaries.TryGetValue(activity.Athlete.FirstName, out UserSummary summary))
                {
                    summary = new UserSummary
                    {
                        User = activity.Athlete.FirstName
                    };
                    summaries[activity.Athlete.FirstName] = summary;
                }

                summary.Count++;
                summary.TotalTime += (long)activity.ElapsedTime!;
                summary.TotalMovingTime += (long)activity.MovingTime!;
                switch (activity.Type)
                {
                    case ActivityType.Ride:
                        summary.DistanceBiked += activity.Distance.Value;
                        break;

                    case ActivityType.Run:
                        summary.DistanceRan += activity.Distance.Value;
                        break;

                    case ActivityType.Walk:
                        summary.DistanceWalked += activity.Distance.Value;
                        break;
                }
            }

            return summaries.Values;
        }

        private static async Task<IEnumerable<SummaryActivity>> FetchData(
            BlobContainerClient containerClient,
            string accessToken)
        {
            Configuration.ApiKey["access"] = accessToken;
            Configuration.ApiKeyPrefix["access"] = "Bearer";
            ApiClient client = new ApiClient();
            ClubsApi clubsApi = new ClubsApi(client);

            // Get a reference to a blob
            string azureFileName = "data.json";
            BlobClient blobClient = containerClient.GetBlobClient(azureFileName);

            // Download the blob's contents and save it to a file
            BlobDownloadInfo download = await blobClient.DownloadAsync();
            string fc;
            using (StreamReader reader = new StreamReader(download.Content))
            {
                fc = await reader.ReadToEndAsync();
            }

            Dictionary<string, Activity> stored = JsonConvert.DeserializeObject<Activity[]>(fc)
                .ToDictionary(a => a.Id);

            List<SummaryActivity> activities = clubsApi.GetClubActivitiesById(661551, 1, 200);
            foreach (SummaryActivity summary in activities)
            {
                if (summary.Name == "Bacon cheeseburger not a winning pre-workout meal" && summary.Athlete.FirstName == "Matthew" && summary.ElapsedTime == 1685)
                {
                    break;
                }

                Activity activity = new Activity(summary);
                stored[activity.Id] = activity;
            }

            // Sanity Check
            if (stored.Values.Any(s => s.Summary.Athlete.FirstName == "Alex" && s.Summary.Distance == 7964.3 && s.Summary.MovingTime == 3048))
            {
                throw new Exception("Found bad value, can't trust the sort");
            }

            string data = JsonConvert.SerializeObject(stored.Values);

            // Open the file and upload its data
            MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(data ?? ""));
            await blobClient.UploadAsync(stream, true);
            stream.Close();

            return stored.Values.Select(s => s.Summary);
        }
    }

    public class Activity
    {
        public Activity(
            SummaryActivity summary)
        {
            Id = $"{summary.Athlete.FirstName}_{summary.Athlete.LastName}_{summary.ElapsedTime}" +
                $"_{summary.Distance}";
            Summary = summary;
        }

        public string Id { get; }

        public SummaryActivity Summary { get; }
    }

    public class UserSummary
    {
        public string User { get; set; }

        public int Count { get; set; }

        public long TotalTime { get; set; }

        public long TotalMovingTime { get; set; }

        public float DistanceRan { get; set; }

        public float DistanceBiked { get; set; }

        public float DistanceWalked { get; set; }

        public float TotalDistance()
        {
            return (float)(DistanceRan + DistanceWalked + (DistanceBiked / 3.5));
        }
    }

    public static class Extensions
    {
        public static string InMiles(
            this float meters)
        {
            double miles = meters * 0.000621371;
            return Math.Round(miles, 2).ToString();
        }

        public static string InHours(
            this long seconds)
        {
            return TimeSpan.FromSeconds(seconds).ToString();
        }
    }
}