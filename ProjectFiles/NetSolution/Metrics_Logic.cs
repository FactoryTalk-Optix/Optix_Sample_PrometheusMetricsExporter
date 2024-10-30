#region Using directives
using UAManagedCore;
using FTOptix.NetLogic;
using Prometheus;
using FTOptix.HMIProject;
using System.Diagnostics;
using System.Threading.Tasks;
using System;
using System.Runtime.InteropServices;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using OpenTelemetry.Exporter;
using FTOptix.WebUI;
#endregion

public class Metrics_Logic : BaseNetLogic
{
    // Please see the README.md file for the full explanation of the code

    #region NetLogic creation and disposal methods
    public override void Start()
    {
        Log.Info("Metrics.Start", "Starting the metrics server in 5 seconds...");
        metricsInitialization = new DelayedTask(StartMetrics, 5000, LogicObject);
        metricsInitialization.Start();
    }

    public override void Stop()
    {
        // Stop the periodic task and dispose all metrics
        Log.Info("Metrics.Stop", "Stopping the metrics server");
        metricsInitialization?.Dispose();
        metricsTask?.Dispose();
        metricServer?.Dispose();
        meterProvider?.Dispose();
    }

    private void StartMetrics()
    {
        Log.Info("Metrics.Start", "Starting the metrics server");
        var prometheusStarted = PrometheusInitialization();
        var openTelemetryStarted = OpenTelemetryInitialization();
        if (prometheusStarted || openTelemetryStarted)
        {
            // Start the periodic task to refresh the metrics
            metricsTask = new PeriodicTask(UpdadteMetricsMethod, 500, LogicObject);
            metricsTask.Start();
            Log.Info("Metrics.Start", "Metrics server started successfully");
        }
        else
        {
            Log.Error("Metrics.Start", "Failed to start the metrics server");
        }
    }
    #endregion

    #region Metrics initialization
    private bool OpenTelemetryInitialization()
    {
        Log.Info("Metrics.Start", "Starting the OpenTelemetry metrics server");

        // Get the OpenTelemetry target address
        var otel_adddress = Environment.GetEnvironmentVariable("OTEL_TARGET", EnvironmentVariableTarget.Process);
        if (string.IsNullOrEmpty(otel_adddress))
        {
            Log.Warning("Metrics.Start", "OTEL_TARGET environment variable is not set, using `localhost`");
            otel_adddress = "localhost";
        }
        else
        {
            Log.Info("Metrics.Start", "OTEL_TARGET environment variable is set to: " + otel_adddress);
        }

        // Get the OpenTelemetry target port
        var otel_port = Environment.GetEnvironmentVariable("OTEL_PORT", EnvironmentVariableTarget.Process);
        if (string.IsNullOrEmpty(otel_port))
        {
            Log.Warning("Metrics.Start", "OTEL_PORT environment variable is not set, using `4317`");
            otel_port = "4317"; // or "4318" for HttpProtobuf
        }
        else
        {
            Log.Info("Metrics.Start", "OTEL_PORT environment variable is set to: " + otel_port);
        }

        // Get the OpenTelemetry protocol
        var protocol = Environment.GetEnvironmentVariable("OTEL_PROTOCOL", EnvironmentVariableTarget.Process);
        if (string.IsNullOrEmpty(protocol))
        {
            Log.Warning("Metrics.Start", "OTEL_PROTOCOL environment variable is not set, using `Grpc`");
            protocol = "0"; // or "HttpProtobuf"
        }
        else if (protocol == "0")
        {
            Log.Info("Metrics.Start", "OTEL_PROTOCOL environment variable is set to: Grpc");
        }
        else
        {
            Log.Info("Metrics.Start", "OTEL_PROTOCOL environment variable is set to: HttpProtobuf");
        }

        // Set the OpenTelemetry protocol
        var otlpExportProtocol = protocol != "0" ? OtlpExportProtocol.HttpProtobuf : OtlpExportProtocol.Grpc;

        try
        {
            // Start the OpenTelemetry metrics server
            Log.Info("Metrics.Start", "Starting the OpenTelemetry metrics server with the following parameters: " +
                "OTEL_TARGET=" + otel_adddress + ", OTEL_PORT=" + otel_port + ", OTEL_PROTOCOL=" + (protocol != "0" ? "HttpProtobuf" : "Grpc"));
            meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter("FTOptixRuntime")
                .AddConsoleExporter()
                .AddOtlpExporter(options =>
                {
                    // Set the protocol to Grpc or HttpProtobuf
                    options.Protocol = otlpExportProtocol;

                    // Set the endpoint
                    options.Endpoint = new Uri($"http://{otel_adddress}:{otel_port}");
                })
                .Build();
            Log.Info("Metrics.Start", "OpenTelemetry metrics server started successfully");
            return true;
        }
        catch (Exception e)
        {
            Log.Error("Metrics.Start", "Failed to start the OpenTelemetry metrics server: " + e.Message);
            return false;
        }
    }

