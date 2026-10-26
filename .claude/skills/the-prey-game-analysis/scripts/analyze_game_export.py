#!/usr/bin/env python3
"""Summarise The Prey game exports (GET /games/export/today -> list of GameExportDto).

Standard library only. Usage:

    python -I analyze_game_export.py <export.json> [--game CODE_OR_ID] [--playfield polygon.json]
                                     [--show-names] [--gap-seconds N]

The export may be a single game object or a list of games. Participants are printed as
"Hunter" / "Prey-1", "Prey-2", ... unless --show-names is given; user ids are never printed.
--playfield takes a PlayFieldDto JSON (with "points") or a bare list of {latitude, longitude}
points and enables the boundary check. Without it the boundary section is skipped (the export
does not contain the polygon).

Rule constants mirror server/src/Games/HexMaster.ThePrey.Games/DomainModels/Game.cs. Update them
here when the domain changes (see the the-prey-game-rules skill).
"""

import argparse
import json
import math
import statistics
import sys
from datetime import datetime, timedelta

# --- constants from Game.cs / GameParticipant.cs ---------------------------------------------
DEVICE_REPORT_SECONDS = 5          # LocationReportingIntervalSeconds
SWEEP_SECONDS = 30                 # SweepIntervalSeconds
TAG_RANGE_M = 50                   # TagRangeMeters
HEADSTART_THRESHOLD_M = 25         # HunterDelayMovementThresholdMeters
HEADSTART_PENALTY_MIN = 10         # HunterDelayPenaltyMinutes
BOUNDARY_PENALTY_MIN = 5           # PenaltyDurationMinutes
PASSIVE_MIN, OUT_MIN = 5, 7        # TryTransitionByTimeout
GPS_JUMP_MPS = 15                  # speeds above this between readings are flagged as GPS jumps


def parse_ts(value):
    if value is None:
        return None
    text = value.replace("Z", "+00:00")
    # Python < 3.11 cannot parse 7-digit fractions; trim to microseconds.
    if "." in text:
        head, rest = text.split(".", 1)
        frac = "".join(ch for ch in rest if ch.isdigit())
        tz = rest[len(frac):]
        text = f"{head}.{frac[:6].ljust(6, '0')}{tz}"
    return datetime.fromisoformat(text)


def haversine_m(a, b):
    lat1, lon1, lat2, lon2 = map(math.radians, (a[0], a[1], b[0], b[1]))
    h = math.sin((lat2 - lat1) / 2) ** 2 + math.cos(lat1) * math.cos(lat2) * math.sin((lon2 - lon1) / 2) ** 2
    return 2 * 6_371_000 * math.asin(math.sqrt(h))


def inside(polygon, point):
    """Ray casting, even-odd rule; longitude = x, latitude = y (same as RayCastingBoundaryChecker)."""
    if len(polygon) < 3:
        return True
    y, x = point
    result = False
    j = len(polygon) - 1
    for i in range(len(polygon)):
        yi, xi = polygon[i]
        yj, xj = polygon[j]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
            result = not result
        j = i
    return result


def coord(obj):
    if not obj:
        return None
    return (obj["latitude"], obj["longitude"])


def rel(ts, start):
    """Format a timestamp relative to game start as +mm:ss (or -mm:ss)."""
    if ts is None or start is None:
        return "n/a"
    seconds = (ts - start).total_seconds()
    sign = "-" if seconds < 0 else "+"
    seconds = abs(seconds)
    return f"{sign}{int(seconds // 60):02d}:{int(seconds % 60):02d}"


def fmt_s(seconds):
    return "n/a" if seconds is None else f"{seconds:.0f}s"


def load_polygon(path):
    with open(path, encoding="utf-8") as handle:
        data = json.load(handle)
    points = data.get("points", data.get("Points")) if isinstance(data, dict) else data
    return [(p.get("latitude", p.get("Latitude")), p.get("longitude", p.get("Longitude"))) for p in points]


def readings_of(participant):
    rows = []
    for loc in participant.get("locations") or []:
        rows.append((parse_ts(loc["recordedAt"]), coord(loc["coordinate"]), loc.get("checked", True)))
    rows.sort(key=lambda r: r[0])
    return rows


def label_participants(game, show_names):
    labels, prey_no = {}, 0
    hunter_id = game.get("hunterUserId")
    for p in game["participants"]:
        is_hunter = p.get("isHunter", p["userId"] == hunter_id)
        if is_hunter:
            base = "Hunter"
        else:
            prey_no += 1
            base = f"Prey-{prey_no}"
        if p["userId"] == game.get("ownerUserId"):
            base += " (creator)"
        labels[p["userId"]] = f"{base} [{p['displayName']}]" if show_names else base
    return labels


def nearest_before(rows, ts):
    """Latest reading at or before ts (what the server would use as 'latest known')."""
    best = None
    for r in rows:
        if r[0] <= ts:
            best = r
        else:
            break
    return best


