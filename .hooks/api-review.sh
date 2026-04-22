#!/usr/bin/env bash
# api-review.sh — Comprehensive local code review (mirrors Copilot PR review checks).
#
# Dimensions covered:
#   A. API constraint / example consistency
#   B. Options validation alignment
#   C. XML documentation completeness on public API
#   D. Interface shim delegation integrity
#   E. Sealed / non-sealed policy (protocol-pure vs applicative)
#   F. Changelog / spec-diff / tasks traceability
#
# Called by pre-push hook. Exit 0 = pass, exit 1 = violations.

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
FAIL=0
WARN=0

pass() { echo "  ✓ $*"; }
warn() { echo "  ⚠ $*"; WARN=1; }
fail() { echo "  ✗ $*" >&2; FAIL=1; }

section() { echo ""; echo "── $* ──"; }

# ─────────────────────────────────────────────────────────────────────────────
# A. API constraint / example consistency
# ─────────────────────────────────────────────────────────────────────────────
section "A. API constraint / example consistency"

EXTENSIONS="$REPO_ROOT/src/CometBFT.Client.Extensions/ServiceCollectionExtensions.cs"

# A1 — fully-parameterized REST overload exists
if grep -q "AddCometBftRest<TBlock, TTxResult, TValidator, TInterface, TClient>" "$EXTENSIONS"; then
    pass "AddCometBftRest 5-param overload present"
else
    fail "AddCometBftRest 5-param overload missing — consumer interfaces with custom domain types cannot be registered"
fi

# A2 — fully-parameterized WebSocket overload exists
if grep -q "AddCometBftWebSocket<TTx, TBlock, TTxResult, TValidator, TInterface, TClient>" "$EXTENSIONS"; then
    pass "AddCometBftWebSocket 6-param overload present"
else
    fail "AddCometBftWebSocket 6-param overload missing — consumer interfaces with custom domain types cannot be registered"
fi

# A3 — 2-param REST delegates (no pipeline duplication)
REST_2P_BODY=$(awk '/where TInterface : class, ICometBftRestClient$/{found=1} found && /=>/{print; exit}' "$EXTENSIONS")
if echo "$REST_2P_BODY" | grep -q "AddCometBftRest<"; then
    pass "2-param AddCometBftRest delegates to 5-param (no pipeline duplication)"
else
    fail "2-param AddCometBftRest does not delegate — Polly pipeline may be duplicated"
fi

# A4 — 3-param WebSocket delegates
WS_3P_BODY=$(awk '/where TInterface : class, ICometBftWebSocketClient<TTx>$/{found=1} found && /=>/{print; exit}' "$EXTENSIONS")
if echo "$WS_3P_BODY" | grep -q "AddCometBftWebSocket<"; then
    pass "3-param AddCometBftWebSocket delegates to 6-param (no logic duplication)"
else
    fail "3-param AddCometBftWebSocket does not delegate — registration logic may be duplicated"
fi

# A5 — non-generic REST delegates to 2-param
NONGEN_REST=$(awk '/public static IServiceCollection AddCometBftRest\(/{found=1} found && /=>/{print; exit}' "$EXTENSIONS")
if echo "$NONGEN_REST" | grep -q "AddCometBftRest<"; then
    pass "Non-generic AddCometBftRest delegates to 2-param"
else
    fail "Non-generic AddCometBftRest does not delegate — Polly pipeline duplication risk"
fi

# A6 — README Extension Guide references correct overload arity
README="$REPO_ROOT/src/CometBFT.Client.Core/README.md"
if grep -q "5-param\|AddCometBftRest<CosmosBlock\|AddCometBftRest<TBlock" "$README"; then
    pass "README Extension Guide references 5-param REST overload"
else
    fail "README Extension Guide uses 2-param REST overload for custom domain types — will not compile"
fi
if grep -q "6-param\|AddCometBftWebSocket<CosmosTx\|AddCometBftWebSocket<TTx, CosmosBlock" "$README"; then
    pass "README Extension Guide references 6-param WebSocket overload"
