using FluentValidation;
using FluentValidation.Results;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Validation.Paths;

namespace NzbDrone.Core.Movies
{
    public interface IAddMovieValidator
    {
        ValidationResult Validate(Movie instance);
    }

    public class AddMovieValidator : AbstractValidator<Movie>, IAddMovieValidator
    {
        public AddMovieValidator(RootFolderValidator rootFolderValidator,
                                 RecycleBinValidator recycleBinValidator,
                                 MoviePathValidator moviePathValidator,
                                 MovieAncestorValidator movieAncestorValidator,
                                 IConfigService configService)
        {
            RuleFor(c => c.Path).Cascade(CascadeMode.Stop)
                                .IsValidPath()
                                .SetValidator(rootFolderValidator)
                                .When(_ => !configService.PlaceInRootFolder)
                                .SetValidator(recycleBinValidator)
                                .SetValidator(moviePathValidator)
                                .When(_ => !configService.PlaceInRootFolder)
                                .SetValidator(movieAncestorValidator)
                                .When(_ => !configService.PlaceInRootFolder);
        }
    }
}
