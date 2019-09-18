# NUNet
NUNet stands for (Not Unity's Network) and it is a simple to use multi-threaded C# network abstraction layer. It abstracts TCP and UDP connections per client by introducing an easy to use API. Network packets are managed through queues and are made available through callbacks in both NUClient and NUServer classes.

## Usage
Server:
- Start server with NUServer.Start(), starts with default ip 127.0.0.1 and port 25565
- Listen for connections by subscribing to NUServer.onClientConnected action.
- Process received packets with NUServer.onClientPacketReceived action.
- Close server with NUServer.Shutdown()
- Send message with NUServer.Send()

Client:
- Start client with NUClient.Connect(), connects to default ip 127.0.0.1 and port 25565
- Connection callback available with NUClient.onConnected.
- Process received packets with NUClient.onPacketReceived action.
- Send message with NUClient.Send()
- Close connection with NUClient.Disconnect()

## WIP
- UDP Broadcast
- Web API

## Planed
- Meet-up Master Server and Routines (using NodeJS)
- NAT Punch-through