else
    fail "README Extension Guide uses 3-param WebSocket overload for custom domain types — will not compile"
fi

# A7 — Sample comment references correct overload arity
SAMPLE="$REPO_ROOT/samples/CometBFT.Client.Sample/Program.cs"
if grep -q "5-param\|AddCometBftRest<CosmosBlock" "$SAMPLE"; then
    pass "Sample comment references 5-param REST overload"
else
    fail "Sample comment uses wrong REST overload for custom domain types"
fi
if grep -q "6-param\|AddCometBftWebSocket<CosmosTx" "$SAMPLE"; then
    pass "Sample comment references 6-param WebSocket overload"
else
    fail "Sample comment uses wrong WebSocket overload for custom domain types"
fi

# A8 — spec-diff extension pattern references correct overload
SPEC_DIFF="$REPO_ROOT/openspec/changes/extensibility-v2/specs/spec-diff.md"
if [ -f "$SPEC_DIFF" ] && grep -q "5-param\|AddCometBftRest<CosmosBlock\|AddCometBftRest<TBlock" "$SPEC_DIFF"; then
    pass "spec-diff.md extension pattern references correct REST overload"
elif [ ! -f "$SPEC_DIFF" ]; then
    warn "spec-diff.md not found — skip overload check"
else
    fail "spec-diff.md extension pattern uses wrong REST overload for custom domain types"
fi

# ─────────────────────────────────────────────────────────────────────────────
# B. Options validation alignment
# ─────────────────────────────────────────────────────────────────────────────
section "B. Options validation alignment"

# B1 — Each method that creates tempOptions and calls Validate() must not also
#      attempt to re-validate inside the singleton factory (double-validation
#      with potentially different instances)
VALIDATE_COUNT=$(grep -c "tempOptions.Validate()" "$EXTENSIONS" || true)
CONFIGURE_COUNT=$(grep -c "services.Configure<" "$EXTENSIONS" || true)
if [ "$VALIDATE_COUNT" -le "$CONFIGURE_COUNT" ]; then
    pass "Eager validation (tempOptions.Validate) count ($VALIDATE_COUNT) ≤ Configure calls ($CONFIGURE_COUNT) — no obvious double-validation"
else
    warn "More Validate() calls than Configure<> registrations — verify options instances are not double-validated"
fi

# B2 — Check there is no inline lambda validation inside AddHttpClient/AddSingleton
#      (would bypass the validated tempOptions pattern)
if grep -A5 "AddHttpClient<" "$EXTENSIONS" | grep -q "\.Validate()"; then
    fail "Found Validate() inside AddHttpClient lambda — options may be validated twice with different instances"
else
    pass "No Validate() inside AddHttpClient lambda — validation uses tempOptions pattern only"
fi

# ─────────────────────────────────────────────────────────────────────────────
# C. XML documentation completeness
# ─────────────────────────────────────────────────────────────────────────────
section "C. XML documentation completeness"

# C1 — All public overloads in ServiceCollectionExtensions must have <summary>
PUBLIC_METHOD_LINES=$(grep -n "public static IServiceCollection Add" "$EXTENSIONS" | wc -l | tr -d ' ')
SUMMARY_COUNT=$(grep -c "<summary>" "$EXTENSIONS" || true)
if [ "$SUMMARY_COUNT" -ge "$PUBLIC_METHOD_LINES" ]; then
    pass "XML <summary> count ($SUMMARY_COUNT) covers all public methods ($PUBLIC_METHOD_LINES)"
else
    fail "XML <summary> count ($SUMMARY_COUNT) < public method count ($PUBLIC_METHOD_LINES) — some methods lack doc"
fi

# C2 — ICometBftWebSocketClient arity label is correct
WS_INTERFACE="$REPO_ROOT/src/CometBFT.Client.Core/Interfaces/ICometBftWebSocketClient.cs"
if grep -q "1-parameter\|1-param" "$WS_INTERFACE"; then
    pass "ICometBftWebSocketClient remarks describe the 1-parameter shim correctly"
