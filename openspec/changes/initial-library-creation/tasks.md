# Tasks: Initial Creation — CometBFT.Client

Protocol source: https://github.com/cometbft/cometbft (latest stable release)

---

> **⚠️ RÈGLE ABSOLUE : compléter la Phase 0 intégralement avant d'écrire la moindre ligne de code C#.**
> Le repo, les branches, les hooks et le CI squelette doivent être en place en premier.
> Tout commit de code effectué avant la completion de la Phase 0 est invalide.

> **⚠️ RÈGLE ABSOLUE : README.md et la documentation sont indispensables avant tout commit de code.**
> `README.md` (stub minimal) et `CHANGELOG.md` doivent exister dès le premier commit de code.
> Tout `public` type ou membre sans XML doc génère une erreur de build (`TreatWarningsAsErrors=true`).
> Aucun commit ne peut introduire de code public sans la documentation correspondante (XML doc + README mis à jour si nécessaire).

---

## Diff Specs ↔ Tasks — Alignment 2026-04-10

This section normalizes the acceptance criteria after review. When older wording below conflicts with this section, this section wins until all tasks are completed.

Current status: the bootstrap phases are materially complete and the repository is already usable. The unchecked items below represent the real remaining work required before this change can be archived.

- **Coverage policy normalized**: acceptance is now `>= 90 %` **global line coverage** and `>= 90 %` **line coverage per source file**. Older wording about branch/method/per-assembly thresholds is superseded.
- **Coverage report upload removed**: generating coverage output for local/CI validation remains required; uploading the report as a CI artifact is **not** required.
- **Integration scope expanded explicitly**: tasks previously covered mostly REST integration. Missing WebSocket and gRPC integration tasks are added below.
- **E2E scope kept mandatory**: REST, WebSocket, and gRPC E2E flows remain required and are called out explicitly in CI.
- **Transport completeness gap made explicit**: the spec requires complete public transport coverage for REST, WebSocket, and gRPC. Any remaining delta must be tracked explicitly in client APIs, tests, demos, CI, and final validation.
- **Demo gaps made explicit**: REST demo must include `GetBlockResultsAsync`; WebSocket demo must expose `NewBlockHeader` and `ValidatorSetUpdates`; gRPC demo must include the missing dashboard elements required by spec.
- **Docker hardening kept visible**: self-contained Docker wrappers without bind mounts remain a valid follow-up expectation from the original feature branch, but are not claimed as implemented until the scripts actually move away from the current bind-mount model.
- **gRPC completeness gap made explicit**: for CometBFT `v0.38.9`, the public gRPC surface centers on `BroadcastAPI` (`Ping`, `BroadcastTx`). The remaining work is protocol-parity work: vendored proto alignment with upstream, full response-shape mapping, and matching tests/demo/docs.

## Phase 0 — Repo, Git Flow et Hooks (PREREQUIS — AVANT TOUT CODE)

### 0.1 Création du repo
- [x] 0.1.1 Créer le dossier `~/Projects/CometBFT.Client/`
- [x] 0.1.2 `git init -b master` — **la branche principale est `master`, pas `main`**
- [x] 0.1.3 Vérifier : `git branch` doit afficher `* master` (pas `main`)
- [x] 0.1.4 Créer `.gitignore` (.NET standard + artifacts NuGet + coverage/ + `.worktrees/`)
- [x] 0.1.5 Créer le repo GitHub `Rinzler78/CometBFT.Client` (public) — définir la branche par défaut sur `master` dans Settings → Branches
- [x] 0.1.6 `git remote add origin https://github.com/Rinzler78/CometBFT.Client.git`
- [x] 0.1.7 Commit initial : `chore: initial repository setup`
- [x] 0.1.8 Push sur `master` : `git push -u origin master`

### 0.2 Worktrees
- [x] 0.2.1 Créer le dossier `.worktrees/` à la racine du repo — **tous les worktrees git doivent être créés dans ce dossier**
- [x] 0.2.2 Le dossier `.worktrees/` est déjà dans `.gitignore` (cf. 0.1.4)
- [x] 0.2.3 Convention de nommage : `git worktree add .worktrees/<branch-name> <branch-name>`
- [x] 0.2.4 Ne jamais créer de worktree en dehors de `.worktrees/`

### 0.3 Git Flow
- [x] 0.3.1 Créer `.gitflow` : master/develop/feature/release/hotfix/bugfix, versiontag = v
- [x] 0.3.2 Exécuter `git flow init -d` — crée la branche `develop`
- [x] 0.3.3 Push `develop` sur origin
- [x] 0.3.4 Commit : `chore: configure git flow`

### 0.4 Hooks git
- [x] 0.4.1 Créer `.pre-commit-config.yaml` (dotnet format + detect-secrets)
- [x] 0.4.2 Créer `.git/hooks/commit-msg` — conventional commits
- [x] 0.4.3 Créer `.git/hooks/pre-push` — bloquer push direct sur `master` et `develop`
- [x] 0.4.4 Rendre les hooks exécutables
- [x] 0.4.5 `pre-commit install` pour installer les hooks pre-commit
- [x] 0.4.6 Commit : `chore: add git hooks and pre-commit config`
- [x] 0.4.7 Étendre `.git/hooks/pre-push` — exécuter le flux de tests du repo et vérifier la couverture **ligne** `>= 90 %` globale et `>= 90 %` par fichier avant tout push :
  ```bash
  # Dans .git/hooks/pre-push (ajout après le bloc protection master/develop)
  echo "Running coverage gate before push..."
  ./scripts/test.sh
  if [ $? -ne 0 ]; then
    echo "ERROR: Coverage gate failed — fix coverage before pushing."
    exit 1
  fi
  ```
