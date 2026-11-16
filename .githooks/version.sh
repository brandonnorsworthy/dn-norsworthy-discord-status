#!/usr/bin/env bash
# Shared by the hooks and CI: where this repo's version lives and what counts as code.
# The only repo-specific file in .githooks/; the hooks themselves are the same everywhere.

# The project whose <Version> is the product version (the image's app).
PROJECT="StatusImageCard.csproj"

# Files whose change never needs a version bump (docs, CI, the hooks, repo meta, README images).
NON_CODE_PATTERN='(^|/)[^/]*\.md$|^docs/|^\.github/|^\.githooks/|^\.gitignore$|^LICENSE|^Assets/'

# Prints the <Version> in the project file at a git ref ("" = the working tree).
version_read() {
  local ref="${1:-}" text
  if [ -n "$ref" ]; then
    text=$(git show "$ref:$PROJECT" 2>/dev/null) || return 0
  else
    text=$(cat "$PROJECT")
  fi
  printf '%s\n' "$text" |
    sed -n 's:.*<Version>[[:space:]]*\([^<[:space:]]*\)[[:space:]]*</Version>.*:\1:p' | head -n1
}

# Writes a new version to the first <Version> in the project file.
version_write() {
  local new="$1"
  sed -i '0,/<Version>[^<]*<\/Version>/s//<Version>'"$new"'<\/Version>/' "$PROJECT"
}

# The files version_write touches.
VERSION_FILES=("$PROJECT")

# Reads file names on stdin; succeeds when at least one is code.
has_code_change() {
  grep -Ev "$NON_CODE_PATTERN" | grep -q .
}

# next_version <current> <major|minor|patch>
next_version() {
  local major minor patch
  IFS=. read -r major minor patch <<<"${1%%[-+]*}"
  case "$2" in
    major) echo "$((major + 1)).0.0" ;;
    minor) echo "$major.$((minor + 1)).0" ;;
    patch) echo "$major.$minor.$((patch + 1))" ;;
  esac
}
