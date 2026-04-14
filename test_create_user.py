import requests
import json

url = "http://localhost:5038/api/auth/login"
creds = {"email": "admin@company.com", "password": "admin123"}
r = requests.post(url, json=creds)
token = r.json()["token"]

headers = {
    "Authorization": f"Bearer {token}",
    "Content-Type": "application/json"
}

user_data = {
    "user": {
        "name": "Rahul Singh",
        "email": "s.rahul@kavitechsolution.com",
        "password": "password123",
        "roleId": "D46FD7D6-9075-4FCE-B4AC-BEEFA4595339", # Super Admin
        "department": "IT",
        "isActive": True
    },
    "scopes": [
        {"scopeType": "Subscription", "scopeValue": "Azure subscription 1"},
        {"scopeType": "Subscription", "scopeValue": "kavitech-Dev/Test"}
    ]
}

r = requests.post("http://localhost:5038/api/users", headers=headers, json=user_data)
print(f"Status: {r.status_code}")
print(f"Body: {r.text}")