elif grep -q "2-param" "$WS_INTERFACE"; then
    fail "ICometBftWebSocketClient remarks still say '2-param' for the 1-parameter shim"
else
    pass "ICometBftWebSocketClient remarks do not reference incorrect arity"
fi

# C3 — Generic type parameters have <typeparam> docs in each new overload
NEW_OVERLOADS=("AddCometBftRest<TBlock" "AddCometBftWebSocket<TTx, TBlock")
for sig in "${NEW_OVERLOADS[@]}"; do
    if grep -B30 "$sig" "$EXTENSIONS" | grep -q "<typeparam"; then
        pass "Overload '$sig' has <typeparam> documentation"
    else
        warn "Overload '$sig' may be missing <typeparam> documentation"
    fi
done

# ─────────────────────────────────────────────────────────────────────────────
# D. Interface shim delegation integrity
# ─────────────────────────────────────────────────────────────────────────────
section "D. Interface shim delegation integrity"

BLOCK_SERVICE="$REPO_ROOT/src/CometBFT.Client.Core/Interfaces/IBlockService.cs"
TX_SERVICE="$REPO_ROOT/src/CometBFT.Client.Core/Interfaces/ITxService.cs"
VAL_SERVICE="$REPO_ROOT/src/CometBFT.Client.Core/Interfaces/IValidatorService.cs"
REST_CLIENT="$REPO_ROOT/src/CometBFT.Client.Core/Interfaces/ICometBftRestClient.cs"

# D1 — Non-generic shims must inherit from the generic version
for f in "$BLOCK_SERVICE" "$TX_SERVICE" "$VAL_SERVICE"; do
    name=$(basename "$f" .cs)
    if grep -q "interface ${name} :" "$f"; then
        pass "$name has non-generic shim inheriting generic version"
    else
        fail "$name missing non-generic shim"
    fi
done

# D2 — ICometBftRestClient shim inherits ICometBftRestClient<Block, TxResult, Validator>
if grep -q "ICometBftRestClient : ICometBftRestClient<Block, TxResult, Validator>" "$REST_CLIENT"; then
    pass "ICometBftRestClient shim inherits correct 3-param generic"
else
    fail "ICometBftRestClient shim does not inherit ICometBftRestClient<Block, TxResult, Validator>"
fi

# D3 — ICometBftWebSocketClient shims
WS_CLIENT="$REPO_ROOT/src/CometBFT.Client.Core/Interfaces/ICometBftWebSocketClient.cs"
if grep -q "ICometBftWebSocketClient<TTx>" "$WS_CLIENT" && \
   grep -q "ICometBftWebSocketClient<TTx, Block<TTx>" "$WS_CLIENT"; then
    pass "ICometBftWebSocketClient 1-param and non-generic shims both present"
else
    fail "ICometBftWebSocketClient shim chain incomplete"
fi

# ─────────────────────────────────────────────────────────────────────────────
# E. Sealed / non-sealed policy
# ─────────────────────────────────────────────────────────────────────────────
section "E. Sealed / non-sealed policy"

DOMAIN_DIR="$REPO_ROOT/src/CometBFT.Client.Core/Domain"

# E1 — Protocol-pure types must remain sealed
SEALED_REQUIRED=("Vote" "ProtocolVersion" "GenesisChunk" "NetworkInfo" "NetworkPeer" "RawTxCodec")
for t in "${SEALED_REQUIRED[@]}"; do
    file="$DOMAIN_DIR/${t}.cs"
    if [ ! -f "$file" ]; then
        # Some types may be in a single file; fall back to grep
        if grep -rn "public record $t[^<{]" "$DOMAIN_DIR" | grep -qv "sealed"; then
            fail "Protocol-pure type '$t' appears non-sealed in Domain/"
        else
            pass "Protocol-pure type '$t' is sealed (or not found as a top-level record)"
        fi
        continue
    fi
    if grep -q "sealed record $t" "$file"; then
        pass "Protocol-pure type '$t' is sealed"
    elif grep -q "public record $t" "$file" && ! grep -q "sealed" "$file"; then
        fail "Protocol-pure type '$t' is NOT sealed in $file"
    else
        pass "Protocol-pure type '$t' sealed check passed"
    fi
