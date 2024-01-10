using System.Net.WebSockets;
using Demo.Server;
using Microsoft.IO;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

var app = builder.Build();

app.UseRouting();
app.MapControllers();
app.UseWebSockets();

app.Run();