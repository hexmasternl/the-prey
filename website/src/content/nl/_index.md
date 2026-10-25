---
title: "The Prey"
description: "The Prey is een GPS-verstoppertje in de echte wereld, voor Android. Prooien verspreiden zich en overleven, terwijl één Jager ze opspoort via GPS. Ren. Verstop je. Overleef."
keywords: ["The Prey", "GPS-spel", "verstoppertje", "locatiespel", "multiplayer buitenspel", "tikkertje", "GPS verstoppertje", "Android spel"]

# ---- Home-pagina tekst (gerenderd door layouts/index.html) ----
hero:
  tag: "// Signaal Ontvangen"
  sub: "Teken het speelveld. Deel de uitnodiging. Verspreid je en verstop je, of spoor ze op voordat de tijd om is."
  ctaRules: "Lees de Regels"
  ctaHowto: "Handleidingen"

game:
  eyebrow: "Het Spel"
  title: "Jaag of Word Gejaagd"
  body:
    - "The Prey zet 2 tot 16 spelers in een speelveld in de echte wereld. Iedereen behalve één speler is Prooi: zij verstoppen zich en blijven bewegen. Eén speler, de Jager, volgt ze op de kaart en tagt ze zodra ze binnen 50 meter zijn. De posities van de Prooi bereiken de Jager met een vaste ping. Elke beweging telt."
    - "Een spel duurt 30, 60 of 90 minuten, standaard 30. De Prooi krijgt een voorsprong terwijl de Jager op zijn plek moet blijven. In de eindfase komen de pings sneller, dus verstoppen wordt lastiger."

phases:
  - value: "5"
    unit: "min"
    accent: "signal"
    title: "Voorsprong"
    desc: "Prooi verspreidt zich. De Jager blijft staan en kan niet taggen."
  - value: "~15"
    unit: "min"
    accent: "signal"
    title: "Actieve Jacht"
    desc: "Posities van de Prooi gaan elke 3 minuten naar de Jager."
  - value: "10"
    unit: "min"
    accent: "caution"
    title: "Eindfase"
    desc: "Elke minuut een ping. Sluit in of houd vol."

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
      tagline: "Kies. Nodig uit. Start."
---
