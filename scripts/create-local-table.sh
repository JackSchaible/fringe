#!/usr/bin/env bash
set -e

ENDPOINT="${DYNAMO_ENDPOINT:-http://localhost:8000}"
TABLE="${DYNAMO_TABLE_NAME:-fringe}"

echo "Creating table '$TABLE' at $ENDPOINT..."

AWS_ACCESS_KEY_ID=local AWS_SECRET_ACCESS_KEY=local \
aws dynamodb create-table \
  --endpoint-url "$ENDPOINT" \
  --region us-east-1 \
  --table-name "$TABLE" \
  --attribute-definitions \
    AttributeName=pk,AttributeType=S \
    AttributeName=sk,AttributeType=S \
    AttributeName=entityType,AttributeType=S \
  --key-schema \
    AttributeName=pk,KeyType=HASH \
    AttributeName=sk,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST \
  --global-secondary-indexes '[
    {
      "IndexName": "entity-type-index",
      "KeySchema": [
        {"AttributeName": "entityType", "KeyType": "HASH"},
        {"AttributeName": "pk",         "KeyType": "RANGE"}
      ],
      "Projection": {"ProjectionType": "ALL"}
    }
  ]'

echo "Done."
