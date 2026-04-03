namespace MvtNet.Tests;

public class ZigZagTests
{
    [TestCase(0, ExpectedResult = 0u)]
    [TestCase(-1, ExpectedResult = 1u)]
    [TestCase(1, ExpectedResult = 2u)]
    [TestCase(-2, ExpectedResult = 3u)]
    [TestCase(2, ExpectedResult = 4u)]
    [TestCase(int.MaxValue, ExpectedResult = (uint)int.MaxValue * 2)]
    public uint ZigZag_EncodesCorrectly(int value)
    {
        return GeometryEncoder.ZigZag(value);
    }
}
