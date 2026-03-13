using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Extras.Files;
using NzbDrone.Core.Extras.Metadata.Files;
using NzbDrone.Core.Extras.Others;
using NzbDrone.Core.Extras.Subtitles;
using Radarr.Http;

namespace Radarr.Api.V3.ExtraFiles
{
    [V3ApiController("extrafile")]
    public class ExtraFileController : Controller
    {
        private readonly IExtraFileService<SubtitleFile> _subtitleFileService;
        private readonly IExtraFileService<MetadataFile> _metadataFileService;
        private readonly IExtraFileService<OtherExtraFile> _otherFileService;
        private readonly IConfigService _configService;

        public ExtraFileController(IExtraFileService<SubtitleFile> subtitleFileService,
                                   IExtraFileService<MetadataFile> metadataFileService,
                                   IExtraFileService<OtherExtraFile> otherExtraFileService,
                                   IConfigService configService)
        {
            _subtitleFileService = subtitleFileService;
            _metadataFileService = metadataFileService;
            _otherFileService = otherExtraFileService;
            _configService = configService;
        }

        [HttpGet]
        public List<ExtraFileResource> GetFiles(int movieId)
        {
            if (_configService.PlaceInRootFolder)
            {
                return new List<ExtraFileResource>();
            }

            var extraFiles = new List<ExtraFileResource>();

            var subtitleFiles = _subtitleFileService.GetFilesByMovie(movieId).OrderBy(f => f.RelativePath).ToList();
            var metadataFiles = _metadataFileService.GetFilesByMovie(movieId).OrderBy(f => f.RelativePath).ToList();
            var otherExtraFiles = _otherFileService.GetFilesByMovie(movieId).OrderBy(f => f.RelativePath).ToList();

            extraFiles.AddRange(subtitleFiles.ToResource());
            extraFiles.AddRange(metadataFiles.ToResource());
            extraFiles.AddRange(otherExtraFiles.ToResource());

            return extraFiles;
        }
    }
}