def analyse(game, args, polygon):
    cfg = game["configuration"]
    started, ends = parse_ts(game.get("startedAt")), parse_ts(game.get("endsAt"))
    created, completed = parse_ts(game.get("createdAt")), parse_ts(game.get("completedAt"))
    labels = label_participants(game, args.show_names)
    hunter_id = game.get("hunterUserId")

    print("=" * 78)
    print(f"Game {game['gameCode']}  status={game['status']}  outcome={game['outcome']}  "
          f"players={len(game['participants'])}")
    print(f"Config: duration={cfg['gameDuration']}m headStart={cfg['hunterDelayTime']}m "
          f"finalStage={cfg['finalStageDuration']}m ping={cfg['defaultLocationInterval']}s "
          f"finalPing={cfg['finalLocationInterval']}s preyBoundary={cfg['enablePreyBoundaryPenalties']} "
          f"hunterBoundary={cfg['enableHunterBoundaryPenalty']}")
    if not started:
        print("Game never started; nothing to analyse.")
        return

    may_move = started + timedelta(minutes=cfg["hunterDelayTime"])
    final_stage = started + timedelta(minutes=cfg["gameDuration"] - cfg["finalStageDuration"])
    print(f"Timeline (relative to start): lobby {rel(created, started)}, hunter may move {rel(may_move, started)}, "
          f"final stage {rel(final_stage, started)}, scheduled end {rel(ends, started)}, completed {rel(completed, started)}")
    if completed and ends:
        delta = (completed - ends).total_seconds()
        if delta >= 0:
            print(f"Ended {delta:.0f}s after scheduled end (expected <= {SWEEP_SECONDS}s sweep latency) -> duration-elapsed")
        else:
            print(f"Ended {-delta / 60:.1f} min early -> all prey caught (tag or Out)")
        if delta > SWEEP_SECONDS + 5:
            print(f"  ! completion lag {delta:.0f}s exceeds one sweep")

    rows = {p["userId"]: readings_of(p) for p in game["participants"]}

    print("\nPer participant")
    print(f"  {'who':<22}{'state':<9}{'n':>5}{'first':>8}{'last':>8}{'median':>8}{'p90':>7}{'maxgap':>8}  notes")
    for p in game["participants"]:
        r = rows[p["userId"]]
        times = [x[0] for x in r]
        gaps = [(b - a).total_seconds() for a, b in zip(times, times[1:])]
        med = statistics.median(gaps) if gaps else None
        p90 = sorted(gaps)[int(0.9 * (len(gaps) - 1))] if gaps else None
        notes = []
        if med is not None:
            if med <= DEVICE_REPORT_SECONDS * 3:
                notes.append("cadence ~5s device posting")
            elif abs(med - cfg["defaultLocationInterval"]) <= 15 or abs(med - cfg["finalLocationInterval"]) <= 15:
                notes.append("cadence follows configured ping (pre-5s protocol)")
            else:
                notes.append("cadence matches neither 5s nor ping")
        long_gaps = [(a, g) for a, g in zip(times, gaps) if g >= args.gap_seconds]
        if long_gaps:
            notes.append(f"{len(long_gaps)} gap(s) >= {args.gap_seconds}s")
        is_prey = p["userId"] != hunter_id  # Passive/Out timeouts never apply to the hunter
        if any(g >= PASSIVE_MIN * 60 for g in gaps):
            notes.append("silence >= 5 min" + (" (Passive)" if is_prey else ""))
        if any(g >= OUT_MIN * 60 for g in gaps):
            notes.append("silence >= 7 min" + (" (Out)" if is_prey else ""))
        unchecked = sum(1 for x in r if not x[2])
        if unchecked:
            notes.append(f"{unchecked} unchecked")
        if times and completed and times[-1] > completed:
            notes.append("readings after completion")
        jumps = 0
        travelled = 0.0
        for a, b in zip(r, r[1:]):
            d = haversine_m(a[1], b[1])
            travelled += d
            dt = (b[0] - a[0]).total_seconds()
            if dt > 0 and d / dt > GPS_JUMP_MPS:
                jumps += 1
        notes.append(f"travelled {travelled / 1000:.2f} km")
        if jumps:
            notes.append(f"{jumps} GPS jump(s) > {GPS_JUMP_MPS} m/s")
        print(f"  {labels[p['userId']]:<22}{p['state']:<9}{len(r):>5}"
              f"{rel(times[0], started) if times else 'n/a':>8}{rel(times[-1], started) if times else 'n/a':>8}"
              f"{fmt_s(med):>8}{fmt_s(p90):>7}{fmt_s(max(gaps) if gaps else None):>8}  {'; '.join(notes)}")
        for start_gap, g in long_gaps[:5]:
            print(f"      gap {g:.0f}s starting {rel(start_gap, started)}")

    # --- hunter head start --------------------------------------------------------------------
    hunter = next((p for p in game["participants"] if p["userId"] == hunter_id), None)
    print("\nHead start")
    if hunter is None:
        print("  no hunter in export")
    else:
        head = [x for x in rows[hunter_id] if x[0] < may_move]
        anchor = coord(hunter.get("delayAnchorLocation")) or (head[0][1] if head else None)
        if not head or anchor is None:
            print("  no hunter readings during the head start (cannot assess)")
        else:
            drift = max(haversine_m(anchor, x[1]) for x in head)
            expected = drift > HEADSTART_THRESHOLD_M
            print(f"  {len(head)} hunter readings before release; max drift from anchor {drift:.0f} m "
                  f"(threshold {HEADSTART_THRESHOLD_M} m) -> penalty expected={expected}, "
                  f"applied={hunter.get('delayPenaltyApplied')}")
            if expected != bool(hunter.get("delayPenaltyApplied")):
                print("  ! expected and applied head-start penalty differ (export may predate the current threshold)")

    # --- penalties ----------------------------------------------------------------------------
    print("\nPenalties")
    any_penalty = False
    for p in game["participants"]:
        for pen in p.get("penalties") or []:
            any_penalty = True
            ends_at = parse_ts(pen["endsAt"])
            if abs((ends_at - (may_move + timedelta(minutes=HEADSTART_PENALTY_MIN))).total_seconds()) < 2:
                kind = "moved-during-delay"
                applied = "n/a"
            else:
                kind = "left-playfield (assumed)"
                applied = rel(ends_at - timedelta(minutes=BOUNDARY_PENALTY_MIN), started)
            print(f"  {labels[p['userId']]:<22}{kind:<26} detected {applied:>7}  ends {rel(ends_at, started)}")
    if not any_penalty:
        print("  none")

    # --- boundary -----------------------------------------------------------------------------
    print("\nBoundary")
    if polygon is None:
        print("  skipped (pass --playfield <PlayFieldDto.json> to check readings against the polygon)")
    else:
        for p in game["participants"]:
            r = [x for x in rows[p["userId"]] if started <= x[0] <= (completed or ends)]
            outside = [x for x in r if not inside(polygon, x[1])]
            role_flag = cfg["enableHunterBoundaryPenalty"] if p["userId"] == hunter_id else cfg["enablePreyBoundaryPenalties"]
            first = rel(outside[0][0], started) if outside else "-"
            print(f"  {labels[p['userId']]:<22}{len(outside):>4}/{len(r):<5} readings outside, first {first:>7}, "
                  f"penalties enabled={role_flag}, penalties recorded={len(p.get('penalties') or [])}")

    # --- hunter-prey distance -----------------------------------------------------------------
    print("\nHunter-prey distance (prey reading vs hunter's latest reading at that moment)")
    if hunter is None:
        print("  no hunter")
    for p in game["participants"]:
        if hunter is None or p["userId"] == hunter_id:
            continue
        samples = []
        for ts, c, _ in rows[p["userId"]]:
            if ts < started:
                continue
            h = nearest_before(rows[hunter_id], ts)
            if h is None:
                continue
            samples.append((ts, haversine_m(h[1], c), (ts - h[0]).total_seconds()))
        if not samples:
            print(f"  {labels[p['userId']]:<22}no comparable readings")
            continue
        closest = min(samples, key=lambda s: s[1])
        in_range = [s for s in samples if s[1] <= TAG_RANGE_M and s[0] >= may_move]
        skew = statistics.median(s[2] for s in samples)
        print(f"  {labels[p['userId']]:<22}closest {closest[1]:.0f} m at {rel(closest[0], started)}; "
              f"{len(in_range)} sample(s) within {TAG_RANGE_M} m after release; median time skew {skew:.0f}s")
        step = max(1, len(samples) // 8)
        trail = ", ".join(f"{rel(s[0], started)} {s[1]:.0f}m" for s in samples[::step])
        print(f"      trail: {trail}")
        if p["state"] == "Tagged":
            last = samples[-1]
            print(f"      tagged: last prey reading {rel(last[0], started)} at {last[1]:.0f} m from hunter "
                  f"({'consistent' if last[1] <= TAG_RANGE_M else 'OUTSIDE tag range at last reading'})")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("export", help="path to an export JSON (one game or a list of games)")
    parser.add_argument("--game", help="only analyse the game with this code or id")
    parser.add_argument("--playfield", help="PlayFieldDto JSON or list of points, enables the boundary check")
    parser.add_argument("--show-names", action="store_true", help="print display names next to role labels")
    parser.add_argument("--gap-seconds", type=int, default=60, help="report reading gaps at least this long (default 60)")
    args = parser.parse_args()

    with open(args.export, encoding="utf-8") as handle:
        data = json.load(handle)
    games = data if isinstance(data, list) else [data]
    if args.game:
        games = [g for g in games if args.game in (g.get("gameCode"), g.get("id"))]
    if not games:
        print("No games in export (or none matched --game).")
        return 1
    polygon = load_polygon(args.playfield) if args.playfield else None

    games.sort(key=lambda g: g.get("startedAt") or "")
    print(f"{len(games)} game(s) in {args.export}")
    for game in games:
        analyse(game, args, polygon)
    return 0


if __name__ == "__main__":
    sys.exit(main())
