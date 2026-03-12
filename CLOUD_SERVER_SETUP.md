# Dedicated Server Setup - Photon Cloud

## Quick Overview
This guide sets up a dedicated Windows server that:
- Runs on your desktop PC
- Connects to Photon Cloud (free tier)
- Acts as Master Client (controls all bots)
- Doesn't spawn a player character
- Uses minimal resources (headless mode, 30 FPS)

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
- **Is Server:** UNCHECKED (we'll use command line instead)
- **Auto Detect Server Mode:** CHECKED ✅
- **Headless Mode:** CHECKED ✅
- **Server Target FPS:** 30

**Local Photon Server:**
- **Use Local Photon Server:** UNCHECKED ❌ (we're using Photon Cloud)

**Connection:**
- **Server Room Name:** KlyraServer (or whatever you want)
- **Max Players:** 24

### Verify Other Components:
- Make sure **BotSpawnManager** exists in your scene (it should)
- Make sure **SimplePlayerSpawner** exists in your scene (it should)

### Save Scene
- Ctrl+S to save

---

## Step 2: Build the Dedicated Server (Do Once)

1. **File → Build Settings**

2. **Platform:**
   - Select **"PC, Mac & Linux Standalone"**
   - Target Platform: **Windows**

3. **Player Settings (click button):**
   - Go to **"Resolution and Presentation"**
   - ✅ **Run In Background:** ENABLED (CRITICAL!)
   - **Fullscreen Mode:** Windowed
   - **Default Screen Width:** 1280
   - **Default Screen Height:** 720

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
echo ================================================
echo       KLYRA DEDICATED SERVER (PHOTON CLOUD)
echo ================================================
echo.
echo Server will connect to Photon Cloud
echo Server will spawn and control all bots
echo Server will NOT spawn a player character
echo.
echo Press Ctrl+C to stop the server
echo ================================================
echo.

KlyraServer.exe -server

echo.
echo Server stopped.
pause
```

4. **Save and close**

---

## Step 4: Running the Server

### Every Time You Want to Host:

**1. Start Server:**
   - Double-click `START_SERVER.bat` in `C:\KlyraServer\`
   - You'll see a window with the game running
   - Top of screen: **"=== DEDICATED SERVER (CONNECTED) ==="**
   - Wait ~5-10 seconds for:
     - Connection to Photon Cloud
     - Room creation
     - Bots to spawn

**2. Build & Run WebGL Client:**
   - In Unity: File → Build Settings → WebGL
   - Build normally (don't change any server settings)
   - Host on web server or upload
   - Players connect and play!

---

## What You'll See

### On Server Window:
```
=== DEDICATED SERVER (CONNECTED) ===
Room: KlyraServer | Players: 3/24
FPS: 30 | Master Client: True
```

### In Console (if visible):
```
Server mode detected from command line arguments
=== DEDICATED SERVER MODE ENABLED ===
Headless mode: Rendering disabled
Server connecting to Photon Cloud...
Server connected to Photon Master Server
Server joined room: KlyraServer
Server is Master Client: True
Spawned bot. 23 remaining. Next spawn in 1s
[SERVER] Player John joined. Total players: 2
```

### What Server Does:
- ✅ Runs with minimal graphics (30 FPS, headless rendering)
- ✅ Connects to Photon Cloud (free tier)
- ✅ Creates room "KlyraServer"
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
- Wait 5-10 seconds after joining room

### "Can't connect to Photon"
- Check your internet connection
- Verify Photon App ID is set in Unity (PhotonServerSettings)
- Make sure you're not over Photon's free tier limits (20 CCU)

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
- Check "Server Room Name" in DedicatedServerManager
- Make sure server shows "CONNECTED" status

**On Client:**
- Make sure they're connecting to Photon Cloud (same app ID)
- Room names must match exactly (case-sensitive)
- Try using JoinOrCreate instead of Join

### "Master Client switches away from server"
- Enable "Run In Background" in Build Settings → Player Settings
- Don't minimize the server window
- Keep the server application focused or use true headless mode

---

## Advanced: True Headless Mode

For maximum performance, run server without ANY graphics:

**Create `START_SERVER_HEADLESS.bat`:**
```batch
@echo off
echo Starting Klyra Dedicated Server (Headless)...
KlyraServer.exe -server -batchmode -nographics -logFile server.log
```

**Note:**
- You won't see a window
- Check `server.log` to see what's happening
- Use Task Manager to close server

---

## Quick Reference

**To Start Server:**
1. Double-click `START_SERVER.bat`
2. Wait for "CONNECTED" status
3. Wait for bots to spawn

**To Connect Clients:**
1. Build WebGL normally
2. Connect to Photon Cloud
3. Join room "KlyraServer"
4. Play!

**To Update Server:**
1. Make changes in Unity
2. Build again to `C:\KlyraServer\`
3. Stop server (Ctrl+C)
4. Restart `START_SERVER.bat`

---

## Benefits

✅ **Better Performance:** Native build runs faster than WebGL hosting bots
✅ **Consistent Bot AI:** Bots always controlled by powerful desktop PC
✅ **Reliability:** Server stays up as long as you run it
✅ **Free:** Uses Photon Cloud free tier (no $115/month)
✅ **Easy:** Run on your own PC, no cloud deployment needed

---

## Photon Cloud Free Tier Limits

- **20 CCU** (Concurrent Users)
- **Unlimited Rooms**
- **Unlimited Messages**

Your setup counts the **dedicated server as 1 CCU**, so you can have **19 players** connected at once.

---

## That's It!

The server will:
- Use minimal resources on your desktop
- Control all bots smoothly
- Stay up as long as you leave it running
- Connect to Photon Cloud (free!)

Clients just connect to Photon Cloud and join the room - they'll see a smooth game with bots already playing!
