#!/usr/bin/env bash
# Print a short-lived (1 h) GitHub token for one of the agent bot apps, scoped
# to this repository. Agents use it to commit, open PRs and review as
# claude-chrislove[bot] / codex-chrislove[bot]:
#
#   export GH_TOKEN=$(scripts/gh-app-token.sh claude)   # or: codex
#
# How it works: a GitHub App proves who it is with a JWT signed by its private
# key, then swaps that JWT for an installation token. The private keys live in
# the git-ignored .secrets/github-apps/ and never leave this machine.
set -euo pipefail

agent=${1:?usage: $0 claude|codex}
root=$(git rev-parse --show-toplevel)
dir="$root/.secrets/github-apps"
slug="${agent}-chrislove"
key="$dir/$slug.pem"
[[ -r $key ]] || { echo "no private key at $key" >&2; exit 1; }

client_id=$(jq -r .client_id "$dir/$slug.json")
repo=$(git -C "$root" remote get-url origin | sed -E 's#^(https://github.com/|git@github.com:)##; s#\.git$##')

b64url() { openssl base64 -A | tr '+/' '-_' | tr -d '='; }

# JWT valid for 9 minutes; backdated 60 s to tolerate clock drift.
now=$(date +%s)
header=$(printf '{"alg":"RS256","typ":"JWT"}' | b64url)
payload=$(printf '{"iat":%d,"exp":%d,"iss":"%s"}' $((now - 60)) $((now + 540)) "$client_id" | b64url)
signature=$(printf '%s.%s' "$header" "$payload" | openssl dgst -sha256 -sign "$key" | b64url)
jwt="$header.$payload.$signature"

api() { curl -fsS -H "Authorization: Bearer $jwt" -H "Accept: application/vnd.github+json" "$@"; }

installation=$(api "https://api.github.com/repos/$repo/installation" | jq -r .id)
api -X POST "https://api.github.com/app/installations/$installation/access_tokens" \
  -d "{\"repositories\":[\"${repo#*/}\"]}" | jq -r .token
