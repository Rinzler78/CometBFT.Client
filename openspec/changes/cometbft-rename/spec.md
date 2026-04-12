# Spec : Renommage Tendermint → CometBFT

## Motivation

Le repo `CometBFT.Client` a été nommé ainsi pour la découvrabilité. Avant publication publique, on aligne sur le nom technique exact — **CometBFT** — qui est le fork actif maintenu par Informal Systems depuis 2023. Tendermint Core est abandonné depuis v0.37.

## Portée

Renommage complet : repo GitHub, répertoire racine, packages NuGet, namespaces C#, identifiants publics, scripts, CI, documentation.

Le protocole wire legacy `tendermint.rpc.grpc` reste intact (backward compat gRPC).

## Décisions de nommage

| Élément | Ancien | Nouveau |
|---------|--------|---------|
| Repo GitHub | `Rinzler78/CometBFT.Client` | `Rinzler78/CometBFT.Client` |
| Packages NuGet | `CometBFT.Client.*` | `CometBFT.Client.*` |
| Namespaces C# | `CometBFT.Client.*` | `CometBFT.Client.*` |
| Identifiants publics | `Tendermint` prefix | `CometBft` prefix (PascalCase) |
| Images Docker | `tendermint-client:*` | `cometbft-client:*` |
| Solution | `CometBFT.Client.sln` | `CometBFT.Client.sln` |

## Inchangé (protocol wire)

- `GrpcProtocol.TendermintLegacy` — nom d'enum de protocole
- Namespace `CometBFT.Client.Grpc.LegacyProto` — proto généré legacy
- Commentaires sur `tendermint.rpc.grpc` — proto package wire

## Critères d'acceptance

- `dotnet build CometBFT.Client.sln --warnaserror` → 0 erreur
- `dotnet format CometBFT.Client.sln --verify-no-changes` → aucune divergence
- `./scripts/test.sh` → 100% tests verts, coverage ≥ 90%
- `grep -r "Tendermint\.Client" src/ tests/ --include="*.cs" | grep -v LegacyProto` → 0 résultat
- `./scripts/publish.sh --dry-run` → 0 warning
