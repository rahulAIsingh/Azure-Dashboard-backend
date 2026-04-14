import csv
import uuid
from datetime import datetime

csv_path = r'C:\Users\RahulSingh\Downloads\azure-cost-export-2026-03-11.csv'
sql_path = r'd:\antigravity\Azure_Dashboard\backend\restore_data.sql'

subscriptions = set()
resource_groups = {} # (RG_Name, Sub_Name) -> dummy
records = []

print(f"Reading CSV: {csv_path}")

with open(csv_path, mode='r', encoding='utf-8') as f:
    reader = csv.DictReader(f)
    for row in reader:
        usage_date = row['UsageDate']
        sub_name = row['Subscription']
        rg_name = row['ResourceGroup']
        res_name = row['ResourceName']
        res_type = row['ResourceType']
        ser_name = row['ServiceName']
        loc = row['Location']
        cost = row['Cost']
        
        subscriptions.add(sub_name)
        resource_groups[(rg_name, sub_name)] = True
        
        records.append({
            'UsageDate': usage_date,
            'SubscriptionName': sub_name,
            'ResourceGroup': rg_name,
            'ResourceName': res_name,
            'ResourceType': res_type,
            'ServiceName': ser_name,
            'Location': loc,
            'Cost': cost
        })

print(f"Found {len(subscriptions)} subscriptions, {len(resource_groups)} resource groups, {len(records)} records.")

with open(sql_path, mode='w', encoding='utf-8') as f:
    f.write("-- SQL Restoration Script\n")
    f.write("USE [AzureFinOps];\n")
    f.write("DELETE FROM AzureCostUsage;\n")
    f.write("DELETE FROM ResourceGroups;\n")
    f.write("DELETE FROM Subscriptions;\n")
    f.write("GO\n\n")

    sub_map = {}
    for sub in subscriptions:
        sub_id = str(uuid.uuid4())
        safe_sub = sub.replace("'", "''")
        f.write(f"INSERT INTO Subscriptions (Id, SubscriptionId, SubscriptionName) VALUES (NEWID(), '{sub_id[:20]}', '{safe_sub}');\n")
    f.write("GO\n\n")

    for (rg, sub) in resource_groups.keys():
        safe_rg = rg.replace("'", "''")
        safe_sub = sub.replace("'", "''")
        f.write(f"INSERT INTO ResourceGroups (Id, ResourceGroupName, SubscriptionId) SELECT NEWID(), '{safe_rg}', Id FROM Subscriptions WHERE SubscriptionName = '{safe_sub}';\n")
    f.write("GO\n\n")

    # Insert records in batches of 500
    batch_size = 500
    for i in range(0, len(records), batch_size):
        batch = records[i:i+batch_size]
        f.write("INSERT INTO AzureCostUsage (Id, UsageDate, SubscriptionName, ResourceGroup, ResourceName, ResourceType, ServiceName, Location, Cost, Currency) VALUES \n")
        values = []
        for r in batch:
            safe_sub = r['SubscriptionName'].replace("'", "''")
            safe_rg = r['ResourceGroup'].replace("'", "''")
            safe_res = r['ResourceName'].replace("'", "''")
            safe_type = r['ResourceType'].replace("'", "''")
            safe_ser = r['ServiceName'].replace("'", "''")
            safe_loc = r['Location'].replace("'", "''")
            v = f"(NEWID(), '{r['UsageDate']}', '{safe_sub}', '{safe_rg}', '{safe_res}', '{safe_type}', '{safe_ser}', '{safe_loc}', {r['Cost']}, 'INR')"
            values.append(v)
        f.write(",\n".join(values))
        f.write(";\nGO\n")

print(f"SQL script generated: {sql_path}")
