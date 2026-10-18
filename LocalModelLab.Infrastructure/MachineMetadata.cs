using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace LocalModelLab.Infrastructure;

public static class MachineMetadata
{
    public static async Task<JsonElement> CaptureAsync(CancellationToken cancellationToken)
    {
        JsonElement? hardware = null;
        string? discoveryError = null;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add("-NoProfile");
                start.ArgumentList.Add("-NonInteractive");
                start.ArgumentList.Add("-Command");
                start.ArgumentList.Add("$ErrorActionPreference='Stop'; [Console]::OutputEncoding=[System.Text.Encoding]::UTF8; " +
                    "$nvidia=$null; if(Get-Command nvidia-smi -ErrorAction SilentlyContinue){ " +
                    "$nvidia=@(& nvidia-smi '--query-gpu=name,uuid,driver_version,memory.total' '--format=csv,noheader,nounits'); " +
                    "if($LASTEXITCODE -ne 0){$nvidia=$null} }; " +
                    "@{cpu=@(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors); " +
                    "physicalMemoryBytes=(Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory; " +
                    "gpu=@(Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion,AdapterRAM); " +
                    "nvidiaSmiCsv=$nvidia; nvidiaSmiColumns='name,uuid,driver_version,memory.total'; nvidiaSmiMemoryUnit='MiB'} | ConvertTo-Json -Depth 5 -Compress");
                using var process = Process.Start(start) ?? throw new IOException("Could not start hardware discovery.");
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
                try { await process.WaitForExitAsync(timeout.Token); }
                catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
                if (process.ExitCode != 0) throw new IOException(await stderr);
                using var document = JsonDocument.Parse(await stdout);
                hardware = document.RootElement.Clone();
            }
            catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or JsonException or OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested) throw;
                discoveryError = ex.Message;
            }
        }
        return JsonSerializer.SerializeToElement(new
        {
            capturedUtc = DateTimeOffset.UtcNow,
            measurementKind = "staticHardwareMetadata",
            telemetry = new { status = "notCollected", gpuUtilization = (double?)null, cpuUtilization = (double?)null,
                peakVramBytes = (long?)null, energyJoules = (double?)null },
            attributionNote = "No per-attempt CPU/GPU attribution. NVIDIA query contains static device metadata only. /api/ps is a post-inference residency snapshot, not utilization or peak memory telemetry.",
            hostName = Environment.MachineName, os = RuntimeInformation.OSDescription,
            osArchitecture = RuntimeInformation.OSArchitecture.ToString(), processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            runtime = RuntimeInformation.FrameworkDescription, logicalProcessors = Environment.ProcessorCount,
            cpuIdentifier = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"), hardware,
            hardwareDiscoveryError = discoveryError,
            hardwareDiscoveryStatus = hardware is not null ? "captured" : OperatingSystem.IsWindows() ? "unavailable" : "unsupportedPlatform",
            gpuMemoryNote = "Prefer nvidiaSmiCsv memory.total in MiB when available. Win32 AdapterRAM may overflow above 4 GiB; if NVIDIA discovery is unavailable, dedicated VRAM is unknown.",
            culture = System.Globalization.CultureInfo.CurrentCulture.Name, timezone = TimeZoneInfo.Local.Id
        });
    }
}
