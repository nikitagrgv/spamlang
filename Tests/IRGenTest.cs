namespace Tests;

public class IRGenTest
{
    [Fact]
    public void IRGen_SimpleProgramIsValid()
    {
        string code = $$"""
                        fn main() -> i32
                        {
                            let val = 1 + 2;
                            checkI32(val); // 3
                            val = (3.0 + 4) as i16;
                            checkI32(val); // 7
                            val = val * val;
                            checkI32(val); // 49
                        }

                        fn checkI32(value: i32)
                        {}w
                        """;
    }
}