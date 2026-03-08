using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Tags;
using Radarr.Api.V3.Calendar;

namespace NzbDrone.Api.Test.v3.Calendar;

[TestFixture]
public class CalendarFeedControllerFixture
{
    private Mock<IMovieService> _movieService;
    private Mock<ITagService> _tagService;
    private CalendarFeedController _controller;

    [SetUp]
    public void Setup()
    {
        _movieService = new Mock<IMovieService>();
        _tagService = new Mock<ITagService>();
        _controller = new CalendarFeedController(_movieService.Object, _tagService.Object);
    }

    private Movie CreateMovie(int id, string title, DateTime? inCinemas = null, DateTime? digitalRelease = null)
    {
        return new Movie
        {
            Id = id,
            Added = DateTime.UtcNow,
            Tags = new HashSet<int>(),
            MovieMetadata = new MovieMetadata
            {
                Id = id,
                Title = title,
                InCinemas = inCinemas,
                DigitalRelease = digitalRelease,
                Status = MovieStatusType.Released,
                Overview = "A test overview",
                Studio = "Test Studio"
            }
        };
    }

    [Test]
    public void should_return_ics_content_type()
    {
        _movieService.Setup(s => s.GetMoviesBetweenDates(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .Returns(new List<Movie>());

        var result = _controller.GetCalendarFeed() as ContentResult;

        result.Should().NotBeNull();
        result.ContentType.Should().Be("text/calendar");
    }

    [Test]
    public void should_return_events_for_movies()
    {
        var movies = new List<Movie>
        {
            CreateMovie(1, "The Temporal Paradox", inCinemas: DateTime.Today)
        };

        _movieService.Setup(s => s.GetMoviesBetweenDates(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .Returns(movies);

        var result = _controller.GetCalendarFeed() as ContentResult;

        result.Content.Should().Contain("The Temporal Paradox");
        result.Content.Should().Contain("VCALENDAR");
    }

    [Test]
    public void should_clamp_past_days_to_365()
    {
        _movieService.Setup(s => s.GetMoviesBetweenDates(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .Returns(new List<Movie>());

        _controller.GetCalendarFeed(pastDays: 9999);

        _movieService.Verify(s => s.GetMoviesBetweenDates(
            It.Is<DateTime>(d => d >= DateTime.Today.AddDays(-365)),
            It.IsAny<DateTime>(),
            It.IsAny<bool>()), Times.Once);
    }

    [Test]
    public void should_clamp_future_days_to_365()
    {
        _movieService.Setup(s => s.GetMoviesBetweenDates(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .Returns(new List<Movie>());

        _controller.GetCalendarFeed(futureDays: 9999);

        _movieService.Verify(s => s.GetMoviesBetweenDates(
            It.IsAny<DateTime>(),
            It.Is<DateTime>(d => d <= DateTime.Today.AddDays(365)),
            It.IsAny<bool>()), Times.Once);
    }

    [Test]
    public void should_clamp_negative_days_to_zero()
    {
        _movieService.Setup(s => s.GetMoviesBetweenDates(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .Returns(new List<Movie>());

        _controller.GetCalendarFeed(pastDays: -10, futureDays: -5);

        _movieService.Verify(s => s.GetMoviesBetweenDates(
            DateTime.Today,
            DateTime.Today,
            It.IsAny<bool>()), Times.Once);
    }

    [Test]
    public void should_skip_unknown_tags()
    {
        _tagService.Setup(s => s.GetTag("valid"))
            .Returns(new Tag { Id = 1, Label = "valid" });
        _tagService.Setup(s => s.GetTag("unknown"))
            .Throws(new InvalidOperationException("Tag not found"));

        var movies = new List<Movie>
        {
            CreateMovie(1, "Tagged Film", inCinemas: DateTime.Today)
        };

        movies[0].Tags = new HashSet<int> { 1 };

        _movieService.Setup(s => s.GetMoviesBetweenDates(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .Returns(movies);

        var result = _controller.GetCalendarFeed(tags: "valid,unknown") as ContentResult;

        result.Should().NotBeNull();
        result.Content.Should().Contain("Tagged Film");
    }

    [Test]
    public void should_handle_null_overview_and_studio()
    {
        var movie = CreateMovie(1, "Minimal Data Film", inCinemas: DateTime.Today);
        movie.MovieMetadata.Value.Overview = null;
        movie.MovieMetadata.Value.Studio = null;

        _movieService.Setup(s => s.GetMoviesBetweenDates(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .Returns(new List<Movie> { movie });

        var result = _controller.GetCalendarFeed() as ContentResult;

        result.Should().NotBeNull();
        result.Content.Should().Contain("Minimal Data Film");
    }

    [Test]
    public void should_skip_events_without_release_date()
    {
        var movie = CreateMovie(1, "No Date Film");

        _movieService.Setup(s => s.GetMoviesBetweenDates(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .Returns(new List<Movie> { movie });

        var result = _controller.GetCalendarFeed() as ContentResult;

        result.Content.Should().NotContain("No Date Film");
    }

    [Test]
    public void should_filter_by_release_type()
    {
        var movie = CreateMovie(1, "Digital Only", digitalRelease: DateTime.Today);

        _movieService.Setup(s => s.GetMoviesBetweenDates(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .Returns(new List<Movie> { movie });

        var result = _controller.GetCalendarFeed(
            releaseTypes: new List<CalendarReleaseType> { CalendarReleaseType.DigitalRelease }) as ContentResult;

        result.Content.Should().Contain("Digital Release");
        result.Content.Should().NotContain("Theatrical Release");
    }
}
