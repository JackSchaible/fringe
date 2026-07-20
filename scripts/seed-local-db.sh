#!/usr/bin/env bash
# Wipes the local DynamoDB table and seeds it with test data.
#
# Scenario
# --------
# Group "Test Group" (invite code ABC123) with two members:
#   user1 = Alice  — loves comedy & musicals
#   user2 = Bob    — loves drama & stand-up
#
# 6 shows spread across two venues on Jul 10–11 2026, with deliberate
# time conflicts so the schedule algorithm has something to resolve.
#
# Expected score order: 101(7) > 102(4) > 103(3) = 104(3) > 106(2) > 105(1)
#
# Usage:
#   ./scripts/seed-local-db.sh
#   DYNAMO_ENDPOINT=http://localhost:8000 DYNAMO_TABLE_NAME=fringe ./scripts/seed-local-db.sh

set -euo pipefail

ENDPOINT="${DYNAMO_ENDPOINT:-http://localhost:8000}"
TABLE="${DYNAMO_TABLE_NAME:-fringe}"

export AWS_ACCESS_KEY_ID=local
export AWS_SECRET_ACCESS_KEY=local
export AWS_DEFAULT_REGION=us-east-1

DDB="aws dynamodb --endpoint-url $ENDPOINT --region us-east-1"

put() {
  local item
  item=$(cat)
  $DDB put-item --table-name "$TABLE" --item "$item" > /dev/null
}

# ── Wipe ──────────────────────────────────────────────────────────────────────

echo "▶  Wiping '$TABLE'..."
$DDB delete-table --table-name "$TABLE" 2>/dev/null || true
$DDB wait table-not-exists --table-name "$TABLE"

# ── Create ────────────────────────────────────────────────────────────────────

echo "▶  Creating table..."
$DDB create-table \
  --table-name "$TABLE" \
  --attribute-definitions \
    AttributeName=pk,AttributeType=S \
    AttributeName=sk,AttributeType=S \
    AttributeName=entityType,AttributeType=S \
  --key-schema \
    AttributeName=pk,KeyType=HASH \
    AttributeName=sk,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST \
  --global-secondary-indexes '[{
    "IndexName": "entity-type-index",
    "KeySchema": [
      {"AttributeName": "entityType", "KeyType": "HASH"},
      {"AttributeName": "pk",         "KeyType": "RANGE"}
    ],
    "Projection": {"ProjectionType": "ALL"}
  }]' > /dev/null
$DDB wait table-exists --table-name "$TABLE"

# ── Venues ────────────────────────────────────────────────────────────────────
# Canonical VENUE#<num> records, independent of the embedded Venue blob on each
# show. Deliberately seeded in three different enrichment states (see FA-33):
#   1  Festival Hub Main Stage    — already geocoded, AddressHash matches its
#                                   current Address/PostalCode → NOT eligible
#                                   for re-geocoding.
#   2  Roxy Theatre               — CoordinateSource "Manual" → NEVER eligible
#                                   for automatic geocoding, regardless of hash.
#   3  Northern Arts Collective   — no coordinates yet → eligible, this is the
#                                   one a local `dotnet run` with a real
#                                   OPENROUTESERVICE_API_KEY will actually geocode.

echo "▶  Seeding venues..."

put << 'EOF'
{
  "pk":               {"S": "VENUE#1"},
  "sk":               {"S": "METADATA"},
  "entityType":       {"S": "VENUE"},
  "VenueNumber":      {"N": "1"},
  "Name":             {"S": "Festival Hub Main Stage"},
  "Address":          {"S": "10320 102 Ave NW"},
  "Phone":            {"S": "780-555-0101"},
  "PostalCode":       {"S": "T5J 2T4"},
  "Latitude":         {"N": "53.5444"},
  "Longitude":        {"N": "-113.4909"},
  "AddressHash":      {"S": "3dff74facb0660a32acb7667eb7bdde4d79ec6dda9eb4a10f8d306522a8e3fdb"},
  "CoordinateSource": {"S": "OpenRouteService"},
  "EnrichedAt":       {"S": "2026-07-15T03:00:00.0000000Z"}
}
EOF