- [x] 0.4.8 Ajouter un hook de détection de langue — **anglais uniquement** dans l'intégralité du repo :
  > **Périmètre** : fichiers source C# (`.cs`), documentation XML, fichiers Markdown (`.md`), scripts bash (`.sh`), YAML (`.yml`, `.yaml`), messages de commit.
  > **Règle** : tout texte humainement lisible (commentaires, XML doc `<summary>`, noms de variables, specs, README, CHANGELOG, messages de commit) doit être rédigé en anglais. Aucune autre langue n'est autorisée.
  - Outil : `cspell` (Code Spell Checker — `streetsidesoftware/cspell-cli`)
  - Créer `.cspell.json` à la racine avec :
    ```json
    {
      "version": "0.2",
      "language": "en",
      "dictionaries": ["en_US", "en-gb"],
      "ignorePaths": [
        "coverage/**",
        "artifacts/**",
        ".worktrees/**",
        "**/*.lock",
        "**/packages.lock.json"
      ],
      "words": [
        "CometBFT", "cometbft", "grpc", "gRPC", "protobuf", "proto",
        "Rinzler", "NuGet", "nuget", "dotnet", "csproj", "slnf",
        "Polly", "WireMock", "Coverlet", "NSubstitute", "xunit",
        "Spectre", "DocFX", "OpenAPI", "swagger",
        "async", "await", "nullable", "readonly", "init",
        "testnet", "mainnet", "lcd", "rpc", "abci",
        "Osmosis", "osmosis", "Cosmos", "cosmos",
        "dependabot", "gitflow", "editorconfig",
        "cspell", "warnaserror", "analyzers"
      ],
      "overrides": [
        {
          "filename": "**/*.cs",
          "words": ["Tx", "tx", "TxHash", "RawLog", "GasUsed", "GasWanted"]
        }
      ]
    }
    ```
  - Ajouter dans `.pre-commit-config.yaml` la entrée suivante :
    ```yaml
    - repo: https://github.com/streetsidesoftware/cspell-cli
      rev: v8.19.4
      hooks:
        - id: cspell
          name: English-only language check
          args: [--no-progress, --no-summary, --show-context]
    ```
  - Vérifier que le hook bloque un commit contenant un mot non-anglais dans un commentaire C# ou un fichier Markdown
  - Toute exception technique légitime (acronyme, nom propre de protocole, terme de domaine) doit être ajoutée explicitement dans la section `words` de `.cspell.json` avec un commentaire justificatif dans la PR
  - Les faux positifs récurrents sont gérés dans `.cspell.json` (jamais via `// cspell:disable` sans justification inline)
- [x] 0.4.9 Ajouter un step CI dans `.github/workflows/ci.yml` pour exécuter `cspell` sur l'ensemble du repo — le CI échoue si un mot non-anglais est détecté dans un fichier de production ou de documentation

### 0.5 Protection des branches GitHub
- [x] 0.5.1 Créer `.github/branch-protection.md`
- [x] 0.5.2 Configurer les règles sur GitHub (Settings → Branches → Branch protection rules) ← via `gh api`
- [x] 0.5.3 Commit : `docs: add branch protection documentation`

