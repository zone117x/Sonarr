using System.Net;
using FluentAssertions;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.Security;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;
using NzbDrone.Test.Common.Categories;

namespace NzbDrone.Core.Test.IndexerTests.NewznabTests
{
    [TestFixture]
    [IntegrationTest]
    public class InvalidApiKeyFixture : CoreTest<Newznab>
    {
        [TestCase("https://www.nzbtv.net")]
        [TestCase("https://www.nmatrix.co.za")]
        [TestCase("https://api.nzbgeek.info")]
        public void should_get_api_key_exception(string url)
        {
            X509CertificateValidationPolicy.Register();
            UseRealHttp();

            Subject.Definition = new IndexerDefinition()
            {
                Name = "Newznab",
                Settings = new NewznabSettings()
                {
                    Url = url,
                    ApiKey = "invalid_api_key"
                }
            };

            Subject.FetchRecent().Should().BeEmpty();

            ExceptionVerification.Expected("Invalid API Key for Newznab", LogLevel.Warn);
            ExceptionVerification.ExpectedWarns(1);
        }
    }
}
