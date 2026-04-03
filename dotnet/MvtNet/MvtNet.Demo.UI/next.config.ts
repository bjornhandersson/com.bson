import type { NextConfig } from "next";

const apiUrl = process.env.API_URL ?? "http://localhost:5000";

const nextConfig: NextConfig = {
  async rewrites() {
    return [
      {
        source: "/tiles/:path*",
        destination: `${apiUrl}/tiles/:path*`,
      },
    ];
  },
};

export default nextConfig;
