"use client";

import { useEffect, useRef } from "react";
import maplibregl from "maplibre-gl";
import "maplibre-gl/dist/maplibre-gl.css";

export default function Home() {
  const mapContainer = useRef<HTMLDivElement>(null);
  const map = useRef<maplibregl.Map | null>(null);

  useEffect(() => {
    if (map.current || !mapContainer.current) {
      return;
    }

    map.current = new maplibregl.Map({
      container: mapContainer.current,
      style: {
        version: 8,
        sources: {
          osm: {
            type: "raster",
            tiles: ["https://tile.openstreetmap.org/{z}/{x}/{y}.png"],
            tileSize: 256,
            attribution: "&copy; OpenStreetMap contributors",
          },
        },
        layers: [
          {
            id: "osm-tiles",
            type: "raster",
            source: "osm",
            minzoom: 0,
            maxzoom: 19,
          },
        ],
      },
      center: [17.1, 58.96], // Midpoint Stockholm – Norrköping
      zoom: 8,
    });

    map.current.on("load", () => {
      map.current!.addSource("mvt-overlay", {
        type: "vector",
        tiles: [window.location.origin + "/tiles/{z}/{x}/{y}"],
        minzoom: 0,
        maxzoom: 14,
      });

      // Route
      map.current!.addLayer({
        id: "mvt-route",
        type: "line",
        source: "mvt-overlay",
        "source-layer": "route",
        paint: {
          "line-color": "#ef4444",
          "line-width": 3,
        },
      });

      // Points
      map.current!.addLayer({
        id: "mvt-points",
        type: "circle",
        source: "mvt-overlay",
        "source-layer": "points",
        paint: {
          "circle-radius": 7,
          "circle-color": "#10b981",
          "circle-stroke-color": "#ffffff",
          "circle-stroke-width": 2,
        },
      });

      // POIs (geohash-queried)
      map.current!.addLayer({
        id: "mvt-pois",
        type: "circle",
        source: "mvt-overlay",
        "source-layer": "pois",
        paint: {
          "circle-radius": 5,
          "circle-color": "#f59e0b",
          "circle-stroke-color": "#ffffff",
          "circle-stroke-width": 1.5,
        },
      });
    });

    return () => {
      map.current?.remove();
      map.current = null;
    };
  }, []);

  return <div ref={mapContainer} style={{ width: "100vw", height: "100vh" }} />;
}
