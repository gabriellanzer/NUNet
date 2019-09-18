# NUNet
NUNet stands for (Not Unity's Network) and it is a simple to use multi-threaded C# network abstraction layer. It abstracts TCP and UDP connections per client by introducing an easy to use API. Network packets are managed through queues and are made available through callbacks in both NUClient and NUServer classes.

## Features
- Easy to use callback system for fast integration
- Automatic thread-safe syncronization of data queue
- Packet splitting based on Ethernet MTU for TCP connections
- Internal protocol and GUID management of clients
- Reconnection and time-out events
- Still receives packets while breakpoints are triggered in game thread
- UDP and TCP connections for reliable and unreliable transmission
- Sequential packets support through TCP connection
- Works with android builds
- Auxiliar functions with NUNet.NUUtilities class
- Abstract Packet class with HASH check
- Automatic callback validation to avoid callbacks removal on object destruction

## Usage
Check ExampleApplication folder for simple usage examples with multiple scenes.

Server:
- Start server with NUServer.Start(), starts with default ip 127.0.0.1 and port 25565
- Listen for connections by subscribing to NUServer.onClientConnected action
- Process received packets with NUServer.onClientPacketReceived action
- Send message with NUServer.SendReliable() and NUServer.SendUnreliable()
- Close server with NUServer.Shutdown()

Client:
- Start client with NUClient.Connect(), connects to default ip 127.0.0.1 and port 25565
- Connection callback available with NUClient.onConnected
- Process received packets with NUClient.onPacketReceived action
- Send message with NUClient.SendReliable() and NUClient.SendUnreliable()
- Close connection with NUClient.Disconnect()

## WIP
- UDP Broadcast
- Web API
- Custom game-loop hook points
- Max queued packets

## Planed
- Meet-up Master Server and Routines (using NodeJS)
- NAT Punch-through

## Knwown Limitations
- Won't work on big-endian systems
