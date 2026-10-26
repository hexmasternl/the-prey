---
title: "FAQ"
description: "Frequently asked questions about The Prey: player counts, joining by invitation link, location permissions, the head start, tagging within 50 meters, playfields, and win conditions."
keywords: ["The Prey FAQ", "frequently asked questions", "location permission", "how to play", "player count", "tagging", "playfield", "help"]
---

{{< qa q="What is The Prey?" >}}
A GPS hide-and-seek game played outdoors. Prey hide and keep moving. One Hunter tracks them on the map and tags them up close. The game ends when every Prey is caught or the timer runs out.
{{< /qa >}}

{{< qa q="Which phones does it run on?" >}}
Android only. There is no iOS app and no web version. During closed testing you install it from Google Play after you [become a tester]({{< relref "/become-a-tester" >}}).
{{< /qa >}}

{{< qa q="How many players do I need?" >}}
At least two (one Hunter and one Prey), and up to 16 in a lobby. There is always exactly one Hunter, so a bigger game simply means more Prey. It plays best with 4 to 12.
{{< /qa >}}

{{< qa q="How do I join a game?" >}}
Tap the invitation link the Game Creator shares from the lobby. The app opens, signs you in if needed, and takes you to the lobby. Tap **Set ready** and wait for the start. There is no code to type.
{{< /qa >}}

{{< qa q="Which location permission does the app need?" >}}
Precise location, set to **Allow all the time**. During a game your position is shared even when the screen is off, through a foreground service with a notification. **Play now** stays grey until the permission is right. Tracking stops when the game ends or you leave it.
{{< /qa >}}

{{< qa q="What happens during the head start?" >}}
The head start (5 minutes by default) gives the Prey time to scatter. The Hunter has to stay put and cannot tag. Moving more than 25 meters earns the Hunter a 10-minute penalty. Prey positions already reach the Hunter's map, so keep moving.
{{< /qa >}}

{{< qa q="How does tagging work?" >}}
The Hunter taps **Tag** and gets a list of the Prey within 50 meters. The server checks the distance. The Hunter picks a Prey and confirms. The tag takes effect straight away, and the Prey does not have to confirm it.
{{< /qa >}}

{{< qa q="Does the Hunter see me live?" >}}
No. The Hunter sees each Prey at their last ping: every 3 minutes by default, every minute in the final stage. During a penalty your position is shared every 10 seconds.
{{< /qa >}}

{{< qa q="What is a playfield?" >}}
The area a game is played in, drawn as an outline of at least three points on the map. You draw it in the app under **Playfields** and choose it when you create a game.
{{< /qa >}}

{{< qa q="Can I leave the playfield?" >}}
Stay inside. In a game with boundary penalties on, leaving the playfield gives you a 5-minute penalty, during which your position is shared every 10 seconds.
{{< /qa >}}

{{< qa q="What if my location stops?" >}}
After 5 minutes without a location the server marks you passive, and after 7 minutes you are out. For a Prey that counts as caught. See [GPS Signal Lost]({{< relref "/how-to/gps-signal-lost" >}}).
{{< /qa >}}

{{< qa q="What happens when I get tagged?" >}}
You become a spectator. You keep watching the game on the map until it ends, but you are out of the hunt.
{{< /qa >}}

{{< qa q="Who wins if time runs out?" >}}
The Prey. If at least one Prey is still in play when the timer hits zero, the Prey win, however many were caught.
{{< /qa >}}

{{< qa q="Can the Game Creator play too?" >}}
Yes. In the lobby the Game Creator can make themselves the Hunter or stay as Prey. Leaving the lobby as Game Creator cancels the game for everyone.
{{< /qa >}}

{{< qa q="Why does the app say \"Update required\"?" >}}
Your version of the app is no longer supported. Tap **Update** to open The Prey in Google Play and install the latest version. If you are in a lobby or a running game, you finish it first: the message appears once you leave it.
{{< /qa >}}

{{< qa q="Can my screen stay on during a game?" >}}
Yes. In **Settings**, under **Display**, turn on **Keep screen on during a game**. Your screen then stays on during the head start and the running game, so the map is ready at every glance. It uses more battery and is off by default. The lobby and the other screens keep your phone's normal screen timeout, and the power button still turns the screen off.
{{< /qa >}}
