# Dedicated Server Setup Guide

## Overview
This guide explains how to build and run a dedicated server for your game. The dedicated server will:
- Act as the Master Client
- Spawn and control all AI bots
- Manage game state (flag captures, tickets, etc.)
- Run with better performance than WebGL

## Step 1: Setup the Server in Your Scene

1. **Create a Server Manager:**
   - In your main scene, create an empty GameObject
   - Name it "DedicatedServerManager"
   - Add the `DedicatedServerManager` component

2. **Configure Server Settings:**
   - **Is Server:** Check this box if you want to test server mode in editor
   - **Auto Detect Server Mode:** Keep enabled - detects `-server` command line argument
   - **Headless Mode:** Enable for production (disables rendering for performance)
   - **Server Target FPS:** Set to 30-60 (servers don't need high FPS)
   - **Server Room Name:** "KlyraServer" (or whatever you want)
   - **Max Players:** 24 (or your desired player limit)

3. **Ensure BotSpawnManager exists:**
   - The BotSpawnManager should already be in your scene
   - It will automatically only spawn bots when running as the dedicated server

## Step 2: Build the Dedicated Server

### Windows Build:

1. **Open Build Settings:**
   - File > Build Settings
   - Select **"PC, Mac & Linux Standalone"**
   - Target Platform: **Windows**

2. **Configure Player Settings:**
   - Click "Player Settings"
   - **Resolution and Presentation:**
     - Run In Background: ✓ **ENABLED** (critical for servers!)
     - Display Resolution Dialog: **Disabled**
     - Fullscreen Mode: **Windowed** (or Maximized Window for easy access)
     - Default Screen Width: 1280
     - Default Screen Height: 720

3. **Server-Specific Settings:**
   - **Other Settings:**
     - Scripting Backend: **IL2CPP** (better performance) or Mono
     - API Compatibility Level: **.NET 4.x**

4. **Build:**
   - Click "Build"
   - Choose a folder like `Builds/DedicatedServer/`
   - Name it `KlyraServer.exe`

### Linux Build (Optional - for cloud hosting):

1. Same as above but:
   - Target Platform: **Linux**
   - Architecture: **x86_64**
   - Build as `KlyraServer.x86_64`

## Step 3: Running the Dedicated Server

### Option A: Command Line (Recommended)

Open Command Prompt or PowerShell and run:

```bash
cd "path/to/your/build/folder"
KlyraServer.exe -server
```

**Command Line Arguments:**
- `-server` or `-dedicated` - Enables server mode
- `-batchmode` - Runs without graphics (true headless)
- `-nographics` - Disables graphics rendering
- `-logFile server.log` - Saves logs to file

**Full Headless Example:**
```bash
KlyraServer.exe -server -batchmode -nographics -logFile server.log
```

### Option B: Double-Click (Testing)

1. **Create a Shortcut:**
   - Right-click `KlyraServer.exe`
   - Create Shortcut
   - Right-click shortcut > Properties
   - In "Target" field, add ` -server` at the end:
     ```
     "C:\Path\To\KlyraServer.exe" -server
     ```
   - Click OK

2. **Double-click the shortcut** to run the server

### Option C: Unity Editor (Development)

1. In Unity Editor, select the DedicatedServerManager GameObject
2. Check "Is Server" in the inspector
3. Press Play
4. The editor will run as a server

## Step 4: Connecting WebGL Clients

1. **Build your WebGL client:**
   - File > Build Settings
   - Switch to **WebGL**
   - Build as normal
   - **Do NOT check "Is Server"** on DedicatedServerManager (or remove it from WebGL build)

2. **Clients will automatically:**
   - Connect to Photon
   - Join the server's room ("KlyraServer")
   - Play as normal players
   - Bots will be controlled by the dedicated server

## Step 5: Verify It's Working

**On Server:**
- You should see: "=== DEDICATED SERVER (CONNECTED) ===" in top-left
- Console shows: "Server mode detected from command line arguments"
- Bots spawn after a few seconds
- Players appear when they join

**On Client:**
- Join the same room name ("KlyraServer")
- You should see bots already in the game
- Bots respond and fight
- No performance issues

## Troubleshooting

### Server not spawning bots:
- Check Console for "Server is Master Client: True"
- Verify BotSpawnManager exists in scene
- Check that bots are not being filtered out

### Clients can't connect:
- Make sure server room name matches
- Check Photon settings are identical
- Verify both builds use same Photon App ID

### Performance issues:
- Enable headless mode
- Lower Server Target FPS to 30
- Use `-batchmode -nographics` for true headless

### Master Client switches away from server:
- Enable "Run In Background" in Player Settings
- Don't minimize the server window
- Use headless mode for production

## Production Deployment

### Running on a VPS/Cloud Server:

1. **Upload build to server:**
   - Copy entire build folder to Linux server
   - Install required dependencies: `sudo apt-get install libglu1`

2. **Run as background process:**
   ```bash
   nohup ./KlyraServer.x86_64 -server -batchmode -nographics -logFile server.log &
   ```

3. **Monitor logs:**
   ```bash
   tail -f server.log
   ```

4. **Auto-restart on crash (systemd):**
   Create `/etc/systemd/system/klyra-server.service`:
   ```ini
   [Unit]
   Description=Klyra Game Server
   After=network.target

   [Service]
   Type=simple
   User=gameserver
   WorkingDirectory=/home/gameserver/KlyraServer
   ExecStart=/home/gameserver/KlyraServer/KlyraServer.x86_64 -server -batchmode -nographics -logFile /var/log/klyra/server.log
   Restart=always
   RestartSec=10

   [Install]
   WantedBy=multi-user.target
   ```

   Enable: `sudo systemctl enable klyra-server`
   Start: `sudo systemctl start klyra-server`

## Benefits of Dedicated Server

✅ **Better Performance:** Native code runs faster than WebGL
✅ **Consistent Bot AI:** Bots always available, controlled by powerful server
✅ **Reliability:** Server stays up 24/7, players can join anytime
✅ **Scalability:** Run multiple servers for different regions
✅ **Control:** Full control over game state and rules

## Next Steps

- Test locally first with `-server` argument
- Build for your target platform
- Test with WebGL clients connecting
- Deploy to cloud if needed (AWS, DigitalOcean, etc.)
