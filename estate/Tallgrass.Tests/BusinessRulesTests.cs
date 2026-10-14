using System;
using NUnit.Framework;
using Tallgrass.Business;

namespace Tallgrass.Tests
{
    [TestFixture]
    public class BusinessRulesTests
    {
        [Test] public void Saturday_is_weekend() { Assert.IsTrue(BusinessRules.IsWeekend(new DateTime(2011, 3, 12))); }
        [Test] public void Monday_is_not_weekend() { Assert.IsFalse(BusinessRules.IsWeekend(new DateTime(2011, 3, 14))); }
        [Test] public void Status_A_is_active() { Assert.AreEqual("Active", BusinessRules.StatusText("A")); }
        [Test] public void Status_unknown() { Assert.AreEqual("Unknown", BusinessRules.StatusText("Q")); }
        [Test, Ignore("needs database")] public void Can_disconnect() { }
        [Test, Ignore("flaky in December")] public void Winter_moratorium() { }
    }
}
