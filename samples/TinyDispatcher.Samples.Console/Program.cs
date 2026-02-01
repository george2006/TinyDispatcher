using TinyDispatcher.Samples;


await PolicySample.Run();

await GlobalMiddlewareSample.Run();

await PerCommandMiddlewareSample.Run();