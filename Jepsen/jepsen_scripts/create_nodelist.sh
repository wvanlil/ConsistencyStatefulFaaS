#!/usr/bin/env bash

grep -E -o "([0-9]{1,3}[\.]){3}[0-9]{1,3}" > ../jepsen.elle/resources/node_file.txt