using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CsProj;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// var b = new GrayscaleBenchmark();
// b.Setup();
// var result = b.ParallelTest();
// Image.LoadPixelData<Rgb24>(result, 512, 512).SaveAsPng("LennaGray.png");

if (args is null || args.Length == 0)
    args = ["--filter", "*"];

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args,
        ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.MediumRun
                .WithToolchain(CsProjCoreToolchain.NetCoreApp90))
            .StopOnFirstError());

public class GrayscaleBenchmark
{
    private byte[] pixels;

    [GlobalSetup]
    public void Setup()
    {
        var image = Image.Load<Rgb24>("Lenna.png");
        pixels = new byte[image.Width * image.Height * (image.PixelType.BitsPerPixel / 8)];
        image.CopyPixelDataTo(pixels);
    }

    [Benchmark(Baseline = true)]
    public byte[] LinearTest()
    {
        var result = new byte[pixels.Length];
        for (var i = 0; i < result.Length; i += 3)
        {
            var gray = (byte)(0.299 * pixels[i] + 0.587 * pixels[i + 1] + 0.114 * pixels[i + 2]);
            result[i] = gray;
            result[i + 1] = gray;
            result[i + 2] = gray;
        }

        return result;
    }

    [Benchmark]
    public byte[] ParallelTest()
    {
        var result = new byte[pixels.Length];
        Parallel.For(0, result.Length / 3, i =>
        {
            var gray = (byte)(0.299 * pixels[i * 3] + 0.587 * pixels[i * 3 + 1] + 0.114 * pixels[i * 3 + 2]);
            result[i * 3] = gray;
            result[i * 3 + 1] = gray;
            result[i * 3 + 2] = gray;
        });

        return result;
    }

    [Benchmark]
    public byte[] SseTest()
    {
        if (!Sse42.IsSupported)
            throw new Exception();

        var result = new byte[pixels.Length];
        var mask = Vector128.Create(new byte[]
        {
            0, 3, 6, 9,
            1, 4, 7, 10,
            2, 5, 8, 11,
            0, 0, 0, 0
        });
        var grayMask = Vector128.Create(new byte[]
        {
            0, 0, 0,
            4, 4, 4,
            8, 8, 8,
            12, 12, 12,
            0, 0, 0, 0
        });
        var rk = Vector128.Create(0.299f);
        var gk = Vector128.Create(0.587f);
        var bk = Vector128.Create(0.114f);
        Span<byte> temp = stackalloc byte[16];

        var i = 0;
        for (; result.Length - i >= 16; i += 12)
        {
            var v = Vector128.Create(pixels, i);
            var planar = Ssse3.Shuffle(v, mask);
            var l = Sse2.UnpackLow(planar, Vector128<byte>.Zero);

            var r = Sse2.UnpackLow(l, Vector128<byte>.Zero).AsInt32();
            var rf = Sse2.ConvertToVector128Single(r);

            var g = Sse2.UnpackHigh(l, Vector128<byte>.Zero).AsInt32();
            var gf = Sse2.ConvertToVector128Single(g);

            var h = Sse2.UnpackHigh(planar, Vector128<byte>.Zero);
            var b = Sse2.UnpackLow(h, Vector128<byte>.Zero).AsInt32();
            var bf = Sse2.ConvertToVector128Single(b);

            var gray = Fma.MultiplyAdd(
                rf, rk,
                Fma.MultiplyAdd(
                    gf, gk,
                    Sse.Multiply(bf, bk)
                )
            );

            var grayb = Sse2.ConvertToVector128Int32(gray).AsByte();
            var grayShuffled = Ssse3.Shuffle(grayb, grayMask);

            grayShuffled.CopyTo(temp);
            temp[..12].CopyTo(result.AsSpan(i));
        }

        for (; i < result.Length; i += 3)
        {
            var gray = (byte)(0.299 * pixels[i] + 0.587 * pixels[i + 1] + 0.114 * pixels[i + 2]);
            result[i] = gray;
            result[i + 1] = gray;
            result[i + 2] = gray;
        }

        return result;
    }

    [Benchmark]
    public byte[] AvxTest()
    {
        if (!Avx2.IsSupported)
            throw new Exception();

        var result = new byte[pixels.Length];
        var perm = Vector256.Create(0, 1, 2, 6, 3, 4, 5, 7);
        var mask = Vector256.Create(new byte[]
        {
            0, 3, 6, 9,
            1, 4, 7, 10,
            2, 5, 8, 11,
            0, 0, 0, 0,

            0, 3, 6, 9,
            1, 4, 7, 10,
            2, 5, 8, 11,
            0, 0, 0, 0,
        });
        var grayMask = Vector256.Create(new byte[]
        {
            0, 0, 0,
            4, 4, 4,
            8, 8, 8,
            12, 12, 12,
            0, 0, 0, 0,

            0, 0, 0,
            4, 4, 4,
            8, 8, 8,
            12, 12, 12,
            0, 0, 0, 0,
        });
        var grayPerm = Vector256.Create(0, 1, 2, 4, 5, 6, 3, 7);
        var rk = Vector256.Create(0.299f);
        var gk = Vector256.Create(0.587f);
        var bk = Vector256.Create(0.114f);
        Span<byte> temp = stackalloc byte[32];

        var i = 0;
        for (; result.Length - i >= 32; i += 24)
        {
            var v = Vector256.Create(pixels, i);
            v = Avx2.PermuteVar8x32(v.AsInt32(), perm).AsByte();
            var planar = Avx2.Shuffle(v, mask);
            var l = Avx2.UnpackLow(planar, Vector256<byte>.Zero);

            var r = Avx2.UnpackLow(l, Vector256<byte>.Zero).AsInt32();
            var rf = Avx.ConvertToVector256Single(r);

            var g = Avx2.UnpackHigh(l, Vector256<byte>.Zero).AsInt32();
            var gf = Avx.ConvertToVector256Single(g);

            var h = Avx2.UnpackHigh(planar, Vector256<byte>.Zero);
            var b = Avx2.UnpackLow(h, Vector256<byte>.Zero).AsInt32();
            var bf = Avx.ConvertToVector256Single(b);

            var gray = Fma.MultiplyAdd(
                rf, rk,
                Fma.MultiplyAdd(
                    gf, gk,
                    Avx.Multiply(bf, bk)
                )
            );

            var grayb = Avx.ConvertToVector256Int32(gray).AsByte();
            var grayShuffled = Avx2.Shuffle(grayb, grayMask);
            grayShuffled = Avx2.PermuteVar8x32(grayShuffled.AsInt32(), grayPerm).AsByte();

            grayShuffled.CopyTo(temp);
            temp[..24].CopyTo(result.AsSpan(i));
        }

        for (; i < result.Length; i += 3)
        {
            var gray = (byte)(0.299 * pixels[i] + 0.587 * pixels[i + 1] + 0.114 * pixels[i + 2]);
            result[i] = gray;
            result[i + 1] = gray;
            result[i + 2] = gray;
        }

        return result;
    }
}