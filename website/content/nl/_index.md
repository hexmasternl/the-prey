---
title: "The Prey"
description: "The Prey is een GPS-verstoppertje in de echte wereld. Prooien verspreiden zich en overleven, terwijl één Jager ze opspoort via GPS. Ren. Verstop je. Overleef."
keywords: ["The Prey", "GPS-spel", "verstoppertje", "locatiespel", "multiplayer buitenspel", "tikkertje", "GPS verstoppertje", "mobiel spel"]

# ---- Home-pagina tekst (gerenderd door layouts/index.html) ----
hero:
  tag: "// Signaal Ontvangen"
  sub: "GPS-verstoppertje. Bepaal de arena. Verspreid je en verstop je. Spoor ze op voordat de tijd om is."
  ctaRules: "Lees de Regels"
  ctaHowto: "Handleidingen"

game:
  eyebrow: "Het Spel"
  title: "Jaag of Word Gejaagd"
  body:
    - "The Prey zet spelers in een echte GPS-arena. De meeste spelers verstoppen zich en rennen. Eén speler — de Jager — traceert en tagt. De kaart ververst op een vaste timer. Elke beweging telt."
    - "Spellen duren zo lang als de host instelt — standaard 30 minuten. Prooi krijgt voorsprong om zich te verspreiden voordat de Jager wordt losgelaten. Daarna — de jacht is begonnen."

phases:
  - value: "5"
    unit: "min"
    accent: "signal"
    title: "Voorsprong"
    desc: "Prooi verspreidt zich. De Jager wordt op zijn plek gehouden."
  - value: "~20"
    unit: "min"
    accent: "signal"
    title: "Actieve Jacht"
    desc: "GPS-pings live. Kaart ververst op de timer van de host."
  - value: "5"
    unit: "min"
    accent: "caution"
    title: "Eindfase"
    desc: "Updates versnellen. Sluit in of houd vol."

roles:
  eyebrow: "Rollen"
  title: "Elke Speler Heeft een Taak"
  items:
    - glyph: "◎"
      variant: "prey"
      url: "/roles/prey/"
      name: "Prooi"
      tagline: "Verstop. Beweeg. Overleef."
    - glyph: "⬤"
      variant: "hunter"
      url: "/roles/hunter/"
      name: "Jager"
      tagline: "Traceer. Sluit in. Tag."
    - glyph: "◈"
      variant: "hunter"
      url: "/roles/game-creator/"
      name: "Spelmaker"
      tagline: "Bepaal. Wijs toe. Lanceer."
---
