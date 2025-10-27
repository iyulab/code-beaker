```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.6899)
Unknown processor
.NET SDK 9.0.306
  [Host]     : .NET 8.0.21 (8.0.2125.47513), X64 RyuJIT AVX2
  Job-OBGYOO : .NET 8.0.21 (8.0.2125.47513), X64 RyuJIT AVX2
  ShortRun   : .NET 8.0.21 (8.0.2125.47513), X64 RyuJIT AVX2

WarmupCount=3  

```
| Method                                   | Job        | IterationCount | LaunchCount | Mean        | Error       | StdDev      | Gen0   | Allocated |
|----------------------------------------- |----------- |--------------- |------------ |------------:|------------:|------------:|-------:|----------:|
| &#39;Submit single task&#39;                     | Job-OBGYOO | 5              | Default     |    709.7 μs |    155.0 μs |    40.26 μs |      - |   3.98 KB |
| &#39;Submit 10 tasks sequentially&#39;           | Job-OBGYOO | 5              | Default     |  6,889.8 μs |  2,378.9 μs |   368.14 μs |      - |  39.01 KB |
| &#39;Submit 100 tasks sequentially&#39;          | Job-OBGYOO | 5              | Default     | 64,362.1 μs |  6,014.1 μs | 1,561.83 μs |      - | 388.35 KB |
| &#39;Submit and retrieve single task&#39;        | Job-OBGYOO | 5              | Default     |  1,480.0 μs |    572.1 μs |   148.56 μs | 1.9531 |  18.43 KB |
| &#39;Full cycle: submit, retrieve, complete&#39; | Job-OBGYOO | 5              | Default     |  3,052.6 μs |  1,108.3 μs |   171.51 μs |      - |  19.37 KB |
| &#39;Full cycle: 10 tasks&#39;                   | Job-OBGYOO | 5              | Default     | 27,174.1 μs | 15,114.6 μs | 3,925.21 μs |      - | 210.16 KB |
| &#39;Submit single task&#39;                     | ShortRun   | 3              | 1           |    736.1 μs |  1,272.0 μs |    69.72 μs |      - |   3.98 KB |
| &#39;Submit 10 tasks sequentially&#39;           | ShortRun   | 3              | 1           | 10,638.3 μs | 44,648.7 μs | 2,447.34 μs |      - |  39.01 KB |
| &#39;Submit 100 tasks sequentially&#39;          | ShortRun   | 3              | 1           | 68,634.2 μs | 81,218.3 μs | 4,451.85 μs |      - | 390.13 KB |
| &#39;Submit and retrieve single task&#39;        | ShortRun   | 3              | 1           |  1,326.6 μs |  1,063.9 μs |    58.31 μs | 1.9531 |  18.33 KB |
| &#39;Full cycle: submit, retrieve, complete&#39; | ShortRun   | 3              | 1           |  2,289.6 μs |  4,257.6 μs |   233.38 μs |      - |  19.37 KB |
| &#39;Full cycle: 10 tasks&#39;                   | ShortRun   | 3              | 1           | 21,125.9 μs | 64,700.7 μs | 3,546.46 μs |      - | 210.16 KB |