    private bool PrometheusInitialization()
    {
        Log.Info("Metrics.Start", "Starting the Prometheus metrics server");
        try
        {
            // Start the metrics server
            var metricsPort = Environment.GetEnvironmentVariable("PROM_PORT", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(metricsPort))
            {
                Log.Warning("Metrics.Start", "PROM_PORT environment variable is not set, using `1234`");
                metricsPort = "1234";
            }
            else
            {
                Log.Info("Metrics.Start", "PROM_PORT environment variable is set to: " + metricsPort);
            }
            metricServer = new MetricServer(port: Convert.ToInt32(metricsPort));
            _ = metricServer.Start();
            Log.Info("Metrics.Start", "Prometheus metrics server started successfully");
            return true;
        }
        catch (Exception e)
        {
            Log.Error("Metrics.Start", "Failed to start the metrics server: " + e.Message);
            return false;
        }
    }
    #endregion

    private void UpdadteMetricsMethod()
    {
        double metricValue;

        try
        {
            // Refresh the total memory information
            metricValue = MemoryUsage.GetTotalMemoryUsage().Result;
            // Update values for Prometheus
            systemRamUsage.Set(metricValue);
            // Update values for OpenTelemetry
            systemRamUsageCounter.Record(metricValue);
            // Refresh the process memory information
            metricValue = MemoryUsage.GetProcessMemoryUsage().Result;
            // Update values for Prometheus
            processMemoryUsage.Set(metricValue);
            // Update values for OpenTelemetry
            processMemoryUsageCounter.Record(metricValue);
        }
        catch (Exception e)
        {
            Log.Error("Metrics.MetricsMethod.", "Failed to refresh RAM metrics: " + e.Message);
        }

        try
        {
            // Refresh the total CPU information
            metricValue = CpuUsage.GetTotalCpuUsage().Result;
            // Update values for Prometheus
            systemCpuUsage.Set(metricValue);
            // Update values for OpenTelemetry
            systemCpuUsageCounter.Record(metricValue);
            // Refresh the process CPU information
            metricValue = CpuUsage.GetCpuUsageForProcess().Result;
            // Update values for Prometheus
            processCpuUsage.Set(metricValue);
            // Update values for OpenTelemetry
            processCpuUsageCounter.Record(metricValue);
        }
        catch (Exception e)
        {
            Log.Error("Metrics.MetricsMethod.", "Failed to refresh CPU metrics: " + e.Message);
        }

        try
        {
            // Refresh the Optix Variable(s)
            metricValue = Project.Current.GetVariable("Model/Variable1").Value;
            // Update values for Prometheus
            optixModelVariable.Set(metricValue);
            // Update values for OpenTelemetry
            optixModelVariableCounter.Record(metricValue);
        }
        catch (Exception e)
        {
            Log.Error("Metrics.MetricsMethod.", "Failed to refresh Variable metrics: " + e.Message);
        }
    }

    #region Private fields for Prometheus and OpenTelemetry metrics
    private PeriodicTask metricsTask;
    private MetricServer metricServer;
    private MeterProvider meterProvider;
    #endregion

