# Use the .NET 8 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY andy-tools.sln ./
COPY Directory.Build.props ./
COPY src/Andy.Tools/Andy.Tools.csproj ./src/Andy.Tools/
COPY examples/Andy.Tools.Examples/Andy.Tools.Examples.csproj ./examples/Andy.Tools.Examples/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY src/ ./src/
COPY examples/ ./examples/

# Build the application
WORKDIR /src/examples/Andy.Tools.Examples
# Rename ProgramContainer.cs to be the main entry point for container builds
RUN mv Program.cs Program.Interactive.cs && \
    mv ProgramContainer.cs Program.cs
RUN dotnet build -c Release

# Use the .NET 8 runtime image for running
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# Install required dependencies for the tools
RUN apt-get update && \
    apt-get install -y \
    curl \
    jq \
    && rm -rf /var/lib/apt/lists/*

# Copy the built application
COPY --from=build /src/examples/Andy.Tools.Examples/bin/Release/net8.0/ ./

# Create a non-root user
RUN useradd -m -s /bin/bash andytools && \
    chown -R andytools:andytools /app

# Switch to non-root user
USER andytools

# Set environment variables
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8

# Default command runs all examples
ENTRYPOINT ["dotnet", "Andy.Tools.Examples.dll"]
CMD ["all"]