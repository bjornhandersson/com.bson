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
      center: [18.0440866, 59.3281936], // Stockholm
      zoom: 10,
    });

    map.current.on("load", () => {
      map.current!.addSource("mvt-overlay", {
        type: "vector",
        tiles: [window.location.origin + "/tiles/{z}/{x}/{y}"],
        minzoom: 0,
        maxzoom: 14,
      });

      map.current!.addLayer({
        id: "mvt-lines",
        type: "line",
        source: "mvt-overlay",
        "source-layer": "stub",
        paint: {
          "line-color": "#ff0000",
          "line-width": 2,
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
