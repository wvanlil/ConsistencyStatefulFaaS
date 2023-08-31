file = open('./store/latest/history.txt')
lines = file.readlines()

nemesis = 0
data = []

data.append({
    "invoke": 0,
    "ok": 0,
    "info": 0
})

data.append({
    "invoke": 0,
    "ok": 0,
    "info": 0
})

for line in lines:
    if "nemesis" in line:
        if ":isolated" in line:
            nemesis = 1
        elif ":network-healed" in line:
            nemesis = 0
    elif ":txn" in line:
        if ":invoke" in line:
            data[nemesis]["invoke"] += 1
        elif ":ok" in line:
            data[nemesis]["ok"] += 1
        elif ":info" in line:
            data[nemesis]["info"] += 1

print(data)
