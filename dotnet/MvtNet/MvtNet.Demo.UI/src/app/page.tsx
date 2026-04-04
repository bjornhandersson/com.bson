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
      center: [17.1, 58.96],
      zoom: 8,
    });

    map.current.on("load", () => {
      map.current!.addSource("mvt", {
        type: "vector",
        tiles: [window.location.origin + "/tiles/{z}/{x}/{y}"],
        minzoom: 0,
        maxzoom: 14,
      });

      // Lake polygon
      map.current!.addLayer({
        id: "areas-fill",
        type: "fill",
        source: "mvt",
        "source-layer": "areas",
        paint: {
          "fill-color": "#3b82f6",
          "fill-opacity": 0.2,
        },
      });

      map.current!.addLayer({
        id: "areas-outline",
        type: "line",
        source: "mvt",
        "source-layer": "areas",
        paint: {
          "line-color": "#3b82f6",
          "line-width": 2,
        },
      });

      // Route line
      map.current!.addLayer({
        id: "route",
        type: "line",
        source: "mvt",
        "source-layer": "route",
        paint: {
          "line-color": "#ef4444",
          "line-width": 3,
        },
      });

      // POIs
      map.current!.addLayer({
        id: "pois",
        type: "circle",
        source: "mvt",
        "source-layer": "pois",
        paint: {
          "circle-radius": 3,
          "circle-color": "#f59e0b",
          "circle-opacity": 0.7,
        },
      });

      // Endpoint markers
      map.current!.addLayer({
        id: "markers",
        type: "circle",
        source: "mvt",
        "source-layer": "markers",
        paint: {
          "circle-radius": 7,
          "circle-color": "#10b981",
          "circle-stroke-color": "#ffffff",
          "circle-stroke-width": 2,
        },
      });

      // Marker labels
      map.current!.addLayer({
        id: "marker-labels",
        type: "symbol",
        source: "mvt",
        "source-layer": "markers",
        layout: {
          "text-field": ["get", "name"],
          "text-offset": [0, 1.5],
          "text-size": 13,
          "text-font": ["Open Sans Regular"],
        },
        paint: {
          "text-color": "#1f2937",
          "text-halo-color": "#ffffff",
          "text-halo-width": 1.5,
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
