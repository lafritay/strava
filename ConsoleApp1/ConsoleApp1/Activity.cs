using IO.Swagger.Model;

namespace ConsoleApp1
{
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
}