put << 'EOF'
{
  "pk":               {"S": "VENUE#2"},
  "sk":               {"S": "METADATA"},
  "entityType":       {"S": "VENUE"},
  "VenueNumber":      {"N": "2"},
  "Name":             {"S": "Roxy Theatre"},
  "Address":          {"S": "8529 Gateway Blvd NW"},
  "Phone":            {"S": "780-555-0202"},
  "PostalCode":       {"S": "T6E 4M3"},
  "Latitude":         {"N": "53.5183"},
  "Longitude":        {"N": "-113.4926"},
  "AddressHash":      {"S": "70d122a620df2e4fd9cfd07c82d307b74650d95dcd76606524aaaa620f509192"},
  "CoordinateSource": {"S": "Manual"},
  "EnrichedAt":       {"S": "2026-06-20T09:00:00.0000000Z"}
}
EOF

put << 'EOF'
{
  "pk":          {"S": "VENUE#3"},
  "sk":          {"S": "METADATA"},
  "entityType":  {"S": "VENUE"},
  "VenueNumber": {"N": "3"},
  "Name":        {"S": "Northern Arts Collective"},
  "Address":     {"S": "11924 Jasper Ave"},
  "Phone":       {"S": "780-555-0303"},
  "PostalCode":  {"S": "T5K 0P6"}
}
EOF

# ── Shows ─────────────────────────────────────────────────────────────────────

echo "▶  Seeding shows..."

put << 'EOF'
{
  "pk":                   {"S": "SHOW#101"},
  "sk":                   {"S": "METADATA"},
  "entityType":           {"S": "SHOW"},
  "ShowId":               {"N": "101"},
  "Title":                {"S": "The Comedy Hour"},
  "PlainTextDescription": {"S": "A riotous hour of stand-up and sketch comedy from the city's funniest up-and-comers."},
  "Tag":                  {"S": "Comedy"},
  "Price":                {"S": "15.00"},
  "Fee":                  {"S": "3.00"},
  "FirstShowDate":        {"S": "2026-07-10"},
  "LengthInMinutes":      {"N": "60"},
  "Venue": {"M": {
    "VenueNumber": {"N": "1"},
    "Name":        {"S": "Festival Hub Main Stage"},
    "Address":     {"S": "10320 102 Ave NW"},
    "Phone":       {"S": "780-555-0101"},
    "PostalCode":  {"S": "T5J 2T4"}
  }},
  "ContentRating": {"M": {
    "Name":        {"S": "General"},
    "Code":        {"S": "G"},
    "Description": {"S": "Suitable for all ages"}
  }}
}
EOF

put << 'EOF'
{
  "pk":                   {"S": "SHOW#102"},
  "sk":                   {"S": "METADATA"},
  "entityType":           {"S": "SHOW"},
  "ShowId":               {"N": "102"},
  "Title":                {"S": "Drama in the Dark"},
  "PlainTextDescription": {"S": "An intense psychological thriller performed in near-total darkness. Not for the faint-hearted."},
  "Tag":                  {"S": "Drama"},
  "Price":                {"S": "20.00"},
  "Fee":                  {"S": "3.50"},
  "FirstShowDate":        {"S": "2026-07-10"},
  "LengthInMinutes":      {"N": "90"},
  "Venue": {"M": {
    "VenueNumber": {"N": "2"},
    "Name":        {"S": "Roxy Theatre"},
    "Address":     {"S": "8529 Gateway Blvd NW"},
    "Phone":       {"S": "780-555-0202"},
    "PostalCode":  {"S": "T6E 4M3"}
  }},
  "ContentRating": {"M": {
    "Name":        {"S": "14 Years and Over"},
    "Code":        {"S": "14A"},
    "Description": {"S": "Coarse language, mature themes"}
  }}
}
EOF

