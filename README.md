# Metric Logic

This project contains a `Metric_Logic` class that exposes various system and process metrics using Prometheus and OpenTelemetry, the two components can be detached they do not strictly relate each other. The metrics are exposed on `http://localhost:1234/metrics`.

![metrics image](./Images/metrics.png)

## Cloning the repository

There are multiple ways to download this project, here is the recommended one

### Clone repository

1. Click on the green `CODE` button in the top right corner
2. Select `HTTPS` and copy the provided URL
3. Open FT Optix IDE
4. Click on `Open` and select the `Remote` tab
5. Paste the URL from step 2
6. Click `Open` button in bottom right corner to start cloning process

## Prerequisites

- FactoryTalk Optix 1.5.x or later (`.NET8` is required)
- .NET Core SDK
- Prometheus

### NuGet packages

The following packages have to be installed (if not automatically restored by FactoryTalk Optix)

- Microsoft.Extensions.ObjectPool (Version 8.0.10)
- prometheus-net (Version 8.2.1)
- System.Diagnostics.PerformanceCounter (Version 8.0.1)
- OpenTelemetry (Version 1.3.0)
- OpenTelemetry.Exporter.Console (Version 1.3.0)

## Configuration

### Environment Variables

The following environment variables are used to configure the metrics servers:

- `OTEL_TARGET`: Specifies the OpenTelemetry target address (default: "localhost")
  - Expected value: any IP address or DNS as a string, example: `OTEL_TARGET=192.168.1.10`
- `OTEL_PORT`: Specifies the OpenTelemetry target port (default: "4317")
  - Expected value: any valid TCP port, example: `OTEL_PORT=4317`
- `OTEL_PROTOCOL`: Specifies the OpenTelemetry protocol (default: "Grpc")
  - Expected value: `0` means **Grpc** while any other value means **HttpProtobuf**, example: `OTEL_PROTOCOL=0`
- `PROM_PORT`: Specifies the Prometheus metrics server port (default: "1234")
  - Expected value: any valid TCP port, example: `PROM_PORT=1234`
  
Environment variables are validated at the script startup, check the console output to validate the parameters and to check the initialization status.

### Allow FTOptixRuntime Access to specific ports (Windows only)

To allow FTOptixRuntime access to some TCP ports (like 1234) without administrator privileges, run the following command in the terminal (as Administrator):

```bash
netsh http add urlacl url=http://+:1234/ user=Everyone
```

Make sure to replace `1234` if needed.

### Prometheus Configuration

Add the following scrape configuration to your Prometheus configuration file (`prometheus.yml`):

```txt
scrape_configs:
  - job_name: 'ftoptix_prometheus'
    static_configs:
    - targets: ['ipaddress:1234']
	
  - job_name: 'FTOptix_OpenTelemetry'
    static_configs:
    - targets: ["otel-collector:8888", "otel-collector:8889"]

```

## Metrics Exposed

The following metrics are exposed:

- `FTOptix_Model_Variable1`: Variable1 from Model folder
- `FTOptix_Diagnostics_totalCpuUsagePercent`: Total CPU usage percent
- `FTOptix_Diagnostics_totalRamUsageMegaBytes`: Total RAM utilization in MB
- `FTOptix_Diagnostics_processCpuUsagePercent`: CPU usage percent of the current process
- `FTOptix_Diagnostics_processMemoryUsageMegaBytes`: Memory usage of the current process in MB

## Code Overview

### Metric_Logic Class

- **Start Method**: Initializes and starts the metrics server on port `1234`, configures OpenTelemetry with a console exporter, and starts a periodic task to refresh the metrics.
- **Stop Method**: Disposes of the periodic task and metrics server.
- **UpdateMetricsMethod**: Refreshes the memory and CPU metrics and updates the Prometheus gauges.

### CpuUsage Class

- **GetCpuUsageForProcess**: Retrieves the CPU usage for the current process, with platform-specific implementations for Windows, Linux, and macOS.
- **GetTotalCpuUsage**: Retrieves the total CPU usage, with platform-specific implementations for Windows, Linux, and macOS.

### MemoryUsage Class

- **GetProcessMemoryUsage**: Retrieves the memory usage of the current process.
- **GetTotalMemoryUsage**: Retrieves the total memory usage, with platform-specific implementations for Windows, Linux, and macOS.

## Running the Code

### Running on Windows host

1. Build and run the project using FactoryTalk Optix Studio.

### Running in a Ubuntu 22 Docker container

1. Expand the Save menu, select `Export`, then `FactoryTalk Optix Application` and select `Ubuntu 22.04 x86_64` as target platform
2. Copy the output folder to any machine where **Docker** is installed
3. In the same folder where the `FTOptixApplication` folder was pasted, create a `Dockerfile`
4. Paste the code below

