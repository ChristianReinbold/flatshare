using IbanValidation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace de.creinbold.FlatShare.Tests
{
    [TestClass]
    public class IBANTest
    {
        public static IBAN GetValidIBAN()
        {
            return (IBAN)"DE95674811345042622227";
        }

        [TestMethod]
        public void FormatsValidIBAN()
        {
            var iban = (IBAN)"DE95 674811345042622 227  ";
            Assert.AreEqual((IBAN)"DE95674811345042622227", iban);
            Assert.AreEqual("DE95 6748 1134 5042 6222 27", iban.ToString());
        }

        [TestMethod]
        public void ValidateDetectsBadChecksum()
        {
            var iban = (IBAN)"DE95674811345042622224";
            Assert.AreEqual(IbanValidationResult.ValueFailsModule97Check, iban.Validate());
        }
    }
}