put << 'EOF'
{
  "pk":                   {"S": "SHOW#103"},
  "sk":                   {"S": "METADATA"},
  "entityType":           {"S": "SHOW"},
  "ShowId":               {"N": "103"},
  "Title":                {"S": "Musical Mayhem"},
  "PlainTextDescription": {"S": "Original songs, big dance numbers, and zero apologies. Two hours of joyful chaos."},
  "Tag":                  {"S": "Musical"},
  "Price":                {"S": "25.00"},
  "Fee":                  {"S": "4.00"},
  "FirstShowDate":        {"S": "2026-07-10"},
  "LengthInMinutes":      {"N": "120"},
  "Venue": {"M": {
    "VenueNumber": {"N": "3"},
    "Name":        {"S": "Northern Arts Collective"},
    "Address":     {"S": "11924 Jasper Ave"},
    "Phone":       {"S": "780-555-0303"},
    "PostalCode":  {"S": "T5K 0P6"}
  }},
  "ContentRating": {"M": {
    "Name":        {"S": "General"},
    "Code":        {"S": "G"},
    "Description": {"S": "Suitable for all ages"}
  }}
}
EOF

put << 'EOF'
{
  "pk":                   {"S": "SHOW#104"},
  "sk":                   {"S": "METADATA"},
  "entityType":           {"S": "SHOW"},
  "ShowId":               {"N": "104"},
  "Title":                {"S": "Stand-Up Spectacular"},
  "PlainTextDescription": {"S": "Five of Edmonton's sharpest comedians. Seventy-five minutes of material you can't repeat at dinner."},
  "Tag":                  {"S": "Comedy"},
  "Price":                {"S": "18.00"},
  "Fee":                  {"S": "3.50"},
  "FirstShowDate":        {"S": "2026-07-10"},
  "LengthInMinutes":      {"N": "75"},
  "Venue": {"M": {
    "VenueNumber": {"N": "1"},
    "Name":        {"S": "Festival Hub Main Stage"},
    "Address":     {"S": "10320 102 Ave NW"},
    "Phone":       {"S": "780-555-0101"},
    "PostalCode":  {"S": "T5J 2T4"}
  }},
  "ContentRating": {"M": {
    "Name":        {"S": "18 Years and Over"},
    "Code":        {"S": "18+"},
    "Description": {"S": "Adult content, explicit language"}
  }}
}
EOF

put << 'EOF'
{
  "pk":                   {"S": "SHOW#105"},
  "sk":                   {"S": "METADATA"},
  "entityType":           {"S": "SHOW"},
  "ShowId":               {"N": "105"},
  "Title":                {"S": "Improv Chaos"},
  "PlainTextDescription": {"S": "Fully unscripted. The audience drives the story. Something different every single night."},
  "Tag":                  {"S": "Comedy"},
  "Price":                {"S": "12.00"},
  "Fee":                  {"S": "2.50"},
  "FirstShowDate":        {"S": "2026-07-10"},
  "LengthInMinutes":      {"N": "60"},
  "Venue": {"M": {
    "VenueNumber": {"N": "2"},
    "Name":        {"S": "Roxy Theatre"},
    "Address":     {"S": "8529 Gateway Blvd NW"},
    "Phone":       {"S": "780-555-0202"},
    "PostalCode":  {"S": "T6E 4M3"}
  }},
  "ContentRating": {"M": {
    "Name":        {"S": "14 Years and Over"},
    "Code":        {"S": "14A"},
    "Description": {"S": "Some coarse language"}
  }}
}
EOF