```Dockerfile
FROM ubuntu:22.04
RUN mkdir /root/optix
WORKDIR /root/optix
COPY ./FTOptixApplication/ /root/optix/
RUN apt update && apt install -y libxcb-cursor0
RUN chmod +x /root/optix/FTOptixRuntime
EXPOSE 8080/tcp # WebPresentationEngine port
EXPOSE 1234/tcp # Prometheus exporter port
ENTRYPOINT ["/root/optix/FTOptixRuntime", "-c"]
```

5. Save the file (after changing the ports if needed)
6. Build the container with `docker build -t ftoptix-telemetry:latest .` (please note the `.` at the end of the command)
7. Execute the container with `docker run -itd -p 8080:8080 -p 1234:1234 ftoptix-telemetry`

## Checking the output

### Monitoring with dotnet-counters

You can monitor the metrics using `dotnet-counters`:

```bash
dotnet-counters monitor -n FTOptixRuntime --counters [metric name]
```

Replace `[metric name]` with the name of the metric you want to monitor.

### Access the metrics endpoint using a Web Browser

Access the metrics at `http://ipaddress:1234/metrics` (replace IP address and TCP port as needed).

### Logging

Errors during metric refresh are logged using the `Log.Error` method, these are shown to the FactoryTalk Optix logs.

## Sample **Grafana** + **Prometheus** + **OpenTelemetry** + **FT Optix** stack

This is just an example of how to run an instance of Grafana + Prometheus + OpenTelemetry to see the exported metrics

- **Grafana**: used to create cool dashboards
- **Prometheus**: used to collect metrics and pass it to Grafana
- **Ubuntu**: optional - only used to edit configs
- **OpenTelemetry Collector**: receiving OpenTelemetry data from Optix and passing it to Prometheus using the exporter
- **FT Optix**: sending the telemetry data to OpenTelemetry and exposing the Prometheus endpoint

```yaml
services:     
  grafana:
    image: grafana/grafana
    container_name: grafana
    ports:
      - 3000:3000 # Set TCP port to access dashboards
    restart: unless-stopped
    environment:
      - GF_SECURITY_ADMIN_USER=admin
      - GF_SECURITY_ADMIN_PASSWORD=Password01
    volumes:
      - grafana:/etc/grafana/provisioning/datasources
      - grafana_db:/var/lib/grafana
      
  prometheus:
    image: prom/prometheus
    container_name: prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
    #ports:
      #- 9090:9090 # Open this port only if the container is outside grafana stack
    restart: unless-stopped
    volumes:
      - prom_etc:/etc/prometheus
      - prom_data:/prometheus

  ubuntu:
    # This container is only used to edit the Prometheus configuration
    image: ubuntu:latest
    container_name: grafana_ubuntu
    stdin_open: true
    tty: true
    volumes:
      - prom_etc:/etc/stack/prometheus_etc
      - prom_data:/etc/stack/prometheus_data
      - grafana:/etc/stack/grafana_sources
      - grafana_db:/etc/stack/grafana_db
      - otel_config:/etc/stack/otel_config

  otel-collector:
    image: otel/opentelemetry-collector-contrib
    container_name: otel-collector
    volumes:
      - otel_config:/etc/otelcol-contrib/
    #ports: # Make sure to open only the ports that are needed outside the stack
      #- 1888:1888 # pprof extension
      #- 8888:8888 # Prometheus metrics exposed by the Collector
      #- 8889:8889 # Prometheus exporter metrics
      #- 13133:13133 # health_check extension
      #- 4317:4317 # OTLP gRPC receiver
      #- 4318:4318 # OTLP http receiver
      #- 55679:55679 # zpages extension

  ftoptix:
    # This container was built using the steps above
    image: ftoptix-telemetry:latest
    container_name: grafana_ftoptix
    stdin_open: true
    tty: true
    ports:
      #- 1234:1234 # Only if deployed outside the stack
      - 8080:8080 # As configured in WebPresentationEngine
    environment: # Configure as needed
      - OTEL_TARGET=192.168.1.10
      - OTEL_PORT=4317
      - OTEL_PROTOCOL=0
      - PROM_PORT=1234

volumes:
  grafana:
  grafana_db:
  prom_etc:
  prom_data:
  otel_config:
```

## Disclaimer

Rockwell Automation maintains these repositories as a convenience to you and other users. Although Rockwell Automation reserves the right at any time and for any reason to refuse access to edit or remove content from this Repository, you acknowledge and agree to accept sole responsibility and liability for any Repository content posted, transmitted, downloaded, or used by you. Rockwell Automation has no obligation to monitor or update Repository content

The examples provided are to be used as a reference for building your own application and should not be used in production as-is. It is recommended to adapt the example for the purpose, observing the highest safety standards.
