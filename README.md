> [!CAUTION]
> **⚠️ This repository is NOT maintained and is used for testing purposes only.**  
> It is **not part of the final solution** and should not be used in production.

![Status: Unmaintained](https://img.shields.io/badge/status-unmaintained-red?style=for-the-badge)
![Purpose: Testing Only](https://img.shields.io/badge/purpose-testing--only-orange?style=for-the-badge)
![Not Final](https://img.shields.io/badge/final--solution-NO-lightgrey?style=for-the-badge)

# Signaling Server (WebRTC solution)

Signaling server to initiate the connection between the robot and the oculus.\
It also has a client on HTTPS via the `/robot/signaling` and `/robot/signaling2` endpoints.

## Pre-requisites

- C# .NET 9
- .NET CLI
- Visual Studio (or Rider)

## Build

Before running the server, go to the .NET project's folder and build it.

```bash
cd SignalingServer
dotnet build
```

## Run

> [!TIP]
> To make it work on HTTPS, you will need to open up the port `5000` and expose it via https with SSL certificates.\
> If that isn't done, you might have issues allowing the webcam to be used on the client side.

Once, you have built the application, simply run it.

```bash
dotnet run
```

Now, the server should be available on port `5000`.

## Using the clients

> [!TIP]
> You can press `F12` to see insightful logs in the broswer directly. Those can help a lot when debugging WebRTC calls.

### `/robot/signaling`

Using this client is pretty simple:
1. Start your webcam by clicking on the button made for that.
2. Open another instance of the client on another device and open up the webcam\
   **OR**\
   Connect to that same endpoint using another WebRTC implementation compatible with this one.
3. Start the call on the first client.
4. Enjoy the WebRTC video call.

### `/robot/signaling2`

Using this client is also pretty simple:
1. Start your webcam by clicking on the button made for that.
2. Open the application where the WebRTC implementation with `H264` video format is done and launch it targetting this endpoint for the communication.
3. Start the call on the first client.
4. Enjoy the WebRTC video call.

---

> Made with care by [@Funnyadd](https://github.com/Funnyadd), [@ImprovUser](https://github.com/ImprovUser), [@cjayneb](https://github.com/cjayneb) and [@RaphaelCamara](https://github.com/RaphaelCamara) ❤️