put << 'EOF'
{
  "pk":                   {"S": "SHOW#106"},
  "sk":                   {"S": "METADATA"},
  "entityType":           {"S": "SHOW"},
  "ShowId":               {"N": "106"},
  "Title":                {"S": "The One-Person Show"},
  "PlainTextDescription": {"S": "A solo performer. Eighty minutes. Seven characters. One devastating story about family."},
  "Tag":                  {"S": "Drama"},
  "Price":                {"S": "22.00"},
  "Fee":                  {"S": "3.50"},
  "FirstShowDate":        {"S": "2026-07-10"},
  "LengthInMinutes":      {"N": "80"},
  "Venue": {"M": {
    "VenueNumber": {"N": "3"},
    "Name":        {"S": "Northern Arts Collective"},
    "Address":     {"S": "11924 Jasper Ave"},
    "Phone":       {"S": "780-555-0303"},
    "PostalCode":  {"S": "T5K 0P6"}
  }},
  "ContentRating": {"M": {
    "Name":        {"S": "14 Years and Over"},
    "Code":        {"S": "14A"},
    "Description": {"S": "Mature themes, mild coarse language"}
  }}
}
EOF

# ── ShowTimes ─────────────────────────────────────────────────────────────────
# Conflicts are deliberate:
#   101@14:00 overlaps 102@14:30  (both end after 14:30)
#   101@19:00 overlaps 103@18:00  (103 runs until 20:00)
#   104@16:00 overlaps 105@15:30  (105 runs until 16:30)

echo "▶  Seeding show times..."

# Show 101 — 60 min
put << 'EOF'
{"pk":{"S":"SHOW#101"},"sk":{"S":"SHOWTIME#2026-07-10T14:00:00.0000000Z"},"DateTime":{"S":"2026-07-10T14:00:00.0000000Z"},"PerformanceTime":{"S":"14:00"},"PerformanceDate":{"S":"2026-07-10"},"PresentationFormat":{"S":"In-Person"},"Reserved":{"BOOL":false}}
EOF
put << 'EOF'
{"pk":{"S":"SHOW#101"},"sk":{"S":"SHOWTIME#2026-07-10T19:00:00.0000000Z"},"DateTime":{"S":"2026-07-10T19:00:00.0000000Z"},"PerformanceTime":{"S":"19:00"},"PerformanceDate":{"S":"2026-07-10"},"PresentationFormat":{"S":"In-Person"},"Reserved":{"BOOL":false}}
EOF

# Show 102 — 90 min (14:30 conflicts with 101@14:00)
put << 'EOF'
{"pk":{"S":"SHOW#102"},"sk":{"S":"SHOWTIME#2026-07-10T14:30:00.0000000Z"},"DateTime":{"S":"2026-07-10T14:30:00.0000000Z"},"PerformanceTime":{"S":"14:30"},"PerformanceDate":{"S":"2026-07-10"},"PresentationFormat":{"S":"In-Person"},"Reserved":{"BOOL":false}}
EOF
put << 'EOF'
{"pk":{"S":"SHOW#102"},"sk":{"S":"SHOWTIME#2026-07-11T20:00:00.0000000Z"},"DateTime":{"S":"2026-07-11T20:00:00.0000000Z"},"PerformanceTime":{"S":"20:00"},"PerformanceDate":{"S":"2026-07-11"},"PresentationFormat":{"S":"In-Person"},"Reserved":{"BOOL":false}}
EOF

# Show 103 — 120 min (18:00 conflicts with 101@19:00)
put << 'EOF'
{"pk":{"S":"SHOW#103"},"sk":{"S":"SHOWTIME#2026-07-10T18:00:00.0000000Z"},"DateTime":{"S":"2026-07-10T18:00:00.0000000Z"},"PerformanceTime":{"S":"18:00"},"PerformanceDate":{"S":"2026-07-10"},"PresentationFormat":{"S":"In-Person"},"Reserved":{"BOOL":false}}
EOF
put << 'EOF'
{"pk":{"S":"SHOW#103"},"sk":{"S":"SHOWTIME#2026-07-11T15:00:00.0000000Z"},"DateTime":{"S":"2026-07-11T15:00:00.0000000Z"},"PerformanceTime":{"S":"15:00"},"PerformanceDate":{"S":"2026-07-11"},"PresentationFormat":{"S":"In-Person"},"Reserved":{"BOOL":false}}
EOF