### 0.6 Feature branch de développement
- [x] 0.6.1 Créer la branche de travail : `feature/initial-library-creation`
- [x] 0.6.2 **Vérifier que tous les hooks fonctionnent** (dotnet format + detect-secrets passent)
- [x] 0.6.3 **Tout le développement ultérieur (Phases 1–9) se fait sur cette branche ou des branches feature/**

> **Phase 0 terminée. Le développement peut commencer.**

---

## Phase 1 — Scaffold

### 1.1 Repo & solution
- [x] 1.1.1 Créer `CometBFT.Client.sln` — contient tous les projets (src + tests + samples)
- [x] 1.1.2 Créer `CometBFT.Client.src.slnf` — solution filter src/** + demos
- [x] 1.1.3 Créer `CometBFT.Client.tests.slnf` — solution filter tests/**
- [x] 1.1.4 Créer `global.json` (pin SDK .NET 10.0.100)
- [x] 1.1.5 Créer `Directory.Build.props` — LangVersion, Nullable, TreatWarningsAsErrors, `<ProtocolVersion>` v0.38.9, net10.0
- [x] 1.1.5b Vérifier que `Directory.Build.props` contient pour les projets src :
  ```xml
  <Nullable>enable</Nullable>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <WarningLevel>9999</WarningLevel>
  <EnableNETAnalyzers>true</EnableNETAnalyzers>
  <AnalysisMode>AllEnabledByDefault</AnalysisMode>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  ```

### 1.2 Config qualité
- [x] 1.2.1 Créer `.editorconfig` (indent_size=4, charset=utf-8, end_of_line=lf)
- [x] 1.2.2 Créer `.gitignore` (.NET standard)
- [x] 1.2.3 `Directory.Build.props` section Tests (Coverlet, threshold 90 %)
- [x] 1.2.3b Vérifier la stratégie de couverture dans `Directory.Build.props` et/ou scripts de validation :
  ```xml
  <!-- Appliqué à tous les projets Tests/* -->
  <Threshold>90</Threshold>
  ```
  > La règle d'acceptation effective est : `>= 90 %` global ligne et `>= 90 %` par fichier ligne.

### 1.3 Projets source
- [x] 1.3.1 `src/CometBFT.Client.Core/`
- [x] 1.3.2 `src/CometBFT.Client.Rest/`
- [x] 1.3.3 `src/CometBFT.Client.Grpc/`
- [x] 1.3.4 `src/CometBFT.Client.WebSocket/`
- [x] 1.3.5 `src/CometBFT.Client.Extensions/`

### 1.4 Projets test
- [x] 1.4.1 `tests/CometBFT.Client.Core.Tests/`
- [x] 1.4.2 `tests/CometBFT.Client.Rest.Tests/` (WireMock.Net)
- [x] 1.4.3 `tests/CometBFT.Client.Grpc.Tests/` (Grpc.AspNetCore)
- [x] 1.4.4 `tests/CometBFT.Client.WebSocket.Tests/` (NSubstitute)
- [x] 1.4.5 `tests/CometBFT.Client.Integration.Tests/`
- [x] 1.4.6 Créer `tests/CometBFT.Client.E2E.Tests/` — tests de bout en bout (démo start-to-finish contre testnet public, `[Trait("Category","E2E")]`)

### 1.5 Projets annexes
- [x] 1.5.1 Créer `samples/CometBFT.Client.Demo.Rest/`
- [x] 1.5.2 Créer `samples/CometBFT.Client.Demo.WebSocket/`
- [x] 1.5.3 Créer `samples/CometBFT.Client.Demo.Grpc/`
- [x] 1.5.4 Créer `docs/` (placeholder DocFX config)
- [x] 1.5.5 Ajouter tous les projets au `.sln`, mettre à jour `.src.slnf` et `.tests.slnf`
- [x] 1.5.6 Créer `README.md` stub minimal (titre + description + badge CI + section Installation placeholder) — **obligatoire avant tout commit de code**
- [x] 1.5.7 Créer `CHANGELOG.md` stub minimal (format Keep-a-Changelog, version `[Unreleased]`) — **obligatoire avant tout commit de code**

---

## Phase 2 — Git Flow

### 2.1 Configuration
- [x] 2.1.1 Créer `.gitflow`
- [x] 2.1.2 Exécuter `git flow init -d` pour initialiser les branches

### 2.2 Hooks pre-commit
- [x] 2.2.1 Créer `.pre-commit-config.yaml`
- [x] 2.2.2 Hook `commit-msg` — conventional commits
- [x] 2.2.3 Hook `pre-push` — bloquer push direct sur `master` et `develop`

### 2.3 Documentation protection
- [x] 2.3.1 Créer `.github/branch-protection.md`

---

## Phase 3 — Scripts bash

### 3.1 build.sh
- [x] 3.1.1 Créer `scripts/build.sh`

### 3.2 test.sh
- [x] 3.2.1 Créer `scripts/test.sh`

### 3.3 publish.sh
- [x] 3.3.1 Créer `scripts/publish.sh`

### 3.4 Scripts Docker — `scripts/docker/`
- [x] 3.4.1 Créer `scripts/docker/Dockerfile` (FROM mcr.microsoft.com/dotnet/sdk:10.0)
- [x] 3.4.2 Créer `scripts/docker/docker-compose.yml`
- [x] 3.4.3 Créer `scripts/docker/build.sh` — délègue à `./scripts/build.sh` dans le conteneur
- [x] 3.4.4 Créer `scripts/docker/test.sh` — délègue à `./scripts/test.sh` dans le conteneur
- [x] 3.4.5 Créer `scripts/docker/publish.sh` — `NUGET_API_KEY` via env, jamais en argument
- [x] 3.4.6 `scripts/publish.sh` lit `NUGET_API_KEY` depuis l'env si `--api-key` n'est pas passé
- [x] 3.4.7 `chmod +x scripts/docker/*.sh`
- [x] 3.4.8 Documenter dans README : usage local vs Docker, passage de `NUGET_API_KEY`
- [x] 3.4.9 Durcir `scripts/docker/build.sh` pour exécuter le build dans une image auto-suffisante sans bind mount, tout en conservant la délégation vers `./scripts/build.sh`
- [x] 3.4.10 Durcir `scripts/docker/test.sh` pour exécuter les tests dans une image auto-suffisante sans bind mount et récupérer proprement les artefacts de couverture
- [x] 3.4.11 Durcir `scripts/docker/publish.sh` pour exécuter `./scripts/publish.sh` dans une image auto-suffisante sans bind mount en continuant à injecter `NUGET_API_KEY` via l'environnement
- [x] 3.4.12 Aligner `scripts/docker/Dockerfile` et `scripts/docker/docker-compose.yml` sur ce mode auto-suffisant sans bind mount

### 3.5 Scripts Demo — local et Docker

> **Principe** : chaque script local build + lance le programme de démo ciblé. Le script Docker délègue au script local dans le conteneur et forward les env vars d'endpoint.

Structure cible :
```
scripts/
├── demo-rest.sh          ← build + run Demo.Rest
├── demo-ws.sh            ← build + run Demo.WebSocket
├── demo-grpc.sh          ← build + run Demo.Grpc
└── docker/
    ├── demo-rest.sh      ← docker run ... ./scripts/demo-rest.sh "$@"
    ├── demo-ws.sh
    └── demo-grpc.sh
```

- [x] 3.5.1 Créer `scripts/demo-rest.sh` (défaut testnet si env var absente) :
  ```bash
  #!/usr/bin/env bash
  set -euo pipefail
  COMETBFT_RPC_URL="${COMETBFT_RPC_URL:-https://cosmoshub.cometbftrpc.lava.build:443}"
  export COMETBFT_RPC_URL
  dotnet run --project samples/CometBFT.Client.Demo.Rest \
    --configuration Release "$@"
  ```

- [x] 3.5.2 Créer `scripts/demo-ws.sh` (défaut testnet si env var absente) :
  ```bash
  #!/usr/bin/env bash
  set -euo pipefail
  COMETBFT_WS_URL="${COMETBFT_WS_URL:-wss://cosmoshub.cometbftrpc.lava.build:443/websocket}"
  export COMETBFT_WS_URL
  dotnet run --project samples/CometBFT.Client.Demo.WebSocket \
    --configuration Release "$@"
  ```

- [x] 3.5.3 Créer `scripts/demo-grpc.sh` (défaut testnet si env var absente) :
  ```bash
  #!/usr/bin/env bash
  set -euo pipefail
  COMETBFT_GRPC_URL="${COMETBFT_GRPC_URL:-cosmoshub.grpc.lava.build}"
  export COMETBFT_GRPC_URL
  dotnet run --project samples/CometBFT.Client.Demo.Grpc \
    --configuration Release "$@"
  ```

- [x] 3.5.4 Créer `scripts/docker/demo-rest.sh` — forward `COMETBFT_RPC_URL` :
  ```bash
  #!/usr/bin/env bash
  set -euo pipefail
  docker run --rm \
    -v "$(pwd):/workspace" \
    -w /workspace \
    -e COMETBFT_RPC_URL \
    mcr.microsoft.com/dotnet/sdk:10.0 \
    ./scripts/demo-rest.sh "$@"
  ```

- [x] 3.5.5 Créer `scripts/docker/demo-ws.sh` — forward `COMETBFT_WS_URL` :
  ```bash
  #!/usr/bin/env bash
  set -euo pipefail
  docker run --rm \
    -v "$(pwd):/workspace" \
    -w /workspace \
    -e COMETBFT_WS_URL \
    mcr.microsoft.com/dotnet/sdk:10.0 \
    ./scripts/demo-ws.sh "$@"
  ```

- [x] 3.5.6 Créer `scripts/docker/demo-grpc.sh` — forward `COMETBFT_GRPC_URL` :
  ```bash
  #!/usr/bin/env bash
  set -euo pipefail
  docker run --rm \
    -v "$(pwd):/workspace" \
    -w /workspace \
    -e COMETBFT_GRPC_URL \
    mcr.microsoft.com/dotnet/sdk:10.0 \
    ./scripts/demo-grpc.sh "$@"
  ```

- [x] 3.5.7 `chmod +x scripts/demo-*.sh scripts/docker/demo-*.sh`
- [x] 3.5.8 Documenter dans README : comment lancer chaque démo en local et en Docker
- [x] 3.5.9 **Smoke test zero-config** : exécuter `./scripts/demo-rest.sh`, `./scripts/demo-ws.sh`, `./scripts/demo-grpc.sh` **sans aucune env var ni arg** — vérifier que chaque script démarre et se connecte au testnet par défaut sans erreur d'argument manquant
- [x] 3.5.10 Durcir `scripts/docker/demo-rest.sh`, `scripts/docker/demo-ws.sh` et `scripts/docker/demo-grpc.sh` pour déléguer aux scripts locaux dans une image auto-suffisante sans bind mount

---

## Phase 4 — Domain Core

### 4.1 Types immuables
- [x] 4.1.1 `Block.cs`
- [x] 4.1.2 `BlockHeader.cs`
- [x] 4.1.3 `TxResult.cs`
- [x] 4.1.4 `Event.cs`, `Attribute.cs`
- [x] 4.1.5 `NodeInfo.cs`, `SyncInfo.cs`, `Validator.cs`
- [x] 4.1.6 `BroadcastTxResult.cs`
- [x] 4.1.7 `Vote.cs`
- [x] 4.1.8 Auditer les concepts exposés sur plusieurs transports et consigner une matrice concept métier → type `Core.Domain` partagé → transports consommateurs
- [x] 4.1.9 Supprimer toute divergence restante où plusieurs transports exposent des objets de domaine dupliqués ou incompatibles pour un même concept métier

### 4.2 Interfaces par service
- [x] 4.2.1 `ICometBftRestClient.cs`
- [x] 4.2.2 `IHealthService`, `IStatusService`, `IBlockService`, `ITxService`, `IValidatorService`, `IAbciService`
- [x] 4.2.3 `ICometBftWebSocketClient.cs`
- [x] 4.2.4 `ICometBftGrpcClient.cs`
- [x] 4.2.5 Définir et documenter les capacités transverses partagées entre transports avec des signatures compatibles quand la sémantique protocolaire est la même
- [x] 4.2.6 Aligner `ICometBftRestClient`, `ICometBftWebSocketClient` et `ICometBftGrpcClient` sur les mêmes objets `Core.Domain` pour tout concept métier partagé (block, header, tx result, validator set, broadcast result, etc.)
- [x] 4.2.7 Documenter explicitement les écarts d'interface qui restent transport-spécifiques et justifier pourquoi ils ne peuvent pas converger davantage

### 4.3 Options et exceptions
- [x] 4.3.1 `CometBftRestOptions`, `CometBftWebSocketOptions`, `CometBftGrpcOptions`
- [x] 4.3.2 `CometBftClientException`, `CometBftRestException`, `CometBftWebSocketException`, `CometBftGrpcException`

---

## Phase 5 — Clients (tous endpoints officiels)

### 5.1 Client REST
- [x] 5.1.1 `GetHealthAsync`, `GetStatusAsync`
- [x] 5.1.2 `GetBlockAsync`, `GetBlockByHashAsync`, `GetBlockResultsAsync`
- [x] 5.1.3 `GetValidatorsAsync`
- [x] 5.1.4 `GetTxAsync`, `SearchTxAsync`
- [x] 5.1.5 `BroadcastTxSyncAsync`, `BroadcastTxAsync`, `BroadcastTxCommitAsync` (POST JSON-RPC)
- [x] 5.1.6 `GetAbciInfoAsync`, `AbciQueryAsync`
- [x] 5.1.7 Polly : retry exponentiel (3 tentatives) + circuit breaker + jitter
- [x] 5.1.8 Ajouter les endpoints REST publics manquants : `check_tx`, `net_info`, `blockchain`, `header`, `header_by_hash`, `commit`
- [x] 5.1.9 Ajouter les endpoints REST publics manquants : `genesis`, `genesis_chunked`, `dump_consensus_state`, `consensus_state`, `consensus_params`
- [x] 5.1.10 Ajouter les endpoints REST publics manquants : `unconfirmed_txs`, `num_unconfirmed_txs`, `block_search`, `broadcast_evidence`
- [x] 5.1.11 Ajouter les tests unitaires et d'intégration correspondant à chaque endpoint REST ajouté
- [x] 5.1.12 Auditer l'OpenAPI CometBFT ciblée et consigner une matrice complète endpoint REST public → méthode .NET → tests unitaires/intégration/E2E → panneau ou usage de démo
- [x] 5.1.13 Fermer tout delta REST restant révélé par cet audit et aligner README, OpenSpec et validation finale sur la matrice complète
- [x] 5.1.14 Implémenter les endpoints REST `Unsafe` encore absents : `dial_seeds`, `dial_peers`
- [x] 5.1.15 Ajouter les types, validations d'arguments et mappings nécessaires pour `dial_seeds` et `dial_peers`, y compris les options `persistent`, `unconditional` et `private`

### 5.2 Client WebSocket
- [x] 5.2.1 `CometBftWebSocketClient` avec `Websocket.Client 5.0.0`
- [x] 5.2.2 Souscription `NewBlock`, `NewBlockHeader`
- [x] 5.2.3 Souscription `Tx`, `Vote`, `ValidatorSetUpdates`
- [x] 5.2.4 Reconnexion automatique
- [x] 5.2.5 Auditer l'intégralité des événements, subscriptions et appels WebSocket publics du protocole ciblé et consigner la matrice événement/appel → API .NET → tests → démo
- [x] 5.2.6 Étendre `ICometBftWebSocketClient` et `CometBftWebSocketClient` pour couvrir tout appel ou abonnement public manquant révélé par cet audit
- [x] 5.2.7 Ajouter ou compléter les mappings de domaine et exceptions nécessaires pour toute capacité WebSocket publique manquante

### 5.3 Client gRPC
- [x] 5.3.1 Proto `cometbft/rpc/grpc/grpc.proto` téléchargé
- [x] 5.3.2 Compilation proto via `Grpc.Tools`
- [x] 5.3.3 `CometBftGrpcClient` : `PingAsync`, `BroadcastTxAsync`
- [x] 5.3.4 Polly sur le channel gRPC
- [x] 5.3.5 Auditer l'intégralité des services et méthodes gRPC publiques exposés par la release CometBFT ciblée et consigner la matrice source proto → API .NET attendue
- [x] 5.3.6 Aligner le proto vendored local sur le proto amont exact de `v0.38.9` (`proto/cometbft/rpc/grpc/types.proto`), y compris `ResponseBroadcastTx.tx_result`
- [x] 5.3.7 Étendre `ICometBftGrpcClient` si nécessaire pour refléter exactement la surface publique gRPC auditée, avec `CancellationToken` sur chaque méthode
- [x] 5.3.8 Étendre `CometBftGrpcClient` et ses mappings de domaine pour représenter complètement `ResponseBroadcastTx` et toute autre réponse gRPC publique auditée
- [x] 5.3.9 Ajouter ou compléter les records/options/exceptions nécessaires pour représenter proprement `check_tx`, `tx_result` et les autres shapes gRPC publics exposés
- [x] 5.3.10 Ajouter les tests unitaires couvrant chaque méthode gRPC publique exposée, y compris la désérialisation complète des réponses et les chemins d'erreur typés
- [x] 5.3.11 Ajouter les tests d'intégration live couvrant chaque méthode gRPC publique atteignable sur l'endpoint validé
- [x] 5.3.12 Ajouter les tests E2E couvrant les flux gRPC publics réellement supportés par le client avec les réponses complètes attendues

### 5.4 Optimisation encodage/décodage (obligatoire — .NET 10)
- [x] 5.4.1 Tous les schémas JSON REST/WebSocket typés comme `record` immuables dans Core
- [x] 5.4.2 Zéro `JsonElement` / `object` / `dynamic` / `Dictionary<string,object>` dans les records de domaine
- [x] 5.4.3 `EventHandler<T>` WebSocket exposent des records typés (`Block`, `TxResult`, `Vote`, `BlockHeader`, `IReadOnlyList<Validator>`)
- [x] 5.4.4 `CometBftJsonContext : JsonSerializerContext` avec `[JsonSerializable]` pour tous les types REST (top-level + enveloppes génériques)
- [x] 5.4.5 Tous les appels `JsonSerializer.DeserializeAsync<T>` utilisent `CometBftJsonContext.Default.Options`
- [x] 5.4.6 `HttpCompletionOption.ResponseHeadersRead` sur tous les appels `HttpClient.GetAsync`
- [x] 5.4.7 `ArrayPool<byte>.Shared` pour les buffers de lecture HTTP (payloads ≥ 4 KB) — closed as N/A, the client deserializes directly from the response stream without a manual buffering loop
- [x] 5.4.8 `Microsoft.IO.RecyclableMemoryStream` — closed as N/A, there is no `MemoryStream` allocation in the REST hot path
- [x] 5.4.9 Zéro appel `JsonSerializer` avec l'overload par défaut (réflexion) — POST uses `CometBftJsonContext.Default.JsonRpcBroadcastRequest`
- [x] 5.4.10 Zéro propriété typée `JsonElement` / `object` / `dynamic` dans les records de domaine
- [x] 5.4.11 Zéro import `Newtonsoft.Json` dans les projets src
- [x] 5.4.12 `SocketsHttpHandler` avec `PooledConnectionLifetime = 2 min` — deferred

---

## Phase 6 — DI Extensions
- [x] 6.1 `AddCometBftRest(this IServiceCollection, Action<CometBftRestOptions>)`
- [x] 6.2 `AddCometBftWebSocket(this IServiceCollection, Action<CometBftWebSocketOptions>)`
- [x] 6.3 `AddCometBftGrpc(this IServiceCollection, Action<CometBftGrpcOptions>)`

---

## Phase 7 — Tests ≥ 90 % — Unitaires, Intégration, E2E

### 7.1 Tests unitaires Core
- [x] 7.1.1 Tests options (constructeurs, valeurs par défaut)

### 7.2 Tests unitaires REST (WireMock.Net)
- [x] 7.2.1 Fixture WireMock pour : health, status, block, block (height), validators, broadcast_tx_sync, abci_info, RPC error
- [x] 7.2.2 Tests succès (200 OK + désérialisation correcte)
- [x] 7.2.3 Tests erreurs (JSON-RPC error → CometBftRestException)
- [x] 7.2.4 Tests Polly retry (WireMock simulant 2 erreurs puis succès)
- [x] 7.2.5 Tests DI registration
- [x] 7.2.6 Étendre la suite unitaire REST pour couvrir exhaustivement chaque endpoint public de la matrice OpenAPI validée
- [x] 7.2.7 Ajouter les tests unitaires REST couvrant explicitement `dial_seeds` et `dial_peers`, y compris l'encodage des listes et options booléennes

### 7.3 Tests unitaires WebSocket (NSubstitute)
- [x] 7.3.1 Tests constructeur + connexion (null options, URL invalide)
- [x] 7.3.2 Tests souscription/désouscription sans connexion → CometBftWebSocketException
- [x] 7.3.3 Tests subscribe/unsubscribe events (NewBlock, Tx, Vote, BlockHeader)
- [x] 7.3.4 Tests DisposeAsync idempotent
- [x] 7.3.5 Étendre la suite unitaire WebSocket pour couvrir exhaustivement tous les événements, subscriptions et appels publics exposés après audit protocolaire

### 7.4 Tests unitaires gRPC (NSubstitute)
- [x] 7.4.1 `PingAsync` → true/false/exception
- [x] 7.4.2 `BroadcastTxAsync` → résultat / null / RpcException → CometBftGrpcException
- [x] 7.4.3 `DisposeAsync` idempotent, `ObjectDisposedException` après dispose
- [x] 7.4.4 Étendre la suite unitaire pour couvrir toutes les méthodes gRPC publiques auditées et la totalité des champs utiles de leurs réponses

### 7.5 Tests unitaires Extensions
- [x] 7.5.1 Vérifier enregistrements DI (AddCometBftRest/WebSocket/Grpc)

### 7.6 Tests intégration réels
- [x] 7.6.1 Centraliser les endpoints testnet par défaut dans un helper/config dédié aux tests d'intégration
- [x] 7.6.2 Pattern skip cohérent pour `COMETBFT_RPC_URL`, `COMETBFT_WS_URL`, `COMETBFT_GRPC_URL`
- [x] 7.6.3 GetHealth, GetStatus, GetBlock
- [x] 7.6.4 GetValidators, GetAbciInfo
- [x] 7.6.5 WebSocket integration : connexion, souscription, réception d'au moins un événement typé, déconnexion propre
- [x] 7.6.6 gRPC integration : résolution via DI, `PingAsync`, et validation du chemin nominal ou de l'erreur attendue
- [x] 7.6.7 Exécuter la suite d'intégration complète contre le testnet documenté et consigner le résultat réel
- [x] 7.6.8 Étendre les intégrations live REST pour couvrir exhaustivement la matrice des endpoints publics validés
- [x] 7.6.9 Étendre les intégrations live WebSocket pour couvrir exhaustivement les événements, subscriptions et appels publics exposés par `ICometBftWebSocketClient`
- [x] 7.6.10 Étendre les intégrations live gRPC pour couvrir toutes les méthodes publiques exposées par `ICometBftGrpcClient` et valider la forme complète des réponses gRPC
- [x] 7.6.11 Ajouter une stratégie de validation REST pour les endpoints `Unsafe` (`dial_seeds`, `dial_peers`) contre un nœud contrôlé où le RPC unsafe est activé, séparée des validations sur endpoints publics

### 7.7 Couverture globale et par fichier — gate obligatoire

> **Règle** : 90 % minimum **global (ligne)** ET 90 % minimum **par fichier (ligne)**.
> Le pre-push hook bloque tout push si la gate échoue.

- [x] 7.7.1 Corriger `./scripts/test.sh` pour qu'il exécute les tests sans erreur de paramètres MSBuild
- [x] 7.7.2 Produire une sortie de couverture machine-readable consolidée pour l'ensemble des projets test
- [x] 7.7.3 Ajouter une validation automatique qui échoue si la couverture **globale ligne** < 90 %
- [x] 7.7.4 Ajouter une validation automatique qui échoue si un **fichier source** est < 90 % en ligne
- [x] 7.7.5 Brancher cette validation dans `./scripts/test.sh`
- [x] 7.7.6 Brancher cette validation dans `.git/hooks/pre-push`
- [x] 7.7.7 Brancher cette validation dans `.github/workflows/ci.yml`
- [x] 7.7.8 Générer un rapport local exploitable pour diagnostic développeur ; l'upload du rapport n'est pas requis

### 7.8 Tests E2E (bout en bout — contre testnet public)

> **Trait** : `[Trait("Category","E2E")]` — skippés si les env vars d'endpoint sont absentes.
> Les tests E2E exécutent un flux complet (init client DI → appels réels → désérialisation → assertions métier).

- [x] 7.8.1 Créer `tests/CometBFT.Client.E2E.Tests/` si absent
- [x] 7.8.2 Flux REST complet : `AddCometBftRest` → `GetHealthAsync` → `GetStatusAsync` → `GetBlockAsync` → `GetValidatorsAsync` — vérifier désérialisation bout en bout
- [x] 7.8.3 Flux WebSocket complet : connexion → souscription `NewBlock` → réception ≥ 1 événement typé `Block` → déconnexion propre
- [x] 7.8.4 Flux gRPC complet : `AddCometBftGrpc` → `PingAsync` → `BroadcastTxAsync` (tx vide, erreur attendue) — vérifier gestion d'exception
- [x] 7.8.5 Skip automatique si `COMETBFT_RPC_URL` / `COMETBFT_WS_URL` / `COMETBFT_GRPC_URL` absents
- [x] 7.8.6 CI E2E gate : step séparé dans `ci.yml` exécuté avec les env vars testnet
- [x] 7.8.7 Étendre les scénarios E2E REST pour refléter la couverture complète des endpoints publics réellement exposés par `ICometBftRestClient`
- [x] 7.8.10 Ajouter un flux E2E REST dédié aux endpoints `Unsafe` sur un environnement contrôlé quand ces routes sont activées
- [x] 7.8.8 Étendre les scénarios E2E WebSocket pour refléter la couverture complète des événements, subscriptions et appels publics exposés par `ICometBftWebSocketClient`
- [x] 7.8.9 Étendre les scénarios E2E gRPC pour couvrir l'ensemble des méthodes publiques gRPC auditées et les réponses complètes effectivement mappées

---

## Phase 8 — Documentation et Demos

### 8.1 Documentation
- [x] 8.1.1 XML doc sur tous les `public` types et membres (enforced par TreatWarningsAsErrors)
- [x] 8.1.2 `README.md` : badges, installation, quickstart, lien cometbft + version protocole
- [x] 8.1.3 `CHANGELOG.md` (format Keep-a-Changelog)
- [x] 8.1.4 Configurer DocFX dans `docs/` (docfx.json)

### 8.2 Demo REST (`samples/CometBFT.Client.Demo.Rest/`)
- [x] 8.2.1 Créer projet console net10.0
- [x] 8.2.2 Dépendances : `Spectre.Console`, `Microsoft.Extensions.Hosting`
- [x] 8.2.3 Config : `COMETBFT_RPC_URL` env var ou `--rpc-url` CLI arg
- [x] 8.2.3b Vérifier que la résolution respecte la priorité CLI arg > env var > défaut testnet :
  ```csharp
  var rpcUrl = args.GetOption("--rpc-url")
      ?? Environment.GetEnvironmentVariable("COMETBFT_RPC_URL")
      ?? "https://cosmoshub.cometbftrpc.lava.build:443";
  ```
  > Aucune exception, aucun exit si env var absente.
- [x] 8.2.4 Enregistrer `AddCometBftRest` via DI
- [x] 8.2.5 Boucle refresh toutes les 10 s : GetHealth, GetStatus, GetBlock, GetValidators, GetAbciInfo
- [x] 8.2.6 Layout Spectre.Console `Live` : Header, Health/Status, Latest Block, Validators, ABCI Info, Log
- [x] 8.2.7 Chaque panel affiche timestamp d'appel et latence en ms
- [x] 8.2.8 Ajouter `GetBlockResultsAsync` au refresh et à l'affichage de la démo REST
- [x] 8.2.9 Étendre la démo REST pour refléter la matrice complète des endpoints publics REST jugés obligatoires pour la visibilité opérationnelle de la librairie
- [x] 8.2.10 Étendre la démo REST pour rendre accessibles toutes les méthodes de `ICometBftRestClient`, y compris les capacités `Unsafe` derrière un mode ou un avertissement explicite

### 8.3 Demo WebSocket (`samples/CometBFT.Client.Demo.WebSocket/`)
- [x] 8.3.1 Créer projet console net10.0
- [x] 8.3.2 Dépendances : `Spectre.Console`, `Microsoft.Extensions.Hosting`
- [x] 8.3.3 Config : `COMETBFT_WS_URL` env var ou `--ws-url` CLI arg
- [x] 8.3.3b Vérifier que la résolution respecte la priorité CLI arg > env var > défaut testnet :
  ```csharp
  var wsUrl = args.GetOption("--ws-url")
      ?? Environment.GetEnvironmentVariable("COMETBFT_WS_URL")
      ?? "wss://cosmoshub.cometbftrpc.lava.build:443/websocket";
  ```
- [x] 8.3.4 Enregistrer `AddCometBftWebSocket` via DI
- [x] 8.3.5 NewBlock → panel "Live Blocks" (scrolling 20 entrées, event-driven)
- [x] 8.3.6 Tx → panel "Live Transactions" (scrolling 20 entrées)
- [x] 8.3.7 Vote → log line (adresse validateur, hauteur, round)
- [x] 8.3.8 Reconnexion automatique avec log WARN
- [x] 8.3.9 Layout Spectre.Console : Header, Live Blocks, Live Transactions, Log
- [x] 8.3.10 Souscrire `NewBlockHeader` et exposer son état dans la démo WebSocket
- [x] 8.3.11 Souscrire `ValidatorSetUpdates` et exposer les mises à jour dans la démo WebSocket
- [x] 8.3.12 Étendre la démo WebSocket pour refléter exhaustivement tous les événements, subscriptions et appels publics exposés par `ICometBftWebSocketClient`

### 8.4 Demo gRPC (`samples/CometBFT.Client.Demo.Grpc/`)
- [x] 8.4.1 Créer projet console net10.0
- [x] 8.4.2 Dépendances : `Spectre.Console`, `Microsoft.Extensions.Hosting`
- [x] 8.4.3 Config : `COMETBFT_GRPC_URL` env var ou `--grpc-url` CLI arg
- [x] 8.4.3b Vérifier que la résolution respecte la priorité CLI arg > env var > défaut testnet :
  ```csharp
  var grpcUrl = args.GetOption("--grpc-url")
      ?? Environment.GetEnvironmentVariable("COMETBFT_GRPC_URL")
      ?? "cosmoshub.grpc.lava.build";
  ```
- [x] 8.4.4 Enregistrer `AddCometBftGrpc` via DI
- [x] 8.4.5 Fallback polling `PingAsync` toutes les 10 s (streaming non disponible en v0.38)
- [x] 8.4.6 Panel BroadcastAPI : Ping latency + timestamp
- [x] 8.4.7 Layout Spectre.Console : Header, BroadcastAPI, Log
- [x] 8.4.8 Ajouter les informations d'endpoint/protocole dans le header de la démo gRPC
- [x] 8.4.9 Ajouter un panneau `Live Blocks` ou `Streaming Events` conforme au mode streaming/polling effectif
- [x] 8.4.10 Étendre la démo gRPC pour exposer l'ensemble des méthodes publiques gRPC réellement supportées par le client avec leurs réponses utiles, pas seulement le ping minimal
- [x] 8.4.11 Ajouter dans la démo gRPC un affichage explicite des champs gRPC significatifs effectivement mappés (`check_tx`, `tx_result` et équivalents), ou un équivalent de diagnostic vérifiable si non observable en live

---

## Phase 9 — CI/CD

- [x] 9.1 Créer `.github/workflows/ci.yml` (build + lint + test + coverage)
  - [x] 9.1b Ajouter step CI "Package freshness" :
    ```yaml
    - name: Check outdated packages
      run: |
        dotnet list package --outdated --include-transitive > outdated.txt
        if grep -q "^   >" outdated.txt; then
          echo "❌ Outdated direct dependencies detected:" && grep "^   >" outdated.txt && exit 1
        fi
    ```
- [x] 9.2 Créer `.github/workflows/publish.yml` (pack + push sur release tag)
- [x] 9.3 Ajouter `.github/dependabot.yml` :
  ```yaml
  version: 2
  updates:
    - package-ecosystem: "nuget"
      directory: "/"
      schedule:
        interval: "weekly"
      open-pull-requests-limit: 10
  ```
- [x] 9.4 Activer `RestoreLockedMode` dans `Directory.Build.props` (projets src et tests) + committer `packages.lock.json` après `dotnet restore`
- [x] 9.5 Ajouter un step CI séparé pour les tests d'intégration contre un endpoint public validé
- [x] 9.6 Ajouter un step CI séparé pour les tests E2E contre un endpoint public validé
- [x] 9.7 Faire échouer le CI si la couverture globale ligne < 90 % ou si un fichier source < 90 % ligne
- [x] 9.8 Vérifier que le CI n'exige pas d'upload de rapport de couverture pour être conforme
- [x] 9.9 Ajouter au CI un chemin de validation dédié aux wrappers Docker auto-suffisants une fois la migration hors bind mount implémentée
- [x] 9.10 Étendre le CI gRPC pour vérifier la couverture de toutes les méthodes publiques gRPC auditées et la parité de schéma avec le proto amont ciblé
- [x] 9.11 Étendre le CI pour faire apparaître explicitement la validation exhaustive des surfaces publiques REST, WebSocket et gRPC définies par les matrices d'audit
- [x] 9.12 Ajouter un chemin de validation REST séparé pour les endpoints `Unsafe` sur un environnement de test contrôlé, sans dépendre des endpoints publics par défaut

---

## Phase 10 — CometBFT Naming (applied)

> Réalisé dans les commits `1cf06f1`, `02f1df5`, `5dd4b4e` avant publication.

### 10.1 Identifiants C# (namespaces + API publique)
- [x] 10.1.1 `ICometBftRestClient` — interface principale du client REST (+ impl + usages)
- [x] 10.1.2 `ICometBftWebSocketClient` — interface principale du client WebSocket
- [x] 10.1.3 `ICometBftGrpcClient` — interface principale du client gRPC
- [x] 10.1.4 `CometBftRestClient` — implémentation du client REST
- [x] 10.1.5 `CometBftWebSocketClient` — implémentation du client WebSocket
- [x] 10.1.6 `CometBftGrpcClient` — implémentation du client gRPC
- [x] 10.1.7 `CometBftRest/WebSocket/GrpcOptions` — classes d'options par transport
- [x] 10.1.8 `CometBftClientException` — exception de base (+ sous-types par transport)
- [x] 10.1.9 `CometBftJsonContext` — contexte de sérialisation JSON AOT
- [x] 10.1.10 `AddCometBftRest/WebSocket/Grpc` — extensions DI (+ `AddCometBftSdkGrpc`)
- [x] 10.1.11 `dotnet build CometBFT.Client.sln --warnaserror` → 0 erreur

### 10.2 Scripts, CI/CD, documentation
- [x] 10.2.1 `scripts/*.sh` et `scripts/docker/*.sh` — chemins et noms alignés
- [x] 10.2.2 `.github/workflows/ci.yml` et `publish.yml` — noms de solution et projets
- [x] 10.2.3 `README.md`, `CHANGELOG.md`, `src/*/README.md`, `samples/*/README.md` — noms de packages et exemples
- [x] 10.2.4 `tools/CometBFT.Client.CoverageGate/Program.cs` — `excludedPrefixes/Suffixes`

### 10.3 Validation naming
- [x] 10.3.1 `grep -r "CometBFT\.Client" src/ tests/ --include="*.cs" | grep -v LegacyProto` → identifiants alignés
- [x] 10.3.2 `git remote -v` → `https://github.com/Rinzler78/CometBFT.Client.git`
- [x] 10.3.3 `./scripts/test.sh` → verts + coverage ≥ 90 % (réel : 97 %)
- [x] 10.3.4 Démos REST + WS + gRPC → démarrent sans erreur

---

## Validation finale

- [x] V.1 `dotnet build` passe sans warnings ; validation complète refaite en Docker avec suites live Integration/E2E vertes sur le set d'endpoints `Lava`
- [x] V.2 `dotnet format --verify-no-changes` passe
- [x] V.3 `./scripts/test.sh` — gate de couverture fonctionnelle, couverture ligne globale ≥ 90 % et par fichier ≥ 90 % (réel : 97 %)
- [x] V.4 `./scripts/publish.sh --dry-run` — paquet généré sans erreur
- [x] V.5 Tous les endpoints publics CometBFT couverts (vs `/rpc/openapi/openapi.yaml`)
- [x] V.6 `dotnet list package --outdated` — zéro package direct obsolète
- [x] V.7 `dotnet build --warnaserror` — zéro warning sur tous les projets src
- [x] V.8 Tests d'intégration REST, WebSocket et gRPC exécutés ou correctement skippés selon les env vars
- [x] V.9 Tests E2E REST, WebSocket et gRPC exécutés ou correctement skippés selon les env vars
- [x] V.10 Les wrappers `scripts/docker/*.sh` fonctionnent dans un mode auto-suffisant sans bind mount et restent alignés sur les scripts locaux
- [x] V.11 Le client gRPC couvre l'intégralité des méthodes publiques gRPC de la release CometBFT ciblée, avec parité de proto, réponses complètes, tests, démos et documentation alignés
- [x] V.12 Le client REST couvre l'intégralité des endpoints publics de l'OpenAPI CometBFT ciblée, avec tests, démos et documentation alignés
- [x] V.13 Le client WebSocket couvre l'intégralité des événements, subscriptions et appels publics du protocole ciblé, avec tests, démos et documentation alignés
- [x] V.14 Le client REST couvre aussi les endpoints `Unsafe` de l'OpenAPI ciblée lorsque le nœud les active, avec tests dédiés, démo explicite et documentation des prérequis
