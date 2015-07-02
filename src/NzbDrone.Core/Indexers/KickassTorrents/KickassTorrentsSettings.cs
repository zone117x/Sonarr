using System;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.KickassTorrents
{
    public class KickassTorrentsSettingsValidator : AbstractValidator<KickassTorrentsSettings>
    {
        public KickassTorrentsSettingsValidator()
        {
            RuleFor(c => c.BaseUrl).ValidRootUrl();
        }
    }

    public class KickassTorrentsSettings : IProviderConfig
    {
        private static readonly KickassTorrentsSettingsValidator Validator = new KickassTorrentsSettingsValidator();

        public KickassTorrentsSettings()
        {
            BaseUrl = "http://kat.cr";
            VerifiedOnly = true;
        }

        [FieldDefinition(0, Label = "Website URL")]
        public String BaseUrl { get; set; }

        [FieldDefinition(1, Label = "Verified Only", Type = FieldType.Checkbox, HelpText = "By setting this to No you will likely get more junk and unconfirmed releases, so use it with caution.")]
        public Boolean VerifiedOnly { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}