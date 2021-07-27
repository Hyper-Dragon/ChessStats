#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base

WORKDIR /app
ENV CHESSSTATS_DEFAULT_PARAMS="hyper-dragon"
RUN apt-get update
#RUN apt-get install -y apt-utils
RUN apt-get install -y libgdiplus
#RUN ln -s /usr/lib/libgdiplus.so /usr/lib/gdiplus.dll
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["ChessStats/ChessStats.csproj", "ChessStats/"]
COPY ["../../ChessDotComSharp/ChessDotComSharp.csproj", "ChessDotComSharp/"]
RUN dotnet restore "ChessStats/ChessStats.csproj"
COPY . .
WORKDIR "/src/ChessStats"
RUN dotnet build "ChessStats.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ChessStats.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "ChessStats.dll","hyper-dragon"]