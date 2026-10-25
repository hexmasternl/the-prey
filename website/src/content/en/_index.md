---
title: "The Prey"
description: "The Prey is a GPS-powered, real-world hide-and-seek game for Android. Prey scatter and survive while one Hunter tracks them down by GPS. Run. Hide. Survive."
keywords: ["The Prey", "GPS game", "hide and seek", "location-based game", "outdoor multiplayer game", "real-world tag game", "GPS hide and seek", "Android game"]

# ---- Home page copy (rendered by layouts/index.html) ----
hero:
  tag: "// Signal Acquired"
  sub: "Draw the playfield. Share the invite. Scatter and hide, or hunt them down before time runs out."
  ctaRules: "Read the Rules"
  ctaHowto: "How-To Guides"

game:
  eyebrow: "The Game"
  title: "Hunt or be Hunted"
  body:
    - "The Prey drops 2 to 16 players into a real-world playfield. Everyone but one is Prey: they hide and keep moving. One player, the Hunter, tracks them on the map and tags them once they are within 50 meters. Prey positions reach the Hunter on a fixed ping. Every move matters."
    - "A game lasts 30, 60 or 90 minutes, 30 by default. Prey get a head start while the Hunter has to stay put. In the final stage the pings come faster, so hiding gets harder."

phases:
  - value: "5"
    unit: "min"
    accent: "signal"
    title: "Head Start"
    desc: "Prey scatter. The Hunter stays put and cannot tag."
  - value: "~15"
    unit: "min"
    accent: "signal"
    title: "Active Hunt"
    desc: "Prey positions ping to the Hunter every 3 minutes."
  - value: "10"
    unit: "min"
    accent: "caution"
    title: "Final Stage"
    desc: "Pings every minute. Close in or hold on."

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
      tagline: "Pick. Invite. Launch."
---