    #region Prometheus metrics declaration
    private static readonly Gauge optixModelVariable = Metrics.CreateGauge("FTOptix_Model_Variable1", "Variable1 from Model folder");
    private static readonly Gauge systemCpuUsage = Metrics.CreateGauge("FTOptix_Diagnostics_totalCpuUsagePercent", "Total CPU usage percent");
    private static readonly Gauge systemRamUsage = Metrics.CreateGauge("FTOptix_Diagnostics_totalRamUsageMegaBytes", "Total RAM utilization in MB");
    private static readonly Gauge processCpuUsage = Metrics.CreateGauge("FTOptix_Diagnostics_processCpuUsagePercent", "CPU usage percent of the current process");
    private static readonly Gauge processMemoryUsage = Metrics.CreateGauge("FTOptix_Diagnostics_processMemoryUsageMegaBytes", "Memory usage of the current process in MB");
    #endregion

    #region OpenTelemetry metrics declaration
    private static readonly Meter meter = new Meter("FTOptixRuntime", "1.0");
    private static readonly Histogram<double> optixModelVariableCounter = meter.CreateHistogram<double>("FTOptix_Model_Variable1");
    private static readonly Histogram<double> systemCpuUsageCounter = meter.CreateHistogram<double>("FTOptix_Diagnostics_totalCpuUsagePercent");
    private static readonly Histogram<double> systemRamUsageCounter = meter.CreateHistogram<double>("FTOptix_Diagnostics_totalRamUsageMegaBytes");
    private static readonly Histogram<double> processCpuUsageCounter = meter.CreateHistogram<double>("FTOptix_Diagnostics_processCpuUsagePercent");
    private static readonly Histogram<double> processMemoryUsageCounter = meter.CreateHistogram<double>("FTOptix_Diagnostics_processMemoryUsageMegaBytes");
    #endregion

    #region Optix Variable(s) declaration
    private DelayedTask metricsInitialization;
    #endregion
}

