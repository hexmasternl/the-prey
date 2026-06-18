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
    - "The Prey zet spelers in een echte GPS-arena. De meeste spelers verstoppen zich en rennen. Eén speler — de Jager — traceert en tagt. Elke 30 seconden ververst de kaart. Elke beweging telt."
    - "Spellen duren 60 minuten. Prooi krijgt 10 minuten voorsprong om zich te verspreiden voordat de Jager hun eerste ping ziet. Daarna — sta je op de radar."

phases:
  - value: "10"
    unit: "min"
    accent: "signal"
    title: "Voorsprong"
    desc: "Prooi verspreidt zich. Geen locatiedata. De Jager wacht."
  - value: "~45"
    unit: "min"
    accent: "signal"
    title: "Actieve Jacht"
    desc: "GPS-pings live. Kaart ververst elke 30 seconden."
  - value: "5"
    unit: "min"
    accent: "caution"
    title: "Eindfase"
    desc: "Waarschuwing afgegeven. Sluit in of houd vol."

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
