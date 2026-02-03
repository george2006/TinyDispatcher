using TinyDispatcher.Samples.Pipelines;


await PolicySample.Run();

await GlobalMiddlewareSample.Run();

await PerCommandMiddlewareSample.Run();

