#!/usr/bin/env bash
# Wipes the local DynamoDB table and seeds it with test data.
#
# Scenario
# --------
# Group "Test Group" (invite code ABC123) with two members:
#   user1 = Alice  — loves comedy & musicals
#   user2 = Bob    — loves drama & stand-up
#
# 8 shows spread across three venues on Jul 10–11 2026, with four different
# kinds of deliberate conflict so the schedule algorithm — and the missed-show
# diagnostics in ScheduleController — have something to resolve/explain:
#   - raw time overlaps (101/102, 101/103, 104/105)
#   - an availability conflict (106 — Bob is unavailable for both of its
#     showtimes; see the "Availability" section below)
#   - a mode-independent transfer conflict (107 — the gap after 105 is too
#     short to reach Venue 3 under any travel mode; see "Transfer matrix")
#   - a mode-dependent transfer conflict (108 — the gap after 102 is too
#     short to walk to Venue 1, but long enough to drive or cycle there; see
#     "Transfer matrix")
#
# Expected score order (walking mode): 101(9) > 102(5) = 104(5) > 103(4) >
# 106(3) > 105(2) > 107(1) = 108(1)
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

put << 'EOF'
{
  "pk":                   {"S": "SHOW#107"},
  "sk":                   {"S": "METADATA"},
  "entityType":           {"S": "SHOW"},
  "ShowId":               {"N": "107"},
  "Title":                {"S": "Sketch Bites"},
  "PlainTextDescription": {"S": "Forty minutes of rapid-fire sketches. Blink and you'll miss three of them."},
  "Tag":                  {"S": "Comedy"},
  "Price":                {"S": "10.00"},
  "Fee":                  {"S": "2.00"},
  "FirstShowDate":        {"S": "2026-07-11"},
  "LengthInMinutes":      {"N": "40"},
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
  "pk":                   {"S": "SHOW#108"},
  "sk":                   {"S": "METADATA"},
  "entityType":           {"S": "SHOW"},
  "ShowId":               {"N": "108"},
  "Title":                {"S": "Nightcap"},
  "PlainTextDescription": {"S": "A quiet, late-night two-hander to close out the day. Forty-five minutes, one table, two glasses."},
  "Tag":                  {"S": "Comedy"},
  "Price":                {"S": "8.00"},
  "Fee":                  {"S": "2.00"},
  "FirstShowDate":        {"S": "2026-07-11"},
  "LengthInMinutes":      {"N": "45"},
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

# Show 107 — 40 min, single showtime (19:10 leaves only a 10-minute gap after
# 105 ends at Venue 2 (19:00) — see the "Transfer matrix" section for why that's
# not enough time to reach Venue 3 on foot)
put << 'EOF'
{"pk":{"S":"SHOW#107"},"sk":{"S":"SHOWTIME#2026-07-11T19:10:00.0000000Z"},"DateTime":{"S":"2026-07-11T19:10:00.0000000Z"},"PerformanceTime":{"S":"19:10"},"PerformanceDate":{"S":"2026-07-11"},"PresentationFormat":{"S":"In-Person"},"Reserved":{"BOOL":false}}
EOF

# Show 108 — 45 min, single showtime (21:56 leaves a 26-minute gap after 102
# ends at Venue 2 (21:30) — too short to walk to Venue 1 (28 min required),
# but enough to cycle or drive there. See the "Transfer matrix" section.
put << 'EOF'
{"pk":{"S":"SHOW#108"},"sk":{"S":"SHOWTIME#2026-07-11T21:56:00.0000000Z"},"DateTime":{"S":"2026-07-11T21:56:00.0000000Z"},"PerformanceTime":{"S":"21:56"},"PerformanceDate":{"S":"2026-07-11"},"PresentationFormat":{"S":"In-Person"},"Reserved":{"BOOL":false}}
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
# Score = rank (1 = top pick). Points formula per user: (totalRanked - rank + 1)
# — note totalRanked is per-user, so a 5th-place vote bumps every one of that
# user's other points by 1 versus a 4-show ballot.
#
# Alice (user1): 101=1, 103=2, 104=3, 105=4, 107=5  → points: 5, 4, 3, 2, 1
# Bob   (user2): 102=1, 101=2, 106=3, 104=4, 108=5  → points: 5, 4, 3, 2, 1
#
# Aggregate: 101=9, 102=5, 103=4, 104=5, 105=2, 106=3, 107=1, 108=1

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
{"pk":{"S":"USER#user1"},"sk":{"S":"VOTE#SHOW#107"},"Score":{"N":"5"},"UpdatedAt":{"S":"2026-07-01T12:00:00.0000000Z"}}
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
put << 'EOF'
{"pk":{"S":"USER#user2"},"sk":{"S":"VOTE#SHOW#108"},"Score":{"N":"5"},"UpdatedAt":{"S":"2026-07-01T13:00:00.0000000Z"}}
EOF

# ── Availability ──────────────────────────────────────────────────────────────
# Once any member has an availability record, unconstrained members must get
# their own fully-open record too — otherwise they'd default to "unavailable"
# (see ScheduleController's anyoneHasAvailability rule) and the whole schedule
# would collapse. So Alice gets one window spanning the entire festival, and
# Bob gets three windows that together cover everything EXCEPT show 106's two
# performances (Jul 10 20:30–21:50 and Jul 11 13:00–14:20) — deliberately
# carving Bob out of both, so 106 is always "BlockedByMembers" no matter which
# of its showtimes is tried.

echo "▶  Seeding availability..."

put << 'EOF'
{
  "pk": {"S": "USER#user1"},
  "sk": {"S": "AVAILABILITY"},
  "Windows": {"L": [
    {"M": {"Start": {"S": "2026-07-10T00:00:00.0000000Z"}, "End": {"S": "2026-07-12T00:00:00.0000000Z"}}}
  ]}
}
EOF

put << 'EOF'
{
  "pk": {"S": "USER#user2"},
  "sk": {"S": "AVAILABILITY"},
  "Windows": {"L": [
    {"M": {"Start": {"S": "2026-07-10T00:00:00.0000000Z"}, "End": {"S": "2026-07-10T20:30:00.0000000Z"}}},
    {"M": {"Start": {"S": "2026-07-10T21:50:00.0000000Z"}, "End": {"S": "2026-07-11T13:00:00.0000000Z"}}},
    {"M": {"Start": {"S": "2026-07-11T14:20:00.0000000Z"}, "End": {"S": "2026-07-12T00:00:00.0000000Z"}}}
  ]}
}
EOF

# ── Transfer matrix ───────────────────────────────────────────────────────────
# A minimal active matrix covering every directional pair among the 3 canonical
# venues, so IVenueTransferTimeProvider resolves real Matrix durations instead
# of silently falling back to the 60-minute MissingDataFallback for every
# cross-venue transition. Values are illustrative round numbers, not derived
# from real geocoding (Venue 3 has no coordinates yet — see the Venues section).
#
# Required gap = raw duration + 20 min scheduling overhead (5 departure + 5
# arrival + 10 reliability buffer, from TransferPolicyOptions' defaults) — the
# same fixed overhead applies no matter which travel mode is selected, only
# the raw duration underneath it changes. With these durations, every existing
# transition in the schedule clears easily except two, both deliberate:
#   - 105 ends at Venue 2 at 19:00 and 107's only showtime starts at Venue 3
#     at 19:10 — a 10-minute gap. Required is 32 min even walking (12 min) and
#     23 min driving (3 min), so 107 misses under every travel mode.
#   - 102 ends at Venue 2 at 21:30 and 108's only showtime starts at Venue 1
#     at 21:56 — a 26-minute gap. Required is 28 min walking (8 min) but only
#     22 min driving (2 min) / 23 min cycling (3 min), so 108 misses walking
#     (the default mode) but joins the schedule if you switch to cycling or
#     driving in the UI.

echo "▶  Seeding transfer matrix..."

put << 'EOF'
{
  "pk":          {"S": "TRANSFER_MATRIX#local-seed-v1"},
  "sk":          {"S": "METADATA"},
  "InputHash":   {"S": "local-seed-v1"},
  "VenueCount":  {"N": "3"},
  "PairCount":   {"N": "6"},
  "GeneratedAt": {"S": "2026-07-15T04:00:00.0000000Z"},
  "Source":      {"S": "LocalSeed"}
}
EOF

put << 'EOF'
{"pk":{"S":"TRANSFER_MATRIX#local-seed-v1"},"sk":{"S":"FROM#1#TO#2"},"FromVenueNumber":{"N":"1"},"ToVenueNumber":{"N":"2"},"WalkingDurationSeconds":{"N":"480"},"WalkingDistanceMeters":{"N":"650"},"CyclingDurationSeconds":{"N":"180"},"CyclingDistanceMeters":{"N":"650"},"DrivingDurationSeconds":{"N":"120"},"DrivingDistanceMeters":{"N":"1200"},"Source":{"S":"LocalSeed"}}
EOF
put << 'EOF'
{"pk":{"S":"TRANSFER_MATRIX#local-seed-v1"},"sk":{"S":"FROM#2#TO#1"},"FromVenueNumber":{"N":"2"},"ToVenueNumber":{"N":"1"},"WalkingDurationSeconds":{"N":"480"},"WalkingDistanceMeters":{"N":"650"},"CyclingDurationSeconds":{"N":"180"},"CyclingDistanceMeters":{"N":"650"},"DrivingDurationSeconds":{"N":"120"},"DrivingDistanceMeters":{"N":"1200"},"Source":{"S":"LocalSeed"}}
EOF
put << 'EOF'
{"pk":{"S":"TRANSFER_MATRIX#local-seed-v1"},"sk":{"S":"FROM#1#TO#3"},"FromVenueNumber":{"N":"1"},"ToVenueNumber":{"N":"3"},"WalkingDurationSeconds":{"N":"600"},"WalkingDistanceMeters":{"N":"800"},"CyclingDurationSeconds":{"N":"240"},"CyclingDistanceMeters":{"N":"800"},"DrivingDurationSeconds":{"N":"120"},"DrivingDistanceMeters":{"N":"1500"},"Source":{"S":"LocalSeed"}}
EOF
put << 'EOF'
{"pk":{"S":"TRANSFER_MATRIX#local-seed-v1"},"sk":{"S":"FROM#3#TO#1"},"FromVenueNumber":{"N":"3"},"ToVenueNumber":{"N":"1"},"WalkingDurationSeconds":{"N":"600"},"WalkingDistanceMeters":{"N":"800"},"CyclingDurationSeconds":{"N":"240"},"CyclingDistanceMeters":{"N":"800"},"DrivingDurationSeconds":{"N":"120"},"DrivingDistanceMeters":{"N":"1500"},"Source":{"S":"LocalSeed"}}
EOF
put << 'EOF'
{"pk":{"S":"TRANSFER_MATRIX#local-seed-v1"},"sk":{"S":"FROM#2#TO#3"},"FromVenueNumber":{"N":"2"},"ToVenueNumber":{"N":"3"},"WalkingDurationSeconds":{"N":"720"},"WalkingDistanceMeters":{"N":"950"},"CyclingDurationSeconds":{"N":"300"},"CyclingDistanceMeters":{"N":"950"},"DrivingDurationSeconds":{"N":"180"},"DrivingDistanceMeters":{"N":"1800"},"Source":{"S":"LocalSeed"}}
EOF
put << 'EOF'
{"pk":{"S":"TRANSFER_MATRIX#local-seed-v1"},"sk":{"S":"FROM#3#TO#2"},"FromVenueNumber":{"N":"3"},"ToVenueNumber":{"N":"2"},"WalkingDurationSeconds":{"N":"720"},"WalkingDistanceMeters":{"N":"950"},"CyclingDurationSeconds":{"N":"300"},"CyclingDistanceMeters":{"N":"950"},"DrivingDurationSeconds":{"N":"180"},"DrivingDistanceMeters":{"N":"1800"},"Source":{"S":"LocalSeed"}}
EOF

put << 'EOF'
{
  "pk":         {"S": "CONFIG"},
  "sk":         {"S": "ACTIVE_TRANSFER_MATRIX"},
  "InputHash":  {"S": "local-seed-v1"},
  "PromotedAt": {"S": "2026-07-15T04:00:00.0000000Z"}
}
EOF

echo ""
echo "✓  Done. 51 items written to '$TABLE'."
echo ""
echo "   Dev login user IDs:  user1 (Alice)  |  user2 (Bob)"
echo "   Group invite code:   ABC123"
echo ""
echo "   Venues: 1 (already geocoded) · 2 (manual override) · 3 (needs geocoding)"
echo ""
echo "   Expected schedule (greedy by group score, walking mode):"
echo "   Jul 10  14:00  The Comedy Hour        101  (9 pts)"
echo "   Jul 10  16:00  Stand-Up Spectacular   104  (5 pts)"
echo "   Jul 10  18:00  Musical Mayhem         103  (4 pts)"
echo "   Jul 11  18:00  Improv Chaos           105  (2 pts)"
echo "   Jul 11  20:00  Drama in the Dark      102  (5 pts)"
echo ""
echo "   Expected missed shows under walking mode (deliberate — see"
echo "   ScheduleController diagnostics):"
echo "   The One-Person Show    106  (3 pts)  — BlockedByMembers (Bob unavailable)"
echo "   Sketch Bites            107  (1 pt)  — TransferConflict (Venue 2 → 3, 10 min gap < 32 min required, every mode)"
echo "   Nightcap                108  (1 pt)  — TransferConflict (Venue 2 → 1, 26 min gap < 28 min required walking)"
echo ""
echo "   Switch to cycling or driving mode in the UI to see 108 join the"
echo "   schedule (Jul 11 21:56, Festival Hub Main Stage) while 106/107 stay missed."
