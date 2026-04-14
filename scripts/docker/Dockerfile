FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /workspace
# Copy all sources into the image at build time — no bind mount required at runtime.
COPY . .
# Pre-build the full solution so test runs can use --no-build inside the container.
RUN dotnet restore CometBFT.Client.sln --locked-mode
RUN dotnet build CometBFT.Client.sln --configuration Release --no-restore -maxcpucount:1
