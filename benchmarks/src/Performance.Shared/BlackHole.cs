using System.Runtime.CompilerServices;
namespace Performance.Shared;

public static class BlackHole
{
    public static volatile int Sink;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Consume(int value)
    {
        Sink = value;
    }
}
