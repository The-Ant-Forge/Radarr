using System.Collections.Generic;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies.Collections;

namespace NzbDrone.Core.Movies
{
    public class MovieScannedHandler : IHandle<MovieScannedEvent>,
                                        IHandle<MovieScanSkippedEvent>
    {
        private readonly IMovieService _movieService;
        private readonly IMovieCollectionService _collectionService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly IConfigService _configService;

        private readonly Logger _logger;

        public MovieScannedHandler(IMovieService movieService,
                                    IMovieCollectionService collectionService,
                                    IManageCommandQueue commandQueueManager,
                                    IConfigService configService,
                                    Logger logger)
        {
            _movieService = movieService;
            _collectionService = collectionService;
            _commandQueueManager = commandQueueManager;
            _configService = configService;
            _logger = logger;
        }

        private void HandleScanEvents(Movie movie)
        {
            var addOptions = movie.AddOptions;

            if (addOptions == null)
            {
                return;
            }

            _logger.Info("[{0}] was recently added, performing post-add actions", movie.Title);

            // If the movie already has a file on disk and UnmonitorOnCutoffMet is enabled,
            // unmonitor it and skip the search — the existing file is assumed to meet quality.
            if (movie.MovieFileId > 0 && _configService.UnmonitorOnCutoffMet)
            {
                _logger.Info("[{0}] already has a file on disk, unmonitoring (UnmonitorOnCutoffMet)", movie.Title);
                movie.Monitored = false;
                _movieService.UpdateMovie(movie);
                movie.AddOptions = null;
                _movieService.RemoveAddOptions(movie);

                return;
            }

            if (addOptions.SearchForMovie)
            {
                _commandQueueManager.Push(new MoviesSearchCommand { MovieIds = new List<int> { movie.Id } });
            }

            if (addOptions.Monitor == MonitorTypes.MovieAndCollection && movie.MovieMetadata.Value.CollectionTmdbId > 0)
            {
                var collection = _collectionService.FindByTmdbId(movie.MovieMetadata.Value.CollectionTmdbId);
                collection.Monitored = true;

                _collectionService.UpdateCollection(collection);
            }

            movie.AddOptions = null;
            _movieService.RemoveAddOptions(movie);
        }

        public void Handle(MovieScannedEvent message)
        {
            HandleScanEvents(message.Movie);
        }

        public void Handle(MovieScanSkippedEvent message)
        {
            HandleScanEvents(message.Movie);
        }
    }
}