# Show 104 — 75 min
put << 'EOF'
{"pk":{"S":"SHOW#104"},"sk":{"S":"SHOWTIME#2026-07-10T16:00:00.0000000Z"},"DateTime":{"S":"2026-07-10T16:00:00.0000000Z"},"PerformanceTime":{"S":"16:00"},"PerformanceDate":{"S":"2026-07-10"},"PresentationFormat":{"S":"In-Person"},"Reserved":{"BOOL":false}}
EOF
put << 'EOF'
{"pk":{"S":"SHOW#104"},"sk":{"S":"SHOWTIME#2026-07-11T14:00:00.0000000Z"},"DateTime":{"S":"2026-07-11T14:00:00.0000000Z"},"PerformanceTime":{"S":"14:00"},"PerformanceDate":{"S":"2026-07-11"},"PresentationFormat":{"S":"In-Person"},"Reserved":{"BOOL":false}}
EOF

# Show 105 — 60 min (15:30 conflicts with 104@16:00)
put << 'EOF'
{"pk":{"S":"SHOW#105"},"sk":{"S":"SHOWTIME#2026-07-10T15:30:00.0000000Z"},"DateTime":{"S":"2026-07-10T15:30:00.0000000Z"},"PerformanceTime":{"S":"15:30"},"PerformanceDate":{"S":"2026-07-10"},"PresentationFormat":{"S":"In-Person"},"Reserved":{"BOOL":false}}
EOF
put << 'EOF'
{"pk":{"S":"SHOW#105"},"sk":{"S":"SHOWTIME#2026-07-11T18:00:00.0000000Z"},"DateTime":{"S":"2026-07-11T18:00:00.0000000Z"},"PerformanceTime":{"S":"18:00"},"PerformanceDate":{"S":"2026-07-11"},"PresentationFormat":{"S":"In-Person"},"Reserved":{"BOOL":false}}
EOF

# Show 106 — 80 min
put << 'EOF'
{"pk":{"S":"SHOW#106"},"sk":{"S":"SHOWTIME#2026-07-10T20:30:00.0000000Z"},"DateTime":{"S":"2026-07-10T20:30:00.0000000Z"},"PerformanceTime":{"S":"20:30"},"PerformanceDate":{"S":"2026-07-10"},"PresentationFormat":{"S":"In-Person"},"Reserved":{"BOOL":false}}
EOF
put << 'EOF'
{"pk":{"S":"SHOW#106"},"sk":{"S":"SHOWTIME#2026-07-11T13:00:00.0000000Z"},"DateTime":{"S":"2026-07-11T13:00:00.0000000Z"},"PerformanceTime":{"S":"13:00"},"PerformanceDate":{"S":"2026-07-11"},"PresentationFormat":{"S":"In-Person"},"Reserved":{"BOOL":false}}
EOF

# ── Users ─────────────────────────────────────────────────────────────────────

echo "▶  Seeding users..."

put << 'EOF'
{"pk":{"S":"USER#user1"},"sk":{"S":"PROFILE"},"Email":{"S":"alice@dev"},"DisplayName":{"S":"Alice"},"GroupId":{"S":"grp-test-001"}}
EOF

put << 'EOF'
{"pk":{"S":"USER#user2"},"sk":{"S":"PROFILE"},"Email":{"S":"bob@dev"},"DisplayName":{"S":"Bob"},"GroupId":{"S":"grp-test-001"}}
EOF

# ── Group ─────────────────────────────────────────────────────────────────────

echo "▶  Seeding group..."

put << 'EOF'
{"pk":{"S":"GROUP#grp-test-001"},"sk":{"S":"METADATA"},"GroupId":{"S":"grp-test-001"},"Name":{"S":"Test Group"},"OwnerId":{"S":"user1"},"InviteCode":{"S":"ABC123"},"CreatedAt":{"S":"2026-07-01T12:00:00.0000000Z"}}
EOF

put << 'EOF'
{"pk":{"S":"GROUP#grp-test-001"},"sk":{"S":"MEMBER#user1"},"UserId":{"S":"user1"},"DisplayName":{"S":"Alice"},"Email":{"S":"alice@dev"},"JoinedAt":{"S":"2026-07-01T12:00:00.0000000Z"}}
EOF

