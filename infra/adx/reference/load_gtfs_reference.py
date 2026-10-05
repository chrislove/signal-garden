#!/usr/bin/env python3
"""
Load Translink's static GTFS stops into ADX, so events can say *where* a vehicle
is relative to its own route: on one of its stops, or near its terminus.

    python3 infra/adx/reference/load_gtfs_reference.py            # download + load
    python3 infra/adx/reference/load_gtfs_reference.py --dry-run  # download + write CSVs only

Produces two small tables from the ~35 MB schedule:
  Stops       every stop: id, name, position                    (~13k rows)
  RouteStops  which stops each route serves, and which of them
              are termini (first or last stop of any trip)     (~45k rows)

Route-level on purpose: every realtime vehicle has a route that matches the
schedule, while ~7% of trips are "UNPLANNED" extras that aren't in it.
The schedule changes most weeks; re-run to refresh (it replaces the tables).

Needs: `az login` with rights to create/replace the two tables.
Data © Translink (Queensland), CC BY 4.0.
"""
import argparse, csv, io, os, sys, urllib.request, zipfile
from collections import defaultdict

sys.path.insert(0, os.path.dirname(__file__))
from load_osm_reference import csv_text, kusto_mgmt  # same ADX plumbing as the OSM loader

GTFS_URL = "https://gtfsrt.api.translink.com.au/GTFS/SEQ_GTFS.zip"

SCHEMA = [
    ".create-merge table Stops (StopId: string, Name: string, Latitude: real, Longitude: real)",
    ".create-merge table RouteStops (RouteId: string, StopId: string, IsTerminus: bool)",
]


def rows(z: zipfile.ZipFile, name: str):
    with z.open(name) as f:
        yield from csv.DictReader(io.TextIOWrapper(f, encoding="utf-8-sig"))


def build(z: zipfile.ZipFile):
    stops = [[s["stop_id"], s["stop_name"], float(s["stop_lat"]), float(s["stop_lon"])]
             for s in rows(z, "stops.txt")]

    route_of_trip = {t["trip_id"]: t["route_id"] for t in rows(z, "trips.txt")}

    # One pass over ~2M stop_times rows: remember each trip's first and last stop,
    # and every (route, stop) pair seen.
    first, last = {}, {}
    served = set()
    for st in rows(z, "stop_times.txt"):
        trip, seq, stop = st["trip_id"], int(st["stop_sequence"]), st["stop_id"]
        route = route_of_trip.get(trip)
        if route is None:
            continue
        served.add((route, stop))
        if trip not in first or seq < first[trip][0]:
            first[trip] = (seq, stop)
        if trip not in last or seq > last[trip][0]:
            last[trip] = (seq, stop)

    termini = defaultdict(set)
    for ends in (first, last):
        for trip, (_, stop) in ends.items():
            termini[route_of_trip[trip]].add(stop)

    route_stops = [[route, stop, str(stop in termini[route]).lower()] for route, stop in sorted(served)]
    return stops, route_stops


def load(table: str, data: list, batch: int = 5000):
    for i in range(0, len(data), batch):
        kusto_mgmt(f".ingest inline into table {table} with (format='csv') <|\n{csv_text(data[i:i + batch])}")
    print(f"  {table}: {len(data)} rows loaded")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true", help="write CSVs to ./gtfs-reference instead of loading")
    ap.add_argument("--zip", help="use an already-downloaded SEQ_GTFS.zip")
    args = ap.parse_args()

    if args.zip:
        data = open(args.zip, "rb").read()
    else:
        print("Downloading static GTFS…")
        data = urllib.request.urlopen(GTFS_URL, timeout=300).read()

    stops, route_stops = build(zipfile.ZipFile(io.BytesIO(data)))
    termini = sum(r[2] == "true" for r in route_stops)
    print(f"  {len(stops)} stops, {len(route_stops)} route-stop pairs ({termini} termini)")

    if args.dry_run:
        os.makedirs("gtfs-reference", exist_ok=True)
        open("gtfs-reference/stops.csv", "w").write(csv_text(stops))
        open("gtfs-reference/route_stops.csv", "w").write(csv_text(route_stops))
        print("Wrote gtfs-reference/*.csv")
        return

    print("Replacing ADX tables…")
    for command in SCHEMA:
        kusto_mgmt(command)
    for table in ("Stops", "RouteStops"):
        kusto_mgmt(f".clear table {table} data")
    load("Stops", stops)
    load("RouteStops", route_stops)


if __name__ == "__main__":
    main()
