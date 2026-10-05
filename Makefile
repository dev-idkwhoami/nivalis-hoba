.DEFAULT_GOAL := help
GAME_PATH ?= $(NIVALIS_GAME_PATH)
DOTNET ?= dotnet
CONFIGURATION ?= Release
export GAME_PATH DOTNET CONFIGURATION
export DOTNET_CLI_TELEMETRY_OPTOUT := 1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE := 1
export DOTNET_CLI_HOME := $(CURDIR)/.tools/cli-home
export NUGET_PACKAGES ?= $(CURDIR)/.tools/nuget

.PHONY: help check-sdk check-game check-dependencies build test model catalog install package clean
help:
	@printf '%s\n' 'make build GAME_PATH="..."  Build HOBA against an initialized BepInEx game' 'make test                   Run managed checks without the game' 'make model                  Export base OBJ/MTL geometry' 'make catalog                Export model catalogue OBJ/MTL geometry' 'make package GAME_PATH="..." Build, test and package both DLLs' 'make install GAME_PATH="..." Build, test and install both DLLs' 'make clean                  Remove build output' 'Overrides: DOTNET=/path/to/dotnet CONFIGURATION=Release'
check-sdk:
	@command -v "$$DOTNET" >/dev/null 2>&1 || { echo '.NET SDK 8 or newer is required; set DOTNET if not on PATH.' >&2; exit 1; }
check-game:
	@test -n "$$GAME_PATH" && test -f "$$GAME_PATH/BepInEx/interop/Assembly-CSharp.dll" || { echo 'Set GAME_PATH to a game installation initialized with BepInEx IL2CPP.' >&2; exit 1; }
check-dependencies:
	@cd dependencies && sha256sum --check SHA256SUMS
build: check-sdk check-game check-dependencies
	@"$$DOTNET" build src/Hoba.csproj -c "$$CONFIGURATION" --nologo "-p:GamePath=$$(cd "$$GAME_PATH" && pwd)"
test: check-sdk
	@"$$DOTNET" run --project tests/Checks.csproj -c "$$CONFIGURATION"
model: check-sdk
	@"$$DOTNET" run --project tests/Checks.csproj -c "$$CONFIGURATION" -- --export bin/models/base
catalog: check-sdk
	@"$$DOTNET" run --project tests/Checks.csproj -c "$$CONFIGURATION" -- --export-catalog bin/models/catalog
install: build test
	@bash tools/artifacts.sh install
package: build test test-config
	@bash tools/artifacts.sh package
clean:
	@bash tools/artifacts.sh clean

.PHONY: check-game-assets
check-game-assets: check-sdk check-game
	@"$$DOTNET" run --project tests/Checks.csproj -c "$$CONFIGURATION" -- --check-game-assets "$$GAME_PATH/Nivalis Nights_Data"

.PHONY: test-config
test-config: check-sdk check-game
	@"$$DOTNET" run --project tools/ConfigChecks/ConfigChecks.csproj -c "$$CONFIGURATION" "-p:GamePath=$$(cd "$$GAME_PATH" && pwd)"