public class CpuUsage
{
    public static async Task<double> GetCpuUsageForProcess()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await GetCpuUsageForProcessWindows();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await GetCpuUsageForProcessLinux();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await GetCpuUsageForProcessMac();
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported OS platform");
        }
    }

    private static async Task<double> GetCpuUsageForProcessWindows()
    {
        var process = Process.GetCurrentProcess();
        var startTime = DateTime.UtcNow;
        var startCpuUsage = process.TotalProcessorTime;
        await Task.Delay(500);
        var endTime = DateTime.UtcNow;
        var endCpuUsage = process.TotalProcessorTime;

        var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
        var totalMsPassed = (endTime - startTime).TotalMilliseconds;

        var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

        return cpuUsageTotal * 100;
    }

    private static async Task<double> GetCpuUsageForProcessLinux()
    {
        // Implement Linux-specific CPU usage retrieval
        // This is a placeholder implementation
        await Task.Delay(500);
        return 0.0;
    }

    private static async Task<double> GetCpuUsageForProcessMac()
    {
        // Implement macOS-specific CPU usage retrieval
        // This is a placeholder implementation
        await Task.Delay(500);
        return 0.0;
    }

    // Global CPU usage
    public static async Task<double> GetTotalCpuUsage()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await GetTotalCpuUsageWindows();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await GetTotalCpuUsageLinux();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await GetTotalCpuUsageMac();
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported OS platform");
        }
    }

    private static async Task<double> GetTotalCpuUsageWindows()
    {
        var startTime = DateTime.UtcNow;
        var startCpuUsage = GetCpuTimesWindows();
        await Task.Delay(500);
        var endTime = DateTime.UtcNow;
        var endCpuUsage = GetCpuTimesWindows();

        var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
        var totalMsPassed = (endTime - startTime).TotalMilliseconds * Environment.ProcessorCount;

        var cpuUsageTotal = cpuUsedMs / totalMsPassed;

        return cpuUsageTotal * 100;
    }

    private static TimeSpan GetCpuTimesWindows()
    {
        var totalCpuTime = new TimeSpan(0);
        var processes = Process.GetProcesses();
        foreach (var process in processes)
        {
            try
            {
                totalCpuTime += process.TotalProcessorTime;
            }
            catch
            {
                // Some processes might not allow access to their CPU times
            }
        }
        return totalCpuTime;
    }

    private static async Task<double> GetTotalCpuUsageLinux()
    {
        var startTime = DateTime.UtcNow;
        var startCpuUsage = GetCpuUsageLinux();
        await Task.Delay(500);
        var endTime = DateTime.UtcNow;
        var endCpuUsage = GetCpuUsageLinux();

        var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
        var totalMsPassed = (endTime - startTime).TotalMilliseconds;

        var cpuUsageTotal = cpuUsedMs / totalMsPassed;

        return cpuUsageTotal * 100;
    }

    private static TimeSpan GetCpuUsageLinux()
    {
        var output = ExecuteBashCommand("grep 'cpu ' /proc/stat");
        var values = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var user = double.Parse(values[1]);
        var nice = double.Parse(values[2]);
        var system = double.Parse(values[3]);
        var idle = double.Parse(values[4]);
        var iowait = double.Parse(values[5]);
        var irq = double.Parse(values[6]);
        var softirq = double.Parse(values[7]);

        var total = user + nice + system + idle + iowait + irq + softirq;
        var active = total - idle;

        return TimeSpan.FromMilliseconds(active);
    }

    private static string ExecuteBashCommand(string command)
    {
        var escapedArgs = command.Replace("\"", "\\\"");
        var process = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{escapedArgs}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.Start();
        string result = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return result;
    }

    private static async Task<double> GetTotalCpuUsageMac()
    {
        var startTime = DateTime.UtcNow;
        var startCpuUsage = GetCpuUsageMac();
        await Task.Delay(500);
        var endTime = DateTime.UtcNow;
        var endCpuUsage = GetCpuUsageMac();

        var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
        var totalMsPassed = (endTime - startTime).TotalMilliseconds;

        var cpuUsageTotal = cpuUsedMs / totalMsPassed;

        return cpuUsageTotal * 100;
    }

    private static TimeSpan GetCpuUsageMac()
    {
        var output = ExecuteBashCommand("top -l 1 | grep 'CPU usage' | awk '{print $3}' | sed 's/%//'");
        var user = double.Parse(output);
        return TimeSpan.FromMilliseconds(user);
    }
}

public class MemoryUsage
{
    public static async Task<double> GetProcessMemoryUsage()
    {
        var process = Process.GetCurrentProcess();
        return await Task.FromResult(process.WorkingSet64 / (1024.0 * 1024.0));
    }

    public static async Task<double> GetTotalMemoryUsage()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await GetTotalMemoryUsageWindows();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await GetTotalMemoryUsageLinux();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await GetTotalMemoryUsageMac();
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported OS platform");
        }
    }

    private static async Task<double> GetTotalMemoryUsageWindows()
    {
        var output = ExecuteCommand("wmic OS get FreePhysicalMemory,TotalVisibleMemorySize /Value");
        var lines = output.Trim().Split('\n');
        var freeMemory = double.Parse(lines[0].Split('=')[1]);
        var totalMemory = double.Parse(lines[1].Split('=')[1]);
        var usedMemory = (totalMemory - freeMemory) / 1024.0;
        return await Task.FromResult(usedMemory);
    }

    private static async Task<double> GetTotalMemoryUsageLinux()
    {
        var output = ExecuteCommand("free -m | grep Mem");
        var values = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var totalMemory = double.Parse(values[1]);
        var usedMemory = double.Parse(values[2]);
        return await Task.FromResult(usedMemory);
    }

    private static async Task<double> GetTotalMemoryUsageMac()
    {
        var output = ExecuteCommand("vm_stat | grep 'Pages active'");
        var activePages = double.Parse(output.Split(':')[1].Trim().Replace(".", ""));
        var usedMemory = (activePages * 4096) / (1024.0 * 1024.0);
        return await Task.FromResult(usedMemory);
    }

    private static string ExecuteCommand(string command)
    {
        var process = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "/bin/bash",
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"/c {command}" : $"-c \"{command}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.Start();
        string result = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return result;
    }
}
