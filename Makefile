# Each of these can be set in the environment or on the command line:
#   VERSION=1.2.0 make push
VERSION   ?= 0.1.0
REGISTRY  ?= docker.io/bbrietzke
PLATFORMS ?= linux/amd64,linux/arm64

SERVER_IMAGE := $(REGISTRY)/thirdlambda-server:$(VERSION)
SERVER_DIR   := ThirdLambda.Server

.DEFAULT_GOAL := help
.PHONY: help build migrate run image push

help: ## Show the available commands
	@awk 'BEGIN {FS = ":.*## "} /^[a-z-]+:.*## / {printf "  %-10s %s\n", $$1, $$2}' $(MAKEFILE_LIST)
	@echo
	@echo "  Image: $(SERVER_IMAGE)"
	@echo "  Platforms: $(PLATFORMS)"

build: ## Compile the solution
	dotnet build ThirdLambda.slnx

# The local targets cd into the project because the app loads appsettings.json from its
# working directory, and `dotnet run --project` would leave that at the repository root.

migrate: ## Apply database migrations to the local SQLite database
	cd $(SERVER_DIR) && dotnet run -- migrate --database sqlite

run: migrate ## Migrate, then run the server locally on SQLite
	cd $(SERVER_DIR) && dotnet run --no-build -- serve --database sqlite

image: ## Build the Linux images for all platforms
	docker buildx build --platform $(PLATFORMS) -f $(SERVER_DIR)/Dockerfile -t $(SERVER_IMAGE) --load .

push: ## Build the Linux images for all platforms and push them
	docker buildx build --platform $(PLATFORMS) -f $(SERVER_DIR)/Dockerfile -t $(SERVER_IMAGE) --push .
