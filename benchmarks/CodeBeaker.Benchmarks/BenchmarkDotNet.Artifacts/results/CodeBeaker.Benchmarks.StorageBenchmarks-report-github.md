```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.6899)
Unknown processor
.NET SDK 9.0.306
  [Host]     : .NET 8.0.21 (8.0.2125.47513), X64 RyuJIT AVX2
  Job-OBGYOO : .NET 8.0.21 (8.0.2125.47513), X64 RyuJIT AVX2
  ShortRun   : .NET 8.0.21 (8.0.2125.47513), X64 RyuJIT AVX2

WarmupCount=3  

```
| Method                            | Job        | IterationCount | LaunchCount | Mean       | Error       | StdDev    | Gen0   | Allocated  |
|---------------------------------- |----------- |--------------- |------------ |-----------:|------------:|----------:|-------:|-----------:|
| &#39;Save single result&#39;              | Job-OBGYOO | 5              | Default     |   2.642 ms |   0.2935 ms | 0.0762 ms |      - |   18.31 KB |
| &#39;Save and retrieve single result&#39; | Job-OBGYOO | 5              | Default     |   3.104 ms |   1.0751 ms | 0.2792 ms | 3.9063 |   48.09 KB |
| &#39;Save 10 results&#39;                 | Job-OBGYOO | 5              | Default     |  25.733 ms |   6.6926 ms | 1.0357 ms |      - |  179.31 KB |
| &#39;Save 100 results&#39;                | Job-OBGYOO | 5              | Default     | 289.259 ms |  15.7674 ms | 2.4400 ms |      - | 1834.59 KB |
| &#39;Save single result&#39;              | ShortRun   | 3              | 1           |   2.783 ms |   2.6368 ms | 0.1445 ms |      - |   18.24 KB |
| &#39;Save and retrieve single result&#39; | ShortRun   | 3              | 1           |   3.812 ms |   8.2677 ms | 0.4532 ms |      - |   48.09 KB |
| &#39;Save 10 results&#39;                 | ShortRun   | 3              | 1           |  36.897 ms | 114.7460 ms | 6.2896 ms |      - |  180.34 KB |
| &#39;Save 100 results&#39;                | ShortRun   | 3              | 1           | 398.574 ms | 130.7485 ms | 7.1668 ms |      - | 1951.03 KB |
