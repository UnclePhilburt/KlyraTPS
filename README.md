# Klyra TPS

A multiplayer third-person shooter built with Unity and Photon PUN 2.

## Features

- Team-based Conquest game mode
- 24 AI bots with smart combat and objective-based behavior
- Dedicated server support
- WebGL multiplayer client
- Flag capture system with ticket-based scoring

## Tech Stack

- **Unity 2022+**
- **Photon PUN 2** - Networking
- **Opsive Ultimate Character Controller** - Character movement and combat
- **NavMesh AI** - Bot pathfinding

## Setup

See the setup guides:
- `CLOUD_SERVER_SETUP.md` - How to run a dedicated server
- `DEDICATED_SERVER_SETUP.md` - Advanced server configuration

## Project Structure

- `Assets/Scripts/` - Custom game scripts
  - Bot AI (BotAttacker, BotDefender, BotPatroller, BotCombat)
  - Game modes (ConquestGameMode)
  - Server management (DedicatedServerManager)
  - Capture points and objectives
- `Assets/Resources/` - Bot and weapon prefabs
- `Assets/Scenes/` - Game maps

## Development

Built with ❤️ for multiplayer TPS action.
