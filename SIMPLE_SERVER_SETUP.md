# Simple Dedicated Server Setup - Local Photon Server

## What You Need
- Your game PC (where you'll run the server)
- Photon Server installed on same PC
- 5-10 minutes

---

## Step 1: Setup in Unity (Do Once)

### Add Server Manager to Scene:
1. Open your main game scene in Unity
2. Right-click in Hierarchy → Create Empty
3. Name it **"DedicatedServerManager"**
4. Click on it → Add Component → **DedicatedServerManager**

### Configure the Server Manager:
In the Inspector, set these values:

**Server Settings:**
- ✅ **Is Server:** UNCHECKED (we'll use command line instead)
- ✅ **Auto Detect Server Mode:** CHECKED
- ✅ **Headless Mode:** CHECKED (no rendering needed)
- **Server Target FPS:** 30

**Local Photon Server:**
- ✅ **Use Local Photon Server:** CHECKED
- **Server Address:** 127.0.0.1
- **Server Port:** 5055 (or whatever your Photon Server uses)

**Connection:**
- **Server Room Name:** KlyraServer
- **Max Players:** 24

### Save Scene
- Ctrl+S to save

---

## Step 2: Build the Server (Do Once)

1. **File → Build Settings**

2. **Platform:**
   - Select **"PC, Mac & Linux Standalone"**
   - Target Platform: **Windows**

3. **Player Settings (click button):**
   - Go to **"Resolution and Presentation"**
   - ✅ **Run In Background:** ENABLED (VERY IMPORTANT!)
   - **Fullscreen Mode:** Windowed
   - **Default Screen Width:** 800
   - **Default Screen Height:** 600

4. **Build:**
   - Click **"Build"**
   - Create folder: `C:\KlyraServer\`
   - Save as: `KlyraServer.exe`
   - Wait for build to complete

---

## Step 3: Create Server Launcher (Do Once)

1. **Create a text file** in `C:\KlyraServer\`
2. **Name it:** `START_SERVER.bat`
3. **Edit it and paste this:**

```batch
@echo off
echo Starting Klyra Dedicated Server...
echo.
echo Server will run in this window.
echo Press Ctrl+C to stop the server.
echo.

KlyraServer.exe -server

echo.
echo Server stopped.
pause
```

4. **Save and close**

---

## Step 4: Running Everything

### Every Time You Want to Play:

**1. Start Photon Server:**
   - Run your Photon Server software
   - Make sure it's running on port 5055

**2. Start Game Server:**
   - Double-click `START_SERVER.bat` in `C:\KlyraServer\`
   - You'll see a window with the game running
   - Green text at top says "=== DEDICATED SERVER (CONNECTED) ==="
   - Wait ~5 seconds for bots to spawn

**3. Build & Run WebGL Client:**
   - In Unity: File → Build Settings → WebGL
   - Build normally (don't change any server settings)
   - Host on local web server or upload
   - Players connect and play!

---

## What You'll See

### On Server Window:
```
=== DEDICATED SERVER (CONNECTED) ===
Room: KlyraServer | Players: 1/24
FPS: 30 | Master Client: True
```

### In Console (if visible):
```
Server mode detected from command line arguments
=== DEDICATED SERVER MODE ENABLED ===
Headless mode: Rendering disabled
Server connecting to LOCAL Photon Server at 127.0.0.1:5055...
Server connected to Photon Master Server
Server joined room: KlyraServer
Server is Master Client: True
Spawned bot. 23 remaining. Next spawn in 1s
```

### What Server Does:
- ✅ Runs with minimal graphics (just the window, no heavy rendering)
- ✅ Spawns and controls all 24 bots
- ✅ Manages flag captures and tickets
- ✅ Acts as Master Client (permanent host)
- ✅ Does NOT spawn a player character for itself

### What Clients See:
- Bots already in the game when they join
- Smooth bot AI and combat
- Normal gameplay

---

## Stopping the Server

1. Focus the server window
2. Press **Ctrl+C** or just close the window
3. Bots will disappear from clients' games

---

## Troubleshooting

### "Server not spawning bots"
- Make sure you see "Server is Master Client: True" in console
- Check BotSpawnManager is in your scene

### "Can't connect to Photon Server"
- Verify Photon Server is running
- Check port is 5055 (or match your Photon Server config)
- Make sure "Use Local Photon Server" is checked

### "Server spawned a player character"
- This shouldn't happen if setup correctly
- Verify you ran with `-server` argument
- Check console for "Server mode detected"

### "Performance still bad"
- Server should use very little GPU (headless mode)
- If CPU is high, lower "Server Target FPS" to 20
- Close unnecessary programs on server PC

### "Clients can't find room"
**On Server:**
- Check "Server Room Name" is "KlyraServer"

**On Client:**
- Make sure they're connecting to same Photon Server
- Room names must match exactly

---

## Quick Reference

**Server Computer (Your PC):**
1. Run Photon Server
2. Run `START_SERVER.bat`
3. Leave it running

**Players (WebGL):**
1. Connect to game
2. Join room "KlyraServer"
3. Play!

**To Update:**
1. Make changes in Unity
2. Build again to `C:\KlyraServer\`
3. Stop server (Ctrl+C)
4. Restart `START_SERVER.bat`

---

## That's It!

The server will:
- Use minimal resources (30 FPS, headless rendering)
- Control all bots smoothly
- Stay up as long as you leave it running
- Connect to your local Photon Server

Clients just connect and play like normal, but now with better performance and reliable bot AI!
