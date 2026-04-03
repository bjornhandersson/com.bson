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
      center: [18.0440866, 59.3281936],
      zoom: 12,
    });

    map.current.on("load", () => {
      map.current!.addSource("mvt-overlay", {
        type: "vector",
        tiles: [window.location.origin + "/tiles/{z}/{x}/{y}"],
        minzoom: 0,
        maxzoom: 14,
      });

      // Polygon fill
      map.current!.addLayer({
        id: "mvt-geofences-fill",
        type: "fill",
        source: "mvt-overlay",
        "source-layer": "geofences",
        paint: {
          "fill-color": "#3b82f6",
          "fill-opacity": 0.15,
        },
      });

      // Polygon outline
      map.current!.addLayer({
        id: "mvt-geofences-outline",
        type: "line",
        source: "mvt-overlay",
        "source-layer": "geofences",
        paint: {
          "line-color": "#3b82f6",
          "line-width": 2,
        },
      });

      // LineString
      map.current!.addLayer({
        id: "mvt-tracks",
        type: "line",
        source: "mvt-overlay",
        "source-layer": "tracks",
        paint: {
          "line-color": "#f59e0b",
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
          "circle-color": "#ef4444",
          "circle-stroke-color": "#ffffff",
          "circle-stroke-width": 2,
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
