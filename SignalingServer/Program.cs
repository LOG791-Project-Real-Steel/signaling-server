using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();

Dictionary<string, WebSocket?> clients = new()
{
    ["robot"] = null,
    ["oculus"] = null
};

Queue<string> cachedRobotMessages = new();
Queue<string> cachedOculusMessages = new();

app.Map("/robot/signaling", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket request expected");
        return;
    }

    var role = context.Request.Query["role"].ToString().ToLower();
    if (role != "robot" && role != "oculus")
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Missing or invalid ?role=robot|oculus");
        return;
    }

    var ws = await context.WebSockets.AcceptWebSocketAsync();
    Console.WriteLine($"{role} connected");
    clients[role] = ws;

    // Send cached messages to new peer
    string other = role == "robot" ? "oculus" : "robot";
    if (clients[other] is { State: WebSocketState.Open })
    {
        var queue = role == "robot" ? cachedOculusMessages : cachedRobotMessages;
        while (queue.TryDequeue(out var msg))
        {
            await ws.SendAsync(Encoding.UTF8.GetBytes(msg), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    var buffer = new byte[8192];
    try
    {
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var target = role == "robot" ? clients["oculus"] : clients["robot"];

            if (target is { State: WebSocketState.Open })
            {
                await target.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else
            {
                var queue = role == "robot" ? cachedRobotMessages : cachedOculusMessages;
                queue.Enqueue(message);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"WebSocket error: {ex.Message}");
    }
    finally
    {
        clients[role] = null;
        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", CancellationToken.None);
        Console.WriteLine($"{role} disconnected");
    }
});

app.Run("http://0.0.0.0:5000");
