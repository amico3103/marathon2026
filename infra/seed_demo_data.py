import argparse
import json
import time
from pathlib import Path

import boto3
from botocore.config import Config
from botocore.exceptions import ClientError


IMAGE_BY_ADDRESS = {
    "9 Abashidze Street": "abashidze-street-9.jpg",
    "14 Baratashvili Street": "baratashvili-street-14.jpg",
    "48 Rustaveli Avenue": "rustaveli-avenue-48.jpg",
}
PRIMARY_KEYS = {"Locations": "id", "Dishes": "id", "Tables": "id", "Waiters": "userId"}


def load_items(path: Path, table: str) -> list[dict]:
    document = json.loads(path.read_text(encoding="utf-8"))
    return [request["PutRequest"]["Item"] for request in document[table]]


def build_items(locations_path: Path, dishes_path: Path, image_base_url: str, demo_waiters: bool) -> dict[str, list[dict]]:
    locations = load_items(locations_path, "Locations")
    dishes = load_items(dishes_path, "Dishes")
    location_ids = [item["id"]["S"] for item in locations]
    dish_ids = [item["id"]["S"] for item in dishes]
    if len(location_ids) != len(set(location_ids)) or len(dish_ids) != len(set(dish_ids)):
        raise ValueError("Duplicate location or dish IDs in seed data")
    if any(item["locationId"]["S"] not in location_ids for item in dishes):
        raise ValueError("A dish references an unknown location")

    tables = []
    waiters = []
    for location in locations:
        location_id = location["id"]["S"]
        address = location["address"]["S"]
        image_name = IMAGE_BY_ADDRESS[address]
        location["imageUrl"] = {"S": f"{image_base_url.rstrip('/')}/assets/img/locations/{image_name}"}
        table_count = int(location["totalCapacity"]["N"])
        if table_count < 1:
            raise ValueError(f"Location {location_id} has no tables")
        for table_number in range(1, table_count + 1):
            tables.append({
                "id": {"S": f"demo-table-{location_id}-{table_number}"},
                "locationId": {"S": location_id},
                "capacity": {"N": "4"},
                "tableNumber": {"N": str(table_number)},
            })
        if demo_waiters:
            waiters.append({
                "userId": {"S": f"demo-waiter-{location_id}"},
                "locationId": {"S": location_id},
                "createdAt": {"S": "2026-10-28T00:00:00.000Z"},
                "reservations": {"L": []},
            })

    for dish in dishes:
        if "Fats" in dish:
            if "fats" in dish:
                raise ValueError(f"Dish {dish['id']['S']} has both fats and Fats")
            dish["fats"] = dish.pop("Fats")

    return {"Locations": locations, "Dishes": dishes, "Tables": tables, "Waiters": waiters}


def put_if_absent(client, table: str, item: dict) -> str:
    key_name = PRIMARY_KEYS[table]
    key = {key_name: item[key_name]}
    try:
        client.put_item(
            TableName=table,
            Item=item,
            ConditionExpression="attribute_not_exists(#pk)",
            ExpressionAttributeNames={"#pk": key_name},
        )
        return "inserted"
    except ClientError as error:
        if error.response["Error"]["Code"] != "ConditionalCheckFailedException":
            raise
        existing = client.get_item(TableName=table, Key=key, ConsistentRead=True).get("Item")
        if existing != item:
            raise ValueError(f"Conflicting existing item in {table}: {key}") from error
        return "unchanged"


def main() -> None:
    parser = argparse.ArgumentParser(description="Seed the deployed restaurant demo data")
    parser.add_argument("--locations", type=Path, required=True)
    parser.add_argument("--dishes", type=Path, required=True)
    parser.add_argument("--image-base-url", default="https://d2hntky491rgpt.cloudfront.net")
    parser.add_argument("--profile", default="marathon2026")
    parser.add_argument("--region", default="eu-north-1")
    parser.add_argument("--demo-waiters", action="store_true")
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()

    items_by_table = build_items(args.locations, args.dishes, args.image_base_url, args.demo_waiters)
    print("Planned items:", {table: len(items) for table, items in items_by_table.items()})
    if not args.apply:
        return

    session = boto3.Session(profile_name=args.profile, region_name=args.region)
    client = session.client("dynamodb", config=Config(retries={"mode": "adaptive", "max_attempts": 10}))
    for table, items in items_by_table.items():
        for item in items:
            outcome = put_if_absent(client, table, item)
            print(f"{table} {item[PRIMARY_KEYS[table]]['S']}: {outcome}")
            time.sleep(1)


if __name__ == "__main__":
    main()
