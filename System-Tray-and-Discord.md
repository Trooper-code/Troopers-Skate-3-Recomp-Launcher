With **Use system tray** on:
- Minimizing hides the window to the tray.
- PLAY tucks the launcher into the tray while you play.
- Double-click the icon to bring the window back.
- Right-click it for: **Open launcher**, **Play**, **Play with profile ▸** (every saved profile), and **Exit launcher**.

## Discord Rich Presence

While the game runs, presence shows **"Skating in Skate 3"** on your Discord profile with a timer. The second line shows the profile you launched with, or your render resolution and renderer (for example "Recomp · 1440p · enhanced renderer").

It talks to the Discord desktop app over Discord's local connection; no Discord library or account login is involved. Discord needs an **Application ID**, which you set up once:

1. Go to **discord.com/developers/applications**, click **New Application** and name it **Skate 3**. That name is what friends see ("Playing Skate 3").
2. Copy the **Application ID** into the launcher's **Application ID** box.
3. *(Optional)* Under **Rich Presence → Art Assets**, upload a Skate 3 image with the key **skate3**, or type your own key in **Image asset key**.
4. Turn on **Show Skate 3 on my Discord profile** and press **Test connection**. It confirms the connection (showing your Discord name) and shows a test presence for 15 seconds.

Presence also works from quick-play shortcuts, which wait in the background until the game closes.
