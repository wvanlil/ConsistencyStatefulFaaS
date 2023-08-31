#!/usr/bin/env bash

containers=${1:-5}
for i in $(seq 1 $containers); do
  lxc-stop -n n$i
done