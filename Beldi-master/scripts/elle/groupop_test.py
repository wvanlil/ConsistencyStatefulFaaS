import requests
import random

url = "https://u2i3ah2mqantpriz4vdbhridju0ginny.lambda-url.us-east-1.on.aws/"

separate = [
    {
        "InstanceId": str(random.randint(0, 1000000000)),
        "CallerName": "",
        "Async": True,
        "Input": "[[:r 0 nil]]"
    },
    {
        "InstanceId": str(random.randint(0, 1000000000)),
        "CallerName": "",
        "Async": True,
        "Input": "[[:append 0 10]]"
    },
    {
        "InstanceId": str(random.randint(0, 1000000000)),
        "CallerName": "",
        "Async": True,
        "Input": "[[:r 0 nil]]"
    }
]

single = {
    "InstanceId": str(random.randint(0, 1000000000)),
    "CallerName": "",
    "Async": True,
    "Input": "[[:r 1 nil] [:append 1 10] [:r 1 nil]]"
}

print("Separate calls:")
for data in separate:
    response = requests.post(url, json=data)
    print(response.json())

print("\nSingle call:")
response = requests.post(url, json=single)
print(response.json())
