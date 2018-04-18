using Generator.SchemaReaders;
using NUnit.Framework;

namespace Generator.Tests.Unit
{
    [TestFixture]
    public class CleanUpTests
    {
        [Test]
        [TestCase("HelloWorldTest", "HelloWorldTest", true)]
        [TestCase("Hello_World_Test", "HelloWorldTest", true)]
        [TestCase("Hello-World-Test", "HelloWorldTest", true)]
        [TestCase("Hello World Test", "HelloWorldTest", true)]
        [TestCase("hello world test", "HelloWorldTest", true)]
        [TestCase("HelloWORLD", "HelloWorld", true)]
        [TestCase("Helloworld", "Helloworld", true)]
        [TestCase("helloworld", "Helloworld", true)]
        [TestCase("HELLOWORLD", "Helloworld", true)]

        [TestCase("HelloWorldTest", "HelloWorldTest", false)]
        [TestCase("Hello_World_Test", "Hello_World_Test", false)]
        [TestCase("Hello-World-Test", "Hello-World-Test", false)]
        [TestCase("Hello World Test", "HelloWorldTest", false)]
        [TestCase("Hello world test", "Helloworldtest", false)]
        [TestCase("HelloWORLD", "HelloWORLD", false)]
        [TestCase("Helloworld", "Helloworld", false)]
        [TestCase("helloworld", "helloworld", false)]
        [TestCase("HELLOWORLD", "HELLOWORLD", false)]
        public void Test(string test, string expected, bool useCamelCase)
        {
            // Act
            string clean = SchemaReader.CleanUp(test);
            string singular = clean;
            string nameHumanCase = (useCamelCase ? Inflector.ToTitleCase(singular) : singular).Replace(" ", "").Replace("$", "").Replace(".", "");

            // Assert
            Assert.AreEqual(expected, nameHumanCase);
        }
    }
}