put << 'EOF'
{"pk":{"S":"GROUP#grp-test-001"},"sk":{"S":"MEMBER#user2"},"UserId":{"S":"user2"},"DisplayName":{"S":"Bob"},"Email":{"S":"bob@dev"},"JoinedAt":{"S":"2026-07-01T13:00:00.0000000Z"}}
EOF

put << 'EOF'
{"pk":{"S":"INVITE#ABC123"},"sk":{"S":"METADATA"},"GroupId":{"S":"grp-test-001"}}
EOF

# ── Votes ─────────────────────────────────────────────────────────────────────
# Score = rank (1 = top pick). Points formula: (totalRanked - rank + 1)
#
# Alice (user1): 101=1, 103=2, 104=3, 105=4  → points: 4, 3, 2, 1
# Bob   (user2): 102=1, 101=2, 106=3, 104=4  → points: 4, 3, 2, 1
#
# Aggregate: 101=7, 102=4, 103=3, 104=3, 106=2, 105=1

echo "▶  Seeding votes..."

put << 'EOF'
{"pk":{"S":"USER#user1"},"sk":{"S":"VOTE#SHOW#101"},"Score":{"N":"1"},"UpdatedAt":{"S":"2026-07-01T12:00:00.0000000Z"}}
EOF
put << 'EOF'
{"pk":{"S":"USER#user1"},"sk":{"S":"VOTE#SHOW#103"},"Score":{"N":"2"},"UpdatedAt":{"S":"2026-07-01T12:00:00.0000000Z"}}
EOF
put << 'EOF'
{"pk":{"S":"USER#user1"},"sk":{"S":"VOTE#SHOW#104"},"Score":{"N":"3"},"UpdatedAt":{"S":"2026-07-01T12:00:00.0000000Z"}}
EOF
put << 'EOF'
{"pk":{"S":"USER#user1"},"sk":{"S":"VOTE#SHOW#105"},"Score":{"N":"4"},"UpdatedAt":{"S":"2026-07-01T12:00:00.0000000Z"}}
EOF

put << 'EOF'
{"pk":{"S":"USER#user2"},"sk":{"S":"VOTE#SHOW#102"},"Score":{"N":"1"},"UpdatedAt":{"S":"2026-07-01T13:00:00.0000000Z"}}
EOF
put << 'EOF'
{"pk":{"S":"USER#user2"},"sk":{"S":"VOTE#SHOW#101"},"Score":{"N":"2"},"UpdatedAt":{"S":"2026-07-01T13:00:00.0000000Z"}}
EOF
put << 'EOF'
{"pk":{"S":"USER#user2"},"sk":{"S":"VOTE#SHOW#106"},"Score":{"N":"3"},"UpdatedAt":{"S":"2026-07-01T13:00:00.0000000Z"}}
EOF
put << 'EOF'
{"pk":{"S":"USER#user2"},"sk":{"S":"VOTE#SHOW#104"},"Score":{"N":"4"},"UpdatedAt":{"S":"2026-07-01T13:00:00.0000000Z"}}
EOF

echo ""
echo "✓  Done. 35 items written to '$TABLE'."
echo ""
echo "   Dev login user IDs:  user1 (Alice)  |  user2 (Bob)"
echo "   Group invite code:   ABC123"
echo ""
echo "   Venues: 1 (already geocoded) · 2 (manual override) · 3 (needs geocoding)"
echo ""
echo "   Expected schedule (greedy by group score):"
echo "   Jul 10  14:00  The Comedy Hour        101  (7 pts)"
echo "   Jul 10  16:00  Stand-Up Spectacular   104  (3 pts)"
echo "   Jul 10  18:00  Musical Mayhem         103  (3 pts)"
echo "   Jul 10  20:30  The One-Person Show    106  (2 pts)"
echo "   Jul 11  18:00  Improv Chaos           105  (1 pt)"
echo "   Jul 11  20:00  Drama in the Dark      102  (4 pts)"
