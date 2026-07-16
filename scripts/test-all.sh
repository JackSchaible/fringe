#!/bin/bash
set -uo pipefail

mkdir -p coverage

# infra's frontend/fringe-stack tests synthesize a BucketDeployment that reads
# this build output from disk, so it must exist before test:infra runs.
echo "Building fringe-client..."
pnpm --dir fringe-client run build > coverage/build-client.log 2>&1
if [ $? -ne 0 ]; then
  echo "  FAIL  build:client  (see coverage/build-client.log)"
  exit 1
fi

names="server client infra"
failed=0

for name in $names; do
  echo "Running test:$name..."
  pnpm run "test:$name" > "coverage/test-$name.log" 2>&1
  eval "result_$name=$?"
done

echo
echo "Summary:"
for name in $names; do
  eval "result=\$result_$name"
  if [ "$result" -eq 0 ]; then
    echo "  PASS  $name"
  else
    echo "  FAIL  $name  (see coverage/test-$name.log)"
    failed=1
  fi
done

exit $failed
