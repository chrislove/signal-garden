#!/usr/bin/env python3
"""
Load OpenStreetMap reference layers into ADX: cafés, river water polygons, and
the bridges that cross them. They're what turn "a vehicle is sitting still" into
"a vehicle is sitting still 38 m from a café" (or in the Brisbane River).

    python3 infra/adx/reference/load_osm_reference.py            # fetch + load
    python3 infra/adx/reference/load_osm_reference.py --dry-run  # fetch + write CSVs only

Needs: `az login` with Ingestor + table-admin rights on the three tables, Node (npx osmtogeojson
assembles OSM multipolygons), and the tables from infra/adx/schema.kql.
Each run replaces the tables' contents, so it's safe to re-run when OSM changes.

Data © OpenStreetMap contributors, ODbL — attribution required wherever shown.
"""
import argparse, csv, io, json, os, subprocess, sys, tempfile, time, urllib.parse, urllib.request

CLUSTER = os.environ.get("ADX_QUERY_URI", "https://kvc-3mg9phxvpr6hnnguf7.australiaeast.kusto.windows.net")
DATABASE = "signalgarden"
USER_AGENT = "signal-garden/0.1 (personal learning project; github.com/chrislove/signal-garden)"
OVERPASS = ["https://overpass-api.de/api/interpreter",
            "https://maps.mail.ru/osm/tools/overpass/api/interpreter"]

SEQ = "-28.25,152.6,-26.6,153.6"          # cafés: Sunshine Coast to Gold Coast
BRISBANE = "-27.75,152.7,-27.25,153.25"   # rivers + bridges: greater Brisbane
WATER = '["natural"="water"]["water"~"^(river|canal|tidal_channel)$"]'

QUERIES = {
    "cafes": f'[out:json][timeout:200];(node["amenity"="cafe"]({SEQ});'
             f'way["amenity"="cafe"]({SEQ}););out center tags;',
    "water": f'[out:json][timeout:250];(relation{WATER}({BRISBANE});way{WATER}({BRISBANE}););out geom;',
    # Bridges over any of the water above, so a bus on the Story Bridge isn't "in the river".
    # (Must use the same water filter: a river-only filter once left Kedron Brook's
    # rail bridge out, and a passing train became a Possible Aquatic Transfer Event.)
    "bridges": f'[out:json][timeout:250];(relation{WATER}({BRISBANE});way{WATER}({BRISBANE}););'
               f'map_to_area->.a;(way(area.a)["bridge"]["highway"];way(area.a)["bridge"]["railway"];);'
               f'out geom tags;',
}


def overpass(query: str) -> dict:
    """Overpass is a shared, often-busy service: try each mirror, back off, retry."""
    body = urllib.parse.urlencode({"data": query}).encode()
    for attempt in range(4):
        for url in OVERPASS:
            req = urllib.request.Request(url, data=body, headers={"User-Agent": USER_AGENT})
            try:
                raw = urllib.request.urlopen(req, timeout=300).read()
                if raw.lstrip().startswith(b"{"):
                    return json.loads(raw)
            except Exception as e:  # busy / timeout: try the next mirror
                print(f"  {url}: {e}", file=sys.stderr)
        time.sleep(20 * (attempt + 1))
    sys.exit("Overpass unavailable — try again later.")


def to_geojson(osm: dict) -> dict:
    with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as f:
        json.dump(osm, f)
    out = subprocess.run(["npx", "-y", "osmtogeojson", f.name], capture_output=True, text=True, check=True)
    os.unlink(f.name)
    return json.loads(out.stdout)


def cafes_rows(osm: dict):
    for e in osm["elements"]:
        tags = e.get("tags", {})
        lat, lon = (e["lat"], e["lon"]) if e["type"] == "node" else (e["center"]["lat"], e["center"]["lon"])
        yield [f'{e["type"]}/{e["id"]}', tags.get("name", "Unnamed café"), lat, lon]


# Bridges a vehicle in the feed can't be on (buses, trains, ferries — not bikes).
NOT_FOR_VEHICLES = {"cycleway", "footway", "path", "pedestrian", "steps", "bridleway", "construction"}


def water_rows(geojson: dict):
    for f in geojson["features"]:
        p = f["properties"]
        yield [p["id"], p.get("name", ""), p.get("water", ""), json.dumps(f["geometry"])]


def bridge_rows(geojson: dict):
    for f in geojson["features"]:
        p = f["properties"]
        kind = p.get("highway") or f'railway:{p.get("railway", "")}'
        if kind in NOT_FOR_VEHICLES:
            continue
        yield [p["id"], p.get("name", ""), kind, json.dumps(f["geometry"])]


def csv_text(rows) -> str:
    buf = io.StringIO()
    csv.writer(buf, lineterminator="\n").writerows(rows)
    return buf.getvalue()


def kusto_mgmt(command: str):
    token = subprocess.run(["az", "account", "get-access-token", "--resource", "https://kusto.kusto.windows.net",
                            "--query", "accessToken", "-o", "tsv"], capture_output=True, text=True, check=True).stdout.strip()
    req = urllib.request.Request(f"{CLUSTER}/v1/rest/mgmt", method="POST",
                                 data=json.dumps({"db": DATABASE, "csl": command}).encode(),
                                 headers={"Authorization": f"Bearer {token}", "Content-Type": "application/json"})
    return json.load(urllib.request.urlopen(req, timeout=300))


def load(table: str, rows: list, batch: int):
    """`.ingest inline` needs only Ingestor rights and lands synchronously. Fine for
    reference data this size; telemetry goes through queued ingestion instead."""
    for i in range(0, len(rows), batch):
        chunk = rows[i:i + batch]
        kusto_mgmt(f".ingest inline into table {table} with (format='csv') <|\n{csv_text(chunk)}")
    print(f"  {table}: {len(rows)} rows loaded")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true", help="write CSVs to ./osm-reference instead of loading")
    args = ap.parse_args()

    print("Fetching OSM layers…")
    cafes = list(cafes_rows(overpass(QUERIES["cafes"])))
    water = list(water_rows(to_geojson(overpass(QUERIES["water"]))))
    bridges = list(bridge_rows(to_geojson(overpass(QUERIES["bridges"]))))
    print(f"  {len(cafes)} cafés, {len(water)} water bodies, {len(bridges)} bridges")

    if args.dry_run:
        os.makedirs("osm-reference", exist_ok=True)
        for name, rows in (("cafes", cafes), ("water", water), ("bridges", bridges)):
            open(f"osm-reference/{name}.csv", "w").write(csv_text(rows))
        print("Wrote osm-reference/*.csv")
        return

    print("Replacing ADX reference tables…")
    for table in ("Cafes", "WaterBodies", "Bridges"):
        kusto_mgmt(f".clear table {table} data")
    load("Cafes", cafes, 2000)
    load("WaterBodies", water, 5)      # polygons are big; keep each command small
    load("Bridges", bridges, 100)


if __name__ == "__main__":
    main()
