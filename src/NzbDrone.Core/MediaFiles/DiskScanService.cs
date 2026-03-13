using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.MediaFiles.MovieImport;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies;
using NzbDrone.Core.RootFolders;

namespace NzbDrone.Core.MediaFiles
{
    public interface IDiskScanService
    {
        void Scan(Movie movie);
        string[] GetVideoFiles(string path, bool allDirectories = true);
        string[] GetNonVideoFiles(string path, bool allDirectories = true);
        List<string> FilterPaths(string basePath, IEnumerable<string> paths, bool filterExtras = true);
    }

    public class DiskScanService :
        IDiskScanService,
        IExecute<RescanMovieCommand>
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IMakeImportDecision _importDecisionMaker;
        private readonly IImportApprovedMovie _importApprovedMovies;
        private readonly IConfigService _configService;
        private readonly IMovieService _movieService;
        private readonly IMediaFileService _mediaFileService;
        private readonly IMediaFileTableCleanupService _mediaFileTableCleanupService;
        private readonly IRootFolderService _rootFolderService;
        private readonly IUpdateMediaInfo _updateMediaInfoService;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public DiskScanService(IDiskProvider diskProvider,
                               IMakeImportDecision importDecisionMaker,
                               IImportApprovedMovie importApprovedMovies,
                               IConfigService configService,
                               IMovieService movieService,
                               IMediaFileService mediaFileService,
                               IMediaFileTableCleanupService mediaFileTableCleanupService,
                               IRootFolderService rootFolderService,
                               IUpdateMediaInfo updateMediaInfoService,
                               IEventAggregator eventAggregator,
                               Logger logger)
        {
            _diskProvider = diskProvider;
            _importDecisionMaker = importDecisionMaker;
            _importApprovedMovies = importApprovedMovies;
            _configService = configService;
            _movieService = movieService;
            _mediaFileService = mediaFileService;
            _mediaFileTableCleanupService = mediaFileTableCleanupService;
            _rootFolderService = rootFolderService;
            _updateMediaInfoService = updateMediaInfoService;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _scanLocks =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        // Visible for testing — clears stale locks between test runs
        internal static void ResetScanLocks()
        {
            _scanLocks.Clear();
        }

        private static readonly Regex ExcludedExtrasSubFolderRegex = new Regex(@"(?:\\|\/|^)(?:extras|extrafanart|behind the scenes|deleted scenes|featurettes|interviews|other|scenes|sample[s]?|shorts|trailers)(?:\\|\/)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ExcludedSubFoldersRegex = new Regex(@"(?:\\|\/|^)(?:@eadir|\.@__thumb|plex versions|\.[^\\/]+)(?:\\|\/)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ExcludedExtraFilesRegex = new Regex(@"(-(trailer|other|behindthescenes|deleted|featurette|interview|scene|short)\.[^.]+$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ExcludedFilesRegex = new Regex(@"^\.(_|unmanic|DS_Store$)|^Thumbs\.db$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public void Scan(Movie movie)
        {
            var scanLock = _scanLocks.GetOrAdd(movie.Path, _ => new SemaphoreSlim(1, 1));

            if (!scanLock.Wait(0))
            {
                _logger.Debug("Scan already in progress for '{0}', skipping", movie.Path);
                return;
            }

            try
            {
                ScanInternal(movie);
            }
            finally
            {
                scanLock.Release();
            }
        }

        private void ScanInternal(Movie movie)
        {
            var rootFolder = _rootFolderService.GetBestRootFolderPath(movie.Path);

            var movieFolderExists = _diskProvider.FolderExists(movie.Path);

            if (!movieFolderExists)
            {
                if (!_diskProvider.FolderExists(rootFolder))
                {
                    _logger.Warn("Movie's root folder ({0}) doesn't exist.", rootFolder);
                    _eventAggregator.PublishEvent(new MovieScanSkippedEvent(movie, MovieScanSkippedReason.RootFolderDoesNotExist));
                    return;
                }

                if (_diskProvider.FolderEmpty(rootFolder))
                {
                    _logger.Warn("Movie's root folder ({0}) is empty. Rescan will not update movies as a failsafe.", rootFolder);
                    _eventAggregator.PublishEvent(new MovieScanSkippedEvent(movie, MovieScanSkippedReason.RootFolderIsEmpty));
                    return;
                }
            }

            // Skip scan if folder hasn't been modified since last scan
            if (movieFolderExists && movie.LastDiskScanTime.HasValue)
            {
                try
                {
                    var folderLastWrite = _diskProvider.FolderGetLastWrite(movie.Path);

                    if (folderLastWrite < movie.LastDiskScanTime.Value)
                    {
                        _logger.Debug("Skipping scan of {0}: folder unchanged since last scan ({1})",
                            movie.Title,
                            movie.LastDiskScanTime.Value);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Unable to check folder last write time for {0}, proceeding with scan", movie.Path);
                }
            }

            _logger.ProgressInfo("Scanning disk for {0}", movie.Title);

            if (!movieFolderExists)
            {
                if (_configService.CreateEmptyMovieFolders)
                {
                    if (_configService.DeleteEmptyFolders)
                    {
                        _logger.Debug("Not creating missing movie folder: {0} because delete empty movie folders is enabled", movie.Path);
                    }
                    else
                    {
                        _logger.Debug("Creating missing movie folder: {0}", movie.Path);

                        _diskProvider.CreateFolder(movie.Path);
                        SetPermissions(movie.Path);
                    }
                }
                else
                {
                    _logger.Debug("Movie's folder doesn't exist: {0}", movie.Path);
                }

                CleanMediaFiles(movie, new List<string>());
                CompletedScanning(movie, new List<string>());

                return;
            }

            var videoFilesStopwatch = Stopwatch.StartNew();
            var mediaFileList = FilterPaths(movie.Path, GetVideoFiles(movie.Path)).ToList();
            videoFilesStopwatch.Stop();
            _logger.Trace("Finished getting movie files for: {0} [{1}]", movie, videoFilesStopwatch.Elapsed);

            // When PlaceInRootFolder is enabled, all movies share one folder.
            // Pre-filter the file list to only files whose name contains the movie title
            // to avoid reading media info from hundreds of unrelated files.
            if (_configService.PlaceInRootFolder && movie.Title.IsNotNullOrWhiteSpace())
            {
                var allVideoFiles = mediaFileList;
                var beforeCount = mediaFileList.Count;
                var titleWithYear = NormalizeForComparison($"{movie.Title} ({movie.Year})");
                mediaFileList = mediaFileList.Where(f =>
                {
                    var fileName = NormalizeForComparison(Path.GetFileNameWithoutExtension(f));
                    return fileName != null && fileName.StartsWith(titleWithYear, StringComparison.OrdinalIgnoreCase);
                }).ToList();

                // Fall back to title-only match if year-match found nothing (reuse cached file list)
                if (mediaFileList.Count == 0)
                {
                    var movieTitle = NormalizeForComparison(movie.Title);
                    mediaFileList = allVideoFiles
                        .Where(f =>
                        {
                            var fileName = NormalizeForComparison(Path.GetFileNameWithoutExtension(f));
                            return fileName != null && fileName.StartsWith(movieTitle, StringComparison.OrdinalIgnoreCase);
                        }).ToList();
                }

                _logger.Debug("PlaceInRootFolder: filtered {0} files to {1} for {2}", beforeCount, mediaFileList.Count, titleWithYear);

                if (mediaFileList.Any())
                {
                    _logger.Trace("PlaceInRootFolder: matched files for {0}: {1}", movie.Title, string.Join(", ", mediaFileList.Select(Path.GetFileName)));
                }
            }

            CleanMediaFiles(movie, mediaFileList);

            var movieFiles = _mediaFileService.GetFilesByMovie(movie.Id);
            var unmappedFiles = MediaFileService.FilterExistingFiles(mediaFileList, movieFiles, movie);

            var decisionsStopwatch = Stopwatch.StartNew();
            var decisions = _importDecisionMaker.GetImportDecisions(unmappedFiles, movie, false);
            decisionsStopwatch.Stop();
            _logger.Trace("Import decisions complete for: {0} [{1}]", movie, decisionsStopwatch.Elapsed);
            _importApprovedMovies.Import(decisions, false);

            // Update existing files that have a different file size
            var fileInfoStopwatch = Stopwatch.StartNew();
            var filesToUpdate = new List<MovieFile>();

            foreach (var file in movieFiles)
            {
                var path = Path.Combine(movie.Path, file.RelativePath);
                var fileSize = _diskProvider.GetFileSize(path);

                if (file.Size == fileSize)
                {
                    continue;
                }

                file.Size = fileSize;

                if (!_updateMediaInfoService.Update(file, movie))
                {
                    filesToUpdate.Add(file);
                }
            }

            // Update any files that had a file size change, but didn't get media info updated.
            if (filesToUpdate.Any())
            {
                _mediaFileService.Update(filesToUpdate);
            }

            fileInfoStopwatch.Stop();
            _logger.Trace("Reprocessing existing files complete for: {0} [{1}]", movie, fileInfoStopwatch.Elapsed);

            var possibleExtraFiles = new List<string>();

            if (_configService.PlaceInRootFolder)
            {
                _logger.Debug("PlaceInRootFolder: skipping extra file scan for {0} (shared root folder)", movie);
            }
            else
            {
                var filesOnDisk = GetNonVideoFiles(movie.Path);
                possibleExtraFiles = FilterPaths(movie.Path, filesOnDisk);
            }

            RemoveEmptyMovieFolder(movie.Path);
            CompletedScanning(movie, possibleExtraFiles);
        }

        private void CleanMediaFiles(Movie movie, List<string> mediaFileList)
        {
            _logger.Debug("{0} Cleaning up media files in DB", movie);
            _mediaFileTableCleanupService.Clean(movie, mediaFileList);
        }

        private void CompletedScanning(Movie movie, List<string> possibleExtraFiles)
        {
            _logger.Info("Completed scanning disk for {0}", movie.Title);

            movie.LastDiskScanTime = DateTime.UtcNow;
            _movieService.UpdateLastDiskScanTime(movie);

            _eventAggregator.PublishEvent(new MovieScannedEvent(movie, possibleExtraFiles));
        }

        public string[] GetVideoFiles(string path, bool allDirectories = true)
        {
            _logger.Debug("Scanning '{0}' for video files", path);

            var filesOnDisk = _diskProvider.GetFiles(path, allDirectories).ToList();

            var mediaFileList = filesOnDisk.Where(file => MediaFileExtensions.Extensions.Contains(Path.GetExtension(file)))
                                           .ToList();

            _logger.Trace("{0} files were found in {1}", filesOnDisk.Count, path);
            _logger.Debug("{0} video files were found in {1}", mediaFileList.Count, path);

            return mediaFileList.ToArray();
        }

        public string[] GetNonVideoFiles(string path, bool allDirectories = true)
        {
            _logger.Debug("Scanning '{0}' for non-video files", path);

            var filesOnDisk = _diskProvider.GetFiles(path, allDirectories).ToList();

            var mediaFileList = filesOnDisk.Where(file => !MediaFileExtensions.Extensions.Contains(Path.GetExtension(file)))
                                           .ToList();

            _logger.Trace("{0} files were found in {1}", filesOnDisk.Count, path);
            _logger.Debug("{0} non-video files were found in {1}", mediaFileList.Count, path);

            return mediaFileList.ToArray();
        }

        public List<string> FilterPaths(string basePath, IEnumerable<string> paths, bool filterExtras = true)
        {
            var filteredPaths =  paths.Where(path => !ExcludedSubFoldersRegex.IsMatch(basePath.GetRelativePath(path)))
                                      .Where(path => !ExcludedFilesRegex.IsMatch(Path.GetFileName(path)))
                                      .ToList();

            if (filterExtras)
            {
                filteredPaths = filteredPaths.Where(path => !ExcludedExtrasSubFolderRegex.IsMatch(basePath.GetRelativePath(path)))
                                             .Where(path => !ExcludedExtraFilesRegex.IsMatch(Path.GetFileName(path)))
                                             .ToList();
            }

            return filteredPaths;
        }

        private void SetPermissions(string path)
        {
            if (!_configService.SetPermissionsLinux)
            {
                return;
            }

            try
            {
                _diskProvider.SetPermissions(path, _configService.ChmodFolder, _configService.ChownGroup);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Unable to apply permissions to: " + path);
                _logger.Debug(ex, ex.Message);
            }
        }

        private void RemoveEmptyMovieFolder(string path)
        {
            if (_configService.DeleteEmptyFolders)
            {
                if (_configService.PlaceInRootFolder)
                {
                    _logger.Debug("PlaceInRootFolder: skipping empty folder cleanup for shared root folder");
                    return;
                }

                _diskProvider.RemoveEmptySubfolders(path);

                if (_diskProvider.FolderEmpty(path))
                {
                    _diskProvider.DeleteFolder(path, true);
                }
            }
        }

        private static string NormalizeForComparison(string value)
        {
            if (value == null)
            {
                return null;
            }

            return value.Normalize();
        }

        public void Execute(RescanMovieCommand message)
        {
            if (message.MovieId.HasValue)
            {
                var movie = _movieService.GetMovie(message.MovieId.Value);
                Scan(movie);
            }
            else
            {
                List<Movie> allMovies;

                if (_configService.RefreshMonitoredOnly)
                {
                    allMovies = _movieService.GetMonitoredMovies();
                    _logger.Debug("RefreshMonitoredOnly is enabled, scanning {0} monitored movies", allMovies.Count);
                }
                else
                {
                    allMovies = _movieService.GetAllMovies();
                }

                var scannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var movie in allMovies)
                {
                    if (scannedPaths.Add(movie.Path))
                    {
                        Scan(movie);
                    }
                    else
                    {
                        _logger.Debug("Skipping scan of {0}. Reason: Path already scanned this cycle", movie.Title);
                    }
                }
            }
        }
    }
}
