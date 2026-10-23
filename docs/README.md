# The Prey — Game Documentation

> A real-world GPS-based hide-and-seek game for Android.

## Overview

**The Prey** is a location-based outdoor game where a group of players is split into two roles: **Preys** and **Hunters**. Preys get a head start to hide within a defined playfield. After the head start, their GPS locations are periodically pushed to the server so Hunters can track them down. A prey is eliminated when physically tagged by a hunter.

## Documentation Index

| Document | Description |
|---|---|
| [Game Design](./game-design/game-mechanics.md) | Full game rules, flow, and mechanics |
| [Player Roles](./game-design/player-roles.md) | Detailed role descriptions for Hunters and Preys |
| [Playfield](./game-design/playfield.md) | How the playfield is created, saved, and managed |
| [Architecture Overview](./architecture/overview.md) | High-level system architecture |
| [Server](./architecture/server.md) | ASP.NET backend, game session management, push notifications |
| [API Reference](./api/endpoints.md) | REST API endpoint definitions |
| [Signaling & Real-time](./api/realtime.md) | Real-time communication (SignalR / push notifications) |

## Tech Stack

| Layer | Technology |
|---|---|
| Client | Native Android app (Kotlin) — `app/src` |
| Backend | ASP.NET Core (C#) — `server/src` |
| Real-time | Azure Web PubSub / Push Notifications (FCM) |
| Website | Hugo — `website/src` |

## Repository Layout

| Folder | Contents |
|---|---|
| `app/src` | Native Android client app (Kotlin) |
| `server/src` | Backend: modular-monolith ASP.NET Core API, orchestrated with .NET Aspire |
| `server/infrastructure` | Bicep for the Azure landing zone and the backend APIs |
| `website/src` | Promotional website (Hugo, theprey.nl) |
| `website/infrastructure` | Bicep for the website's Static Web App |
