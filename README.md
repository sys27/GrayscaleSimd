# GrayscaleSimd

> // BenchmarkDotNet v0.14.0
> 
> // Runtime=.NET 9.0.1 (9.0.124.61010), X64 RyuJIT AVX2
> 
> // GC=Concurrent Workstation
> 
> // HardwareIntrinsics=AVX2,AES,BMI1,BMI2,FMA,LZCNT,PCLMUL,POPCNT,AvxVnni,SERIALIZE VectorSize=256
> 
> // Job: MediumRun(IterationCount=15, LaunchCount=2, WarmupCount=10)
> 
> BenchmarkDotNet v0.14.0
> 
> 12th Gen Intel Core i7-12700K, 1 CPU, 20 logical and 12 physical cores
> 
> .NET SDK 9.0.102
> 
>   [Host]    : .NET 9.0.1 (9.0.124.61010), X64 RyuJIT AVX2
> 
>   MediumRun : .NET 9.0.1 (9.0.124.61010), X64 RyuJIT AVX2
> 
> Job=MediumRun  Toolchain=.NET 9.0  IterationCount=15
> 
> LaunchCount=2  WarmupCount=10  

| Method       | Mean      | Error    | StdDev   | Ratio |
|------------- |----------:|---------:|---------:|------:|
| LinearTest   | 615.76 μs | 3.947 μs | 5.907 μs |  1.00 |
| ParallelTest | 228.74 μs | 4.631 μs | 6.641 μs |  0.37 |
| SseTest      | 133.07 μs | 1.304 μs | 1.951 μs |  0.22 |
| AvxTest      |  94.35 μs | 0.936 μs | 1.401 μs |  0.15 |