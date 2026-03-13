using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.AutoTagging;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Collections;
using NzbDrone.Core.Movies.Commands;
using NzbDrone.Core.Movies.Credits;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MovieTests
{
    [TestFixture]
    public class RefreshMovieBulkFixture : CoreTest<RefreshMovieService>
    {
        private Movie _movie1;
        private Movie _movie2;
        private Movie _movie3;
        private MovieMetadata _movieInfo1;
        private MovieMetadata _movieInfo2;
        private MovieMetadata _movieInfo3;
        private MovieCollection _movieCollection;

        [SetUp]
        public void Setup()
        {
            _movie1 = Builder<Movie>.CreateNew()
                .With(s => s.Id = 1)
                .With(s => s.MovieMetadataId = 101)
                .With(s => s.MovieMetadata.Value.TmdbId = 100)
                .With(s => s.MovieMetadata.Value.Title = "Starlight Express")
                .With(s => s.MovieMetadata.Value.Status = MovieStatusType.Released)
                .With(s => s.Path = @"C:\Test\Movies".AsOsAgnostic())
                .Build();

            _movie2 = Builder<Movie>.CreateNew()
                .With(s => s.Id = 2)
                .With(s => s.MovieMetadataId = 102)
                .With(s => s.MovieMetadata.Value.TmdbId = 200)
                .With(s => s.MovieMetadata.Value.Title = "The Phantom Lighthouse")
                .With(s => s.MovieMetadata.Value.Status = MovieStatusType.Released)
                .With(s => s.Path = @"C:\Test\Movies".AsOsAgnostic())
                .Build();

            _movie3 = Builder<Movie>.CreateNew()
                .With(s => s.Id = 3)
                .With(s => s.MovieMetadataId = 103)
                .With(s => s.MovieMetadata.Value.TmdbId = 300)
                .With(s => s.MovieMetadata.Value.Title = "Nebula Drift")
                .With(s => s.MovieMetadata.Value.Status = MovieStatusType.Released)
                .With(s => s.Monitored = false)
                .With(s => s.Path = @"C:\Test\Movies\Nebula Drift".AsOsAgnostic())
                .Build();

            _movieInfo1 = _movie1.MovieMetadata.Value.JsonClone();
            _movieInfo2 = _movie2.MovieMetadata.Value.JsonClone();
            _movieInfo3 = _movie3.MovieMetadata.Value.JsonClone();

            _movieCollection = Builder<MovieCollection>.CreateNew().Build();

            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.GetMovie(1))
                  .Returns(_movie1);

            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.GetMovie(2))
                  .Returns(_movie2);

            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.GetMovie(3))
                  .Returns(_movie3);

            Mocker.GetMock<IMovieMetadataService>()
                  .Setup(s => s.Get(101))
                  .Returns(_movie1.MovieMetadata.Value);

            Mocker.GetMock<IMovieMetadataService>()
                  .Setup(s => s.Get(102))
                  .Returns(_movie2.MovieMetadata.Value);

            Mocker.GetMock<IMovieMetadataService>()
                  .Setup(s => s.Get(103))
                  .Returns(_movie3.MovieMetadata.Value);

            Mocker.GetMock<IAddMovieCollectionService>()
                  .Setup(v => v.AddMovieCollection(It.IsAny<MovieCollection>()))
                  .Returns(_movieCollection);

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.GetBestRootFolderPath(It.IsAny<string>(), null))
                  .Returns(string.Empty);

            Mocker.GetMock<IAutoTaggingService>()
                  .Setup(s => s.GetTagChanges(It.IsAny<Movie>()))
                  .Returns(new AutoTaggingChanges());
        }

        private void GivenBulkMovieInfo(params MovieMetadata[] movies)
        {
            Mocker.GetMock<IProvideMovieInfo>()
                  .Setup(s => s.GetBulkMovieInfo(It.IsAny<List<int>>()))
                  .Returns(movies.ToList());
        }

        private void GivenAllMovies(params Movie[] movies)
        {
            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.GetAllMovies())
                  .Returns(movies.ToList());
        }

        private void GivenMonitoredMovies(params Movie[] movies)
        {
            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.GetMonitoredMovies())
                  .Returns(movies.ToList());
        }

        private void GivenRefreshMonitoredOnly()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.RefreshMonitoredOnly)
                  .Returns(true);
        }

        [Test]
        public void should_use_bulk_api_for_multiple_movies()
        {
            GivenAllMovies(_movie1, _movie2);
            GivenBulkMovieInfo(_movieInfo1, _movieInfo2);

            Subject.Execute(new RefreshMovieCommand { Trigger = CommandTrigger.Manual });

            Mocker.GetMock<IProvideMovieInfo>()
                  .Verify(v => v.GetBulkMovieInfo(It.Is<List<int>>(l => l.Count == 2)), Times.Once());

            // Should not fall back to individual refresh
            Mocker.GetMock<IProvideMovieInfo>()
                  .Verify(v => v.GetMovieInfo(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_fall_back_to_individual_when_bulk_fails()
        {
            GivenAllMovies(_movie1, _movie2);

            Mocker.GetMock<IProvideMovieInfo>()
                  .Setup(s => s.GetBulkMovieInfo(It.IsAny<List<int>>()))
                  .Throws(new Exception("Bulk API error"));

            Mocker.GetMock<IProvideMovieInfo>()
                  .Setup(s => s.GetMovieInfo(_movie1.TmdbId))
                  .Returns(new Tuple<MovieMetadata, List<Credit>>(_movieInfo1, new List<Credit>()));

            Mocker.GetMock<IProvideMovieInfo>()
                  .Setup(s => s.GetMovieInfo(_movie2.TmdbId))
                  .Returns(new Tuple<MovieMetadata, List<Credit>>(_movieInfo2, new List<Credit>()));

            Subject.Execute(new RefreshMovieCommand { Trigger = CommandTrigger.Manual });

            ExceptionVerification.ExpectedErrors(1);

            // Should fall back to individual calls
            Mocker.GetMock<IProvideMovieInfo>()
                  .Verify(v => v.GetMovieInfo(It.IsAny<int>()), Times.Exactly(2));
        }

        [Test]
        public void should_not_update_credits_in_bulk_mode()
        {
            GivenAllMovies(_movie1, _movie2);
            GivenBulkMovieInfo(_movieInfo1, _movieInfo2);

            Subject.Execute(new RefreshMovieCommand { Trigger = CommandTrigger.Manual });

            Mocker.GetMock<ICreditService>()
                  .Verify(v => v.UpdateCredits(It.IsAny<List<Credit>>(), It.IsAny<MovieMetadata>()), Times.Never());
        }

        [Test]
        public void should_update_metadata_for_all_movies_in_bulk()
        {
            GivenAllMovies(_movie1, _movie2);
            GivenBulkMovieInfo(_movieInfo1, _movieInfo2);

            Subject.Execute(new RefreshMovieCommand { Trigger = CommandTrigger.Manual });

            Mocker.GetMock<IMovieMetadataService>()
                  .Verify(v => v.Upsert(It.IsAny<MovieMetadata>()), Times.Exactly(2));
        }

        [Test]
        public void should_only_refresh_monitored_movies_when_refresh_monitored_only_enabled()
        {
            GivenRefreshMonitoredOnly();
            GivenMonitoredMovies(_movie1, _movie2);
            GivenBulkMovieInfo(_movieInfo1, _movieInfo2);

            Subject.Execute(new RefreshMovieCommand { Trigger = CommandTrigger.Manual });

            // Should use GetMonitoredMovies, not GetAllMovies
            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.GetMonitoredMovies(), Times.Once());

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.GetAllMovies(), Times.Never());

            // Should bulk refresh only 2 movies
            Mocker.GetMock<IProvideMovieInfo>()
                  .Verify(v => v.GetBulkMovieInfo(It.Is<List<int>>(l => l.Count == 2)), Times.Once());
        }

        [Test]
        public void should_deduplicate_scan_paths_with_place_in_root_folder()
        {
            // Both movies share the same path (PlaceInRootFolder mode)
            GivenAllMovies(_movie1, _movie2);
            GivenBulkMovieInfo(_movieInfo1, _movieInfo2);

            Subject.Execute(new RefreshMovieCommand { Trigger = CommandTrigger.Manual });

            // Should only scan once even though two movies share the path
            Mocker.GetMock<IDiskScanService>()
                  .Verify(v => v.Scan(It.IsAny<Movie>()), Times.Once());
        }

        [Test]
        public void should_scan_each_unique_path_once()
        {
            // movie1 and movie2 share root, movie3 has own folder
            GivenAllMovies(_movie1, _movie2, _movie3);
            GivenBulkMovieInfo(_movieInfo1, _movieInfo2, _movieInfo3);

            Subject.Execute(new RefreshMovieCommand { Trigger = CommandTrigger.Manual });

            // Should scan twice: once for shared root, once for movie3's folder
            Mocker.GetMock<IDiskScanService>()
                  .Verify(v => v.Scan(It.IsAny<Movie>()), Times.Exactly(2));
        }

        [Test]
        public void should_mark_missing_movie_as_deleted_in_bulk_mode()
        {
            GivenAllMovies(_movie1, _movie2);

            // Only return movie1 in bulk response — movie2 is "missing" from TMDb
            GivenBulkMovieInfo(_movieInfo1);

            Subject.Execute(new RefreshMovieCommand { Trigger = CommandTrigger.Manual });

            Mocker.GetMock<IMovieMetadataService>()
                  .Verify(v => v.Upsert(It.Is<MovieMetadata>(m => m.Status == MovieStatusType.Deleted)), Times.Once());

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_use_individual_refresh_for_single_movie_command()
        {
            Mocker.GetMock<IProvideMovieInfo>()
                  .Setup(s => s.GetMovieInfo(_movie1.TmdbId))
                  .Returns(new Tuple<MovieMetadata, List<Credit>>(_movieInfo1, new List<Credit>()));

            Subject.Execute(new RefreshMovieCommand(new List<int> { _movie1.Id }));

            // Single movie should use individual refresh (with credits), not bulk
            Mocker.GetMock<IProvideMovieInfo>()
                  .Verify(v => v.GetMovieInfo(_movie1.TmdbId), Times.Once());

            Mocker.GetMock<IProvideMovieInfo>()
                  .Verify(v => v.GetBulkMovieInfo(It.IsAny<List<int>>()), Times.Never());

            // Credits should be updated for single refresh
            Mocker.GetMock<ICreditService>()
                  .Verify(v => v.UpdateCredits(It.IsAny<List<Credit>>(), It.IsAny<MovieMetadata>()), Times.Once());
        }
    }
}