done

# E2 — Abstract base types exist for Block and TxResult
if grep -q "abstract record BlockBase" "$DOMAIN_DIR/Block.cs"; then
    pass "BlockBase abstract record present"
else
    fail "BlockBase abstract record missing in Domain/Block.cs"
fi
if grep -q "abstract record TxResultBase" "$DOMAIN_DIR/TxResult.cs"; then
    pass "TxResultBase abstract record present"
else
    fail "TxResultBase abstract record missing in Domain/TxResult.cs"
fi

# E3 — Applicative types (Block, TxResult) are non-sealed and derive from bases
for pair in "Block.cs:BlockBase" "TxResult.cs:TxResultBase"; do
    file_name="${pair%%:*}"
    base="${pair##*:}"
    file="$DOMAIN_DIR/$file_name"
    if grep -q "sealed" "$file" && ! grep -q "abstract record" "$file"; then
        fail "Applicative type in $file_name is still sealed"
    elif grep -q ": $base(" "$file"; then
        pass "$file_name inherits $base"
    else
        warn "$file_name may not inherit $base — verify"
    fi
done

# ─────────────────────────────────────────────────────────────────────────────
# F. Changelog / spec / tasks traceability
# ─────────────────────────────────────────────────────────────────────────────
section "F. Changelog / spec / tasks traceability"

CHANGELOG="$REPO_ROOT/CHANGELOG.md"

# F1 — CHANGELOG has [2.0.0] or [Unreleased] section
if grep -q "\[2\.0\.0\]\|\[Unreleased\]" "$CHANGELOG"; then
    pass "CHANGELOG has a release section"
else
    fail "CHANGELOG missing [2.0.0] or [Unreleased] section"
fi

# F2 — Tasks file marks all phases complete (no [ ] unchecked items in phases 0-8)
TASKS="$REPO_ROOT/openspec/changes/extensibility-v2/tasks.md"
if [ -f "$TASKS" ]; then
    UNCHECKED=$(grep -c "^- \[ \]" "$TASKS" || true)
    # Phase 9 (PR/merge/tag) is expected to still be open; anything else is a gap
    UNCHECKED_NON_RELEASE=$(grep "^- \[ \]" "$TASKS" | grep -cv "PR\|Merge\|Tag\|merge\|tag" || true)
    if [ "$UNCHECKED_NON_RELEASE" -eq 0 ]; then
        pass "All implementation tasks complete in tasks.md (Phase 9 release steps excluded)"
    else
        warn "$UNCHECKED_NON_RELEASE non-release tasks still unchecked in tasks.md"
    fi
fi

# F3 — spec-diff.md exists and has all 4 sections
if [ -f "$SPEC_DIFF" ]; then
    SECTIONS=$(grep -c "^## [0-9]\." "$SPEC_DIFF" || true)
    if [ "$SECTIONS" -ge 4 ]; then
        pass "spec-diff.md has $SECTIONS sections (≥ 4 expected)"
    else
        warn "spec-diff.md has only $SECTIONS sections — may be incomplete"
    fi
fi

# ─────────────────────────────────────────────────────────────────────────────
# Summary
# ─────────────────────────────────────────────────────────────────────────────
echo ""
if [ "$FAIL" -eq 1 ]; then
    echo "  Code review FAILED — fix the issues marked ✗ before pushing."
    echo ""
    exit 1
elif [ "$WARN" -eq 1 ]; then
    echo "  Code review passed with warnings ⚠ — review the items above."
    echo ""
    exit 0
else
    echo "  Code review passed ✓"
    echo ""
    exit 0
fi
