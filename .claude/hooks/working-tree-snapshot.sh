# Sourced by the SubagentStart and SubagentStop hooks, run from the repository root.
# working_tree_snapshot prints the id of a git tree holding the working tree as it is now: tracked
# files with their uncommitted edits plus untracked files; ignored files stay out. It works on a
# temporary copy of the index, so the real index is never touched.
working_tree_snapshot() {
  local index tree
  index="$(mktemp)"
  if [ -f "$(git rev-parse --git-path index)" ]; then
    cp "$(git rev-parse --git-path index)" "$index"
  else
    rm -f "$index"
  fi
  if GIT_INDEX_FILE="$index" git add -A && tree="$(GIT_INDEX_FILE="$index" git write-tree)"; then
    rm -f "$index"
    echo "$tree"
  else
    rm -f "$index"
    return 1
  fi
}
