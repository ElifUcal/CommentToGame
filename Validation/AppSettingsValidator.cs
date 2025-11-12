// Validation/AppSettingsValidator.cs
using FluentValidation;

public class AppSettingsValidator : AbstractValidator<AppSettings>
{
    public AppSettingsValidator()
    {
        RuleFor(x => x.General.SiteName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Security.Timeout).InclusiveBetween(5, 240);
        RuleFor(x => x.Security.MinLength).InclusiveBetween(6, 64);
        RuleFor(x => x.Content.MaxFileMb).InclusiveBetween(1, 100);
        RuleFor(x => x.Seo.Canonical).Must(uri => Uri.IsWellFormedUriString(uri, UriKind.Absolute));
        RuleFor(x => x.Seo.OgImage).Must(uri => Uri.IsWellFormedUriString(uri, UriKind.Absolute));
    }
}
