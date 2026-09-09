#!/usr/bin/env python3
"""Deterministic loopback spatial-scene fixture. Never forwards requests upstream."""
import argparse
import math
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit, parse_qs, unquote

REVISION, CATALOG = 9001, 9001
LIMITS = {"community": (5000, 15000, 500, 1500), "system": (5000, 15000, 512, 5000),
          "discussion": (2048, 15000, 512, 5000), "thread": (2000, 12000, 250, 1500)}


def node(kind, number, *, identity=None, parent=""):
    return {"id": identity or f"{kind}:fixture-{number}", "name": f"Fixture {kind} {number}", "type": kind,
            "val": 100000 - number, "x": number * 3, "y": (number % 7) * 2, "z": (number % 11) * 5,
            "parent_id": parent, "position_provenance": "deterministic fixture", "source_updated_at": "2026-09-06T00:00:00Z",
            "body": "Deterministic fixture reply", "metrics": {"activity_count": 100000 - number,
            "unique_users": 100 - number % 50, "comment_count": 100 - number % 50, "reply_count": 3, "depth": number % 3}}


def document(scope, root, stress):
    maximum, link_maximum, _, _ = LIMITS[scope]
    count = maximum if stress else 12
    if scope == "community":
        nodes = [node("subreddit", index) for index in range(count)]
    elif scope == "system":
        nodes = [node("subreddit",0,identity=root)] + [node("post",index,identity=f"post:{root}:fixture-{index}",parent=root) for index in range(count-1)]
    elif scope == "discussion":
        nodes = [node("post",0,identity=root)] + [node("comment",index,identity=f"comment:{root}:fixture-{index}",parent=root) for index in range(count-1)]
    else:
        nodes = [node("comment",0,identity=root)] + [node("comment",index+1,identity=f"comment:{root}:reply-{index+1}",parent=root) for index in range(count-1)]
    links = []
    desired = link_maximum if stress else count - 1
    for index in range(desired):
        source = index % count
        offset = index // count + 1
        target = (source + offset) % count
        links.append({"source": nodes[source]["id"], "target": nodes[target]["id"], "type": "activity",
                      "weight": 10 + index % 20, "directed": False})
    return nodes, links


class Fixture(BaseHTTPRequestHandler):
    stress = False
    fault = ""
    calls = 0

    def do_GET(self):
        type(self).calls += 1
        uri = urlsplit(self.path); query = parse_qs(uri.query); path = unquote(uri.path)
        if path.endswith("/manifest"):
            return self.reply({"revision_id": REVISION, "spatial_catalog_id": CATALOG, "dimensions": 3,
                               "scene_contract": "spatial-scene-v1", "bounds": {"min_x":0,"max_x":300,"min_y":0,"max_y":300,"min_z":0,"max_z":300}})
        if path.endswith("/overview"):
            return self.reply({"revision_id":REVISION,"spatial_catalog_id":CATALOG,
                               "nodes":[node("community",i) for i in range(8)],"links":[]})
        scope = "community" if "/community/" in path else query.get("expand", [""])[0]
        if scope not in LIMITS: return self.reply({"error":"unknown fixture scope"},404)
        if query.get("revision") != [str(REVISION)] or query.get("spatial_catalog_id") != [str(CATALOG)]:
            return self.reply({"error":"wrong pin"},409)
        root = path.rsplit("/",1)[-1]
        nodes, links = document(scope,root,self.stress)
        cursor = query.get("cursor",["page:0"])[0]
        try: page = int(cursor.removeprefix("page:"))
        except ValueError: return self.reply({"error":"invalid cursor"},400)
        _,_,node_size,link_size = LIMITS[scope]
        if self.stress: link_size = math.ceil(len(links) / math.ceil(len(nodes) / node_size))
        selected_nodes = nodes[page*node_size:(page+1)*node_size]
        selected_links = links[page*link_size:(page+1)*link_size]
        more = (page+1)*node_size < len(nodes) or (page+1)*link_size < len(links)
        result = {"revision_id":REVISION,"spatial_catalog_id":CATALOG,"scope":scope,"root_id":root,
                  "nodes":selected_nodes,"links":selected_links,"truncated":more,
                  "next_cursor":f"page:{page+1}" if more else ""}
        if self.fault == "wrong-root": result["root_id"] = "wrong"
        if self.fault == "missing-scope": result.pop("scope")
        if self.fault == "repeated-cursor" and more: result["next_cursor"] = "page:1"
        if self.fault == "missing-cursor" and more: result["next_cursor"] = ""
        self.reply(result)

    def reply(self, data, status=200):
        body = json.dumps(data,separators=(",", ":")).encode()
        self.send_response(status); self.send_header("Content-Type","application/json")
        self.send_header("Content-Length",str(len(body))); self.end_headers(); self.wfile.write(body)

    def log_message(self,*args): pass


if __name__ == "__main__":
    parser=argparse.ArgumentParser(); parser.add_argument("--port",type=int,default=18765)
    parser.add_argument("--stress",action="store_true")
    parser.add_argument("--fault",choices=["", "wrong-root", "missing-scope", "repeated-cursor", "missing-cursor"],default="")
    args=parser.parse_args(); Fixture.stress=args.stress; Fixture.fault=args.fault
    server=ThreadingHTTPServer(("127.0.0.1",args.port),Fixture)
    print(f"fixture http://127.0.0.1:{server.server_port}/api stress={args.stress}",flush=True)
    server.serve_forever()
