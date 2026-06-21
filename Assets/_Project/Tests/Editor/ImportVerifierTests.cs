using NUnit.Framework;

namespace Soulslike.Tests
{
    public class ImportVerifierTests
    {
        /// <summary>
        /// Asserts every tracked FBX still matches its known-good import baseline.
        /// Failure message lists each violation as: asset / field / actual / expected.
        /// </summary>
        [Test]
        public void TrackedAssets_ImportConfig_MatchesBaseline()
        {
            var violations = ImportVerifier.Verify();
            if (violations.Count > 0)
                Assert.Fail(ImportVerifier.BuildReport(violations));
        }
    }
}
