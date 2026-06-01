# TinySuite and the sample app

TinyDispatcher is part of TinySuite, the small family of libraries made of TinyDispatcher, TinyValidations, and TinyEvents.

Each package stays focused:

- TinyDispatcher owns command and query execution.
- TinyValidations owns application input validation.
- TinyEvents owns reliable application-event handling through the outbox pattern.

## TheTinyApplicationLayer sample

The shared sample lives in the sibling `TheTinyApplicationLayer` repository.

It is an ASP.NET Core and Blazor application that uses the three TinySuite NuGet packages together:

```text
Blazor Form
-> API Endpoint
-> TinyValidations
-> TinyDispatcher
-> Use Case
-> TinyEvents Outbox
-> Worker
-> Event Consumer
```

TinyDispatcher appears in the middle of that flow. It dispatches the accepted command to the use-case handler after validation has passed, keeping the application entry point explicit while the surrounding packages handle validation and durable event side effects.

Use the sample when you want to see how TinyDispatcher fits with the rest of TinySuite in a real application shape rather than as an isolated package demo.
