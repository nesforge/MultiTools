using System.Diagnostics;
using System.Globalization;

internal static class PackagingMeasurements
{
    public static void Run()
    {
        string root = Environment.CurrentDirectory;
        string[] binaries = ["dist/previous/ClickMoves-1.2.exe", "dist/comparison-uncompressed/MiniTools.exe", "dist/win-x64/MiniTools.exe"];
        var report = new List<string> { "12/10/2026 · Windows x64 · 5 aperturas por variante, extracción propia vacía por apertura.",
            "Ventana detectada por proceso; memoria tras 500 ms. Caché de disco de Windows sin vaciar; no equivale a arranque frío del sistema." };
        foreach (string binary in binaries)
        {
            var times = new List<long>(); var memory = new List<long>();
            for (int run = 0; run < 5; run++)
            {
                string folder = Path.Combine(root, "TestResults", Guid.NewGuid().ToString("N"), "measure");
                Directory.CreateDirectory(folder);
                var start = new ProcessStartInfo(Path.Combine(root, binary)) { UseShellExecute = false };
                start.Environment["DOTNET_ROOT"] = Path.Combine(folder, "missing-runtime");
                start.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
                start.Environment["DOTNET_BUNDLE_EXTRACT_BASE_DIR"] = folder;
                var watch = Stopwatch.StartNew();
                using var process = Process.Start(start)!;
                try
                {
                    while (watch.ElapsedMilliseconds < 10000)
                    {
                        process.Refresh();
                        if (process.HasExited) throw new Exception("El ejecutable terminó al abrir.");
                        if (process.MainWindowHandle != IntPtr.Zero) break;
                        Thread.Sleep(20);
                    }
                    if (process.MainWindowHandle == IntPtr.Zero) throw new Exception("No aparece la ventana.");
                    times.Add(watch.ElapsedMilliseconds);
                    Thread.Sleep(500); process.Refresh(); memory.Add(process.WorkingSet64);
                }
                finally
                {
                    if (!process.HasExited) { process.CloseMainWindow(); if (!process.WaitForExit(5000)) process.Kill(); }
                }
            }
            times.Sort(); memory.Sort();
            report.Add($"{binary}: {new FileInfo(Path.Combine(root, binary)).Length} bytes; ventana mediana {times[2]} ms (rango {times[0]}–{times[^1]}); memoria mediana {(memory[2] / 1048576d).ToString("0.0", CultureInfo.InvariantCulture)} MiB.");
            Console.WriteLine(report[^1]);
        }
        File.WriteAllLines(Path.Combine(root, "PackagingResults.txt"), report);
    }
}
