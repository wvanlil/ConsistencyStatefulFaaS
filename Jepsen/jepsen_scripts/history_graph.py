import matplotlib as mpl
import matplotlib.pyplot as plt
import numpy as np

file = open('./store/latest/history.edn')
lines = file.readlines()

data = []

for line in lines:
    if ':time ' in line:
        x = line.split(':time ')[1].split(',')[0]
        data.append(int(x) / 1000000000)

fig, ax = plt.subplots()
ax.xaxis.set_major_locator(mpl.ticker.MultipleLocator(30))
ax.plot(data, range(len(data)))

plt.show()
