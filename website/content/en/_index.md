---
title: "The Prey"
description: "The Prey is a GPS-powered, real-world hide-and-seek game. Prey scatter and survive while Hunters track them down by GPS. Run. Hide. Survive."
keywords: ["The Prey", "GPS game", "hide and seek", "location-based game", "outdoor multiplayer game", "real-world tag game", "GPS hide and seek", "mobile game"]

# ---- Home page copy (rendered by layouts/index.html) ----
hero:
  tag: "// Signal Acquired"
  sub: "GPS-based hide-and-seek. Define the arena. Scatter and hide. Hunt them down before time runs out."
  ctaRules: "Read the Rules"
  ctaHowto: "How-To Guides"

game:
  eyebrow: "The Game"
  title: "Hunt or be Hunted"
  body:
    - "The Prey drops players into a real-world GPS arena. One team hides and runs. The other tracks and tags. Every 30 seconds the map updates. Every move matters."
    - "Games last 60 minutes. Prey have a 10-minute head start to scatter before Hunters see their first ping. After that — you are on the radar."

phases:
  - value: "10"
    unit: "min"
    accent: "signal"
    title: "Head Start"
    desc: "Prey scatter. No location data. Hunters wait."
  - value: "~45"
    unit: "min"
    accent: "signal"
    title: "Active Hunt"
    desc: "GPS pings live. Map updates every 30 seconds."
  - value: "5"
    unit: "min"
    accent: "caution"
    title: "Final Stretch"
    desc: "Warning issued. Close in or hold on."

roles:
  eyebrow: "Roles"
  title: "Every Player Has a Job"
  items:
    - glyph: "◎"
      variant: "prey"
      url: "/roles/prey/"
      name: "Prey"
      tagline: "Hide. Move. Survive."
    - glyph: "⬤"
      variant: "hunter"
      url: "/roles/hunter/"
      name: "Hunter"
      tagline: "Track. Close in. Tag."
    - glyph: "◈"
      variant: "hunter"
      url: "/roles/game-creator/"
      name: "Game Creator"
      tagline: "Define. Assign. Launch."
---
