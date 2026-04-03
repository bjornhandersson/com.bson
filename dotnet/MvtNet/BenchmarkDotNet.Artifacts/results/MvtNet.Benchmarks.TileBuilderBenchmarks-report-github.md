```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.3 (24G419) [Darwin 24.6.0]
Apple M1 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]   : .NET 10.0.5 (10.0.5, 10.0.526.15411), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.5 (10.0.5, 10.0.526.15411), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                                     | Mean        | Error        | StdDev    | Gen0    | Gen1   | Allocated |
|------------------------------------------- |------------:|-------------:|----------:|--------:|-------:|----------:|
| &#39;Single point&#39;                             |    481.2 ns |     29.87 ns |   1.64 ns |  0.2918 | 0.0010 |   1.79 KB |
| &#39;Point with attributes&#39;                    |    962.5 ns |     50.94 ns |   2.79 ns |  0.5093 | 0.0038 |   3.13 KB |
| &#39;10 points&#39;                                |  3,831.1 ns |    119.91 ns |   6.57 ns |  1.1139 | 0.0153 |   6.87 KB |
| &#39;100 points&#39;                               | 31,622.6 ns |  2,192.89 ns | 120.20 ns |  7.2021 | 0.6104 |  44.28 KB |
| &#39;LineString (5 pts)&#39;                       |    741.2 ns |     31.80 ns |   1.74 ns |  0.3643 | 0.0010 |   2.23 KB |
| &#39;Polygon (4 pts)&#39;                          |    659.3 ns |     12.13 ns |   0.66 ns |  0.3090 | 0.0010 |    1.9 KB |
| &#39;Mixed tile (realistic)&#39;                   |  3,203.1 ns |    109.64 ns |   6.01 ns |  1.2016 | 0.0191 |   7.38 KB |
| &#39;LineString (3000 pts, 200km, zoomed in)&#39;  | 56,312.3 ns | 11,152.29 ns | 611.29 ns |  4.0283 |      - |  24.91 KB |
| &#39;LineString (3000 pts, 200km, zoomed out)&#39; | 84,370.4 ns |  5,744.34 ns | 314.87 ns | 14.5264 | 1.7090 |  89.53 KB |
| &#39;Point outside tile (skip)&#39;                |    371.3 ns |      6.00 ns |   0.33 ns |  0.2255 | 0.0005 |   1.38 KB |
