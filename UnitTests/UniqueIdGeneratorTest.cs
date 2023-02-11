using System;
using AutoNumber.Documents;
using AutoNumber.Interfaces;
using NSubstitute;
using NUnit.Framework;

namespace AutoNumber.UnitTests;

[TestFixture]
public class UniqueIdGeneratorTest
{
    private const string TestScope = nameof(TestScope);

    [Test]
    public void ConstructorShouldNotRetrieveDataFromStore()
    {
        var store = Substitute.For<IOptimisticDataStore>();
        // ReSharper disable once ObjectCreationAsStatement
        new UniqueIdGenerator(store);
        store.DidNotReceiveWithAnyArgs().GetAutoNumberState(null);
    }

    [Test]
    public void MaxWriteAttemptsShouldThrowArgumentOutOfRangeExceptionWhenValueIsNegative()
    {
        var store = Substitute.For<IOptimisticDataStore>();
        Assert.That(() =>
                // ReSharper disable once ObjectCreationAsStatement
                new UniqueIdGenerator(store)
                {
                    MaxWriteAttempts = -1
                }
            , Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void MaxWriteAttemptsShouldThrowArgumentOutOfRangeExceptionWhenValueIsZero()
    {
        var store = Substitute.For<IOptimisticDataStore>();
        Assert.That(() =>
                // ReSharper disable once ObjectCreationAsStatement
                new UniqueIdGenerator(store)
                {
                    MaxWriteAttempts = 0
                }
            , Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void NextIdShouldReturnNumbersSequentially()
    {

        var store = Substitute.For<IOptimisticDataStore>();
        store.GetAutoNumberState(TestScope).Returns(CreateState(0), CreateState(250));
        store.TryOptimisticWrite(Arg.Any<AutoNumberState>()).Returns(true);

        var subject = new UniqueIdGenerator(store)
        {
            BatchSize = 3
        };

        Assert.AreEqual(0, subject.NextId(TestScope));
        Assert.AreEqual(1, subject.NextId(TestScope));
        Assert.AreEqual(2, subject.NextId(TestScope));
    }

    [Test]
    public void NextIdShouldRollOverToNewBlockWhenCurrentBlockIsExhausted()
    {
        var store = Substitute.For<IOptimisticDataStore>();
        store.GetAutoNumberState(TestScope).Returns(CreateState(0), CreateState(250));
        store.TryOptimisticWrite(Arg.Is<AutoNumberState>(s => s.NextAvailableNumber == 3)).Returns(true);
        store.TryOptimisticWrite(Arg.Is<AutoNumberState>(s => s.NextAvailableNumber == 253)).Returns(true);

        var subject = new UniqueIdGenerator(store)
        {
            BatchSize = 3
        };

        Assert.AreEqual(0, subject.NextId(TestScope));
        Assert.AreEqual(1, subject.NextId(TestScope));
        Assert.AreEqual(2, subject.NextId(TestScope));
        Assert.AreEqual(250, subject.NextId(TestScope));
        Assert.AreEqual(251, subject.NextId(TestScope));
        Assert.AreEqual(252, subject.NextId(TestScope));
    }

    [Test]
    public void NextIdShouldThrowExceptionWhenRetriesAreExhausted()
    {
        var store = Substitute.For<IOptimisticDataStore>();
        store.GetAutoNumberState(TestScope).Returns(CreateState(0));
        store.TryOptimisticWrite(Arg.Is<AutoNumberState>(s => s.NextAvailableNumber == 3)).Returns(false, false, false, true);

        var generator = new UniqueIdGenerator(store)
        {
            MaxWriteAttempts = 3
        };

        try
        {
            generator.NextId(TestScope);
        }
        catch (Exception ex)
        {
            StringAssert.StartsWith("Failed to update the data store after 3 attempts.", ex.Message);
            return;
        }

        Assert.Fail("NextId should have thrown and been caught in the try block");
    }

    private static AutoNumberState CreateState(long nextAvailableNumber) =>
        new()
        {
            Id = TestScope,
            NextAvailableNumber = nextAvailableNumber
        };
}