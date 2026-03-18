using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.MediaFiles.MovieImport;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.DiskScanServiceTests
{
    [TestFixture]
    public class PlaceInRootFolderFixture : CoreTest<DiskScanService>
    {
        private Movie _movie1;
        private Movie _movie2;
        private string _rootFolder;

        [SetUp]
        public void Setup()
        {
            DiskScanService.ResetScanLocks();

            _rootFolder = @"C:\Test\Movies".AsOsAgnostic();

            _movie1 = Builder<Movie>.CreateNew()
                .With(s => s.Id = 1)
                .With(s => s.Path = _rootFolder)
                .With(s => s.MovieMetadata.Value.Title = "Starlight Express")
                .With(s => s.MovieMetadata.Value.Year = 2024)
                .With(s => s.LastDiskScanTime = null)
                .Build();

            _movie2 = Builder<Movie>.CreateNew()
                .With(s => s.Id = 2)
                .With(s => s.Path = _rootFolder)
                .With(s => s.MovieMetadata.Value.Title = "The Phantom Lighthouse")
                .With(s => s.MovieMetadata.Value.Year = 2023)
                .With(s => s.LastDiskScanTime = null)
                .Build();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(It.IsAny<string>()))
                  .Returns(false);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetParentFolder(It.IsAny<string>()))
                  .Returns((string path) => Directory.GetParent(path).FullName);

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.GetBestRootFolderPath(It.IsAny<string>(), null))
                  .Returns(_rootFolder);

            Mocker.GetMock<IMediaFileService>()
                  .Setup(s => s.GetFilesByMovie(It.IsAny<int>()))
                  .Returns(new List<MovieFile>());

            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.UpdateLastDiskScanTime(It.IsAny<Movie>()));
        }

        private void GivenPlaceInRootFolder()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.PlaceInRootFolder)
                  .Returns(true);
        }

        private void GivenRootFolderExists()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(_rootFolder))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderEmpty(_rootFolder))
                  .Returns(false);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetDirectories(_rootFolder))
                  .Returns(new[] { _rootFolder });
        }

        private void GivenFiles(IEnumerable<string> files)
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetFiles(It.IsAny<string>(), true))
                  .Returns(files.ToArray());
        }

        [Test]
        public void should_filter_files_to_movie_title_when_place_in_root_folder_enabled()
        {
            GivenPlaceInRootFolder();
            GivenRootFolderExists();

            var movie1File = Path.Combine(_rootFolder, "Starlight Express (2024).mkv").AsOsAgnostic();
            var movie2File = Path.Combine(_rootFolder, "The Phantom Lighthouse (2023).mkv").AsOsAgnostic();
            var unrelatedFile = Path.Combine(_rootFolder, "Random Movie (2020).mkv").AsOsAgnostic();

            GivenFiles(new List<string> { movie1File, movie2File, unrelatedFile });

            Subject.Scan(_movie1);

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(
                      It.Is<List<string>>(l => l.Count == 1 && l[0] == movie1File),
                      _movie1,
                      false), Times.Once());
        }

        [Test]
        public void should_fall_back_to_title_only_when_year_match_finds_nothing()
        {
            GivenPlaceInRootFolder();
            GivenRootFolderExists();

            // File has no year in the name
            var movieFile = Path.Combine(_rootFolder, "Starlight Express.mkv").AsOsAgnostic();

            GivenFiles(new List<string> { movieFile });

            Subject.Scan(_movie1);

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(
                      It.Is<List<string>>(l => l.Count == 1 && l[0] == movieFile),
                      _movie1,
                      false), Times.Once());
        }

        [Test]
        public void should_not_match_same_title_with_different_year_in_fallback()
        {
            GivenPlaceInRootFolder();
            GivenRootFolderExists();

            // Movie in DB is "Starlight Express (2024)" but file on disk is the 2019 version
            var wrongYearFile = Path.Combine(_rootFolder, "Starlight Express (2019).mkv").AsOsAgnostic();

            GivenFiles(new List<string> { wrongYearFile });

            Subject.Scan(_movie1);

            // Should NOT match — different year means different movie
            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(
                      It.Is<List<string>>(l => l.Count == 0),
                      _movie1,
                      false), Times.Once());
        }

        [Test]
        public void should_match_title_only_file_in_fallback_but_reject_wrong_year()
        {
            GivenPlaceInRootFolder();
            GivenRootFolderExists();

            // No-year file should match, wrong-year file should not
            var noYearFile = Path.Combine(_rootFolder, "Starlight Express.mkv").AsOsAgnostic();
            var wrongYearFile = Path.Combine(_rootFolder, "Starlight Express (2019).mkv").AsOsAgnostic();

            GivenFiles(new List<string> { noYearFile, wrongYearFile });

            Subject.Scan(_movie1);

            // Primary filter (title+year) finds nothing, fallback should only match the no-year file
            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(
                      It.Is<List<string>>(l => l.Count == 1 && l[0] == noYearFile),
                      _movie1,
                      false), Times.Once());
        }

        [Test]
        public void should_skip_extra_files_scan_when_place_in_root_folder_enabled()
        {
            GivenPlaceInRootFolder();
            GivenRootFolderExists();

            var movieFile = Path.Combine(_rootFolder, "Starlight Express (2024).mkv").AsOsAgnostic();
            GivenFiles(new List<string> { movieFile });

            Subject.Scan(_movie1);

            // Should only call GetFiles once (for video files), not twice (no extras scan)
            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.GetFiles(It.IsAny<string>(), It.IsAny<bool>()), Times.Once());

            // Extra files should be empty
            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.Is<MovieScannedEvent>(
                      c => c.Movie != null && c.PossibleExtraFiles.Count == 0)), Times.Once());
        }

        [Test]
        public void should_skip_empty_folder_cleanup_when_place_in_root_folder_enabled()
        {
            GivenPlaceInRootFolder();
            GivenRootFolderExists();

            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.DeleteEmptyFolders)
                  .Returns(true);

            var movieFile = Path.Combine(_rootFolder, "Starlight Express (2024).mkv").AsOsAgnostic();
            GivenFiles(new List<string> { movieFile });

            Subject.Scan(_movie1);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.RemoveEmptySubfolders(It.IsAny<string>()), Times.Never());

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.DeleteFolder(It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_not_filter_files_when_place_in_root_folder_disabled()
        {
            GivenRootFolderExists();

            var movieFile = Path.Combine(_rootFolder, "Starlight Express (2024).mkv").AsOsAgnostic();
            var unrelatedFile = Path.Combine(_rootFolder, "Random Movie (2020).mkv").AsOsAgnostic();

            GivenFiles(new List<string> { movieFile, unrelatedFile });

            Subject.Scan(_movie1);

            // Should pass all files (no title filtering)
            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(v => v.GetImportDecisions(
                      It.Is<List<string>>(l => l.Count == 2),
                      _movie1,
                      false), Times.Once());
        }

        [Test]
        public void should_skip_scan_when_folder_unchanged_since_last_scan()
        {
            GivenRootFolderExists();

            var lastScan = DateTime.UtcNow;
            var folderWrite = lastScan.AddMinutes(-10);

            _movie1.LastDiskScanTime = lastScan;

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderGetLastWrite(_rootFolder))
                  .Returns(folderWrite);

            Subject.Scan(_movie1);

            // Should not proceed to file listing
            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.GetFiles(It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_scan_when_folder_modified_since_last_scan()
        {
            GivenRootFolderExists();

            var lastScan = DateTime.UtcNow.AddHours(-1);
            var folderWrite = DateTime.UtcNow;

            _movie1.LastDiskScanTime = lastScan;

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderGetLastWrite(_rootFolder))
                  .Returns(folderWrite);

            var movieFile = Path.Combine(_rootFolder, "Starlight Express (2024).mkv").AsOsAgnostic();
            GivenFiles(new List<string> { movieFile });

            Subject.Scan(_movie1);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.GetFiles(It.IsAny<string>(), It.IsAny<bool>()), Times.AtLeastOnce());
        }

        [Test]
        public void should_scan_when_no_previous_scan_time()
        {
            GivenRootFolderExists();

            _movie1.LastDiskScanTime = null;

            var movieFile = Path.Combine(_rootFolder, "Starlight Express (2024).mkv").AsOsAgnostic();
            GivenFiles(new List<string> { movieFile });

            Subject.Scan(_movie1);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.GetFiles(It.IsAny<string>(), It.IsAny<bool>()), Times.AtLeastOnce());
        }

        [Test]
        public void should_update_last_disk_scan_time_after_successful_scan()
        {
            GivenRootFolderExists();

            var movieFile = Path.Combine(_rootFolder, "Starlight Express (2024).mkv").AsOsAgnostic();
            GivenFiles(new List<string> { movieFile });

            Subject.Scan(_movie1);

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.UpdateLastDiskScanTime(It.Is<Movie>(m => m.LastDiskScanTime.HasValue)), Times.Once());
        }
    }
}
