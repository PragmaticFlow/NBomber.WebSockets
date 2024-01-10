using NBomber.CSharp;
using NBomber.WebSockets;
using NBomber.Data;

var payload = Data.GenerateRandomBytes(10_000_000);

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
            // var str = Encoding.UTF8.GetString(response.Data.Span);
            // JsonSerializer.Deserialize<string>(response.Data.Span);
            return Response.Ok(sizeBytes: response.Data.Length);
        });

        var disconnect = await Step.Run("disconnect", context, async () =>
        {
            await websocket.Close();
            return Response.Ok();
        });

        return Response.Ok();
    })
    .WithoutWarmUp()
    .WithLoadSimulations(Simulation.KeepConstant(copies: 1, during: TimeSpan.FromSeconds(30)));

NBomberRunner
    .RegisterScenarios(scenario)
    .Run();