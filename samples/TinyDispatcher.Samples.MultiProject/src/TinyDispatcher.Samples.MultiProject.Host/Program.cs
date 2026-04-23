using System.Threading.Tasks;

namespace TinyDispatcher.Samples.MultiProject.Host;

internal static class Program
{
    public static Task Main()
    {
        return MultiProjectSample.RunAsync();
    }
}
