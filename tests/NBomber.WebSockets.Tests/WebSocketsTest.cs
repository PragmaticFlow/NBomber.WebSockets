using NBomber;
using NBomber.CSharp;
using NBomber.Data;
using NBomber.WebSockets;

namespace Tests.WebSockets;

public class WebSocketsTest
{
    [Fact]
    public void EndToEnd()
    {
        var clientPool = new ClientPool<WebSocket>();
        var payload = Data.GenerateRandomBytes(200);

        var scenario = Scenario.Create("websockets_client_pool", async ctx =>
        {
            var websocket = clientPool.GetClient(ctx.ScenarioInfo);

            var ping = await Step.Run("ping", ctx, async () =>
            {
                await websocket.Send(payload);
                return Response.Ok(sizeBytes: payload.Length);
            });

            var pong = await Step.Run("pong", ctx, async () =>
            {
                using var response = await websocket.Receive(ctx.ScenarioCancellationToken);
                // var str = Encoding.UTF8.GetString(response.Data.Span);
                // var user = JsonSerializer.Deserialize<T>(response.Data.Span);
                return Response.Ok(sizeBytes: response.Data.Length);
            });

            return Response.Ok();
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(Simulation.KeepConstant(10, TimeSpan.FromSeconds(5)))
        .WithInit(async ctx =>
        {
            for (var i = 0; i < 100; i++)
            {
                var websocket = new WebSocket(new WebSocketConfig());
                await websocket.Connect("ws://localhost:5139/ws");
                await Task.Delay(10);

                clientPool.AddClient(websocket);
            }
        })
        .WithClean(ctx =>
        {
            clientPool.DisposeClients(client => client.Dispose());
            return Task.CompletedTask;
        });

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        Assert.True(stats.AllOkCount > 0);
        Assert.True(stats.AllFailCount == 0);

        foreach (var scenarioStats in stats.ScenarioStats)
        {
            foreach (var stepStats in scenarioStats.StepStats)
                Assert.True(stepStats.Ok.Latency.MaxMs > 0);
        }
    }
}