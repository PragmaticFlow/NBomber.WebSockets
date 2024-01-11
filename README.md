# NBomber.WebSockets
[![build](https://github.com/PragmaticFlow/NBomber.WebSockets/actions/workflows/build.yml/badge.svg)](https://github.com/PragmaticFlow/NBomber.WebSockets/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/nbomber.websockets.svg)](https://www.nuget.org/packages/nbomber.websockets/)

NBomber plugin for defining WebSockets scenarios. **WebSocket** is wrapper over native [ClientWebSocket](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.clientwebsocket?view=net-8.0) that provides extenions to simplify WebSockets handling:

- **Send** and **Receive** methods use reusable memory pools to reduce memory allocations.
- **Receive** method follows Pull-based semantics that simplifies writing load test scenarios due to the liner composition of the request/response handling.

<!-- ### Documentation
Documentation is located [here](https://nbomber.com/docs/protocols/http) -->

```csharp
var scenario = Scenario.Create("web_sockets", async context =>
{
    using var websocket = new WebSocket(new WebSocketConfig());

    var connect = await Step.Run("connect", context, async () =>
    {
        await websocket.Connect("ws://localhost:5231/ws");
        return Response.Ok();
    });

    var ping = await Step.Run("ping", context, async () =>
    {
        await websocket.Send(payload);
        return Response.Ok(sizeBytes: payload.Length);
    });

    var pong = await Step.Run("pong", context, async () =>
    {
        using var response = await websocket.Receive();
        return Response.Ok(sizeBytes: response.Data.Length);
    });

    var disconnect = await Step.Run("disconnect", context, async () =>
    {
        await websocket.Close();
        return Response.Ok();
    });

    return Response.Ok();
});
```
