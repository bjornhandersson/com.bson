import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  async rewrites() {
    return [
      {
        source: "/tiles/:path*",
        destination: "http://localhost:5000/tiles/:path*",
      },
    ];
  },
};

export default nextConfig;
