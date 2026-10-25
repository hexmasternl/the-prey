---
title: "Regels"
description: "De volledige spelregels van The Prey: spelinstellingen en standaardwaarden, voorsprong, locatie-pings, taggen binnen 50 meter, straffen, stil vallen, en hoe de Jager en de Prooi winnen."
keywords: ["The Prey regels", "spelregels", "voorsprong", "locatie-ping", "taggen", "straf", "winvoorwaarden", "verstoppertje regels"]
---

## Spelconcept

The Prey is een GPS-verstoppertje dat buiten wordt gespeeld, binnen een gebied dat een **speelveld** heet. Een spel heeft 2 tot 16 spelers. Precies één speler is de **Jager**, die volgt en tagt. Alle anderen zijn **Prooi**: zij verstoppen zich en blijven bewegen.

Het spel heeft een tijdslimiet. De Jager wint door alle Prooi te vangen voordat de tijd om is. De Prooi wint als er nog minstens één prooi in het spel is wanneer de klok nul bereikt.

---

## Spelinstellingen

De Spelmaker kiest deze instellingen bij het aanmaken van het spel en kan ze in de lobby nog aanpassen. De standaardwaarden staan vetgedrukt.

| Instelling | Opties | Wat het doet |
|---|---|---|
| Speelduur | **30**, 60, 90 min | Hoe lang de jacht duurt. |
| Vertraging jager | 3, **5**, 10 min | De voorsprong: de tijd die de Prooi krijgt voordat de Jager mag bewegen. |
| Eindfase | 3, 5, **10** min | De laatste minuten van het spel, met snellere pings. |
| Locatie-ping | 2, **3**, 5 min | Hoe vaak de posities van de Prooi met de Jager worden gedeeld. |
| Ping in eindfase | **1**, 2, 3 min | De ping tijdens de eindfase. Nooit langer dan de locatie-ping. |

Met de standaardwaarden doorloopt een spel drie fasen:

| Fase | Standaardduur | Wat er gebeurt |
|---|---|---|
| Voorsprong | 5 minuten | De Prooi verspreidt zich en verstopt zich. De Jager blijft op zijn plek en kan niet taggen. |
| Actieve Jacht | ~15 minuten | De Jager wordt losgelaten. Posities van de Prooi gaan elke 3 minuten naar de Jager. |
| Eindfase | Laatste 10 minuten | Elke minuut een ping om het net te sluiten. |
| Totaal | 30 minuten | Het spel eindigt en iedereen ziet de uitslag. |

---

## Locatie-pings

Tijdens een spel stuurt je telefoon elke paar seconden je positie naar de server, ook als de app dicht is of het scherm uit staat. Dat betekent niet dat iedereen je live ziet:

- De **Jager** ziet elke prooi op de positie van zijn laatste ping. Pings volgen de instelling locatie-ping, en in de eindfase de snellere ping in eindfase. Ze beginnen zodra het spel start, dus de Jager ziet je al tijdens de voorsprong.
- De **Prooi** ziet de Jager en de andere prooien op de kaart, en hoe dichtbij de dichtstbijzijnde Jager is.

Zie elke markering als een laatst bekende locatie. De speler is waarschijnlijk al verder.

---

## Taggen

De Jager kan taggen zodra de voorsprong voorbij is. Een tik op **Taggen** opent een lijst met de prooien binnen **50 meter** van de Jager. De server controleert de afstand, dus niemand die verder weg is staat in de lijst. De Jager kiest één prooi en bevestigt. Is die prooi net buiten bereik geglipt, dan weigert de server de tag en ververst de lijst.

Een getagde prooi doet niet meer mee aan de jacht en kijkt de rest van het spel mee als toeschouwer.

---

## Straffen

| Straf | Wanneer | Duur |
|---|---|---|
| Jager bewoog tijdens de voorsprong | De Jager beweegt meer dan 25 meter van zijn startpositie voordat de voorsprong voorbij is. | 10 minuten |
| Speelveld verlaten | Een speler is buiten het speelveld, in een spel waarin straffen voor de grens aan staan. | 5 minuten |

Zolang een straf loopt, wordt de positie van de speler elke 10 seconden gedeeld in plaats van met de normale ping, dus een prooi met straf kan zich nergens verstoppen. Beide HUD's laten het zien: bij de Prooi knippert het rollabel **Straf**, en de Jager ziet hoeveel prooien straf hebben.

---

## Stil Vallen

Het spel moet van je horen. Bereikt er 5 minuten lang geen locatie van je telefoon de server, dan word je passief. Na 7 minuten lig je **eruit**. Voor een prooi telt eruit liggen hetzelfde als gevangen worden. Laat locatie aan staan op "Altijd toestaan" en laat de melding van de spelvolging met rust.

---

## Winvoorwaarden

**De Jager wint** als alle Prooi gevangen is (getagd of eruit) voordat de speltimer afloopt.

**De Prooi wint** als er nog minstens één prooi in het spel is wanneer de timer nul bereikt.

Niemand kan een spel vroegtijdig beëindigen. Als het afloopt, ziet elke speler de uitslag voor zijn eigen rol: gewonnen of verloren.
