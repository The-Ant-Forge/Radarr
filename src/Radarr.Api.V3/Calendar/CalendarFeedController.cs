using System;
using System.Collections.Generic;
using System.Linq;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Tags;
using Radarr.Http;

namespace Radarr.Api.V3.Calendar
{
    [V3FeedController("calendar")]
    public class CalendarFeedController : Controller
    {
        private readonly IMovieService _movieService;
        private readonly ITagService _tagService;

        public CalendarFeedController(IMovieService movieService, ITagService tagService)
        {
            _movieService = movieService;
            _tagService = tagService;
        }

        [HttpGet("Radarr.ics")]
        public IActionResult GetCalendarFeed(int pastDays = 7, int futureDays = 28, string tags = "", bool unmonitored = false, IReadOnlyCollection<CalendarReleaseType> releaseTypes = null)
        {
            pastDays = Math.Clamp(pastDays, 0, 365);
            futureDays = Math.Clamp(futureDays, 0, 365);

            var start = DateTime.Today.AddDays(-pastDays);
            var end = DateTime.Today.AddDays(futureDays);
            var parsedTags = new List<int>();

            if (tags.IsNotNullOrWhiteSpace())
            {
                foreach (var tagName in tags.Split(','))
                {
                    try
                    {
                        parsedTags.Add(_tagService.GetTag(tagName).Id);
                    }
                    catch
                    {
                        // Skip unknown tags rather than failing the entire feed
                    }
                }
            }

            var movies = _movieService.GetMoviesBetweenDates(start, end, unmonitored);
            var calendar = new Ical.Net.Calendar
            {
                ProductId = "-//radarr.video//Radarr//EN"
            };

            var calendarName = "Radarr Movies Calendar";
            calendar.AddProperty(new CalendarProperty("NAME", calendarName));
            calendar.AddProperty(new CalendarProperty("X-WR-CALNAME", calendarName));

            foreach (var movie in movies.OrderBy(v => v.Added))
            {
                if (parsedTags.Any() && parsedTags.None(movie.Tags.Contains))
                {
                    continue;
                }

                if (releaseTypes is not { Count: not 0 } || releaseTypes.Contains(CalendarReleaseType.CinemaRelease))
                {
                    CreateEvent(calendar, movie.MovieMetadata, "cinematic");
                }

                if (releaseTypes is not { Count: not 0 } || releaseTypes.Contains(CalendarReleaseType.DigitalRelease))
                {
                    CreateEvent(calendar, movie.MovieMetadata, "digital");
                }

                if (releaseTypes is not { Count: not 0 } || releaseTypes.Contains(CalendarReleaseType.PhysicalRelease))
                {
                    CreateEvent(calendar, movie.MovieMetadata, "physical");
                }
            }

            var serializer = (IStringSerializer)new SerializerFactory().Build(calendar.GetType(), new SerializationContext());
            var icalendar = serializer.SerializeToString(calendar);

            return Content(icalendar, "text/calendar");
        }

        private void CreateEvent(Ical.Net.Calendar calendar, MovieMetadata movie, string releaseType)
        {
            var date = movie.InCinemas;
            var eventType = "_cinemas";
            var summaryText = "(Theatrical Release)";

            if (releaseType == "digital")
            {
                date = movie.DigitalRelease;
                eventType = "_digital";
                summaryText = "(Digital Release)";
            }
            else if (releaseType == "physical")
            {
                date = movie.PhysicalRelease;
                eventType = "_physical";
                summaryText = "(Physical Release)";
            }

            if (!date.HasValue)
            {
                return;
            }

            var occurrence = calendar.Create<CalendarEvent>();
            occurrence.Uid = "Radarr_movie_" + movie.Id + eventType;
            occurrence.Status = movie.Status == MovieStatusType.Announced ? EventStatus.Tentative : EventStatus.Confirmed;

            occurrence.Start = new CalDateTime(date.Value.Year, date.Value.Month, date.Value.Day);

            occurrence.Description = movie.Overview ?? string.Empty;
            occurrence.Categories = new List<string> { movie.Studio ?? string.Empty };

            occurrence.Summary = $"{movie.Title} {summaryText}";
        }
    }
}
